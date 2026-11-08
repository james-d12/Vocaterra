You are bringing a milestone branch up to date with its base in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Where
The git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/u02`, on branch `U02-conversation-surface` at 5ccba6d2 (U02: the conversation surface: the current exchange with older lines behind PageUp/O, one coaching line, the lesson heading in the panel header, a scripted `--expand` step; built and tested, not merged). Its base `feature/adaptive_design` has since gained N09 (the drive rate as a setting: `--needs-rate` and `--settings` in `LaunchOptions.cs`, a settings panel on F8, new strings in `content/locale/en/ui.json`). cd there first; edit nothing elsewhere. Commit with the prefix `U02: `. Do not push anything (the branch stays local; the owner, 2026-11-05).

## Environment and resources
Prefix commands with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Every dotnet build/test/run goes through `scripts/limited.sh` (builds -m:4, tests -m:2); `scripts/content-loop.sh` and `scripts/test.sh` call it themselves (run content-loop directly, never under limited.sh). Another agent (N08) builds beside you: limited.sh may make you wait for a slot; a command killed with exit 137 or 143 may have met the shared memory cap: rerun it once. Never start two heavy commands yourself; no background commands, no Monitor. Send long output to log files under /tmp/claude-1000/u02sync/ and print at most 30 lines (`grep -E 'error|warn|Failed' log | head -30`). The sandbox refuses command lines containing the words `eval` or `hash`: use a wrapper script under /tmp/claude-1000/u02sync/ for those.

## The work
1. `git merge --no-ff feature/adaptive_design` (the local branch; do not fetch). Expect conflicts in:
   - **`src/Vocaterra.Game/Startup/LaunchOptions.cs`:** the one `if (arg is "--journal" or ...` line of flag names: N09 added `"--settings"` after `"--crafting"`, U02 added `"--expand"` after `"--leave"`; keep both names (and both sides' `case` arms, help text and properties, which merge cleanly).
   - **`content/locale/manifest.json`:** generated; take either side (the content loop rewrites it).
   Check that the auto-merged files both sides touched (`VoxelGame.cs`, `FrameView.cs`, `LocaleText.cs`, `content/locale/en/ui.json` (it must parse and hold both sides' new keys), `docs/game/running.md`, `tests/Vocaterra.Game.Tests/UI/Common/UiScaleLayoutTests.cs`) keep both sides. Commit the merge once no conflict marker is left (`grep -rn '^<<<<<<<\|^>>>>>>>' src tests content docs | head`).
2. Run `scripts/content-loop.sh --speech --tier full` (no game-v1 edit on either side, so no grammar step; add `--record` if it says the goldens moved). It refreshes the locale manifest, the speech files and the checklist, then runs the whole suite.
3. If a test fails where the two sides meet (for example a launch-option test that lists every flag; a layout test of the dialogue panel or the settings panel at a text scale; a key-binding test that counts keys (U02 added PageUp and O in the dialogue, N09 F8)), fix the test or the smaller side so both milestones' intent holds; never weaken a fuzz oracle or delete a test. Say each fix in the report. A core test failing for a reason that is not the merge: report it, do not patch.
4. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings), `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`, `scripts/limited.sh scripts/content.sh validate`.
5. Commit the regenerated files (and any fix); do not push. Do not commit bin/, obj/ or artifacts/; `git status` clean at the end.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (a few lines)
The final commit SHA; how each conflict was resolved; the content loop's summary (each step's line); the test count with models (9501 on the base, 9521 on U02 before the merge; expect about 9546); each test fixed and why. Before your final message, check no process of yours still runs.
