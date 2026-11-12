# Y10 report: the stake and the plot's outline

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y10-stake`, HEAD `10fdbf57` (commits `f2598b2e`, `a8cfc811`, merge of `feature/settlement` with Y07 `44833197`, `10fdbf57`);
worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y10`.

## Gate (after the merge)
Build 0 warnings, format clean; with models 11,323 passed, 3 skipped; without models 11,276 passed, 50 skipped; validate and the A2
gate clean; goldens unmoved; the bundle hash in `save-v8.json`.

## Built
- Item `stake` (`ItemId.Stake = 35`, stack 1, places nothing); icon `BlockCell.IconStake` (a pale stake with a red pennant, drawn in
  code, last in the atlas).
- Word `estaca`: `cefr/a1.txt` (with a reason), `lexicon/game.json` (`estaca`, `estacas`), `game/nouns.json` (`items: ["stake"]`),
  `locale/en/nouns.json`; its word card recorded (two WAVs). `stake` added to the item enums of the nouns, objective and host-events
  schemas (Y11 can name it in `give_item`).
- Use hook: `Core/Input/StakeUse.Applies` (right click, mouse captured, stake selected, a block aimed at); `WorldInput`'s optional
  `stake` callback, wired by `VoxelGame` to `session.UseStake(BlockPos)`. A talk or a panel frees the mouse, so nothing happens then.
- Session (`VoxelSession.Land.cs`): `UseStake(BlockPos)` → `StakeResult` (NotApplied, Marked, Refused, AlreadyOwned); `MarkPlot(x, z)`
  → `StakeUse(Result, Check)`; `MarkPlotWithHint(x, z)` (no stake needed); `PlotRefusalLine(PlotCheck)`; `PlotHudLine()`.
- Outline `OverlayMeshes.PlotOutline(plot, surface, owned)`: four corner posts 2.5 high and a rail 0.45 above the ground, each at its
  column's height; built for plots within 64 blocks; drawn after the figures in the sun's diffuse light (not the chunks' `sunTint`).
  Pending: bare amber posts, a dashed rail. Owned: white posts with green flags, a solid white rail (shape as well as colour).
- HUD line (`PlotHudLayer`, under the crosshair, hidden in a talk): "Plot 10 × 10 · price 10 coins" or "Your land", within 24
  blocks or while the stake is selected.
- Flag `--mark-plot x,z` (with `--dev`): uses the stake at start and prints the result; a refusal does not exit.

Anchor: the aimed column is the plot's middle (`PlotAround`: 5 columns west and north, 4 east and south); `--mark-plot 266,165` gives
the plot at 261,160.

## Hints (`ui.plot*`)
"You cannot mark land in the village." / "…at the camp." / "…on the road."; "You can mark land only in the meadows, the open grass north
and south of the village."; "You cannot mark a plot on water."; "A tree is in the way. Mark a plot where no trees stand."; "A plot needs
plain grass or earth, not stone, sand or planks."; "That ground is too steep for a plot. Look for flatter land."; "Plot marked: {width}
by {depth} blocks, {price} coins. Use the stake again to move it."; "This land is already yours."; "You already have your land. The
stake has nothing more to mark." A refusal keeps the old pending plot; marking again moves it free; once land is owned the stake marks
nothing.

## The core's reading (`villager-open`)
«Quiero una estaca», «¿Me das una estaca?» → `request_item` {estaca}. «una estaca», «la estaca» alone → `unknown`, as «una manzana».

## Tests
Added `Land/StakeTests` (12), `Graphics/World/PlotOutlineTests` (5), `DevLaunchTests` cases, `IconStake` in the extruded-icon test.
Changed: noun counts 29 → 30 in three tests, `InventoryTests`' item list and stack limits.

## Runs in the game (seed 42, `--dev --skip-to-session 2 --give estaca`)
Village `--mark-plot 285,206`: refused, "You cannot mark land in the village.", nothing saved. North meadow `--mark-plot 266,165`:
"marked the pending plot 261,160 (10 by 10)", `systems.land` {pending 261,160,10,10}, outline and HUD line shown. `--own-plot
261,160`: owned, white posts, flags, solid rail, "Your land". Screenshots in `artifacts/y10/` of the y10 worktree. (The first rail lay
in the grass and was hidden; lifted to 0.45; the owned rail made white because green vanished in grass.)

## Merge of feature/settlement (Y07)
Conflicts only in the two manifests and the fixture's hash; regenerated (bundle `7f63e29e…`); the rest merged on its own.

## For Y11 and Y12
Y11: give the stake with `GrantItems(ItemId.Stake, 1)` or `give_item` naming `stake`; the price `Land.Rules.PlotPrice`; `OwnPlot()`;
the step "marked" is `PendingPlot != null`; the word and its card exist. Y12: `IsOwned`, the owned outline drawn without the stake;
planting on the plot still open.

## Known, not fixed
"1 coins" if the price were ever 1 (no plural key). The outline mesh stays under 4,096 vertices (about a 36 × 36 plot; larger would
silently not draw; the test pins 36 × 36). The outline uses the sun's light, not the chunks' `sunTint`. The stake checks no reach beyond
the normal aim distance.
