You are bringing a milestone branch up to date with its base in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Where
The git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n09`, on branch `N09-needs-rate` at f06c0c84 (N09: the drive rates as a setting: `SaveSettings.NeedsRate`, `--needs-rate`, a settings panel on F8; built and tested, not merged). Its base `feature/adaptive_design` has since gained U01 (clean defaults: `--dev` in `LaunchOptions.cs`, F3 for the overlay and the developer note, the objective tracker, the tip box's place, the dialogue footer's short form, `--dev` in `scripts/scripted-runs.sh`). cd there first; edit nothing elsewhere. Commit with the prefix `N09: `. Do not push anything (the branch stays local; the owner, 2026-11-05).

## Environment and resources
Prefix commands with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Every dotnet build/test/run goes through `scripts/limited.sh` (builds -m:4, tests -m:2); `scripts/content-loop.sh` and `scripts/test.sh` call it themselves (run content-loop directly, never under limited.sh). Another agent (U02) builds beside you: limited.sh may make you wait for a slot; a command killed with exit 137 or 143 may have met the shared memory cap: rerun it once. Never start two heavy commands yourself; no background commands, no Monitor. Send long output to log files under /tmp/claude-1000/n09sync/ and print at most 30 lines (`grep -E 'error|warn|Failed' log | head -30`). The sandbox refuses command lines containing the words `eval` or `hash`: use a wrapper script under /tmp/claude-1000/n09sync/ for those.

## The work
1. `git merge --no-ff feature/adaptive_design` (the local branch; do not fetch). Expect conflicts in:
   - **`src/Vocaterra.Game/Startup/LaunchOptions.cs`:** U01 added `--dev` (`Dev`), N09 added `--needs-rate` (`NeedsRate`) and `--settings`: keep both sides, each option with its parsing, help text and tests.
   - **`content/locale/manifest.json`:** generated; take either side (the content loop rewrites it).
   Check that the auto-merged files both sides touched (`VoxelGame.cs`, `Program.cs`, `KeyBindings.cs`, `InputKeys.cs`, `ControlsLayer.cs`, `LayerStack.cs`, `content/locale/en/ui.json` and `keys.json`, `docs/game/running.md`) keep both sides and that the JSON parses. Commit the merge once no conflict marker is left (`grep -rn '^<<<<<<<\|^>>>>>>>' src tests content docs | head`).
2. Run `scripts/content-loop.sh --grammar --speech --tier full` (add `--record` if it says the goldens moved). It re-hashes the bundle, refreshes the locale manifest, the save fixtures' bundle hash, the grammar cache, the speech WAVs and the checklist, then runs the whole suite.
3. If a test fails where the two sides meet (for example a launch-option test that lists every flag; a layout test of the controls overlay, now listing both F3's new meaning and F8; a key-binding test that counts keys), fix the test or the smaller side so both milestones' intent holds; never weaken a fuzz oracle or delete a test. Say each fix in the report. A core test failing for a reason that is not the merge: report it, do not patch.
4. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings), `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`, `scripts/limited.sh scripts/content.sh validate`.
5. Commit the regenerated files (and any fix); do not push. Do not commit bin/, obj/ or artifacts/; `git status` clean at the end.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (a few lines)
The final commit SHA; how each conflict was resolved; the content loop's summary (each step's line); the test count with models (9476 on the base, 9486 on N09 before the merge; expect about 9501); each test fixed and why. Before your final message, check no process of yours still runs.
