You are implementing the game milestone **V25, a load keeps its day**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game (`src/Vocaterra.Game.Core` mostly, `src/Vocaterra.Game` where the counters are shown or logged).** It fixes the two major bugs of the QA pass `docs/qa-reports/2026-11-01-progression.md` (QA-progression-1 and 2; read both), with the design the owner chose on 2026-11-01 (`docs/game/DECISIONS.md`, D-game-22, "Owner's answers, 2026-11-01 (on the QA passes …)", item 1).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/v25`, already on your branch `V25-load-keeps-day` (from `feature/village-followups`): cd there first, and never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (a core milestone); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs, so expect a command to wait at times.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `V25: `. Push after every commit: `git push -u origin V25-load-keeps-day` the first time, then `git push origin V25-load-keeps-day`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 10 GB free).** Build only the projects you need, never the whole solution: `tests/Vocaterra.Game.Core.Tests`, `src/Vocaterra.Game` and `tests/Vocaterra.Game.Tests` (and `src/Vocaterra.Host` with its tests only if you change it). The orchestrator's gate builds and tests the whole solution afterwards. Delete run folders you no longer need.

## The bug
`SaveGame.Restore` (`src/Vocaterra.Game.Core/Save/SaveGame.cs`, about line 322) sets `session.PlaySession = document.Session + 1` on a load: S10's rule, "a load is the next session", from when each sitting was a session. Since V12c a session is a day of the village, advanced by sleeping at the player's fire, and `PlaySession` drives the objective, the lessons that unlock, the check battery and the orders (`session` and `deliverTo`). So:
- a save in session 5 loads as session 6 while the objective still names session 5, and the open order `oven-wood` is refused («Necesitas más monedas.»): the learner keeps five wood and two coins for good (QA-progression-1);
- a save made just after a sleep into session 9 loads as session 10, and session 9 is skipped (QA-progression-2).
Every learner who quits and comes back meets this.

## The owner's design: two counters
- **The day** (the village session): lessons, objectives, orders, unlocks, the check battery, the HUD's "Session n", the objective line, `--skip-to-session`. It is restored **exactly as saved**, and only a sleep (or the test aids `--sleep` and `--skip-to-session`) moves it.
- **The sitting**: goes up on each launch and each load. It serves the phrase-credit spacing rule ("a different play session", `docs/game/DECISIONS.md` lines about 88 to 95, which keeps its meaning) and the session logs' folder and `session_start` record.
- **The spacing rule keeps its effect:** everything that counted as a different play session before (a launch, a load, a sleep) still does. Decide whether a sleep also moves the sitting or the rule looks at a different day or a different sitting; say which in the report and why.
- **An older save migrates** (X01; a missing field gets a default): its `Session` is the day, and the sitting continues from it, so the progress records' session numbers stay comparable. The save fixtures (`tests/Vocaterra.Game.Core.Tests/Fixtures/`) must still load.
- Go through every use of `PlaySession` (`grep -rn PlaySession src/ tests/`, about 20 files) and put each on the day or the sitting. Rename where it makes the code clearer (for example `Day` and `Sitting`), keeping the save's JSON readable by older code paths only through the migration.

## Tests (each failing before the fix)
- A save in a session and its load give the same day, objective and open orders (the QA's session 5 oven order: delivered after the load).
- A save right after a sleep loads on that new day (no skipped session).
- A load moves the sitting; the spacing rule credits a use in a new sitting as before.
- An older save (a fixture) loads on its saved day.
- The game run: the QA's two commands (in the report, QA-progression-1 and 2) before and after, through `scripts/limited.sh`, with `DISPLAY=:0`; read the stdout's `load:` line and look at the screenshot's HUD and objective.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/game/ARCHITECTURE.md` (by `grep -n` for save and session); `docs/code_guidelines.md`.
- `docs/qa-reports/2026-11-01-progression.md`: QA-progression-1 and 2.
- `docs/game/DECISIONS.md`: the phrase-credit rule (grep "different play session") and D-game-22's answers of 2026-11-01.
- `docs/game/VILLAGE_PLAN.md`: the V12c row (grep `^| V12c`) and the V25 row.
- The code: `Save/SaveGame.cs`, `Session/VoxelSession*.cs`, `Session/SessionSkip.cs`, `Session/SleepOffer.cs`, `Language/Lessons/LessonRunner.cs`, `Language/Checks/CheckBattery.cs`, `Language/Phrases/ReviewDeck.cs`, `src/Vocaterra.Game/Flow/SessionStart.cs`, `Program.cs` (the `load:` line).

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project Vocaterra.Game.Core.Tests` and `scripts/test.sh project Vocaterra.Game.Tests`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution); the gate runs the full suite.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`.
- Game runs need `DISPLAY=:0` and `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; give each run its own `--session-dir` under `/tmp/claude-1000/v25/`.

## Rules
- A game milestone edits only the game projects and their tests, `content/`, `assets/` and `docs/game/`; this one should need no content edit. It takes the core as it is.
- `Vocaterra.Game.Core` references no package and uses no `System.Random`, `Guid` or clock (`ArchitectureRulesTests`); engine code only in `src/Vocaterra.Game`.
- A save must still load an older save (X01); a new field gets a default.
- Update `docs/game/README.md` where it describes saves and sessions (grep "next session" and "load"), in a few lines.
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- Write code that reads like the surrounding code.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- Each project you changed builds with `-warnaserror`, 0 warnings; `Vocaterra.Game.Core.Tests` and `Vocaterra.Game.Tests` pass (the counts before and after).
- `dotnet format --verify-no-changes` on each project you changed.
- The two game runs above, before and after.
- Do not commit `bin/`, `obj/` or `artifacts/`. The orchestrator's gate then builds and tests the whole solution, with the scripted runs compared.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- The two counters: names, where each lives, which uses went to which; the sleep's choice for the spacing rule.
- The save field and the migration.
- The tests added; the counts before and after.
- The two game runs before and after (the `load:` lines, the HUD, the order).
- Anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra'`, kill only your own) and that the worktree is clean and pushed.
