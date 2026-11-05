You are implementing the game milestone **N01, the needs model**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It is the first milestone of the adaptation (the needs series, D-game-24: "player needs first"); L01 and N02 to N07 build on it, so its types are their foundation: keep them small, plain and well tested.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n01`, already on the branch `N01-needs-model` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N01: `, and push after every commit (`git push -u origin N01-needs-model` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n01/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n01/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n01/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
- After any edit of `content/game-v1`: `scripts/content-loop.sh --speech` (it refreshes the hash, the save fixtures, the LanguageTool cache and the WAVs; `--record` when the goldens move; `--tier full` once before the report). Content outside game-v1's hash: `content/locale/` (the English), `content/schema/`. How to edit content: `content/README.md`.

## Rules
- **What this milestone edits:** the game's code and tests (`src/Vocaterra.Game.Core`, `src/Vocaterra.Game`, `src/Vocaterra.Host`, `tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`), `content/game-v1`, `content/locale`, `content/schema`, `assets/` and `docs/game/` (not the plans or the decisions).
- It takes the core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`, `docs/nlp/`) as it is. A core gap is reported, never patched; a core test that fails after a content edit is wanted feedback: report it.
- A regression check is a C# test, never a script with text baselines.
- Engine code only in `src/Vocaterra.Game`; `Vocaterra.Game.Core` references no package (`ArchitectureRulesTests`). Logic in Core, drawing and devices in Game (`docs/process/code-guidelines.md`, `docs/game/architecture.md`).
- Do not edit either `implementation-plan.md`, `docs/game/activities-plan.md`, `docs/game/village-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, the design `.docx`, or anything under `.claude/` or `.github/workflows/`.
- Determinism: no `System.Random` and no wall clock in the game's logic; the host owns the stored time, the seed and the repeat counts. Assets CC0 only (D-game-5).
- Every save version from 1 loads; a save round-trips.

## The grammar cache (a known limit of this sandbox)
Here the dotnet process cannot reach the local LanguageTool server (curl can; dotnet reports "no LanguageTool server"), so `--grammar` cannot run. Do not try to bypass the sandbox and do not edit `content/grammar/` by hand. Run the content loop without `--grammar`; its refresh step lists any string of `content/game-v1` the cache lacks, and `GrammarTests.TheCommittedCacheReplaysTheBundleOfflineWithItsWaivers` fails on such a miss. So add no new Spanish text to `content/game-v1` in this milestone unless the design cannot do without it: player-facing English goes in `content/locale/en/`, and a Spanish line the design needs is reported (its text and file) and left for a later milestone, or, if you must add it, stop before the report and say so; the milestone then waits for the owner's grammar refresh.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9019 passed and 3 skipped before you; report the new count.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- `scripts/limited.sh scripts/content.sh validate`; the lint at A2 clean (`scripts/content.sh lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`).
- **The scripted runs** (`docs/game/scripted-runs.md`): `scripts/scripted-runs.sh` before your first edit and after, compared with `python3 -I scripts/compare-runs.py`, one at a time through `scripts/limited.sh`. Explain every difference; re-baseline only the runs this milestone is meant to move.
- **CI's case:** the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder); report any wall-clock test within four times of its budget.
- Do not commit `bin/`, `obj/` or `artifacts/`. When you finish, `git status` is clean and the branch is pushed.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA, pushed.
- Each deliverable: what you built (file and type), its tests.
- **Values chosen** where the design left one open, and every place the design and the code disagreed.
- The behaviour checks' lines; the scripted runs' differences; the save versions tested.
- The test count before and after; CI's case; anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|content-loop|scripted-runs'`, kill only your own).

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/process/code-guidelines.md`; `docs/game/architecture.md` (by `grep -n`); `docs/game/saves.md`; `content/README.md`.
- `docs/game/decisions.md`: D-game-24 (`grep -n '^## D-game-24'`, about 45 lines) and D-game-25; D-game-23's "Owner's answers, 2026-11-04".
- `docs/game/implementation-plan.md` section 4.9 (your row).
- The design (`/tmp/claude-1000/orch/adaptation-design.txt`): section 9's opening, 9.1 to 9.4, 9.10 and 9.13 in full; 9.5 to 9.7 for the operations the model must offer (their wiring is N02 to N04's).
- Code: `src/Vocaterra.Game.Core/Save/SaveGame.cs` (`CurrentVersion` = 5, the migrations, the world digest at about lines 300 and 353), `Save/SaveDocument.cs`, `Event/WorldEvent.cs` (`WorldEvents.All`), `Content/GameData.cs` and `GameDataValidator.cs`, `Session/VoxelSession.cs` and `Session/VoxelSession.Sleep.cs` (the partial to imitate), `src/Vocaterra.Game/VoxelGame.cs` about lines 540 to 560 (which ticks stop for the check battery and the F8 report), `src/Vocaterra.Game/Startup/LaunchOptions.cs` (the flags), `Physics/VoxelPhysics.cs` about line 32 (the walk speed, for later).

