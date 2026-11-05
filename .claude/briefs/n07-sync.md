You are bringing a milestone branch up to date with its base in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Where
The git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n07`, on branch `N07-cooking-fire` at 50c17886 (N07: cooking at the fire: the noun `fuego`, the roast apple, item 33, the fire's recipes; built and tested, not merged). Its base `feature/adaptive_design` has since gained L01 (lessons as optional classes: a new lesson `first-class`, class offer pools with WAVs, `classAnswers` in `game/words.json`, `metNpcs` in the save, the Lessons page). cd there first; edit nothing elsewhere. Commit with the prefix `N07: ` and push (`git push origin N07-cooking-fire`). Never push or merge `feature/*` or `main`.

## Environment and resources
Prefix commands with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Every dotnet build/test/run goes through `scripts/limited.sh` (builds -m:4, tests -m:2); `scripts/content-loop.sh` and `scripts/test.sh` call it themselves (run content-loop directly, never under limited.sh). Another agent (U01) builds beside you: limited.sh may make you wait for a slot; a command killed with exit 137 or 143 may have met the shared memory cap: rerun it once. Never start two heavy commands yourself; no background commands, no Monitor. Send long output to log files under /tmp/claude-1000/n07sync/ and print at most 30 lines (`grep -E 'error|warn|Failed' log | head -30`). The sandbox refuses command lines containing the words `eval` or `hash`: use a wrapper script under /tmp/claude-1000/n07sync/ for those.

## The work
1. `git fetch -q origin && git merge --no-ff origin/feature/adaptive_design`. Expect conflicts only in generated files: `content/game-v1/manifest.json`, `content/locale/manifest.json`, `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json`: take either side (the content loop rewrites them). Check that the auto-merged files that both sides touched (`grep` the merge's file list for `content/game-v1/game/`, `content/locale/en/`, `LocaleText.cs`, `VoxelGame.cs`, `docs/game/running.md`, `docs/game/speech-checklist.md`) keep both sides and that the JSON parses. Commit the merge once no conflict marker is left (`grep -rn '^<<<<<<<\|^>>>>>>>' src tests content docs | head`).
2. Run `scripts/content-loop.sh --grammar --speech --tier full` (add `--record` if it says the goldens moved). It re-hashes the bundle, refreshes the locale manifest, the save fixtures' bundle hash, the grammar cache, the speech WAVs and the checklist, then runs the whole suite.
3. If a test fails where the two sides meet (for example a test that counts nouns, items, recipes, pools or speech lines, now that both sides added some; an L01 class test that crafts or eats; N07's `FireCookingTests` on a camp whose lessons changed), fix the test or the smaller side so both milestones' intent holds; never weaken a fuzz oracle or delete a test. Say each fix in the report. A core test failing for a reason that is not the merge: report it, do not patch.
4. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings), `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`, `scripts/limited.sh scripts/content.sh validate`.
5. Commit the regenerated files (and any fix) and push. Do not commit bin/, obj/ or artifacts/; `git status` clean at the end.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (a few lines)
The final commit SHA; how each conflict was resolved; the content loop's summary (each step's line); the test count with models (9451 on the base, 9443 on N07 before the merge; expect about 9461); each test fixed and why. Before your final message, check no process of yours still runs.
