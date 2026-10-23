#!/usr/bin/env bash
# Orchestrator merge gate for the whole repository (the language core, the Vocaterra.Nlp.* projects, and the game).
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into the target branch (main, or $GATE_TARGET when set; every series merges into main since 2026-10-22) with --no-ff (aborting that merge on conflict), then runs the gate on Vocaterra.sln: build with warnings as
# errors, the whole test suite (the core's unit, golden and fuzz suites and the game's; model-backed tests run when VOCATERRA_MODELS_DIR is
# set), the format check, the game bundle validation and the core's offline licence scan. Every check's log is kept under .claude/gate-logs/
# so that an intermittent failure can be identified afterwards. Exit status is 0 only when every merge succeeded and every check passed.
#
# The licence scan (scripts/license-scan.sh) covers Vocaterra.Nlp.slnf (the core projects) and everything they pull in; the game's own
# third-party packages (MonoGame, FontStashSharp) are not in it.
set -uo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# Resource limits (owner, 2026-10-22, after two desktop crashes): every heavy step runs through scripts/limited.sh (one heavy command
# on the machine at a time, a memory and CPU cap, no lingering build servers), the build with -m:4 and the tests with -m:2.
L="$(cd "$(dirname "$0")" && pwd)/limited.sh"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1

[ "$#" -ge 1 ] || { echo "usage: scripts/merge-gate.sh \"<label>:<branch>\" [...]" >&2; exit 2; }
TARGET="${GATE_TARGET:-main}"
[ "$(git branch --show-current)" = "$TARGET" ] || { echo "not on $TARGET"; exit 1; }
[ -z "$(git status --short)" ] || { echo "$TARGET is dirty"; git status --short; exit 1; }

# Speech models: the core's ModelStore reads VOCATERRA_MODELS_DIR. The pinned models are fetched once into a shared folder outside the
# repository (scripts/fetch-models.sh with VOCATERRA_MODELS_DIR set), so tests that need a model run in the gate instead of being skipped.
if [ -z "${VOCATERRA_MODELS_DIR:-}" ]; then
  if [ -d "$HOME/.local/share/vocaterra/models" ]; then
    export VOCATERRA_MODELS_DIR="$HOME/.local/share/vocaterra/models"
  elif [ -d "$ROOT/models/vosk-model-small-es-0.42" ]; then
    export VOCATERRA_MODELS_DIR="$ROOT/models"
  fi
fi
echo "models: ${VOCATERRA_MODELS_DIR:-none}"

mkdir -p .claude/gate-logs
STAMP="$(date +%Y%m%d-%H%M%S)"
failed=()
for spec in "$@"; do
  label="${spec%%:*}"; branch="${spec#*:}"
  echo "=== merging $label ($branch) ==="
  if git merge --no-ff --no-edit -m "Merge $label ($branch) into $TARGET" "$branch" >/dev/null 2>&1; then
    echo "merged: $(git log --oneline -1)"
  else
    echo "CONFLICT in $label:"; git diff --name-only --diff-filter=U; git merge --abort; failed+=("$label")
  fi
done

echo "=== gate ==="
BUILDLOG=".claude/gate-logs/build-$STAMP.log"
{ "$L" dotnet restore Vocaterra.sln -m:4 && "$L" dotnet build Vocaterra.sln -warnaserror --no-restore -m:4; } > "$BUILDLOG" 2>&1
build_rc=$?
grep -E "Warning\(s\)|Error\(s\)" "$BUILDLOG" | sort | uniq -c
grep -E ": error " "$BUILDLOG" | head -10

TESTLOG=".claude/gate-logs/test-$STAMP.log"
"$L" dotnet test Vocaterra.sln --no-build -m:2 > "$TESTLOG" 2>&1
test_rc=$?
SUMMARY="$(grep -E "^(Passed|Failed)!" "$TESTLOG" | sed -E 's/,//g')"
echo "$SUMMARY" | awk 'NF {f+=$4; p+=$6; s+=$8} END {print "tests: passed="p" failed="f" skipped="s}'
grep -E "^\s+Failed |\[FAIL\]" "$TESTLOG" | head -20

