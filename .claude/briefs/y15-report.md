# Y15 report (condensed): the stake taken at the purchase

Branch `Y15-stake-taken` (dda2fa03, d9b4ce7f), gated into `feature/settlement` 2026-11-12 (merge 55248e07): 11,469 passed, 3 skipped
(Y14: 11,463), format clean, content gate clean.

- The sale (`SellPlot`, `VoxelSession.Land.cs`): every stake in the bag goes to the mayor through `Take` after the coins, before the kit
  (log `item_taken estaca`). A refused sale keeps the stake; a sale with no stake held (`--mark-plot`) goes through; a stake in a chest
  stays there.
- The room rule (`NpcEffect.PlotMiss`, `NpcHostEvents.cs`): room for the kit is checked on a copy of the bag with the coins and the stakes
  out, by the worker and the session alike. `no_room` now happens only with no stake held.
- The step `my-land.stake` counts the bag plus one once the plot is owned (`HeldForSteps`, `VoxelSession.cs`), so it stays done in the
  run and after a load; with `--own-plot` it reads 1/1 from the start. The objective's `about` updated; bundle hashed, fixture updated.
- `SecondStake`: the mayor gives no stake once land is owned (a request after the purchase gets none).
- `ui.plotBought`: "You bought the plot for {price} coins. The mayor keeps the stake; the camp kit is in your bag."
- After the purchase: no offer (`LandOfferAt`), `already_owned` first, HUD "Your land" without the stake, the stake's hook not applied.
- Tests: 6 added, 1 changed (`no_room` holds no stake), in `PlotPurchaseTests`.
- Scripted runs: r41 gains `item_taken estaca`, its saves and shots differ (no stake in the bag); r42 gains `objective_step estaca 1/1`
  at tick 1 (`--own-plot`).
- Documents: architecture.md, saves.md, scripted-runs.md, lessons.md, running.md, content/README.md.
