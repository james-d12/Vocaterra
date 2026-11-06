You are implementing the game milestone **W06, the procedure runner**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game, in `Vocaterra.Game.Core` only**: a deterministic state machine with its progress, its save and its events, and no UI (W07 builds the station view on it).

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w06`, already on the branch `W06-procedure-runner` (from `feature/bread-pilot` at its tip, with W05 merged). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree (`.claude/worktrees/adaptive` is the owner's playtest checkout: never touch it); do not switch branches.
- **Commit after each item that builds and passes its tests**, with the prefix `W06: `. Uncommitted work is lost if the machine crashes: never end your turn, or wait on anything, with uncommitted work.
- **Do not push**: your branch stays local and the gate merges it from there (owner, 2026-11-05). Never push or merge `feature/bread-pilot`, `feature/adaptive_design` or `main`. When you finish, leave the worktree on your branch, clean.
- **Run everything in the foreground and finish in one turn.** Do not start background commands or monitors and then end your turn to wait for them: W05's agent did that and stopped twice with its work uncommitted. A full suite takes about five minutes in the foreground; wait for it.

## The specification
- `docs/game/activities-plan.md`: section 4.2 (`sed -n 107,119p`), 4.6 (`sed -n 139,145p`), 4.7's table (`sed -n 146,158p`), and section 6's paragraph **W06** (`sed -n 261,271p`). That paragraph's API, tests and "done when" are the milestone.
- The owner's design document, extracted one paragraph per line at `/tmp/claude-1000/orch/adaptation-design.txt`: **10.2 "Guidance ladder" (`sed -n 703,717p`)** and **10.9 to 10.15 (`sed -n 843,1001p`)**: the steps, support, help and evidence, how the help fades, ingredients, saving and transactions, mastery. 10.14 (W08's teacher) only for the hooks the runner must leave (the teacher present or not; the host turns are W08's). 10.7 (`sed -n 793,833p`) is the data shape W05 built. Read only those ranges; do not edit the `.docx`.
- W05's code is the data you run: `src/Vocaterra.Game.Core/Activities/` (`ProcedureBook`, `StationBook`, `ActionTable`) and its findings entry (`grep -n 'W05, procedures' docs/game/implementation-plan.md`): a step may name a `use`; `anyOrder` is a group name; `what` an item or fixture noun, `where`/`from`/`with` fixtures.
- Existing code to follow: `Language/Phrases/PhraseProgress.cs` (records, support levels, the gap rules shared with uses), `Mathematics/WorldRng.cs` (`WorldRng.For(seed, "proc", bake)` for the drawn order), `Items/Crafting/CraftingRules.cs` (the instant craft; it logs `item_crafted`, W01), `Save/SaveDocument.cs` (`SystemsSave`, version 6, N01), `Session/GuideSession.Review.cs` (the partial-class pattern for `GuideSession.Activities.cs`), `Event/WorldEvent.cs` (`WorldEvents.All`).
- The owner adopted the design's section 18.2 recommendations provisionally (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"): decision 3 (the fold rule) is one of them. **Decision 19 (accessible instruction evidence) is open; use this provisional value**: an act records how its instruction was presented (`heard`: the audio played; `read`: the text was shown; both may be true) and the runner applies rung moves, folding and mastery the same whichever way it was presented, while the evidence keeps `heard` and `read` separate so W11 can report listening and reading apart. Keep it one small policy type the owner can change. List it under "values chosen".
- Where the design and the code disagree on a file or a name, the code wins; say so in the report. List every value you choose where the design leaves one open.

## What to build (the paragraph's items, in this order, each committed)
1. `Activities/ProcedureProgress` and `PhraseProgress.RecordAct` (the credit rules of 4.2 and 10.10, the gap shared with uses).
2. `Activities/ProcedureRunner`: `Start`, `Act` (right, wrong verb, wrong thing, out of order), `Repeat`, `Slower`, `Help`, `Leave`, `View`, events, the drawn order, folding, the reward and its bonus, the atomic exchange, `ReadyToCollect` on a full bag, mastery.
3. The four world events of 4.7 in `WorldEvents.All` and any schema that lists event names (as W01 did).
4. `systems.procedures` in the version-6 save: `Capture`/`Restore`, preserving needs; an existing version-6 save with no procedure entry loads with none.
5. `Session/GuideSession.Activities.cs`: the wiring, and the mastered recipe's one-click bake through `CraftingRules` with the procedure's `uses` and `makes`.
6. The tests the paragraph lists, on a three-step test procedure built in the test (no bundle edit), plus a save fixture or test for version 5 to 6 and for every older version still loading.

Out of scope: the station view and scripted steps (W07), the teacher's host kinds and the missing ingredient (W08), flour and the bread content (W10), telemetry (W11). Add no Spanish and no content to `content/game-v1` unless a test needs nothing else (it should not).

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/w06/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 11 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload; the owner is playing the game on this machine)
- Run every `dotnet restore|build|test|format|run` and every `scripts/content.sh` call through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` runs directly.
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh project tests/Vocaterra.Game.Core.Tests` or `scripts/test.sh fast` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 or 143 means a cap killed the command: lower the parallelism or rerun; never raise a cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/w06/`.
- Model-gated tests read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/w06/`.

## Rules
- **What this milestone edits:** `src/Vocaterra.Game.Core`, its tests `tests/Vocaterra.Game.Core.Tests` (and `tests/Vocaterra.Host.Tests` or the game's tests only if an event list or save test there must take the new names), `content/schema` (event names) and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched.
- A regression check is a C# test, never a script with text baselines.
- `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`); logic in Core (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Determinism: no `System.Random` and no wall clock in the runner; the host passes the seed, the bake count and the times. Every save version from 1 loads; a save round-trips.
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full`, all passing: 9614 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`.
- No scripted run should move (the runner has no entry point a run reaches until W07): skip the scripted runs unless you touched `VoxelSession` or the save path a run uses, and say which.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local, not pushed).
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open (decision 19's included), and every place the design and the code disagreed.
- The API W07 will call (one line per member), so its brief can name it.
- The save versions tested; the test count before and after; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
