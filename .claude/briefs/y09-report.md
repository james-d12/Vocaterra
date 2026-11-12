# Y09 report: land rules and protection

(The agent's report, 2026-11-12, condensed by the orchestrator.)

Branch `Y09-land-rules` at `adb00ab7` (on `60f9f07a`), cut from `aec538f6`; worktree `/home/james/Dev/Vocaterra/.claude/worktrees/y09`.

## Built
- **Data:** `game/land.json`, `content/schema/land.schema.json`, `LandData` in `GameData` (`Land`, `WithLand` for tests),
  `LandValidator` (from `GameDataValidator`), manifest, provenance, a README row. `protectedSites`: `village` (margin 2, `includeBlend`:
  24 + 8 + 2 = 34, follows the scene), `camp.guide` (margin 2 = 14); `protectedBlocks`: `path`, `bridge`, a square margin of 2;
  `eligible`: `meadow.north`, `meadow.south`, radius 22; `plot`: 10 × 10, price 10, maxSlope 1. Each protection has a tag (`village`,
  `camp`, `road`) that picks the hint. The validator refuses a missing site, a disc covering nothing, negative margins, unknown blocks,
  empty regions, non-positive sizes, a price below 0, a negative slope.
- **Core `Land/`:** `LandRules` (a byte mask per column, built once at session start), `CheckPlot(grid, plot)` →
  `PlotCheck(Fault, Protection)`, `PlotFault` in order Protected, OutsideMeadows, Water, Tree, NotGround, TooSteep (`GroundOf` = the
  highest block not air, undergrowth, trunk or leaves; a tree = trunk or leaves within 24 above; every footprint column inside a meadow
  disc); `Plot` (west-north corner and size; `PlotAt`, `PlotAround`), `Protection`, `IBuildRules`, `LandRegister`, `LandSave`/`PlotSave`.
- **The hook:** `VoxelSimulation.BuildRules` (`VoxelSimulation.Land.cs`; a host setting, not in the digest; null = the old behaviour).
  `TryPlace` refuses every block but a torch on a protected column (`SimulationEventKind.PlaceRefused`, the session turns it into the
  hint). Planks become gatherable (30 ticks, wood back) only inside the owned plot; elsewhere `TakeBackRefused`, once per hold
  (`LiveVoxelInput.GatherFor` returns planks so the press reaches the simulation). `--pour` and the scripted `--place`, `--interact`,
  `--pour` go through the same rules.
- **Hints** (`locale/en/ui.json`): `landVillage` "You cannot build in the village.", `landCamp` "You cannot build at the camp.",
  `landRoad` "You cannot build on the road.", `landTakeBack` "You can only take planks back on your own land."
- **Save:** `systems.land` = `{pending?, owned?}`, each `{x, z, width, depth}`, written only when a plot exists; version 8; a load puts
  the plots back without a re-check; a plot with no size or outside the world is a `SaveException`.
- **Session** (`VoxelSession.Land.cs`): `Land`, `PendingPlot`, `OwnedPlot`, `IsOwned(x, z)`, `SetPendingPlot(x, z)` → `PlotCheck`
  (keeps the old pending one on a refusal), `ClearPendingPlot()`, `OwnPlot()` (re-checks the pending plot, owns it, clears pending;
  false if none pending, one owned, or the re-check fails; no coins).
- **Dev aid** `--own-plot x,z` (with `--dev`; the west-north corner; exit 2 with the reason on a refusal); help text, docs.
- Docs: content/README, architecture.md (Land/ and a paragraph), saves.md, running.md, the Core README.

## What is placed where
A player places only planks (from wood) and torches. Planks: refused in the village, at Luna's camp, on roads and the bridge; allowed in
the wild and the meadows, into river water too (a plank dam; tested). Torches: allowed everywhere. The river's course is unprotected but
for the bridge. The water plan's buckets were never built. `--pour` is refused on protected columns (applied to dev aids as the brief
said).

## Plots (non-overlapping valid 10 × 10, best of six greedy orders), asserted in `LandRulesTests`
42: north 7, south 7; 7: 7, 5; 1234: 7, 3; 2025: 7, 6.

## Tests
With models 11,298 passed, 3 skipped (11,221 before); without models 11,251 passed, 50 skipped. Build 0 warnings, format clean,
validate passes, A2 lint clean. Bundle hash `9df0748a…` in `save-v8.json`; goldens untouched. Added `Land/` tests (`LandDataTests`,
`LandRulesTests`, `PlotFit`, `LandPlacementTests`, `LandSessionTests`, `LandSaveTests`, `LandTestScene`), `Scripted/LandStepTests`, a
`DevLaunchTests` case. Changed: `WaterSaveTests.PourSpot` (an unprotected column), `NoLiteralTextTests`' allow-list (`LandValidator.cs`).
`AtmosphereSimulationTests` digests unmoved.

## Runs in the game (seed 42, `--dev`)
Village (285, 38, 206): refused, "You cannot build in the village.", the wood kept. Wild (266, 37, 165): placed. Take back in the wild:
refused, "You can only take planks back on your own land."; no `land` entry. With `--own-plot 261,160`: placed, then gathered in 30
ticks, wood back; `systems.land.owned` {261, 160, 10, 10}. Screenshots in `artifacts/y09/` of the y09 worktree.
Until Y11 a plank placed in the wild cannot be taken back (the hint says why). Holding interact on a scene plank (a house floor) shows the
same hint once per hold: player-placed and scene planks are not told apart (that would need saved state); a bit noisy.

## Scripted runs (r01, r03, r10, r20, r21, r26, r30)
All the same but r01's `sounds.log` (voice-end tick and footstep gains), which differs the same way between two captures of one build:
noise. (Its "before" captures overwrote seven run folders of an older `before/` folder in the shared scratchpad; it compared copies.)

## For Y10, Y11, Y12
- Y10: `session.Land.Rules.PlotAround(x, z)` (the stake's column in the middle) or `PlotAt` (corner); `session.SetPendingPlot(cornerX,
  cornerZ)` → `PlotCheck` (`Fault`, and `Protection` when Protected); no locale strings for the faults yet; the hint line is
  `session.TakeHints()`, the hints `VoxelSession.LandVillageHint` and others; the outline reads `PendingPlot`, `OwnedPlot`,
  `Land.Rules.PlotPrice`.
- Y11: the price `Land.Rules.PlotPrice` (10); call `OwnPlot()` inside the exchange after the coin check (it re-checks and returns
  false on a problem); it owns whatever is pending; a miss's reasons from `PendingPlot`, `OwnedPlot`, `Land.Rules.CheckPlot`.
- Y12: `IsOwned(x, z)` for containment; camp-kit blocks must not go through `TryPlace` (no block but a torch on protected columns;
  planks on owned land are allowed); camp blocks need their own take-back rule (only planks come back today); a load does not re-check
  the plot; torches are always allowed.

## Known, not fixed
Origins need every footprint column inside a meadow disc (stricter than "centre within 20"; counts pass). The seed-42 test coordinates
need re-picking if Y08 changes the meadows' trees or terrain. The spawn ledge and the trail are road-protected (path plus 2). Scene
planks give the take-back hint. Merge notes: `GameData`'s constructor gained a trailing `LandData` (every `With*` changed);
`SystemsSave` gained `Land` last; the bundle needs hashing again; small additions in `LaunchOptions.cs`, `Program.cs`, `ui.json`.
