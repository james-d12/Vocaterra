#!/usr/bin/env bash
# Orchestrator merge gate for the whole repository (the language core, the engine Vocaterra.Nlp and its Vocaterra.Nlp.* projects, and the game).
# Usage: scripts/merge-gate.sh "<label>:<branch>" ["<label>:<branch>" ...]
# Merges each branch into the target branch $GATE_TARGET, a feature branch, never main (owner, 2026-10-23: nothing is merged into or pushed
# to main; the owner merges feature branches by pull request), with --no-ff (aborting that merge on conflict), then runs the gate on
# Vocaterra.sln: build in Release with warnings as errors (B29a: the tests run on the Release build, 5 to 8 times faster than Debug for world
# generation), the test suite without the fuzz suite and the timing tests (the core's unit and golden suites and the game's; model-backed tests run
# when VOCATERRA_MODELS_DIR is set), then the timing tests as their own step, alone, after the suite, the format check, the game bundle validation
# with the content gate (the lint at A2, the bundle's ceiling, since V08), the grammar replay and the licence scans. The fuzz suite runs in CI
# only (owner, 2026-11-18, D-game-34 item 58). Every check's log is kept under .claude/gate-logs/ so that an intermittent failure can be identified
# afterwards. Exit status is 0 only when every merge succeeded and every check passed.
#
# B29a, the whole gate is one limited.sh command: the script re-runs itself once under scripts/limited.sh, so it holds one slot and one
# scope's caps (10 GB, 10 cores, no swap) from the merge to the last scan and never queues for a slot after it starts; its inner limited.sh
# calls run directly through the re-entrancy (VOCATERRA_LIMITED=1). The merge, the commit message and the log names are unchanged.
#
# A failed test (a step's TRX files under .claude/gate-logs/trx-<stamp>/) is re-run alone in its project and labelled: FLAKY (passed alone) or
# FAILED. The step passes when every failure is FLAKY; the FLAKY names are printed in the summary and kept in .claude/gate-logs/flaky-<stamp>.txt for
# the orchestrator to record. More than 20 failures in a step fail it without a re-run. After the test steps the gate prints each assembly's
# time and its slowest class's time next to the last gate's (timing-<stamp>.json, scripts/gate-tests.py), with a warning, never a failure,
# for 10 percent growth or a class over 5 minutes.
#
# The licence scan (scripts/license-scan.sh) covers Vocaterra.Nlp.slnf (the core projects) and everything they pull in; the content schema
# test's project (tests/Vocaterra.Game.Core.Tests, with NJsonSchema); and the game's shipped projects, Vocaterra.Game (MonoGame, FontStashSharp,
# the speech runtime), Vocaterra.Host and tools/Vocaterra.SpeechRender. The one LGPL package, MonoGame.Library.OpenAL, passes only through its
# exact-version entry in scripts/license-exceptions.txt (owner, 2026-11-17, D-game-34 item 49).
set -uo pipefail
if [ -d "$HOME/.dotnet" ]; then export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"; fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
# Resource limits (owner, 2026-10-22, after two desktop crashes): every heavy step runs through scripts/limited.sh (a slot, a memory and CPU cap,
# no lingering build servers), the build with -m:4 and the tests with -m:2.
CONFIG="${VOCATERRA_CONFIG:-Release}"
L="$(cd "$(dirname "$0")" && pwd)/limited.sh"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT" || exit 1

[ "$#" -ge 1 ] || { echo "usage: scripts/merge-gate.sh \"<label>:<branch>\" [...]" >&2; exit 2; }
TARGET="${GATE_TARGET:-}"
[ -n "$TARGET" ] || { echo "set GATE_TARGET to the feature branch to merge into (never main)" >&2; exit 2; }
[ "$TARGET" != "main" ] || { echo "refusing to merge into main (owner, 2026-10-23); use a feature branch" >&2; exit 2; }
[ "$(git branch --show-current)" = "$TARGET" ] || { echo "not on $TARGET"; exit 1; }
[ -z "$(git status --short)" ] || { echo "$TARGET is dirty"; git status --short; exit 1; }

# One slot for the whole gate (B29a): re-run under limited.sh once; the inner calls then run directly.
if [ "${VOCATERRA_LIMITED:-}" != 1 ]; then
  exec "$L" bash "$ROOT/scripts/merge-gate.sh" "$@"
fi

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
{ "$L" dotnet restore Vocaterra.sln -m:4 && "$L" dotnet build Vocaterra.sln -c "$CONFIG" -warnaserror --no-restore -m:4; } > "$BUILDLOG" 2>&1
build_rc=$?
grep -E "Warning\(s\)|Error\(s\)" "$BUILDLOG" | sort | uniq -c
grep -E ": error " "$BUILDLOG" | head -10

