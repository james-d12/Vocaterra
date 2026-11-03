You are implementing the game milestone **V30, the PR #28 review's game fixes**, of the Vocaterra repository (C#, .NET 10). The game is `Vocaterra.Game.Core` (engine-free systems), `Vocaterra.Game` (MonoGame), `Vocaterra.Host` (console host) and `tools/Vocaterra.SpeechRender`; the language core is `Vocaterra.Nlp` and `Vocaterra.Nlp.*` and is not yours to edit.

Do the work yourself; do not spawn subagents.

## Why
The owner had PR #28 (`feature/village-followups` into `main`) reviewed. The orchestrator checked the findings against the code and kept these for this milestone, in PR #28, before the merge.
- **"Confirmed"** means the orchestrator read the code and agrees.
- **"Verify"** means a plausible claim: write the test first. If the test shows no bug, keep the test, change no code, and report it.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/village`: cd there first, then `git checkout -b V30-review-fixes @@BASE@@` (the tip of `feature/village-followups`). Never edit `/home/user/Dev/Vocaterra` or any other worktree.
- **The owner wants work pushed often.** Commit after each item (or each pair of close items) that builds and passes its tests, with the prefix `V30: `. Push after every commit: `git push -u origin V30-review-fixes` the first time, then `git push origin V30-review-fixes`.
- Never push `main` or `feature/village-followups`, and do not merge. When you finish, leave the worktree on your branch, clean.
- Disk is short (about 11 GB free). Build only the projects you need while iterating.

## The items
Each item gets a regression test in the owning test project (`tests/Vocaterra.Game.Core.Tests`, `tests/Vocaterra.Game.Tests` or `tests/Vocaterra.Host.Tests`). The test must fail before the fix (say so in the report) and pass after it.

1. **A failed `--shot` does not fail the run** (confirmed; `src/Vocaterra.Game/Scripted/ScriptedRunEnd.cs`).
   - Line 163 sets `ExitCode = 1` when a mid-run shot cannot be saved, but line 171 overwrites it with the final screenshot's result. The console prints the failure and the process exits 0.
   - Fix: the worst code wins (the higher of the two, keeping `RunawayExitCode`'s meaning).
2. **An accent fix records the wrong verb form** (verify; `src/Vocaterra.Game.Core/Npc/GuideTurn.cs` about line 66, V26).
   - When a core note gives a word's accented form, only `Text` is replaced, and the person, number, tense and mood stay the word's own.
   - The claim: for the answer `Luz compró pescado`, a typed `compro` (a valid present form) is recorded as `compró` with the first person present's features, so the verb table credits the wrong cell.
   - Check what the core's reading of that word is when its note is set. If the features are wrong, take them from the note's reading (read `Note` in the core's API, `docs/nlp/API.md` by `grep -n`).
   - If the core gives no reading with the note's form, that is a core gap: report it and do not patch around it.
3. **A refused gift is still handed over** (confirmed; `src/Vocaterra.Game.Core/Npc/NpcTurnRules.cs` about lines 80 to 95).
   - When the agent has no transition for the miss event, `AnswerMisses` does `continue`, so a `GiveItem` the NPC refused stays in the turn's events and is made.
   - Fix: the refused gift is never made, with or without a transition.
   - Also make content validation (`GameDataValidator` or the host's `--validate-content`, whichever owns host events) fail on an `instead` or `asked` target that names no host event, so a typo in `host-events.json` is caught. Test that too.
4. **An NPC's copy of a shared line falls back to the guide's file** (confirmed; `src/Vocaterra.Game.Core/Sound/NpcVoice.cs` about line 208, and `SpeechLines.cs` about line 110, which documents the fallback).
   - When an NPC's copy (`SpeechKind.NpcCopy`, line id `<id>.<npc>`) has no file, the line plays nothing.
   - Fix: play the guide's file of the same line, and count it so the speech summary can say it fell back.
5. **The save's old flag** (confirmed; `src/Vocaterra.Game.Core/Save/SaveGame.cs` about line 164).
   - The save writes `KeyWordsTaught` as "any NPC", but older builds, and this build's reader of an old save (about line 385), read it as the shopkeeper's.
   - Fix: write `KeyWordsTaughtBy.Contains(NpcService.ShopkeeperAgent)`. Keep the save fixtures' tests passing, and update a fixture only if one pins the value.
6. **The recall message's session** (verify; `src/Vocaterra.Game.Core/Session/PlaytestFlow.cs` about line 71).
   - The message prints `_session.Day - 1`, but since V25 a load keeps `Day` as saved.
   - Trace how a recall run is launched (`--playtest`, `--session`, the load). Fix it if it names the wrong session.
7. **A plural-only phrase is not credited** (confirmed; `src/Vocaterra.Game.Core/Language/Phrases/PhraseSheet.cs`, `Names` about line 268, used about line 251).
   - It looks for the item's singular word (` manzana `) in the row's texts, so a row whose every variant says `manzanas` is never credited.
   - Fix: match the item's singular and plural forms, from the bundle's lexicon or the phrase's own analysis, never a hand-made `+s`.
   - Test with a row that has only plural variants.
8. **The session skip reads the wrong number** (confirmed; `src/Vocaterra.Game.Core/Session/SessionSkip.cs`, `NumberIn` about line 172).
   - It matches the item by prefix (`pan` matches `panadería`) and takes the first number in the phrase. In `Compré tres manzanas y dos panes` it reads 3 for bread.
   - Fix: the item as a whole word (its singular or plural), and the number that goes with it.
9. **The host and the game answer a miss alike** (review items 10 and 13; `src/Vocaterra.Host/ConversationSession.cs` about lines 150 to 170).
   - The host keeps its own copy of the stall's bottle rule and of the host-event extraction instead of calling `NpcTurnRules`.
   - After a miss, it updates each NPC's state only when the miss gave lines, and it lets a line the prompt does not take mark the learner down, which the game does not (V27).
   - Fix: the host calls the same `NpcTurnRules` code as the game, so `--transcript` gives what the game does. Test it with a miss and an off-prompt line through both paths.
   - `tests/Vocaterra.Host.Tests/LineHostPathTests.cs` (M76) must still pass. A case whose expectation moves is a finding: list it.
10. **The Lessons page keeps its place** (verify; `src/Vocaterra.Game.Core/Language/Book/BookModel.cs`, `Show` about line 102).
    - Turning to the Lessons page scrolls to `next:` (V21b), but `_selected` resets to 0, so the first arrow key jumps back to the top.
    - Fix: the selection starts at the row the page opened on.
11. **Dead and duplicated code** (review item 12):
    - `NpcService.FeedbackLinePrefix` (`NpcService.Guide.cs` about line 14) is unused;
    - `VerdictReport.cs` about line 13 has a third copy of the same prefix (keep one definition and use it);
    - `NpcTurnRules.Prepositions` is never read.
    - Remove or share them. Grep before removing.

**Not yours:**
- **The near-miss rule's empty answer set** (review item 8, `VerdictAssembler.IsNearMiss`) is core code. The core's M75 rewrites that rule after the merge.
- **The pool names written in code** (review item 14) waits for V29, which adds the weather and `¿cómo estás?` pools.

## Rules
- A game milestone edits only the game projects, their tests, `content/game-v1` (only if an item needs it; say why), and `docs/game/`.
- It takes the core as it is: never edit `src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp*`, `tools/Vocaterra.Nlp.Cli` or `docs/nlp/`. A core gap stops that part: report it.
- Do not edit `scripts/merge-gate.sh`, either `IMPLEMENTATION_PLAN.md`, either `DECISIONS.md`, `docs/HANDOVER_*.md`, `.claude/agents/`, or anything under `.github/workflows/`.
- Engine code only in `src/Vocaterra.Game` (D-game-1). `Vocaterra.Game.Core` references no package.
- Determinism: no `System.Random`, no clock in the evaluation path, ordinal comparisons.
- If you edit content, run `scripts/content-loop.sh --grammar --speech` (never under `scripts/limited.sh`), then `--tier full` once.

## Output and context (the cost of a milestone is its turns times its context)
- Read the sections named, not whole files. Prefer `grep -n`, and keep `sed -n` ranges under about 150 lines.
- Send every heavy command's output to a log file under `/tmp/claude-1000/v30/` and print at most 30 lines of it.
- Wait for a long command by running it in the foreground, not by polling a log. **Never leave a wait loop or a background process behind.**

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- **Never run `scripts/test.sh` or `scripts/content-loop.sh` under `scripts/limited.sh`:** they call it themselves.
- While iterating, use `scripts/test.sh project <test project>`. Run `scripts/test.sh full` once before reporting.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`. Use a wrapper script under `/tmp/claude-1000/v30/` for those.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll --transcript "<line>" --prompt <id>`.
- The game: from `src/Vocaterra.Game/bin/Debug/net10.0`, `DISPLAY=:0 dotnet Vocaterra.Game.dll --seed 42 --mute --session-dir <dir> --content <repo>/content/game-v1 --skip-to-session 10 --talk <npc> --say "<line>" --leave --screenshot <png> --frames 10`, through `scripts/limited.sh`.

## Read first
- `CLAUDE.md`.
- `docs/game/ARCHITECTURE.md` by `grep -n '^#'`.
- The files each item names, and their tests.

## Your checks before reporting (from the worktree root)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings (unused usings fail the build since M77).
- The full suite, all passing (it was @@COUNT@@ passed and 3 skipped).
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- One game run at session 10 with a talk to Luz and to the stall, and the scripted runs compared before and after in the same work folder (`scripts/scripted-runs.sh`, `scripts/compare-runs.py`). Explain every difference.
- Do not commit `bin/`, `obj/` or `artifacts/`.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDfGVmxEaTFXNb87eD1
```

## Report (one page, under 450 words)
- The branch and the final commit SHA, pushed.
- **Each item:**
  - fixed (where, the test, what the test gave before), or
  - verified as no bug (the test that shows it), or
  - stopped (why: a core gap, or content that needs the owner).
- The host test cases that moved (item 9), if any; the scripted runs' differences.
- The test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|limited.sh|content-loop'`, kill only your own) and that the worktree is clean and pushed.
