#!/usr/bin/env bash
# Run a heavy command (dotnet build, test, format, run; content.sh; prerender) inside resource limits, so the machine never maxes out.
# The desktop crashed twice on 2026-10-22 when two agents built and tested at once (about 16 MSBuild nodes each, the compiler server
# and parallel test hosts on 16 cores and 11 GiB of RAM). Every heavy command, by the gate and by agents, goes through this script.
#
#   scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4
#
# What it does:
#   1. Serialises: one heavy command at a time on the whole machine (flock on a lock shared by every worktree). A second agent waits.
#   2. Preflight: waits while available memory is under VOCATERRA_MIN_FREE_MB (default 3000) or the 1-minute load is above the core count.
#   3. Caps: a systemd user scope with MemoryMax (default 6G), no swap, CPUQuota (default 1000% = 10 of 16 cores), nice 10. Over the cap
#      the kernel kills the command inside the scope; the desktop is untouched. Exit code 137 means "hit the memory cap".
#   4. No lingering servers: MSBuild node reuse, the MSBuild server and the shared Roslyn compiler server are off, so nothing outlives
#      the command.
#   5. Marks its command: VOCATERRA_LIMITED=1 is set for it, so a script that calls limited.sh itself (scripts/content-loop.sh) refuses to run
#      under it instead of waiting for ever on the lock it already holds.
#   6. Re-entrant (M67): called with VOCATERRA_LIMITED=1 already set (scripts/limited.sh scripts/test.sh ..., where test.sh calls limited.sh
#      for each of its steps), it runs the command directly: it is already inside the scope and holds the lock. Before, the inner call
#      waited for ever on the lock the outer one held (2026-11-01, 21 minutes). scripts/content-loop.sh still refuses to run under it.
set -uo pipefail

[ "$#" -gt 0 ] || { echo "usage: scripts/limited.sh <command> [args...]" >&2; exit 2; }

if [ "${VOCATERRA_LIMITED:-}" = 1 ]; then
  echo "limited.sh: already inside limited.sh (VOCATERRA_LIMITED=1); running the command directly, under the outer lock and caps" >&2
  exec "$@"
fi

MEM_MAX="${VOCATERRA_MEM_MAX:-6G}"
CPU_QUOTA="${VOCATERRA_CPU_QUOTA:-1000%}"
MIN_FREE_MB="${VOCATERRA_MIN_FREE_MB:-3000}"
LOCK="${XDG_RUNTIME_DIR:-/tmp}/vocaterra-heavy.lock"

export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false
export DOTNET_NOLOGO=1
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
export VOCATERRA_LIMITED=1

exec 9>"$LOCK"
if ! flock -n 9; then
  echo "limited.sh: another heavy command is running; waiting for it" >&2
  flock 9
fi

cores="$(nproc)"
for _ in $(seq 1 60); do
  avail_mb="$(awk '/MemAvailable/ {print int($2/1024)}' /proc/meminfo)"
  load="$(cut -d' ' -f1 /proc/loadavg)"
  if [ "$avail_mb" -ge "$MIN_FREE_MB" ] && awk -v l="$load" -v c="$cores" 'BEGIN {exit !(l <= c)}'; then break; fi
  echo "limited.sh: waiting (available ${avail_mb} MB, load ${load} on ${cores} cores)" >&2
  sleep 10
done

oom_rc=0
if systemd-run --user --scope --quiet true >/dev/null 2>&1; then
  # A named scope, no --collect: the unit stays (failed) after an oom-kill, so its Result can be read. The command's own exit code is not
  # enough: dotnet test exits 0 when the kernel kills a test host (H08a, 2026-11-07), so the scope's oom_kill count is read too.
  unit="vocaterra-limited-$$-$(date +%s)"
  systemd-run --user --scope --quiet --unit="$unit" \
    -p MemoryMax="$MEM_MAX" -p MemorySwapMax=0 -p CPUQuota="$CPU_QUOTA" \
    nice -n 10 "$@"
  rc=$?
  result="$(systemctl --user show -p Result --value "$unit.scope" 2>/dev/null || true)"
  [ "$result" = "oom-kill" ] && oom_rc=1
  systemctl --user reset-failed "$unit.scope" >/dev/null 2>&1 || true
else
  echo "limited.sh: systemd user scope unavailable; running with nice and the lock only (no memory cap)" >&2
  nice -n 10 "$@"
  rc=$?
fi
if [ "$rc" -eq 137 ] || [ "$oom_rc" -eq 1 ]; then
  echo "limited.sh: the command (or a process in it) was killed at the ${MEM_MAX} memory cap (command exit code ${rc}); lower -m or test parallelism" >&2
  exit 137
fi
exit "$rc"
