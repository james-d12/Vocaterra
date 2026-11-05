# N04 report (Sonnet 5.5, 2026-11-05), saved by the orchestrator

Branch `N04-health-death`, final commit `99e4eec1`, on feature/adaptive_design at 73a3041e.

- Health in play: N01 already stepped it; tested from the session (`Needs/DeathTests.cs`). `PlayerNeeds`: health below 1e-9 snaps to 0.
- Death (`Session/VoxelSession.Death.cs`, inside `StepNeeds`, so never while paused or with `--needs off`): wake on the nearest free ground 2-5
  blocks from `firering.player` (always built); health and drives 50, stamina full; `player_died` (`hambre`/`sed`; the lower drive if neither is at
  its last stage), `player_woke (death)`; no night, no day move, no resets. `VoxelGame` cuts to black and fades in over 3 s.
- Chest (`Needs/BelongingsChest.cs`): the bag minus `MaxStack == 1` items (only the book and the axe; the bottle stacks to 99, so it goes in the
  chest, unlike the design's reading) into a Chest block on the nearest free ground; a second death merges into one chest. E at the chest
  (`TakeBelongings`) takes what fits; `belongings_recovered (all|some)`. `RunDayResets` removes it; a load does not. Saved as `systems.chest`
  (cell and stacks; omitted when null), the block in the chunk edits; save version stays 6.
- Host steps `--take-belongings`, `--need <need> <n>` (mid-script). Runs r25-thirst and r26-death added; the 22 others unchanged.
- No dead ends: a flood of the standable-cell graph from the banks; the 30 farthest cells' shortest walks against 240 s at 0.85 speed.
- English keys `ui.beaconBelongings` ("your things"), `ui.diedHunger`, `ui.diedThirst`, `ui.chestRest`; Spanish for N05 («tus cosas» ...).
- Checks: `--skip-to-session 10 --set-need thirst 0 --set-need health 5 --wait 25` -> death, woke at 152,73,106 (50s, stamina 100), chest 5 items
  at 155,73,115; a load keeps the chest; `--sleep` removes it.
- Tests 9219 -> 9246 passed, 3 skipped; CI's case 9204, 45 skipped.
