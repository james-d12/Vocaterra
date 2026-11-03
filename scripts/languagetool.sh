#!/usr/bin/env bash
# Starts, stops or reports the local LanguageTool server that `vocaterra-content grammar` calls (docs/nlp/decisions.md D10). The server is a
# separate development-time process: the public API is never used, and no content leaves the machine.
#
# Usage: scripts/languagetool.sh start|stop|status
#   start   runs the server in a capped systemd user scope (MemoryMax=2G, MemorySwapMax=0, CPUQuota=200%, nice 10, java -Xmx1200m) on
#           localhost:${VOCATERRA_LT_PORT:-8081}, waits until it answers /v2/languages, then returns. It refuses to start when less than
#           ${VOCATERRA_LT_MIN_FREE_MB:-4096} MB is available. Without systemd user scopes it runs under nice with a pid file.
#   stop    stops the server (and checks that the port is free).
#   status  prints "running" or "stopped"; exit 0 when running, 1 when stopped.
#
# The Java runtime and LanguageTool come from scripts/fetch-tools.sh (tools/manifest.json). This script does not use scripts/limited.sh: the
# server is idle between runs and its own scope is the cap.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
manifest="$repo_root/tools/manifest.json"
tools="${VOCATERRA_TOOLS_DIR:-$HOME/.local/share/vocaterra/tools}"
port="${VOCATERRA_LT_PORT:-8081}"
min_free_mb="${VOCATERRA_LT_MIN_FREE_MB:-4096}"
unit="vocaterra-languagetool"
pidfile="$tools/languagetool.pid"
log="$tools/lt-server.log"
url="http://localhost:$port"

fail() { printf 'languagetool.sh: %s\n' "$*" >&2; exit 1; }

usage() {
  printf 'usage: scripts/languagetool.sh start|stop|status\n' >&2
  exit 2
}

# The install paths come from the manifest, so this script and fetch-tools.sh cannot disagree.
install_path() {
  python3 -I - "$manifest" "$1" <<'PY'
import json
import sys

manifest = json.load(open(sys.argv[1], encoding="utf-8"))
wanted = sys.argv[2]
for tool in manifest["tools"]:
    if tool["id"].startswith(wanted):
        print(tool["install"]["dir"] + "/" + tool["install"]["check"])
        break
else:
    sys.exit("no tool " + wanted + " in the manifest")
PY
}

scope_active() {
  systemctl --user is-active --quiet "$unit.scope" 2>/dev/null
}

server_answers() {
  curl -fsS -m 3 "$url/v2/languages" >/dev/null 2>&1
}

mode="${1:-}"
case "$mode" in
  start | stop | status) ;;
  *) usage ;;
esac

java_bin="$tools/$(install_path temurin)"
lt_jar="$tools/$(install_path languagetool)"

case "$mode" in
  status)
    if scope_active || { [ -f "$pidfile" ] && kill -0 "$(cat "$pidfile")" 2>/dev/null; }; then
      printf 'running at %s\n' "$url"
      exit 0
    fi
    if server_answers; then
      printf 'running at %s (started outside this script)\n' "$url"
      exit 0
    fi
    printf 'stopped\n'
    exit 1
    ;;

  stop)
    if scope_active; then
      systemctl --user stop "$unit.scope"
    fi
    if [ -f "$pidfile" ]; then
      pid="$(cat "$pidfile")"
      if kill -0 "$pid" 2>/dev/null; then
        kill "$pid"
      fi
      rm -f "$pidfile"
    fi
    for _ in $(seq 1 30); do
      if ! server_answers; then
        printf 'stopped\n'
        exit 0
      fi
      sleep 1
    done
    fail "the server still answers on $url after stop"
    ;;

  start)
    if server_answers; then
      printf 'already running at %s\n' "$url"
      exit 0
    fi
    [ -x "$java_bin" ] || fail "no Java runtime at $java_bin; run scripts/fetch-tools.sh first"
    [ -f "$lt_jar" ] || fail "no LanguageTool server jar at $lt_jar; run scripts/fetch-tools.sh first"
    available_mb="$(awk '/MemAvailable/ {print int($2/1024)}' /proc/meminfo)"
    if [ "$available_mb" -lt "$min_free_mb" ]; then
      fail "only ${available_mb} MB is available and the server needs about 2 GB (at least ${min_free_mb} MB free); free memory first"
    fi

    server_cmd=(nice -n 10 "$java_bin" -Xmx1200m -cp "$lt_jar" org.languagetool.server.HTTPServer --port "$port")
    mkdir -p "$tools"
    : > "$log"
    if systemd-run --user --scope --quiet true >/dev/null 2>&1; then
      setsid systemd-run --user --scope --quiet --unit="$unit" \
        -p MemoryMax=2G -p MemorySwapMax=0 -p CPUQuota=200% \
        "${server_cmd[@]}" >>"$log" 2>&1 </dev/null &
      printf '%s\n' "$!" > "$pidfile"
    else
      printf 'languagetool.sh: systemd user scopes are unavailable; starting without the memory cap\n' >&2
      setsid "${server_cmd[@]}" >>"$log" 2>&1 </dev/null &
      printf '%s\n' "$!" > "$pidfile"
    fi

    for _ in $(seq 1 120); do
      if server_answers; then
        printf 'started: LanguageTool at %s (log %s)\n' "$url" "$log"
        exit 0
      fi
      sleep 1
    done
    tail -n 20 "$log" >&2 || true
    fail "the server did not answer on $url within 120 s"
    ;;
esac