## The deliverables
1. **Data:** `content/game-v1/game/needs.json` with `content/schema/needs.schema.json`, loaded by `GameData` and checked by `GameDataValidator` (every food is an item, every rate positive, the stages in order, factors in range). It holds, with the design's starting values (9.2 to 9.4): per drive (hunger, thirst, sleep) its rate per second of active play, its effort factors (sprinting, gathering) and its four stages (the threshold, the line's key, the effects: stamina penalty, refill factor, walking factor, sprint allowed, health change and its interval, health recovery blocked, the view effect's name); health's recovery, its warning stages (below 50, below 25) and their effects; stamina's sprint cost, gathering cost, refill, delay, minimum maximum (20) and minimum refill (a quarter); rest at a lit fire (radius 3, factors 2); a night's and a collapse's effects and the collapse delay (60 s); the foods (item, hunger, thirst, healing, what it leaves) for the items that exist today (the berry `fruta`, the apple, bread, the water bottle); the death rules (where the player wakes, the waking values, what stays in the bag, the chest's lifetime). Leave the regrowth and gift rules (N06) and the roast apple (N07) out; the schema must let them be added without a break.
2. **The model:** a new folder `src/Vocaterra.Game.Core/Needs/` with `PlayerNeeds`: plain state for the five meters (hunger, thirst, sleep 0 to 100; stamina 0 to its current maximum; health 0 to 100) and the exhaustion timer, with `Step` (a span of active play and what the player is doing: idle, walking, sprinting, gathering, resting at a lit fire), `Eat`, `Drink`, `Sleep` (a night), `Collapse`, `Die` and `Wake`, and `StageOf` and `Effects` (the stamina maximum and refill, the walking factor, sprint allowed, the health change) **computed from the meters and the data, never stored** (9.10). Stages are evaluated from the meter after every change exactly as 9.3 says (healthy at 75 to 100; stage 1 at 50 ≤ v < 75; stage 2 at 25 ≤ v < 50; stage 3 at 0 < v < 25; stage 4 at 0). The links are 9.4's rules: penalties add, refill factors multiply (never below a quarter), the slowest walking factor in force applies (never multiplied), health losses add at both last stages, recovery only while no drive is at stage 3 or 4, rest doubles refill and recovery. No clock and no `System.Random`: time comes from the host's stored ticks.
3. **The wiring:** a new partial `src/Vocaterra.Game.Core/Session/VoxelSession.Needs.cs` (as `VoxelSession.Sleep.cs`) that owns the `PlayerNeeds` and steps it on the world tick **only when play is active**: the game tells the session each tick whether play is active (no conversation, lesson panel, book, inventory, crafting, station, report, menu, F8 or the check battery, and not while speech is recording or processing; 9.2's "Time"). Raise `need_stage` (detail `hambre`, `sed`, `sueño` or `salud`) when a drive's or health's stage changes. Effects on the player (walking speed, sprint), eating, drinking, sleep, the collapse and death are wired by N02 to N04: N01 only computes them and exposes them for those milestones and for tests.
4. **World events:** `meal_eaten`, `water_drunk`, `need_stage`, `player_collapsed`, `player_died`, `player_woke`, `belongings_recovered`, each joining `WorldEvents.All` and every schema that lists event names (find them with `grep -rn` on an existing event name such as `item_crafted` or a current one in `content/schema/`). Only `need_stage` is raised in N01.
5. **Save version 6:** `SaveGame.CurrentVersion` = 6; an optional `systems` map on `SaveDocument` (a map from a system's name to its entry, so that W06 adds `systems.procedures` without another format change); its `needs` entry stores the five meters and the exhaustion timer (later milestones add regrowth, gift and chest state as optional fields). Versions 1 to 5 load with full needs. **The needs stay out of the world digest** (a load and a night compare it; adding them would refuse every older save); a load checks them by range instead and refuses or clamps a value out of range (say which). A night is a load in the same run, so the needs entry passes through it. Update `docs/game/saves.md`.
6. **Test aids:** `--needs off` freezes the needs full (the language runs keep their baselines) and `--set-need <need> <value>` sets one at launch (repeatable). Add them to `LaunchOptions` and its help text, and to `docs/game/running.md` or wherever the flags are listed.
7. **Needs on by default** in ordinary play. Every existing scripted run that would move because of the needs passes `--needs off` (edit the run definitions, not the baselines), so the scripted runs compare equal before and after; say which runs got the flag.
8. **Docs:** a short "Needs" part in `docs/game/architecture.md` (the folder, the partial, the data, the active-play rule) and the data file in `content/README.md`'s list.

## Values chosen (the design's decision 18, adopted provisionally; put them in `needs.json`)
- Gathering costs stamina per gather tick so that a minute of continuous gathering by hand costs about as much as 10 seconds of sprinting; the axe, which needs a quarter of the ticks, costs a quarter. State the number.
- A night and a collapse never lower a drive below 10; a drive already below 10 is left where it is (neither raised nor lowered).
- At full hunger the food is kept, even cooked food (no eating for healing alone).
- The rest (repeated deaths, an occupied chest site, returning before the chest expires) is N04's; leave room for it in the data.

## The tests (9.13, the parts that are N01's; under `tests/Vocaterra.Game.Core.Tests/Needs/`)
- Each drive falls only in active play, at its rate and its effort factors; nothing falls while play is not active (one test per kind of inactive state the session knows).
- Boundary tests at each threshold just below, exactly at and just above 0, 25, 50 and 75, on both decay and recovery; stage effects agree with 9.3 (a table-driven test over the data).
- Stamina's maximum is 100 minus the three penalties, never below 20; the refill factors multiply, never below a quarter; no sprint at any drive's last stage or at very weak health; 9.4's worked example (25, 35 and 70 minutes) gives a maximum of 55 and a refill factor of 0.544.
- Health damage adds at both last stages; recovery only with every drive below stage 3; rest doubles refill and recovery.
- The warning times of 9.4's "No dead ends" (about 30 minutes for thirst, 45 for hunger, without effort; about 20 and 30 with continuous effort), computed from the data.
- A night's and a collapse's effects with the floors above; a collapse after 60 active seconds at the sleep drive's last stage.
- Determinism: the same inputs give the same meters, stages and events; a save round-trips; the migration from version 5; every version from 1 loads; the world digest is unchanged by the needs.
- The data: the validator refuses a food that is no item, a non-positive rate, stages out of order.

## Behaviour checks (before the report; show the lines)
- A game run with `--set-need thirst 30 --screenshot` and a short walk: the save's `systems.needs` shows thirst falling, and a `need_stage` event when it crosses 25.
- The same run with `--needs off`: the meters stay full.
- A version-5 save from `tests/Vocaterra.Game.Core.Tests/Fixtures/` loads with full needs.
