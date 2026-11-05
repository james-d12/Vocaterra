You are implementing the game milestone **N04, health and death**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game.** It builds on N01 (the needs model), N02 (eating, drinking, sprinting, rest) and N03 (the night, the collapse, the day's-resets seam): it adds health's losses and recovery in play, death, the belongings chest and waking at the camp fire.

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/n04`, already on the branch `N04-health-death` (from `feature/adaptive_design` at its tip when you start). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches.
- **Commit and push often** (the owner's rule): commit after each item that builds and passes its tests, with the prefix `N04: `, and push after every commit (`git push -u origin N04-health-death` the first time).
- Never push `main` or `feature/adaptive_design`, and do not merge. When you finish, leave the worktree on your branch, clean.

## The specification
The owner's Adaptation Design Document is the specification. Its text (extracted from `docs/game/design/sources/Vocaterra_Adaptation_Design_Document.docx`, one paragraph or table cell per line) is at `/tmp/claude-1000/orch/adaptation-design.txt`: read the sections this brief names with `grep -n` and `sed -n` (the headings are numbered, e.g. `grep -n '^9\.[0-9]* ' …`), never the whole file. Do not edit the `.docx`. Where the design and the code disagree on a file or line, the code wins; say so in the report. The owner adopted the design's section 18.2 recommendations provisionally on 2026-11-04 (`docs/game/decisions.md`, D-game-23, "Owner's answers, 2026-11-04"); where this brief fixes a value the design leaves open, use it and list it in the report under "values chosen".

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/n04/`, and print at most 30 lines of it.
- Keep `sed -n` ranges under about 150 lines; prefer `grep -n` first. Never read `docs/game/speech-checklist.md`, `manifest.json` or the whole of `content/PHRASES.md`.
- Wait for a long command in the foreground. **Never leave a wait loop or a background process behind** (a `while pgrep -f …` loop matches its own command line and never ends).
- **Disk:** about 15 GB free. While iterating, build only the projects you need; build the whole solution once at the end.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run`, every `scripts/content.sh` call and every game run through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly (content-loop refuses limited.sh, exit 3).
- Never start two heavy commands at once: no `&`, no background builds. Use `scripts/test.sh fast` or `scripts/test.sh project <test project>` while iterating; `scripts/test.sh full` once before the report.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/n04/`.
- Model-gated tests and game runs read `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`. Game runs need `DISPLAY=:0`, each its own `--session-dir` under `/tmp/claude-1000/n04/`; scripted steps need `--screenshot`. The host: `dotnet src/Vocaterra.Host/bin/Debug/net10.0/Vocaterra.Host.dll`.
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
- `scripts/test.sh full` (or `scripts/content-loop.sh --speech --tier full` if you edited content), all passing: 9219 passed and 3 skipped before you; report the new count.
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
- The design: 9.4 (health's losses: 1 every 6 s at hunger's last stage, 1 every 4 s at thirst's, adding with both; recovery 1 every 12 s while no drive is at stage 3 or 4, twice at rest; the no-dead-ends rule), 9.7 (death and its cost; the owner's decision 10, adopted provisionally under D-game-23: the chest), the scripted-runs line of 9.10 (r25-thirst, r26-death), 9.13 (the death, chest and no-dead-ends tests), and the N04 row of 9.12.
- `.claude/briefs/n01-report.md`, `n02-report.md`, `n03-report.md`, then the code they name: `PlayerNeeds` (`Die`, `Wake`, health's rules), `VoxelSession.Needs.cs`, N03's day's-resets seam, the Chest block and its use (`grep -rn 'Chest' src/Vocaterra.Game.Core | head`), the beacon (`grep -rln 'Beacon' src/Vocaterra.Game.Core`).
- `docs/game/scripted-runs.md` (how a run is added and baselined), `docs/game/running.md`, `docs/game/saves.md`.

## The deliverables
1. **Health in play.** If N01 already steps health's losses and recovery, test them from the session (the rates, both at once, recovery only with every drive below stage 3, doubled at rest); otherwise wire them through `PlayerNeeds`.
2. **Death.** At zero health the screen fades and the player wakes by their own fire at the cliff camp (the one place to sleep; if the camp fire is not built yet, the camp's spot, say which) with health and the three drives at 50 and stamina at its effective maximum. Raise `player_died` (detail `hambre` or `sed`, the drive that killed) and `player_woke`. A death is not a night: the day and the sitting do not move, and the day's resets do not run. A lesson, an order or an objective in progress continues where it was; the learner record, lessons, objectives and placed blocks are unchanged. Death cannot happen while the meters are paused.
3. **The chest (decision 10).** Everything in the bag except the items that stack to 1 (the tools, the book, the bottle: check how the bag defines a stack of 1) stays where the player died, in a Chest block on the nearest free ground cell. The beacon can point to it as «tus cosas» (that label is existing Spanish if the bundle has it; if not, an English key and the Spanish listed for N05). E at the chest takes back what the bag can hold and leaves the rest. At the next night (N03's seam) the chest disappears with whatever is in it. The chest and its contents are saved (in the `systems` map or the world's block edits, whichever the save already uses for a placed chest; say which) and round-trip.
4. **No dead ends (9.4).** A test: from the farthest walkable point of the generated valley for the default seed, at the last stages of thirst and hunger with full health, the walk to the nearest water at walking speed takes less time than the health left lasts. Compute it from the world and the rates, not a hand-picked number.
5. **Runs.** Two new scripted runs, `r25-thirst` (the thirst stages, a drink by hand and one from the bottle) and `r26-death` (a collapse from sleep, then death from thirst, waking at the camp, the chest taken back), added and baselined the way `docs/game/scripted-runs.md` says; the other runs unchanged (they take `--needs off`). If a host option is missing to drive them, add the smallest one and document it.

Player-facing text (the death screen's line, if any) is an English key in `content/locale/en/`; list the Spanish N05 will need. Add no Spanish to `content/game-v1`.

## The tests (under `tests/Vocaterra.Game.Core.Tests/Needs/` and the session's tests)
Health's losses at each rate and both at once; recovery's condition and rest; death at exactly zero; the waking values and place; the events and their details; the day and sitting unchanged by a death; the chest's contents (the bag minus the items that stack to 1), its cell, taking back what fits, its removal at the next night and not at a load; lesson, order, objective, learner record and placed blocks unchanged across a death; no death while paused; the no-dead-ends test; saves round-trip with a chest; determinism.

## Behaviour checks (before the report; show the lines)
`r25-thirst` and `r26-death` end to end with their event lines; with `--set-need thirst 0 --set-need health 5` and a wait: `player_died (sed)`, the waking position and values, the chest's cell; a load of the result shows the chest; `--sleep` after it: the chest gone.