FORMATLOG=".claude/gate-logs/format-$STAMP.log"
"$L" dotnet format Vocaterra.sln --verify-no-changes --no-restore > "$FORMATLOG" 2>&1
format_rc=$?
[ "$format_rc" -eq 0 ] && echo "format: clean" || { echo "format: DIRTY"; head -20 "$FORMATLOG"; }

CONTENTLOG=".claude/gate-logs/content-$STAMP.log"
"$L" bash scripts/content.sh validate > "$CONTENTLOG" 2>&1
content_rc=$?
tail -1 "$CONTENTLOG"
# The prototype's content gate (S5, D-game-10 and D-game-12): strict lint at A1 (the guide's host-voiced pools are rendered by the game) and
# the CEFR check at A1 and A2.
if [ -f content/PHRASES.md ]; then
  { "$L" bash scripts/content.sh lint content/game-v1 --strict --level a1 --host-pools 'guide.*' &&
    "$L" bash scripts/content.sh cefr-check content/game-v1 --level a1 &&
    "$L" bash scripts/content.sh cefr-check content/game-v1 --level a2; } >> "$CONTENTLOG" 2>&1 || { content_rc=1; echo "content gate: FAIL (lint --strict or cefr-check)"; }
  [ "$content_rc" -eq 0 ] && echo "content gate: lint --strict a1 and cefr-check a1/a2 clean"
fi

# The LanguageTool grammar check (core M30, D10's dev-time half): ADVISORY, offline from the committed cache, never fails the gate. It
# becomes required after the first waiver round (owner, 2026-10-22); S23 adds the game bundle.
GRAMMARLOG=".claude/gate-logs/grammar-$STAMP.log"
if [ -f content/grammar/v1-cache.json ]; then
  "$L" dotnet run --project tools/Vocaterra.Nlp.ContentBuilder --no-build -- grammar content/v1 --offline \
    --cache content/grammar/v1-cache.json --waivers content/grammar/v1-waivers.json > "$GRAMMARLOG" 2>&1
  echo "grammar (advisory, content/v1): $(grep -E '^grammar:' "$GRAMMARLOG" | tail -1)"
fi
# The game bundle (S23): REQUIRED since its first waiver round (owner, 2026-10-22). --strict fails on any unwaived warning or a cache miss;
# after a content change, refresh the strings file and the cache (docs/game/README.md) with the local server, then rerun.
grammar_rc=0
if [ -f content/grammar/game-v1-cache.json ]; then
  { "$L" bash scripts/content.sh grammar content/game-v1 --strings content/grammar/game-v1-strings.jsonl --strings-only --offline \
      --strict; } >> "$GRAMMARLOG" 2>&1 || grammar_rc=1
  echo "grammar (required, game-v1): $(grep -E '^grammar:' "$GRAMMARLOG" | tail -1)"
  [ "$grammar_rc" -eq 0 ] || echo "grammar: FAIL (game-v1; see $GRAMMARLOG)"
fi

LICENSELOG=".claude/gate-logs/license-$STAMP.log"
"$L" bash scripts/license-scan.sh --offline > "$LICENSELOG" 2>&1
license_rc=$?
tail -1 "$LICENSELOG"

ok=1
[ "$build_rc" -eq 0 ] || ok=0
{ [ "$test_rc" -eq 0 ] && [ -n "$SUMMARY" ]; } || ok=0
[ "$format_rc" -eq 0 ] || ok=0
[ "$content_rc" -eq 0 ] || ok=0
[ "$license_rc" -eq 0 ] || ok=0
[ "${grammar_rc:-0}" -eq 0 ] || ok=0
echo "logs: $BUILDLOG $TESTLOG $FORMATLOG $CONTENTLOG $LICENSELOG ${GRAMMARLOG:-}"
[ "${#failed[@]}" -eq 0 ] && echo "merge-failures: none" || { echo "merge-failures: ${failed[*]}"; ok=0; }
if [ "$ok" -eq 1 ]; then echo "result: PASS"; else echo "result: FAIL"; exit 1; fi
