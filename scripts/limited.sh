#!/usr/bin/env bash
# Run a heavy command (dotnet build, test, format, run; content.sh; prerender) inside resource limits, so the machine never maxes out.
# The desktop crashed twice on 2026-10-22 when two agents built and tested at once with no limits (about 16 MSBuild nodes each, the
# compiler server and parallel test hosts on 16 cores and 11 GiB of RAM). Every heavy command, by the gate and by agents, goes through
# this script.
#
#   scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4
#
# What it does:
#   1. Slots: at most VOCATERRA_HEAVY_SLOTS (default 2; the owner, 2026-11-05) heavy commands at once on the whole machine (one flock
#      per slot, shared by every worktree). A further command waits for a free slot. VOCATERRA_HEAVY_SLOTS=1 restores one at a time.
#   2. Preflight: waits while available memory is under VOCATERRA_MIN_FREE_MB (default 3000) or the 1-minute load is above the core count.
#   3. Caps, two levels:
#      - every command in its own systemd user scope: MemoryMax (default 6G), no swap, CPUQuota (default 1000% = 10 of 16 cores), nice 10;
#      - all the scopes together in one slice, vocaterra-heavy.slice (under vocaterra.slice): VOCATERRA_TOTAL_MEM_MAX (default 8G), no
#        swap, VOCATERRA_TOTAL_CPU_QUOTA (default 1400% = 14 of 16 cores). One command alone still gets its full 6G; two together can never
#        take more than 8G, leaving about 3 GiB for the desktop.
#      Over a cap the kernel kills the command inside its scope or the slice; the desktop is untouched. Exit code 137 (or 143) means "hit
#      a memory cap": lower -m or test parallelism, or wait for the other slot's command and run again.
#   4. Reports the command's peak memory on stderr when it ends, so the caps can be tuned from real numbers.
#   5. No lingering servers: MSBuild node reuse, the MSBuild server and the shared Roslyn compiler server are off, so nothing outlives
#      the command.
#   6. Marks its command: VOCATERRA_LIMITED=1 is set for it, so a script that calls limited.sh itself (scripts/content-loop.sh) refuses to run
#      under it instead of waiting for ever on the slot it already holds.
#   7. Re-entrant (M67): called with VOCATERRA_LIMITED=1 already set (scripts/limited.sh scripts/test.sh ..., where test.sh calls limited.sh
#      for each of its steps), it runs the command directly: it is already inside the scope and holds a slot. Before, the inner call
#      waited for ever on the lock the outer one held (2026-11-01, 21 minutes). scripts/content-loop.sh still refuses to run under it.
#
# Slot 0's lock is the one-slot script's lock (vocaterra-heavy.lock), so an older copy of this script in another worktree still takes a
# slot; its command runs outside the slice, though, so the slice's total does not cover it.
set -uo pipefail

[ "$#" -gt 0 ] || { echo "usage: scripts/limited.sh <command> [args...]" >&2; exit 2; }

if [ "${VOCATERRA_LIMITED:-}" = 1 ]; then
  echo "limited.sh: already inside limited.sh (VOCATERRA_LIMITED=1); running the command directly, under the outer slot and caps" >&2
  exec "$@"
fi

MEM_MAX="${VOCATERRA_MEM_MAX:-6G}"
CPU_QUOTA="${VOCATERRA_CPU_QUOTA:-1000%}"
TOTAL_MEM_MAX="${VOCATERRA_TOTAL_MEM_MAX:-8G}"
TOTAL_CPU_QUOTA="${VOCATERRA_TOTAL_CPU_QUOTA:-1400%}"
MIN_FREE_MB="${VOCATERRA_MIN_FREE_MB:-3000}"
SLOTS="${VOCATERRA_HEAVY_SLOTS:-2}"
SLICE="vocaterra-heavy.slice"
RUN_DIR="${XDG_RUNTIME_DIR:-/tmp}"

case "$SLOTS" in ''|*[!0-9]*|0) echo "limited.sh: VOCATERRA_HEAVY_SLOTS must be a positive number" >&2; exit 2 ;; esac

export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false
export DOTNET_NOLOGO=1
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
export VOCATERRA_LIMITED=1

lock_path() { if [ "$1" -eq 0 ]; then echo "$RUN_DIR/vocaterra-heavy.lock"; else echo "$RUN_DIR/vocaterra-heavy-$1.lock"; fi; }

# Take the first free slot; poll every 5 s while all are taken. The slot's lock stays on its descriptor until the script exits.
slot=""
waited=0
while [ -z "$slot" ]; do
  for i in $(seq 0 $((SLOTS - 1))); do
    exec {lockfd}>"$(lock_path "$i")"
    if flock -n "$lockfd"; then slot="$i"; break; fi
    exec {lockfd}>&-
  done
  if [ -z "$slot" ]; then
    [ "$waited" -eq 0 ] && echo "limited.sh: all ${SLOTS} heavy slots are taken; waiting for one" >&2
    waited=1
    sleep 5
  fi
done

cores="$(nproc)"
for _ in $(seq 1 60); do
  avail_mb="$(awk '/MemAvailable/ {print int($2/1024)}' /proc/meminfo)"
  load="$(cut -d' ' -f1 /proc/loadavg)"
  if [ "$avail_mb" -ge "$MIN_FREE_MB" ] && awk -v l="$load" -v c="$cores" 'BEGIN {exit !(l <= c)}'; then break; fi
  echo "limited.sh: waiting (available ${avail_mb} MB, load ${load} on ${cores} cores)" >&2
  sleep 10
done

# Runs inside the scope: the command, then its scope's peak memory (cgroup v2 memory.peak) on stderr.
inner='"$@"; rc=$?
cg="$(cut -d: -f3 /proc/self/cgroup)"
if [ -r "/sys/fs/cgroup$cg/memory.peak" ]; then
  awk -v s="$VOCATERRA_SLOT" "{printf \"limited.sh: peak memory %.1f GB (slot %s)\\n\", \$1/1073741824, s}" "/sys/fs/cgroup$cg/memory.peak" >&2
fi
exit $rc'

if systemd-run --user --scope --quiet true >/dev/null 2>&1; then
  systemctl --user set-property --runtime "$SLICE" \
    MemoryMax="$TOTAL_MEM_MAX" MemorySwapMax=0 CPUQuota="$TOTAL_CPU_QUOTA" >/dev/null 2>&1 \
    || echo "limited.sh: could not set the ${SLICE} totals; each command keeps its own caps" >&2
  VOCATERRA_SLOT="$slot" systemd-run --user --scope --quiet --collect --slice="$SLICE" \
    -p MemoryMax="$MEM_MAX" -p MemorySwapMax=0 -p CPUQuota="$CPU_QUOTA" \
    nice -n 10 bash -c "$inner" limited "$@"
  rc=$?
else
  echo "limited.sh: systemd user scope unavailable; running with nice and the slot lock only (no memory cap)" >&2
  nice -n 10 "$@"
  rc=$?
fi
case "$rc" in
  137|143) echo "limited.sh: the command was killed (exit $rc), most likely at a memory cap (${MEM_MAX} for one command, ${TOTAL_MEM_MAX} for all); lower -m or test parallelism, or run again when the other slot is free" >&2 ;;
esac
exit "$rc"
