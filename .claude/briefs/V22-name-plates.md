You are implementing the game milestone **V22, name plates**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The language core is the `Vocaterra.Nlp` engine and the Spanish pack `src/Vocaterra.Nlp.Es`; the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`. **This milestone's work is in the game's drawing code (`src/Vocaterra.Game`).** It is one of the follow-ups after PR #20's merge (`docs/HANDOVER_VILLAGE_PHASE_B.md` section 9.3 F).

Do the work yourself; do not spawn subagents.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/v22`, already on your branch `V22-name-plates` (from `feature/village-followups` at `9dfa08b`): cd there first, and never edit `/home/user/Dev/Vocaterra`, the worktree `.claude/worktrees/village` or any other worktree. **Other agents run at the same time** (QA testers and a core milestone); every heavy command goes through `scripts/limited.sh`, whose machine-wide lock makes yours wait for theirs, so expect a command to wait at times.
- **The owner wants work pushed often.** Commit after each coherent step that builds and passes its tests, with the prefix `V22: `. Push after every commit: `git push -u origin V22-name-plates` the first time, then `git push origin V22-name-plates`.
- Never push `main` or `feature/village-followups`, and do not merge.
- **Disk is short (about 11 GB free).** Build only the projects you need, never the whole solution: `src/Vocaterra.Game` and `tests/Vocaterra.Game.Tests` (and `tests/Vocaterra.Game.Core.Tests` only if you touch `src/Vocaterra.Game.Core`). The orchestrator's gate builds and tests the whole solution afterwards. Delete screenshot folders you no longer need; keep only the before and after shots you report.

## The problem
Name plates (the NPC's name drawn above its figure) show through figures and walls: on session 8's morning Luz's plate is drawn over the shopkeeper's face, and in phase A the farmer's plate showed through (V09, scripted run r17; `docs/game/README.md`, grep "Name plates show through figures"). Every NPC's plate in view is drawn, always on top.

## The owner's design (2026-11-01, D-game-22)
- **Show only the plate of the NPC the player faces within talking range, and the plate of the beacon's target** (the NPC the current objective points at), no other.
  - "Faces within talking range" uses the game's own rule for who the player would talk to (the same target the interaction prompt or `--interact` picks); do not invent a second range.
  - When the faced NPC is the beacon's target, one plate.
- **Draw plates depth-tested against the scene and the figures**, so a wall, a building or another figure in front hides the plate (or the part of it behind them). The plate keeps facing the camera and stays readable at the game's text scales (`--text-scale`).
- It holds in the first-person view and in `--third-person`.

## Read first (the sections named, not whole files)
- `CLAUDE.md`; `docs/game/ARCHITECTURE.md` (the layers and the drawing order, by `grep -n`); `docs/code_guidelines.md`.
- Find the plate drawing first: `grep -rni 'plate' src/Vocaterra.Game --include=*.cs` (likely in `Graphics/Figures/FigureDirector.cs`, `NpcFigures.cs` or `Graphics/DrawHelpers.cs`), then how the 3D scene and the figures write depth, and how the beacon's target and the talk target are known.
- `src/Vocaterra.Game/Startup/LaunchOptions.cs` for the screenshot, `--skip-to-session`, `--teleport`, `--third-person` and `--text-scale` flags; `docs/game/SCRIPTED_RUNS.md`, grep `r17` and the screenshot commands.

## What to build
1. Plate selection as a small pure function (testable without a graphics device): given the NPCs, the player's position and facing, the talk target and the beacon's target, it returns the plates to draw. Tests for: nothing in range and no beacon target (no plate), the faced NPC (one), the beacon's target far away (one), both the same NPC (one), both different (two), an NPC in range but not faced (none).
2. Depth-tested drawing of the selected plates, after the scene and the figures, with a depth state that tests but does not write, so the plate is hidden behind nearer geometry and does not hide later 3D.
3. Nothing else changes: the HUD, the beacon, the dialogue panel and the figures look as before.

## Proof (screenshots: read each PNG and look at it)
- **Before and after**, the same command each time:
  - session 8's morning (`--skip-to-session 8`), standing where Luz's plate covered the shopkeeper's face;
  - the farmer in phase A (the place of r17);
  - one shot with the faced NPC and a different beacon target in view (two plates);
  - one in `--third-person`, and one at the largest `--text-scale`.
- Save the after shots under `/tmp/claude-1000/v22/` and name them in the report; the orchestrator will look at them.

## Resources (the desktop has crashed from overload)
- Run every `dotnet restore|build|test|format|run` and every game or screenshot run through `scripts/limited.sh` (builds `-m:4`, tests `-m:2`). Never start two heavy commands at once: no `&`, no background builds.
- While iterating, use `scripts/test.sh project Vocaterra.Game.Tests`. Do not run `scripts/test.sh fast` or `full` (they build the whole solution); the gate runs the full suite.
- Exit code 137 means the 6 GB cap killed the command: lower the parallelism, and never work around the cap.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1`.
- The sandbox refuses a command line that contains the words `eval` or `hash`.
- Game runs need `DISPLAY=:0` and `VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`; give each run its own `--session-dir` under `/tmp/claude-1000/v22/`.

## Rules
- A game milestone edits only the game projects and their tests, `content/`, `assets/` and `docs/game/`; this one should need only `src/Vocaterra.Game`, its tests and one line in `docs/game/README.md` (the open point "Name plates show through figures" marked fixed by V22). It takes the core as it is.
- Engine code goes only in `src/Vocaterra.Game` (D-game-1); `Vocaterra.Game.Core` references no package and uses no `System.Random`, `Guid` or clock (`ArchitectureRulesTests`).
- Do not edit `scripts/`, `docs/game/IMPLEMENTATION_PLAN.md`, `docs/game/VILLAGE_PLAN.md`, `docs/nlp/`, `docs/qa-reports/` or either `DECISIONS.md`. Never create or edit anything under `.github/workflows/`.
- Write code that reads like the surrounding code.

## Your checks before reporting (from the worktree root, each through scripts/limited.sh)
- `dotnet build src/Vocaterra.Game -warnaserror -m:4` and `dotnet build tests/Vocaterra.Game.Tests -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh project Vocaterra.Game.Tests` all passing (and `Vocaterra.Game.Core.Tests` if you touched Game.Core); the counts before and after.
- `dotnet format` with `--verify-no-changes` on each project you changed.
- The screenshots above, looked at.
- Do not commit `bin/`, `obj/` or `artifacts/`. The orchestrator's gate then builds and tests the whole solution (7420 passed and 3 skipped before the follow-ups, with models).

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```

## Report (one page, under 400 words)
- The branch and the final commit SHA, pushed.
- Where the plates were drawn and what changed (selection, depth state, drawing order).
- The tests added; the test counts before and after.
- The screenshots' paths, before and after, with one line each on what they show.
- Anything left undone, and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra'`, kill only your own) and that the worktree is clean and pushed.
