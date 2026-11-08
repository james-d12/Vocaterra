You are implementing the game milestone **W02, tools and materials**, of the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours. **This milestone is code and tests only**: a material per block and a tool spec per item replace the axe's special case, with no change in behaviour.

Do the work yourself; do not spawn subagents.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/w02`, already on the branch `W02-tools-materials` (from `feature/bread-pilot` at its tip). cd there first; never edit `/home/user/Dev/Vocaterra` or any other worktree; do not switch branches. The worktree is new and not built: build once first.
- **Commit after each item that builds and passes its tests**, with the prefix `W02: `. Never end your turn with uncommitted work.
- **Do not push.** Never push or merge `feature/*` or `main`. Leave the worktree clean on your branch.
- **Run everything in the foreground and finish in one turn.** No background commands, monitors or wait loops.

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**, **Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/w02/` for your logs and session folders, about 11 GB of disk free, and **9852 passed and 3 skipped** as the count before you. **No `content/game-v1` edit** (blocks stay code, as C09's assessment advised); so `scripts/test.sh full` is your full run.

## The specification
`docs/game/activities-plan.md` section 6, paragraph **W02** (`sed -n 219,228p`). That paragraph is the milestone: `Material` per block in `Voxel/BlockDefinition.cs` (none, wood, plant, stone, soil, water); an optional `ToolSpec(Material Class, int Speed, int Yield = 1)` in `Items/ItemDefinition.cs`, the axe `(Wood, 4)`; `VoxelSimulation.GatherTicksOf` divides by the held tool's speed when its class matches the block's material, and `AxeSpeedUp` goes. Hands stay speed 1. Where the paragraph and the code disagree on a name or a file, the code wins; say so in the report. `docs/process/code-guidelines.md` for the style; `Vocaterra.Game.Core` references no package.

## What to build (each committed)
1. `Material` and every block's material (trunks wood, the berry bush plant; list the assignment you chose in the report).
2. `ToolSpec` on items; the axe's spec; `GatherTicksOf` reads it; `AxeSpeedUp` and any other axe-only branch removed.
3. Tests: a trunk takes 60 ticks by hand and 15 with the axe (the regression); a tool of another class changes nothing; hands on plants unchanged; the HUD's gather bar reads the same number as the simulation.
4. **The scripted runs** (the Gate section): before your first edit and after, compared; **every run must be unchanged**. Any difference is a bug to fix, not to explain away.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```

## Report (half a page)
The branch and final SHA (local); what you built (files, types); the material of each block; the tests; the scripted-run comparison (all unchanged); the test count before and after; design versus code; anything left undone.

Before your report, make sure no process you started is still running (`pgrep -fa 'dotnet|Vocaterra|scripted-runs'`, kill only your own).