# The test steps (B29a). run_step NAME FILTER [PROJECT...] runs `dotnet test` on the solution (or each named project, one after the other) with the
# filter, TRX files under .claude/gate-logs/trx-<stamp>/NAME, and the console in NAME's log. Every failed test is re-run alone and labelled
# (see the header). The step's counts, the FLAKY and FAILED lines and its result are printed; its return code is 0 only when every failure was FLAKY.
TRXROOT=".claude/gate-logs/trx-$STAMP"
FLAKYFILE=".claude/gate-logs/flaky-$STAMP.txt"
: > "$FLAKYFILE"
MAX_RERUNS=20
run_step() {
  local name="$1" filter="$2"; shift 2
  local log=".claude/gate-logs/$name-$STAMP.log" trx="$TRXROOT/$name" rc=0 target
  local targets=("$@"); [ "${#targets[@]}" -gt 0 ] || targets=(Vocaterra.sln)
  : > "$log"
  for target in "${targets[@]}"; do
    "$L" dotnet test "$target" -c "$CONFIG" --no-build -m:2 --filter "$filter" --logger trx --results-directory "$trx" >> "$log" 2>&1 || rc=1
  done
  local summary
  summary="$(grep -E "^(Passed|Failed)!" "$log" | sed -E 's/,//g')"
  echo "$name: $(echo "$summary" | awk 'NF {f+=$4; p+=$6; s+=$8} END {print "passed="p" failed="f" skipped="s}')"
  [ -n "$summary" ] || rc=1
  # Every test assembly that starts must report a result (H08a: a test host killed for memory printed nothing and dotnet test still exited 0).
  # An assembly whose tests are all filtered out reports "No test matches".
  local runs results
  runs="$(grep -c "Test run for " "$log")"
  results="$(grep -cE "^(Passed|Failed)!|No test matches" "$log")"
  if [ "$runs" -ne "$results" ]; then
    echo "$name: INCOMPLETE, $runs test runs started but $results results; no result for:"
    grep "Test run for " "$log" | sed -E 's/^Test run for //; s/ \(.*$//' | while read -r asm; do
      grep -qE "^(Passed|Failed)!.* - $(basename "$asm") |No test matches" "$log" || echo "  $asm"
    done
    return 1
  fi
  [ "$rc" -eq 0 ] && return 0
  local fails=() line proj fq alone bad=0
  mapfile -t fails < <(python3 -I scripts/gate-tests.py failed "$trx" 2>/dev/null)
  if [ "${#fails[@]}" -eq 0 ]; then echo "$name: FAILED without a failed test in the TRX files (a crash, a killed host or no result); see $log"; return 1; fi
  if [ "${#fails[@]}" -gt "$MAX_RERUNS" ]; then echo "$name: FAILED, ${#fails[@]} tests failed (more than $MAX_RERUNS, not re-run)"; return 1; fi
  for line in "${fails[@]}"; do
    proj="${line%%$'\t'*}"; fq="${line#*$'\t'}"
    alone="$("$L" dotnet test "tests/$proj/$proj.csproj" -c "$CONFIG" --no-build -m:1 --filter "FullyQualifiedName=$fq" 2>&1)"
    printf '=== re-run alone: %s (%s)\n%s\n' "$fq" "$proj" "$alone" >> "$log"
    # A filter that matches no test exits 0, so a pass needs at least one passed test.
    if grep -qE "^Passed!.*Passed: +[1-9]" <<<"$alone" && ! grep -qE "^Failed!" <<<"$alone"; then
      echo "FLAKY $fq"; echo "$name FLAKY $fq ($proj)" >> "$FLAKYFILE"
    else
      echo "FAILED $fq"; bad=1
    fi
  done
  return "$bad"
}

# The suite without the fuzz suite (CI only) and the timing tests (their own step below).
run_step test "Category!=Timing&Category!=Fuzz"
test_rc=$?
# The timing tests alone after the suite, so no other test host loads the machine while they measure (the two projects that have them).
run_step timing "Category=Timing" tests/Vocaterra.Game.Core.Tests/Vocaterra.Game.Core.Tests.csproj tests/Vocaterra.Nlp.Tests/Vocaterra.Nlp.Tests.csproj
timing_rc=$?
[ "$test_rc" -eq 0 ] && [ "$timing_rc" -eq 0 ] || test_rc=1
TESTLOG=".claude/gate-logs/test-$STAMP.log"
SUMMARY="$(cat "$TESTLOG" ".claude/gate-logs/timing-$STAMP.log" 2>/dev/null | grep -E "^(Passed|Failed)!" | sed -E 's/,//g')"
if [ -s "$FLAKYFILE" ]; then echo "flaky (passed alone; record them as findings):"; sed 's/^/  /' "$FLAKYFILE"; else echo "flaky: none"; fi
python3 -I scripts/gate-tests.py report "suite=$TRXROOT/test" "timing=$TRXROOT/timing" --out ".claude/gate-logs/timing-$STAMP.json" --logs .claude/gate-logs || true

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
  if "$L" dotnet run --project src/Vocaterra.Host -c "$CONFIG" --no-build -- --content content/game-v1 --validate-content >> "$CONTENTLOG" 2>&1; then
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
# The game's shipped projects (B26): one scan each, into the same log.
for game_target in src/Vocaterra.Game/Vocaterra.Game.csproj src/Vocaterra.Host/Vocaterra.Host.csproj tools/Vocaterra.SpeechRender/Vocaterra.SpeechRender.csproj; do
  { echo "=== $game_target"; "$L" bash scripts/license-scan.sh --offline --target "$game_target"; } >> "$LICENSELOG" 2>&1 || license_rc=1
  echo "licence scan ($game_target): $(grep -v "^limited.sh:" "$LICENSELOG" | tail -1)"
done

ok=1
[ "$build_rc" -eq 0 ] || ok=0
{ [ "$test_rc" -eq 0 ] && [ -n "$SUMMARY" ]; } || ok=0
[ "$format_rc" -eq 0 ] || ok=0
[ "$content_rc" -eq 0 ] || ok=0
[ "$license_rc" -eq 0 ] || ok=0
[ "${grammar_rc:-0}" -eq 0 ] || ok=0
echo "logs: $BUILDLOG $TESTLOG .claude/gate-logs/timing-$STAMP.log $FORMATLOG $CONTENTLOG $LICENSELOG ${GRAMMARLOG:-}"
[ "${#failed[@]}" -eq 0 ] && echo "merge-failures: none" || { echo "merge-failures: ${failed[*]}"; ok=0; }
if [ "$ok" -eq 1 ]; then echo "result: PASS"; else echo "result: FAIL"; exit 1; fi
