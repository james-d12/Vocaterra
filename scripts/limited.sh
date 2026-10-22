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
set -uo pipefail

MEM_MAX="${VOCATERRA_MEM_MAX:-6G}"
CPU_QUOTA="${VOCATERRA_CPU_QUOTA:-1000%}"
MIN_FREE_MB="${VOCATERRA_MIN_FREE_MB:-3000}"
LOCK="${XDG_RUNTIME_DIR:-/tmp}/vocaterra-heavy.lock"

export MSBUILDDISABLENODEREUSE=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export UseSharedCompilation=false
export DOTNET_NOLOGO=1
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"

[ "$#" -gt 0 ] || { echo "usage: scripts/limited.sh <command> [args...]" >&2; exit 2; }

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

if systemd-run --user --scope --quiet true >/dev/null 2>&1; then
  systemd-run --user --scope --quiet --collect \
    -p MemoryMax="$MEM_MAX" -p MemorySwapMax=0 -p CPUQuota="$CPU_QUOTA" \
    nice -n 10 "$@"
  rc=$?
else
  echo "limited.sh: systemd user scope unavailable; running with nice and the lock only (no memory cap)" >&2
  nice -n 10 "$@"
  rc=$?
fi
[ "$rc" -eq 137 ] && echo "limited.sh: the command was killed at the ${MEM_MAX} memory cap; lower -m or test parallelism" >&2
exit "$rc"
