You are bringing a milestone branch up to date with its base in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Where
The git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/l01`, on branch `L01-lessons-as-classes` at 1534cb17 (L01: lessons become optional classes; built and tested, not merged). Its base `feature/adaptive_design` has since gained N05 (the needs HUD: `hudLines` in `game/words.json` and its schema, `GameWords.HudLines`, the stage lines, the death screen, N06's gift lines with six WAVs). cd there first; edit nothing elsewhere. Commit with the prefix `L01: ` and push (`git push origin L01-lessons-as-classes`). Never push or merge `feature/*` or `main`.

## Environment and resources
Prefix commands with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Every dotnet build/test/run goes through `scripts/limited.sh` (builds -m:4, tests -m:2); `scripts/content-loop.sh` and `scripts/test.sh` call it themselves (run content-loop directly, never under limited.sh). Another agent (N07) builds beside you: limited.sh may make you wait for a slot; a command killed with exit 137 or 143 may have met the shared memory cap: rerun it once. Never start two heavy commands yourself; no background commands, no Monitor. Send long output to log files under /tmp/claude-1000/l01sync/ and print at most 30 lines (`grep -E 'error|warn|Failed' log | head -30`). The sandbox refuses command lines containing the words `eval` or `hash`: use a wrapper script under /tmp/claude-1000/l01sync/ for those.

## The work
1. `git fetch -q origin && git merge --no-ff origin/feature/adaptive_design`. Expect conflicts in:
   - **Source, resolve by hand keeping both sides:** `src/Vocaterra.Game.Core/Language/Words/GameWords.cs` (L01 added `classAnswers`, N05 added `HudLines`: keep both), `content/schema/words.schema.json` (both added a property: keep both), `tests/Vocaterra.Game.Core.Tests/Language/Speech/SpeechLinesTests.cs` (each side raised a pool count: the new count is the base's plus L01's new pools; take it from the test's failure if unsure). Also check the auto-merged `content/game-v1/game/words.json`, `content/locale/en/lines.json` and `ui.json`, `LocaleText.cs` and `SaveDocument.cs` are valid and keep both sides.
   - **Generated, take either side (the content loop rewrites them):** `content/game-v1/manifest.json`, `content/locale/manifest.json`, `docs/game/speech-checklist.md`, `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json`.
   Commit the merge once `git diff --check` is clean and no conflict marker is left (`grep -rn '^<<<<<<<\|^>>>>>>>' src tests content docs | head`).
2. Run `scripts/content-loop.sh --grammar --speech --tier full` (add `--record` if it says the goldens moved). It re-hashes the bundle, refreshes the locale manifest, the save fixtures' bundle hash, the grammar cache, the speech WAVs and the checklist, then runs the whole suite.
3. If a test fails where the two sides meet (for example an N05 test that walks day 1 and now meets L01's class offers or the new `first-class` lesson; a test that counts pools, lessons or tips; N05's `DayOneSafetyTests` with the introduction now shorter), fix the test or the smaller side so both milestones' intent holds; never weaken a fuzz oracle or delete a test. Say each fix in the report. A core test failing for a reason that is not the merge: report it, do not patch.
4. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings), `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`, `scripts/limited.sh scripts/content.sh validate`.
5. Commit the regenerated files (and any fix) and push. Do not commit bin/, obj/ or artifacts/; `git status` clean at the end.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (a few lines)
The final commit SHA; how each conflict was resolved; the content loop's summary (each step's line); the test count with models (9433 on the base, 9403 on L01 before the merge; expect about 9450); each test fixed and why. Before your final message, check no process of yours still runs.
