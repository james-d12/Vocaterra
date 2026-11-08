You are implementing the game milestone **H12, the lake bank**, in the Vocaterra repository (C#, .NET 10, a MonoGame first-person voxel
game that teaches Spanish). The game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}`; the language core (`Vocaterra.Nlp*`) is not yours.
Do the work yourself; do not spawn subagents. Commit prefix `H12: `.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/h12`, already on the branch `H12-lake-bank` (from
`feature/bread-pilot` at its tip, which holds the water series, the bread pilot and H11; it has bin/ and obj/ from an earlier build). cd
there first; never edit `/home/user/Dev/Vocaterra` or another worktree (`adaptive`, `german` are not yours). Commit after each step that
builds and passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run
everything in the foreground and finish in one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**,
**Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/h12/` for your logs, about 10 GB of disk free (check `df -h
/home` before a build; stop and report if it falls under 6 GB), and **10393 passed, 3 skipped** as the count before you. If you edit
`content/game-v1` (the scene's lake), it is a content edit: run the content loop as `docs/process/milestone-briefs.md` "What a content
milestone adds" says (`sed -n 53,77p`), re-record goldens and fixtures only where the world change moves them, and say which moved. Do not
edit `docs/game/decisions.md`, either implementation plan or `docs/game/water-plan.md`; say in the report what they should say.

## The problem and the owner's answer
- H11 (report `.claude/briefs/h11-report.md`, item 5; its tests `tests/Vocaterra.Game.Core.Tests/Scripted/WalkerLeavesTheLakeTests.cs`)
  found that the lake's sand bank at (212, 262) stands 2 blocks above a 1-block-deep shelf, too shallow to swim, so no player can climb out
  there: a wading jump rises about 1.3 blocks and the hop-out (water plan 4.3, `sed -n 116,126p docs/game/water-plan.md`) needs the
  swimming state. A player gets in only by dropping off that cliff.
- **The owner, 2026-11-08: "Fix the lake bank"** (`docs/game/decisions.md`, D-game-26, the paragraph on the cliff). Fix it in the world,
  not in the swim rules: `SwimMotion`, the hop-out and the scripted walker stay as they are.
- The row: `grep -n '^| H12 ' docs/game/water-plan.md`.

## What to build (each committed)
1. **Find every such edge.** Write a pure helper in the tests (or `src/Vocaterra.Game.Core` only if the game has a use for it) that
   scans the shipped world (the scene in `content/game-v1/game/scene.json`, built by `World/Generation/SceneBuilder.cs`,
   `TerrainGenerator.cs`, `Terrain.cs`, `World/Layout/LakeSpec.cs`) for water edges a player cannot leave: a water cell the player can
   stand or swim in, beside a dry standable cell whose floor is out of reach from that water (2 or more blocks up where the water is too
   shallow to swim, or more than the hop-out lifts where it is deep). Use the body's real numbers (`PlayerBody`, `SwimMotion`), not
   guesses. Count the lake, the river, the ponds and the waterfall pool; list each bad stretch in the report.
2. **Fix them at the source.** Reshape where the world is made: the lake's spec in the scene data or the generator's bank shaping
   (a beach slope, a step, or water deep enough to swim at the wall), whichever keeps the lake looking as designed and changes the
   least. Keep the river's and the waterfall's layouts as H07a left them (D-game-26 item 6). Keep the village, the routes, the NPCs' paths
   and the camp unchanged; check that no prop, path or spawn moves. A cliff no player can reach from the water is not a bug.
3. **The regression test:** a C# test that runs the scan over the shipped world and asserts no unexitable edge (the list in the
   failure message). Update `WalkerLeavesTheLakeTests`: the case "the cliff gives up" becomes the walker leaving the lake at 212,262.
4. **The scripted runs:** capture `scripts/scripted-runs.sh` before your first edit (`/tmp/claude-1000/h12/runs-before`) and after, and
   compare with `scripts/compare-runs.py`: a run whose view includes the reshaped bank may change in pixels or digests; explain each
   difference; any other is a bug to fix. One scripted lake run: walk into the lake, swim to (212, 262), leave the water there, with a
   screenshot of the new bank from the water and from the land.

## Report (half a page)
Branch, final SHA, worktree clean; the bad edges found (where, how high, how deep) and how each was fixed; what moved (world digests,
goldens, fixtures) and why; test counts before and after; the scripted runs compared, each difference explained; screenshot paths; what
the orchestrator should record; anything left undone.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
