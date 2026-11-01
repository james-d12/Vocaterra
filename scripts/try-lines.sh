#!/usr/bin/env bash
# Grade lines of Spanish on the bundle and print one stable result line per line, so two runs compare with diff (M65).
#
#   scripts/limited.sh scripts/try-lines.sh [--host] <file>...
#   scripts/limited.sh scripts/try-lines.sh --check            every file of scripts/try-lines/ against its .expected baseline
#
# Each file holds "prompt|line" rows; blank lines and lines starting with # are skipped. Each row prints
#   <prompt> | <line> => <intent> comm=<True|False> <tag>=<expected> ... (or -)
# By default a row goes through the core's demo CLI (tools/Vocaterra.Nlp.Cli, its eval subcommand, on content/game-v1). With --host it goes
# through the game's path (src/Vocaterra.Host --transcript), which first takes out a greeting and the listener's name (Luz, quiero pan).
# Both run the Debug binaries already built in this checkout and stop with exit 2 when one is missing:
#   scripts/limited.sh dotnet build tools/Vocaterra.Nlp.Cli -m:4      scripts/limited.sh dotnet build src/Vocaterra.Host -m:4
# The line files of scripts/try-lines/ are correct Spanish, apart from the items each header lists; *.expected is each file's output when it
# was committed (*.host.expected with --host). A brief can then say "scripts/try-lines.sh --check gives the baseline, apart from the lines
# this milestone fixes". Needs only bash and python3. Run it through scripts/limited.sh, as every command that starts dotnet.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1

CLI="tools/Vocaterra.Nlp.Cli/bin/Debug/net10.0/Vocaterra.Nlp.Cli.dll"
HOST="src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll"
CONTENT="content/game-v1"
LINES_DIR="scripts/try-lines"

usage() {
  echo "usage: scripts/try-lines.sh [--host] <file>...   or   scripts/try-lines.sh --check" >&2
  exit 2
}

# Turns the raw output of each row (the CLI's JSON line, or the host's report) into the stable result line. Input: one record per row,
# "prompt<TAB>line<TAB>output", the output's newlines written as the unit separator (0x1f).
FORMAT='
import json, sys
mode = sys.argv[1]
for row in sys.stdin:
    prompt, line, raw = row.rstrip("\n").split("\t", 2)
    raw = raw.replace("\x1f", "\n")
    intent = comm = None
    errors = []
    try:
        if mode == "cli":
            verdict = json.loads(raw.split("\n", 1)[0])
            intent, comm = verdict.get("intent"), verdict.get("communicated")
            errors = [(e["tag"], e.get("expected") or "") for e in verdict.get("errors", [])]
        else:
            for text in raw.split("\n"):
                text = text.strip()
                if text.startswith("intent="):
                    fields = dict(f.split("=", 1) for f in text.split())
                    intent, comm = fields["intent"], fields["communicated"] == "true"
                elif text.startswith("error "):
                    tag, rest = text[len("error "):].split(": expected ", 1)
                    errors.append((tag, rest[1:rest.index("\x27, heard \x27")]))
        if intent is None:
            raise ValueError("no verdict")
        shown = " ".join(tag + ("=" + expected if expected else "") for tag, expected in errors) or "-"
        print(f"{prompt:14} | {line:42} => {intent} comm={comm} {shown}")
    except Exception:
        print(f"{prompt:14} | {line:42} => RAW {raw[:200]!r}")
'

run_file() {
  local mode="$1" file="$2" prompt line out
  while IFS='|' read -r prompt line || [ -n "$prompt" ]; do
    case "$prompt" in ''|'#'*) continue ;; esac
    if [ "$mode" = host ]; then
      out=$(dotnet "$HOST" --content "$CONTENT" --transcript "$line" --prompt "$prompt" < /dev/null 2>&1)
    else
      out=$(dotnet "$CLI" eval --transcript "$line" --prompt "$prompt" --content "$CONTENT" < /dev/null 2>&1)
    fi
    printf '%s\t%s\t%s\n' "$prompt" "$line" "${out//$'\n'/$'\x1f'}"
  done < "$file" | python3 -I -c "$FORMAT" "$mode"
}

need() {
  [ -f "$1" ] || { echo "scripts/try-lines.sh: $1 is missing; build it first: scripts/limited.sh dotnet build $2 -m:4" >&2; exit 2; }
}

mode=cli
check=0
files=()
for arg in "$@"; do
  case "$arg" in
    --host) mode=host ;;
    --check) check=1 ;;
    -h|--help) usage ;;
    -*) echo "scripts/try-lines.sh: unknown option $arg" >&2; usage ;;
    *) files+=("$arg") ;;
  esac
done
command -v python3 > /dev/null || { echo "scripts/try-lines.sh: python3 is needed" >&2; exit 2; }

if [ "$check" = 1 ]; then
  [ "${#files[@]}" -eq 0 ] || usage
  need "$CLI" tools/Vocaterra.Nlp.Cli
  need "$HOST" src/Vocaterra.Host
  status=0
  for file in "$LINES_DIR"/*.txt; do
    for m in cli host; do
      expected="${file%.txt}.expected"
      [ "$m" = host ] && expected="${file%.txt}.host.expected"
      actual="$(run_file "$m" "$file")"
      if diff -u "$expected" <(printf '%s\n' "$actual") > /dev/null; then
        echo "same as $expected"
      else
        echo "DIFFERS from $expected (- baseline, + now):"
        diff -u "$expected" <(printf '%s\n' "$actual") | tail -n +3
        status=1
      fi
    done
  done
  exit "$status"
fi

[ "${#files[@]}" -gt 0 ] || usage
if [ "$mode" = host ]; then need "$HOST" src/Vocaterra.Host; else need "$CLI" tools/Vocaterra.Nlp.Cli; fi
for file in "${files[@]}"; do
  [ -f "$file" ] || { echo "scripts/try-lines.sh: no file $file" >&2; exit 2; }
  run_file "$mode" "$file"
done
