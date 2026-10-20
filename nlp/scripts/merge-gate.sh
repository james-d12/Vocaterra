#!/usr/bin/env bash
# Orchestrator merge gate.
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into main with --no-ff (aborting that merge on conflict), then runs the full gate:
# build with warnings as errors, the whole test suite with models, format check, offline license scan.
# Logs are kept under .claude/gate-logs/ so an intermittent failure can be identified afterwards.
set -uo pipefail
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1; export DOTNET_NOLOGO=1
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export SLG_MODELS_DIR="${SLG_MODELS_DIR:-$ROOT/models}"
cd "$ROOT" || exit 1
[ "$(git branch --show-current)" = "main" ] || { echo "not on main"; exit 1; }
[ -z "$(git status --short)" ] || { echo "main is dirty"; git status --short; exit 1; }
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
dotnet build -warnaserror > "$BUILDLOG" 2>&1
grep -E "Warning\(s\)|Error\(s\)" "$BUILDLOG" | sort | uniq -c
grep -E "error " "$BUILDLOG" | head -10
TESTLOG=".claude/gate-logs/test-$STAMP.log"
dotnet test --no-build > "$TESTLOG" 2>&1
grep -E "^(Passed|Failed)!" "$TESTLOG" | sed -E 's/,//g' | awk '{f+=$4; p+=$6; s+=$8} END {print "tests: passed="p" failed="f" skipped="s}'
grep -E "^\s+Failed |\[FAIL\]" "$TESTLOG" | head -20
dotnet format --verify-no-changes >/dev/null 2>&1 && echo "format: clean" || echo "format: DIRTY"
bash scripts/license-scan.sh --offline 2>&1 | tail -1
echo "logs: $BUILDLOG $TESTLOG"
[ ${#failed[@]} -eq 0 ] && echo "merge-failures: none" || echo "merge-failures: ${failed[*]}"
