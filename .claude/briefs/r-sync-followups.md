You are finishing a branch sync in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Where
The git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/sync`, on branch `R-sync-followups` at bc42f9a9: a merge of `feature/village-decisions` (V29: NPC weather and wellbeing replies; M78: core catalog, name and recast changes; a refreshed LanguageTool cache) into `feature/adaptive_design` (N01 to N04: the needs, save v6, eating, sleep, death). The merge took the adaptation side for four generated files that conflicted: `content/game-v1/manifest.json`, `content/locale/manifest.json`, `tests/Vocaterra.Game.Core.Tests/Fixtures/save-v1.json` and `save-v4.json`. They are now stale. cd there first; edit nothing elsewhere. Commit with the prefix `R: ` and push (`git push origin R-sync-followups`). Never push or merge `feature/*` or `main`.

## Environment and resources
Prefix commands with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. Every dotnet build/test/run goes through `scripts/limited.sh` (builds -m:4, tests -m:2); `scripts/content-loop.sh` and `scripts/test.sh` call it themselves (run content-loop directly, never under limited.sh). One heavy command at a time. Send long output to log files under /tmp/claude-1000/sync/ and print at most 30 lines. The sandbox refuses command lines containing the words `eval` or `hash`: use a wrapper script under /tmp/claude-1000/sync/ for those.

## The work
1. Run `scripts/content-loop.sh --grammar --speech --tier full` (add `--record` if it says the goldens moved). It re-hashes the bundle, refreshes the locale manifest, the save fixtures' bundle hash, the grammar cache (for any string the merge combined) and the speech WAVs, then runs the whole suite. If its `--grammar` step cannot reach LanguageTool ("no LanguageTool server"), run it without `--grammar` and report that.
2. If a test fails because the two sides meet (for example a V29 test that counts the world events or allowed intents, now that N01 to N04 added events; or a needs test that reads a prompt V29 changed), fix the test or the smaller side so both milestones' intent holds; never weaken a fuzz oracle or delete a test. Say each fix in the report. A core test failing for a reason that is not the merge: report it, do not patch.
3. `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4` (0 warnings), `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`, `scripts/limited.sh scripts/content.sh validate`.
4. Commit the regenerated files (and any fix) and push. Do not commit bin/, obj/ or artifacts/; `git status` clean at the end.

Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf

## Report (a few lines)
The commit SHA; the new bundle hash; the content loop's summary (each step's line); the test count (with models; about 9246 + 43 expected); each test fixed and why. Before your final message, check no process of yours still runs.
