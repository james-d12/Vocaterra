# B02b report (Sonnet 5.5, high; 2026-11-15)

Branch `B02b-terrain-tuning`, head `3ccc69c6` (three commits on 20e75aa5). Agent's suite: 11,912 passed, 0 failed, 3 skipped (about 40
new); build 0 warnings; format clean; validate passes; `SceneTests.GoldenDigest` unchanged. Every heavy command at one slot; no windows.

- Seas: continentalness cell 4096, 3 octaves, warp 1024/384; shore at continentalness 0.075 with a long shelf (sea floor from y 32 to
  below 0 far out); ocean `C <= 125`, inland water `lake-basin`; beach `C <= 145` and height <= 36.
- Mountains: erosion cell 5120, ridges cell 4096 (warp 1024/256); peaks spline from ridges 520, top 240; wider range factor, lower inland
  base: chains with foothills and a spine of 200 to 300.
- Snow and trees: snowy-slopes from y 120; taiga and grove lost `snowCap`; the tree line -300 to -480 (the old taiga could never grow a tree).
- Rivers and lakes: river cell 2048, width 14, sea fade at the new shore; lakes inland (cell 640, threshold 0), about ten times as common.
- Code (parameters in world.json with schema and validator): `height.coast` (peaks fade and hills calm near the sea); `LakeWeight` reads
  the land before rivers carve it (a river on high ground opened lake pits with 11 to 13 block steps); `regionBlend.headwater.distance`
  320 with `IHeadwaterHeightModel` and `BlendedHeightModel.WaterShare(distance)` (no water inside the margin; beyond it river width and
  depth scale by the share; lakes shrink to round ponds). Land bias floor/shoreFloor 150/105 (was 100/-120). Preview tool: `stats`
  command; the heights map's snow from the biome table.
- Ocean share about 60 to 65% (at 56% two of three seeds split into two or three big seas); one spline knot and thresholds: the owner's call.
- Before / after (16,384 square, seeds 42 / 7 / 2026): ocean 13.9/14.1/15.3% to 61.1/59.4/64.8%; largest sea share of ocean 6.1/4.6/6.1%
  to 95/90/86%; land above 150/200/250 5.2/2.0/0.3% to about 14/7/1.5 to 3.5%; lowest snow 32 to 120; river columns reaching sea or
  lake 65 to 73% to 93 to 98%; steepest step 7/9/8 to 4/6/5; walls about 5% to 0.3 to 0.5% of land; mean sea floor 1,024 blocks out
  -8/-14/-14 (none below 10 before). Coast: land 16 to 48 blocks from shore averages 37 to 39 (was 46).
- Headwaters: nearest water 279 to 300 blocks from the valley footprint (274 for the test region). Seed 7's east ridge and south lake gone.
- Ring walk (margin 64): 186/192 (96.9%), 186/192 (96.9%), 190/192 (99.0%); just under 97% for seeds 42 and 7 (the owner's call).
- Tests: `TerrainTuningTests`, `HeadwaterTests` (valley, 3 seeds, and a region at chunk (40,-30)), coast in `HeightModelTests`,
  validators, preview `stats`. "Starts at a spring, no wall at the end" is asserted through the share, the shallowness and the step bound.
- Pins moved: all 18 B03 goldens (columns re-chosen by search where the tuned world has each feature); save-v9.json bundle 2bb5a2a1...,
  state digest 88f2b844...; WorldShapeTests water share 50 to 80% (was 8 to 45) and "no biome covers half" excludes ocean; snow and tree
  line tests; HeightModelTests cases; shipped-number tests; two StreamingPolicyDeterminism edits Stone to Cobble (no-ops in the new terrain).
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b02b/ (65 files, index.md beside B02's and B06's files, before/after tables).
- Still wrong: ranges are rounded massifs or Y-shapes 2,000 to 4,000 long, the spine above 250 a snow plateau, ridge creases on foothills;
  continents small (2,000 to 5,000 blocks), mountains in their cores, ranges ending at the coast; the valley's surroundings a flat coastal
  plain at y 32 to 38 (the land-bias dome), rivers ending in tapered points; seed 2026 a snowy mountain west of the valley outside the
  bias radius; rivers only on low land (none from mountains); 3 to 7% of river columns in bodies reaching neither sea nor lake; seed
  2026's sea not always one body at 32k. Not seen in play.
- Left undone: ridge-kink smoothing, rivers from mountains, a larger continent scale (code beyond a number or a spline).
