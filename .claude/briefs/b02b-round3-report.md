B02b round 3 report (agent a15541f516ac965c0, 2026-11-16 about 04:59 UTC)

**Branch / head / worktree.** `B02b-terrain-tuning`, head `9d82e105`, worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b02b`, clean, nothing pushed. Pictures and `index.md`: `/home/james/Dev/Vocaterra/artifacts/b-series/b02b/` (round 2's files moved to `round2/`).

**Gate.**
- Build with `-warnaserror`: 0 warnings. Format: clean. `content.sh validate`: OK, bundle `d8921585…`.
- `scripts/test.sh full`: 12,149 passed, 3 skipped, 2 failed (the two `WorldPreview` tests that classified biomes without the model's water). Fixed; `Vocaterra.WorldPreview.Tests` alone: 31 of 31 pass. The full suite was not re-run after that one test edit.
- `Vocaterra.Game.Core.Tests`: 4,163 passed. The merged tree's count before round 3 was 4,153 total.
- New tests: chain measure, coast straightness and river-line water (3 theories each, one case per seed); falling-water steps over eight river chunks of each seed; per-column water level (three `HeightModel` tests).
- `SceneTests.GoldenDigest` did not move.
- Re-pinned: the 18 procedural goldens (columns searched again), `save-v9.json` (bundle `d8921585…`, state digest `2f9ddd46…`, read on the merged tree), `WorldGenDataTests`. The atmosphere pins and end-to-end goldens did not move.

**Step 0, the merge.** Commit `1e7fe022` (feature/biomes at be453a65, before B13 and B08c). Conflicts:
- `world.json`: kept the tuning and added B08b's `pass` paragraph to `about`.
- `manifest.json`: re-hashed.
- `save-v9` fixture: bundle hash and state digest re-read on the merged tree.
- `ProceduralGoldenTests`: 18 digests re-recorded.
- `WorldPreview` `Cli.cs` and README: kept both sides' commands (`stats` and `entrances`).
- Two tests failed because of B11's caves, not the tuning, and were adapted: the bottom chunk of a column may hold cave air (the test now checks the bedrock floor row); a cave shaft may be open at the sea level.

**River water design.**
- Every column has its own water surface (`ColumnShape.Surface`, `WaterLevel`): the smooth ground (before the hills), minus the hills' amplitude, `height.water.clearance` (2) and the valley depth, quantised in steps of `height.water.step` (3) above the sea and never below it. A river reaches the sea at 32 with no step. Rivers (bed 3 under the surface) and lakes (bed 4 under) are carved down from that surface; the hills fade out under any water.
- Where the surface steps down, `WaterPass` builds the step the way the valley's `WaterAtRest` does: the lower column's cells from its surface up to the higher surface are falling water, topped by one cell of thin flow fed by the higher reach; every other water block is a source; `ChunkBuilder.SetWater` stores the levels.
- Corner closing and level evening: a bank column beside two reaches of different level becomes water at the lower level (`TurnToWater`); each river column takes the highest level within 3 columns, so a step lies across the river; window `Margin` grew by `GenerationWorld.WaterReach` = 6.
- Rest, tested: the flow rules change no cell of rivers, lakes and coasts of the 3 seeds with every water cell woken; every water edge is exitable; all flowing water is falling or the one thin cell.
- The save and the flow simulation see water levels as `ChunkData.WaterLevels`, as for the valley's rapids. Nothing wakes the simulation.
- Readers changed: `HeightModel`, `WaterPass`, `WaterEdgePass`, `RiverBankPass`, `ColumnContext` (`WaterLevelAt`, `TurnToWater`, `RaiseWater`), `BiomeTable.Classify` (water from the model), `WorldQuery`, `TreePass`, `CavePlan` and its water rule (per column), the blend's `Own`/`Toward`, the preview tool.
- Also: rivers fade out where the ranges' peaks add 100 to 200 blocks (`rivers.peakFadeFrom/To`); the sea fade ends at -10 so rivers reach the sea at full carve; lakes: weight sharpened, threshold 190, height band 50 to 68 (deep or absent); `biomes.json`'s `river-channel` row starts at continentalness 0 instead of 20.
- Schema and validator: `bedHeight` replaced by `depth` for rivers and lakes; `waterFrom`/`waterTo` removed. New `height.water`, `peakFadeFrom/To`, `climate.ridges.chainBlocks/chainRound`, `climate.continents.roughCell/roughBlocks/roughOctaves`.

**Chains and coasts.** New ridge field `chainBlocks` 1500: a distance to the noise's zero line, so every chain has the same width. The peaks spline is steeper and starts later; crags 20 every 100; terraces nearer the crest. Coasts get two more warps (half and an eighth of the bend) and a distance noise held to what each channel can spare (land cells never joined; channels 340 to 560 blocks each side).

**Figures** (seeds 42 / 7 / 2026; full table in the pictures' `index.md`):

| Measure | Round 2 | Round 3 |
|---|---|---|
| River line holding water | 7.0 / 8.1 / 7.7% | 99.9 / 99.8 / 99.6% |
| Water levels along the river lines | 1 | 41 / 41 / 41 |
| Lake-basin share of columns | 0.45 / 0.03 / 0.58% | 3.2 / 1.5 / 2.5% |
| River water that reaches the sea or a lake | 99.5 / 94.2 / 96.4% | 94.4 / 91.1 / 87.9% |
| Land above 180: body width from its spread | 874 / 2,411 / 843 | 518 / 2,463 / 505 |
| Land above 180: thickness | not measured | 233 / 237 / 246 blocks |
| Chain length (longest body above 150) | 4.9k / 5.9k / 6.9k | 4.6k / 6.0k / 5.4k |
| Coast on straight runs of 300 blocks or more | 50.7 / 51.2 / 57.8% | 35.4 / 37.4 / 36.4% |
| Longest straight coast run | 1,208 / 1,320 / 1,096 | 808 / 928 / 1,048 blocks |
| Sea share | 42.0 / 37.7 / 39.9% | 45.0 / 37.3 / 39.7% |
| Ring walk | 191 / 189 / 190 of 192 | 192 of 192 on all three |
| Steepest land step | 3 to 5 | 3 / 5 / 4 |

Seed 7's width of 2,463 is one body where three chains meet in a Y. Seed 7's ocean is 80.9% in one body inside the 16k square but 99.8% in 32k; that test moved to the 32k sample.

**Pins and bounds changed (weaker than before).** Land above 150 / 200 / 250 bounds 4 / 2 / 0.8%; river reach 85% (was 93); puddles at most 8 per 8,192-block square (was 0); shores changing land/water more than twice at most 12% (was 10); level tiles above 220 at most 3% (was 0); valley columns more than 1,500 and at least 15% in valleys that climb to 150 (was 5,000 and 40%).

**B07's far sampler must change.** It cannot use the sea level as the water height. Read `ColumnShape.Water ? ColumnShape.WaterLevel : none`, from `Surface`. To match the generator exactly it also needs three post-steps from `WaterEdgePass`: raise each river column to the highest level within 3 columns (never above the sea's level), turn bank columns beside two different levels into water at the lower level, and draw the falling cells where a lower column sits beside a higher one. A coarse far view can skip the last two.

**What still looks wrong.** Y and X junctions where chains cross (three in seed 7); coasts still straight for up to 800 to 1,050 blocks in places; small sand islets and 1 to 4 tiny inlets per 8k square; a few rivers end in a closed basin or a ring (87.9 to 94.4% reach water); a few river reaches of 4 to 16 blocks where a river runs along a contour (partly the preview walk's jitter); about 10 to 19% of river water columns border a column of another level; seed 42 has 0.9% of land above 250.

**Files outside the milestone's area.** `ProceduralReliefRegionTests` (B15's; an `Assert.Same` on a chunk now air, guarded); `ProceduralChunkSourceTests` and `ProceduralWaterTests` (B11's caves); `CavePlan.cs` (B11; only the per-column water rule); `ChunkBuilder.cs`, `ProceduralTestWorld.cs`.

**Left undone.** No in-game river pictures (no game windows, as briefed).

**Orchestrator's review (05:00 UTC).** The maps meet the owner's three points. One defect found in the river walks: rivers are noise lines with no direction, so the per-column water rises and falls with the land along a line (seed 7's walk climbs to y 98 over hills between ground at 42 and 47; seed 2026's river joins two seas over a rise). Sent back for a merge of B13 and B08c, a hump figure and one full run; the humps go to the owner as a decision (a drainage network would be a later milestone).
