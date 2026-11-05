You are implementing the game milestone **N02, eating, drinking and sprinting**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on N01, the needs model (`src/Vocaterra.Game.Core/Needs/`, `Session/VoxelSession.Needs.cs`, `content/game-v1/game/needs.json`): it gives the player the actions that move the needs. Use N01's types; extend them only where an action needs it, with tests.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n02`, already on the branch `N02-eat-drink-sprint` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N02: `, and push after every commit (`git push -u origin N02-eat-drink-sprint` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n02/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n02/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n02/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: @@COUNT@@ passed and 3 skipped before you; report the new count.
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
- The design: 9.4 (the links; the rest rule's line begins "Rest: standing still"), 9.6 (eat, drink, sprint, the food table), 9.13 (the tests that are N02's: eating and drinking restore and cap, food kept at full hunger, the bottle comes back empty, a drink by hand with nothing held, no sprint at a drive's last stage or very weak health, rest doubles recovery and refill), and the N02 row of section 9.12.
- N01's report, `.claude/briefs/n01-report.md`, and its code: `PlayerNeeds` (`Step(ticks, activity)`, `Eat`, `Drink`, `Effects()`), `NeedsData`, `VoxelSession.Needs.cs`, `WorldEvents.All` (the seven need events; N01 raises only `need_stage`).
- `docs/game/architecture.md` (its Needs section), `docs/game/controls.md` or wherever the key bindings are documented (`grep -rln 'Left Shift\|KeyBinding' docs/game src/Vocaterra.Game*`), and how the book is used with a right-click today (`grep -rn 'UseHeld\|RightClick\|right-click' src/Vocaterra.Game*`).

## The deliverables
1. **Eat.** With a food selected in the hotbar, right-click eats it (food cannot be placed, so right-click is free), through N01's `PlayerNeeds.Eat` and the food values in `needs.json`. At full hunger the food is kept and a line says so. Raise `meal_eaten` with the food's noun as detail.
2. **Drink.** Right-click with the water bottle drinks it and leaves the empty bottle; E while aiming at water within reach (the river, the lake, the well) is a drink by hand, with nothing held and without Spanish. Raise `water_drunk` with `botella` or the place as detail. Session 1's objective still needs the bottle filled, and its hint for water without a bottle stays: check both with a test.
3. **Sprint.** A new control, sprint, Left Shift by default and rebindable like the other controls: about 1.6 times the walking speed while stamina lasts and no stage forbids it (N01's effects say when). Its stamina cost through `Step`'s activity. Scripted runs never hold it, so their walking is unchanged. Document the control where the controls are documented.
4. **The gathering cost.** Gathering by hand and with the axe costs stamina at N01's rate (`needs.json`) through `Step`'s activity; at zero stamina gathering slows but never stops. If N01 already wires it, say so and test it from the session.
5. **Rest at a lit fire.** Standing still within 3 blocks of a lit fire doubles stamina's refill and health's recovery (N01 has the factor; wire the condition). A lit fire is the player's own fire with its torch on the rim and the forge, whose post-torch always burns; give the other fire rings of the world the same lit state where the design says so. Rest does not restore sleep.
6. **Events and the debug output.** `meal_eaten` and `water_drunk` reach the host's event output like `need_stage`; `--needs off` turns every effect of this milestone off but the actions themselves (eating still uses the food; the meters do not move).

The player-facing lines of this milestone (food kept at full hunger, and any other) are keys with English text in `content/locale/en/`; their Spanish is N05's (the grammar-cache section below). Report each key and the Spanish it will need.

## The tests (under `tests/Vocaterra.Game.Core.Tests/Needs/` and the session's tests)
Eating and drinking restore and cap; at full hunger the food is kept; the bottle comes back empty; a drink by hand works with nothing held and only within reach of water; sprint is refused at a drive's last stage and at very weak health and when stamina is 0, and walks at the design's speed otherwise; gathering costs stamina and never stops at zero; rest doubles refill and recovery only standing still within 3 blocks of a lit fire (just inside and just outside 3); the events carry their details; `--needs off` leaves the meters still; determinism (the same inputs give the same meters and events); a save after eating and drinking round-trips.

## Behaviour checks (before the report; show the lines)
Through the host's scripted options (`docs/game/running.md`): with `--set-need hunger 40`, eat a bread or a berry and show the meter before and after and the `meal_eaten` event; with `--set-need thirst 30`, a drink by hand at the river or the well and the `water_drunk` event; with full hunger, the food kept. If the host has no option to eat or drink, add the smallest scripted option that does (document it in `running.md`), or show the checks through tests and say so.
