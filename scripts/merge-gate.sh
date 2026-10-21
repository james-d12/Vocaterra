#!/usr/bin/env bash
# Orchestrator merge gate for the whole repository (the language core under nlp/ and the game under game/).
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into main with --no-ff (aborting that merge on conflict), then runs the gate on Vocaterra.sln: build with warnings as
# errors, the whole test suite (the core's unit, golden and fuzz suites and the game's; model-backed tests run when VOCATERRA_MODELS_DIR is
# set), the format check, the game bundle validation and the core's offline licence scan. Every check's log is kept under .claude/gate-logs/
# so that an intermittent failure can be identified afterwards. Exit status is 0 only when every merge succeeded and every check passed.
#
# The licence scan (nlp/scripts/license-scan.sh) covers nlp/Vocaterra.Nlp.sln, the core and everything it pulls in; the game's own
# third-party packages (MonoGame, FontStashSharp) are not in it.
set -uo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1

[ "$#" -ge 1 ] || { echo "usage: scripts/merge-gate.sh \"<label>:<branch>\" [...]" >&2; exit 2; }
[ "$(git branch --show-current)" = "main" ] || { echo "not on main"; exit 1; }
[ -z "$(git status --short)" ] || { echo "main is dirty"; git status --short; exit 1; }

# Speech models: the core's ModelStore reads VOCATERRA_MODELS_DIR. The pinned models are fetched once into a shared folder outside the
# repository (nlp/scripts/fetch-models.sh with VOCATERRA_MODELS_DIR set), so tests that need a model run in the gate instead of being skipped.
if [ -z "${VOCATERRA_MODELS_DIR:-}" ]; then
  if [ -d "$HOME/.local/share/vocaterra/models" ]; then
    export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
  elif [ -d "$ROOT/nlp/models/vosk-model-small-es-0.42" ]; then
    export VOCATERRA_MODELS_DIR="$ROOT/nlp/models"
  fi
fi
echo "models: ${VOCATERRA_MODELS_DIR:-none}"

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
{ dotnet restore Vocaterra.sln && dotnet build Vocaterra.sln -warnaserror --no-restore; } > "$BUILDLOG" 2>&1
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
dotnet format Vocaterra.sln --verify-no-changes --no-restore > "$FORMATLOG" 2>&1
format_rc=$?
[ "$format_rc" -eq 0 ] && echo "format: clean" || { echo "format: DIRTY"; head -20 "$FORMATLOG"; }

CONTENTLOG=".claude/gate-logs/content-$STAMP.log"
bash scripts/content.sh validate > "$CONTENTLOG" 2>&1
content_rc=$?
tail -1 "$CONTENTLOG"

LICENSELOG=".claude/gate-logs/license-$STAMP.log"
bash nlp/scripts/license-scan.sh --offline > "$LICENSELOG" 2>&1
license_rc=$?
tail -1 "$LICENSELOG"

ok=1
[ "$build_rc" -eq 0 ] || ok=0
{ [ "$test_rc" -eq 0 ] && [ -n "$SUMMARY" ]; } || ok=0
[ "$format_rc" -eq 0 ] || ok=0
[ "$content_rc" -eq 0 ] || ok=0
[ "$license_rc" -eq 0 ] || ok=0
echo "logs: $BUILDLOG $TESTLOG $FORMATLOG $CONTENTLOG $LICENSELOG"
[ "${#failed[@]}" -eq 0 ] && echo "merge-failures: none" || { echo "merge-failures: ${failed[*]}"; ok=0; }
if [ "$ok" -eq 1 ]; then echo "result: PASS"; else echo "result: FAIL"; exit 1; fi
