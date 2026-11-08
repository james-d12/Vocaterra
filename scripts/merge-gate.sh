#!/usr/bin/env bash
# Orchestrator merge gate for the whole repository (the language core, the engine Vocaterra.Nlp and its Vocaterra.Nlp.* projects, and the game).
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into the target branch $GATE_TARGET, a feature branch, never main (owner, 2026-10-23: nothing is merged into or pushed
# to main; the owner merges feature branches by pull request), with --no-ff (aborting that merge on conflict), then runs the gate on
# Vocaterra.sln: build with warnings as
# errors, the whole test suite (the core's unit, golden and fuzz suites and the game's; model-backed tests run when VOCATERRA_MODELS_DIR is
# set), the format check, the game bundle validation with the content gate (the lint at A2, the bundle's ceiling, since V08) and the core's
# offline licence scan. Every check's log is kept under .claude/gate-logs/
# so that an intermittent failure can be identified afterwards. Exit status is 0 only when every merge succeeded and every check passed.
#
# The licence scan (scripts/license-scan.sh) covers Vocaterra.Nlp.slnf (the core projects) and everything they pull in, and the content schema
# test's project (tests/Vocaterra.Game.Core.Tests, with NJsonSchema); the game's own third-party packages (MonoGame, FontStashSharp) are not in it.
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
TARGET="${GATE_TARGET:-}"
[ -n "$TARGET" ] || { echo "set GATE_TARGET to the feature branch to merge into (never main)" >&2; exit 2; }
[ "$TARGET" != "main" ] || { echo "refusing to merge into main (owner, 2026-10-23); use a feature branch" >&2; exit 2; }
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
# Every test assembly that starts must report a result (H08a: a test host killed for memory printed nothing and dotnet test still exited 0).
runs="$(grep -c "Test run for " "$TESTLOG")"
results="$(grep -cE "^(Passed|Failed)!" "$TESTLOG")"
if [ "$runs" -ne "$results" ]; then
  echo "tests: INCOMPLETE, $runs test runs started but $results results; no result for:"
  grep "Test run for " "$TESTLOG" | sed -E 's/^Test run for //; s/ \(.*$//' | while read -r asm; do
    grep -qE "^(Passed|Failed)!.* - $(basename "$asm") " "$TESTLOG" || echo "  $asm"
  done
  test_rc=1
fi

FORMATLOG=".claude/gate-logs/format-$STAMP.log"
"$L" dotnet format Vocaterra.sln --verify-no-changes --no-restore > "$FORMATLOG" 2>&1
format_rc=$?
[ "$format_rc" -eq 0 ] && echo "format: clean" || { echo "format: DIRTY"; head -20 "$FORMATLOG"; }

CONTENTLOG=".claude/gate-logs/content-$STAMP.log"
"$L" bash scripts/content.sh validate > "$CONTENTLOG" 2>&1
content_rc=$?
grep -E "^lint:" "$CONTENTLOG" | tail -1
# The prototype's content gate (S5, D-game-10 and D-game-12), since V08 with the A1/A2 rule of village-plan 3.9 (D-game-22 (b)). The bundle's
# ceiling is A2: the strict lint at A2 with --fail-on-level fails on a lemma above A2 in a dialogue template, a prompt's text or an answer set's
# canonical answer (the guide's host-voiced pools are rendered by the game), and cefr-check at A2 fails on any lemma it lists (the tool itself
# only prints, so its counts are read here). cefr-check at A1 is a report for the owner, what is above A1, and never fails the gate. A lesson's
# own level (a1 or a2) is the game's validator's: the host's --validate-content holds each lesson's Spanish and tenses to it.
if [ -f content/PHRASES.md ]; then
  echo "== lint --strict --level a2 --fail-on-level (the bundle's ceiling; fails the gate)" >> "$CONTENTLOG"
  if "$L" bash scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*' >> "$CONTENTLOG" 2>&1; then
    echo "content gate: lint --strict --level a2 --fail-on-level clean (the ceiling, fails the gate)"
  else
    content_rc=1; echo "content gate: FAIL (lint --strict --level a2 --fail-on-level: a lemma above A2, or another lint error or warning)"
  fi
  echo "== cefr-check a2 (the ceiling; a lemma it lists fails the gate)" >> "$CONTENTLOG"
  CEFR_A2="$("$L" bash scripts/content.sh cefr-check content/game-v1 --level a2 2>&1)"
  cefr_a2_rc=$?
  printf '%s\n' "$CEFR_A2" >> "$CONTENTLOG"
  if [ "$cefr_a2_rc" -eq 0 ] && ! grep -Eq ', [1-9][0-9]* not in the list$' <<<"$CEFR_A2"; then
    echo "content gate: cefr-check a2 clean (the ceiling, fails the gate)"
  else
    content_rc=1; echo "content gate: FAIL (cefr-check a2 lists a lemma above A2, or did not run)"
  fi
  echo "== cefr-check a1 (a report for the owner: the lemmas above A1; never fails the gate)" >> "$CONTENTLOG"
  CEFR_A1="$("$L" bash scripts/content.sh cefr-check content/game-v1 --level a1 2>&1)"
  printf '%s\n' "$CEFR_A1" >> "$CONTENTLOG"
  echo "content gate: cefr-check a1 report, does not fail: $(sed -nE 's/^cefr a1: [0-9]+ lemmas in (.+), [0-9]+ covered, ([0-9]+) not in the list$/\2 above A1 in \1/p' <<<"$CEFR_A1" | paste -sd ';' - | sed 's/;/; /g')"
  echo "== the game's validator (the host's --validate-content: the game's files, and each lesson's Spanish and tenses within its level)" >> "$CONTENTLOG"
  if "$L" dotnet run --project src/Vocaterra.Host --no-build -- --content content/game-v1 --validate-content >> "$CONTENTLOG" 2>&1; then
    echo "content gate: --validate-content clean (each lesson within its level, fails the gate)"
  else
    content_rc=1; echo "content gate: FAIL (--validate-content; see $CONTENTLOG)"
  fi
fi

# The LanguageTool grammar check (core M30, D10's dev-time half), on the repository's one bundle (R02: content/v1 and its advisory step are gone).
# REQUIRED since the first waiver round (owner, 2026-10-22). --strict fails on any unwaived warning or a cache miss, offline from the committed
# cache; after a content change, refresh the strings file and the cache (docs/game/content.md) with the local server, then rerun.
GRAMMARLOG=".claude/gate-logs/grammar-$STAMP.log"
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
grep -v "^limited.sh:" "$LICENSELOG" | tail -1
# The content schema test's project (content-plan C01) is scanned too: it is the one game test project with a package of its own (NJsonSchema).
{ echo "=== tests/Vocaterra.Game.Core.Tests"; "$L" bash scripts/license-scan.sh --offline --target tests/Vocaterra.Game.Core.Tests/Vocaterra.Game.Core.Tests.csproj; } >> "$LICENSELOG" 2>&1 || license_rc=1
echo "licence scan (Game.Core.Tests): $(grep -v "^limited.sh:" "$LICENSELOG" | tail -1)"

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
