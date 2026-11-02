You are implementing the game milestone **V26, the four game bugs V24 found**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's code** (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host` and their tests). The owner chose to fix these four on `feature/village-followups` before its pull request into `main` (D-game-22, 2026-11-02).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, and never edit `/home/user/Dev/Vocaterra` or any other worktree. Create your branch with `git checkout -b V26-v24-game-bugs @@BASE@@` (the tip of `feature/village-followups`). Another agent may read files in this worktree at the same time (it runs no command that writes); do not switch branches once you have started.
- **The owner wants work pushed often.** Commit after each item that builds and passes its tests, with the prefix `V26: `. Push after every commit: `git push -u origin V26-v24-game-bugs` the first time, then `git push origin V26-v24-game-bugs`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The items (each with a test that fails before the fix; one commit each)
1. **Two replies to a line not understood.** At an open prompt a line the NPC does not understand gets the game's «No te entendí. ¿Me lo dices otra vez?» and then the core's repair «¿Cómo?»: `--skip-to-session 10 --talk villager --say "me gusta el río"` prints `villager: No te entendí. ¿Me lo dices otra vez?` and `villager: ¿Cómo?`. `DialogueModel` adds `turn.Repair` after the reply (`src/Vocaterra.Game.Core/Dialogue/DialogueModel.cs`, about line 542). V21's rule is one reply per turn (`NpcTurnRules`, `Compose`). Make a line not understood get one reply. Read which repair kinds the core gives (`grep -rn "RepairKind" src/Vocaterra.Nlp/` for the enum) and keep any repair that adds something the reply does not say (for example a question about a missing slot). Drop only the one that repeats the ask-again, and keep it out of what is spoken too. The host's `--transcript` mode shares `NpcTurnRules`. Keep its report line `repair: …` only if it is a diagnostic, not a reply, and say which you chose. Run `scripts/try-lines.sh --check`, and re-record a baseline only where this change moves it, saying which.
2. **The book credits a refused request.** `Quiero dos manzanas.` to Luz in free talk (session 10, `baker-open`) is refused as it should be («Solo vendo pan.»), but the book logs it as a use of the phrase `quiero-dos-panes`, with the detail `manzana`. V20 (b) credits a free-talk line to a phrase of a begun lesson by its intent (read V20's entry: `grep -n '^- \*\*V20' docs/game/IMPLEMENTATION_PLAN.md`, then about 25 lines, and the code it names). A line whose item differs from the phrase row's noun is not a use of that row. A different number (`quiero tres panes`) still is, if the row's number is a slot (check the row in `content/game-v1` and `content/PHRASES.md` by `grep`). Fix the credit rule, not the content. Test both cases, and `TaughtInFreeTalkTests` must still pass.
3. **The book keeps a typed form without its accent.** After `ayer compre pan` (understood, with the accent note «compré», con tilde: es pasado, M70), the session summary counts `comprar: … forms compre (1 sg pret ind)`, and the book's Verbs page counts the form `compre`. The learner record should count the form the core read, `compré`. The core's `PipelineResult.Notes` (`AccentNote`) carries the accented form; `NpcService.AccentNotesOf` already reads it. Find where the game records a verb's form (`grep -rn "forms\b\|RecordForm\|VerbForm" src/Vocaterra.Game.Core --include=*.cs`) and use the accented form when a note names it. Test: a typed `ayer compre pan` counts `compré`.
4. **A scripted run stops at 18 000 ticks without a message.** `ScriptedRunEnd.RunawayTicks` (`src/Vocaterra.Game/Scripted/ScriptedRunEnd.cs`, line 35 and about line 180) ends a run after five minutes of play and silently drops the steps after it. V24 needed four runs for the route of sessions 4 to 10 because of it. Print one line to stdout and the session log when this happens: the tick, and how many steps were left with the first of them. Decide whether such a run should exit non-zero, and say why (check `scripts/scripted-runs.sh` and `scripts/compare-runs.py` for what an exit code means to them). Do not raise the limit. Test in `tests/Vocaterra.Game.Tests` without a window if the class allows it; otherwise test the message's text, and say so.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/code_guidelines.md`; `docs/game/ARCHITECTURE.md` (by `grep -n`).
- V21's and V21b's entries in `docs/game/IMPLEMENTATION_PLAN.md` section 5 (`grep -n '^- \*\*V21'`, about 25 lines each); V24's (`grep -n '^- \*\*V24'`).
- `docs/game/README.md`, "Open points after the follow-ups" (`grep -n 'Open points after the follow-ups'`): items 1 to 4 are there. **Update that list when you finish:** drop the four points this milestone fixes. Do not edit the plans or the decisions.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/v26/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` to find the place first.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 12 GB free. While iterating, build only the projects you need (`tests/Vocaterra.Game.Core.Tests`, `tests/Vocaterra.Host.Tests`, `src/Vocaterra.Game`, `tests/Vocaterra.Game.Tests`); build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`scripts/limited.sh dotnet build <project> -m:4`; tests with `-m:2`). `scripts/test.sh`, `scripts/try-lines.sh` and `scripts/content-loop.sh` run directly and are **never** wrapped in `scripts/limited.sh`.
- Never start two heavy commands at once: no `&`, no background builds.
- While iterating use `scripts/test.sh fast` or `scripts/test.sh project <test project>`; run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v26/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`; give each its own `--session-dir` under `/tmp/claude-1000/v26/`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.

## Rules
- **What this milestone edits:** the game's code and tests, `docs/game/README.md`'s open points, and the try-lines baselines it moves. If an item needs UI text, put it in `content/locale` (outside `content/game-v1`'s hash; `content/README.md` says how), never in `content/game-v1`. If you do edit `content/game-v1`, run `scripts/content-loop.sh --grammar --speech` directly, `--record` if the goldens move, and `--tier full` once.
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`).
- Do not edit either `IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_VILLAGE_PHASE_B.md`, `scripts/merge-gate.sh`, or anything under `.claude/` or `.github/workflows/`.
- No `System.Random` and no clock in the evaluation path.

## Behaviour checks (before the report; show the lines)
- `--skip-to-session 10 --talk villager --say "me gusta el río"`: one reply.
- `--skip-to-session 10 --talk baker --say "quiero dos manzanas"`: «Solo vendo pan.», no use of `quiero-dos-panes` in the session summary; `--say "quiero tres panes"`: still a use.
- `--skip-to-session 10 --talk baker --say "ayer compre pan"`: the note shows, and the summary counts `compré`.
- A run that reaches 18 000 ticks: the line is printed.
- The scripted runs a change could move (any with a line not understood at an open prompt, a refused request, or `r24-followups`): run each before and after your change (`scripts/scripted-runs.sh` names them; run them one at a time with its own command line through `scripts/limited.sh`) and list every difference.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full`, all passing. It was @@COUNT@@ passed and 3 skipped; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; `scripts/try-lines.sh --check` clean (after any re-record you justify).
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- Each item: the cause (file and line), the fix, the test; for item 1 which repair kinds stay; for item 4 the exit code you chose.
- The behaviour checks' lines; the scripted runs' differences; the try-lines baselines re-recorded, if any.
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).
