#!/usr/bin/env bash
# Orchestrator merge gate for the game repository (Vocaterra).
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into main with --no-ff (aborting that merge on conflict), then runs the gate: build with warnings as errors, the whole
# test suite, the format check and the content bundle validation. Every check's log is kept under .claude/gate-logs/ so that an intermittent
# failure can be identified afterwards. Exit status is 0 only when every merge succeeded and every check passed.
set -uo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1

[ "$#" -ge 1 ] || { echo "usage: scripts/merge-gate.sh \"<label>:<branch>\" [...]" >&2; exit 2; }
[ "$(git branch --show-current)" = "main" ] || { echo "not on main"; exit 1; }
[ -z "$(git status --short)" ] || { echo "main is dirty"; git status --short; exit 1; }

# The core's packages come from the submodule's local feed; pack it once if this checkout has never been packed.
if [ ! -f external/Vocaterra.NLP/artifacts/feed/Vocaterra.Nlp.Core.0.1.0.nupkg ]; then
  echo "=== the core's local feed is missing: initialising the submodule and packing it"
  git submodule update --init external/Vocaterra.NLP || exit 1
  bash external/Vocaterra.NLP/scripts/pack.sh > /dev/null || exit 1
fi

mkdir -p .claude/gate-logs
STAMP="$(date +%Y%m%d-%H%M%S)"
failed=()
for spec in "$@"; do
  label="${spec%%:*}"; branch="${spec#*:}"
  echo "=== merging $label ($branch) ==="
  if git merge --no-ff --no-edit -m "Merge $label ($branch) into main" "$branch" >/dev/null 2>&1; then
    echo "merged: $(git log --oneline -1)"
  else
    echo "CONFLICT in $label:"; git diff --name-only --diff-filter=U; git merge --abort; failed+=("$label")
  fi
done

echo "=== gate ==="
BUILDLOG=".claude/gate-logs/build-$STAMP.log"
dotnet build Vocaterra.sln -warnaserror > "$BUILDLOG" 2>&1
build_rc=$?
grep -E "Warning\(s\)|Error\(s\)" "$BUILDLOG" | sort | uniq -c
grep -E ": error " "$BUILDLOG" | head -10

TESTLOG=".claude/gate-logs/test-$STAMP.log"
dotnet test Vocaterra.sln --no-build > "$TESTLOG" 2>&1
test_rc=$?
SUMMARY="$(grep -E "^(Passed|Failed)!" "$TESTLOG" | sed -E 's/,//g')"
echo "$SUMMARY" | awk 'NF {f+=$4; p+=$6; s+=$8} END {print "tests: passed="p" failed="f" skipped="s}'
grep -E "^\s+Failed |\[FAIL\]" "$TESTLOG" | head -20

FORMATLOG=".claude/gate-logs/format-$STAMP.log"
dotnet format Vocaterra.sln --verify-no-changes > "$FORMATLOG" 2>&1
format_rc=$?
[ "$format_rc" -eq 0 ] && echo "format: clean" || { echo "format: DIRTY"; head -20 "$FORMATLOG"; }

CONTENTLOG=".claude/gate-logs/content-$STAMP.log"
bash scripts/content.sh validate > "$CONTENTLOG" 2>&1
content_rc=$?
tail -1 "$CONTENTLOG"

ok=1
[ "$build_rc" -eq 0 ] || ok=0
{ [ "$test_rc" -eq 0 ] && [ -n "$SUMMARY" ]; } || ok=0
[ "$format_rc" -eq 0 ] || ok=0
[ "$content_rc" -eq 0 ] || ok=0
echo "logs: $BUILDLOG $TESTLOG $FORMATLOG $CONTENTLOG"
[ "${#failed[@]}" -eq 0 ] && echo "merge-failures: none" || { echo "merge-failures: ${failed[*]}"; ok=0; }
if [ "$ok" -eq 1 ]; then echo "result: PASS"; else echo "result: FAIL"; exit 1; fi
