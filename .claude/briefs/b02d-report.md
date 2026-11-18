B02d report (agent acf2a5ee6fc203465, Sonnet 5.5 high, 2026-11-18 about 04:00 UTC)

Branch `B02d-lake-levels`, head `7d0105d4` (feature/biomes at 5a5bd150 merged in), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b02d`.
Cause: `ReadLandmass` gave a lake node max(its flood level, the level of the node it was found from), often a bank node whose level sat
above the dented flood level: one body at 47, 45, 43, 41. Fix (`DrainageNetwork.cs`; pure; `DrainageCell.Near` untouched): each lake body
(eight neighbours) takes its flood level (its spill pass); nodes covered by less than `LakeMin` and spurs (no lake node N, E, S or W)
dropped, repeated until stable; a kept lake node's depth floored at `LakeBasin * 8` (the full carve), so the rim does not speckle; nodes
downstream capped at the lake's level (rivers still only fall); `ProceduralGenerator.Version` 5 (a fresh far-store folder). Puddles
(`TerrainTuningCoastPoolsTests`, limit 3): 42: 0 / 0; 7: 1 / 1; 2026: 4 / 0 (two earlier attempts gave 9 on seed 7, then a 40% loss of lake
share; no limit changed). Lake share: 3.40%, 1.94%, 2.82% (B02c 3.4, 1.9, 2.8). `LakeLevelsTests`: the seed-2026 rows (z 620, 700, 780)
one run each at 41; over three seeds no body holds two levels and no outflow rises above its lake. Pins re-recorded once on B11d: three
lake goldens (42, 7, 2026), the fixture's state digest 4240c459..., atmosphere PinnedFresh d9053e79..., PinnedAfterWalking 9b1308d1...; the
bundle hash unchanged. Full suite after the merge: 12,768 passed, 11 failed (the pins only); after re-recording, the pin classes and
LakeLevelsTests 122 passed; build and format clean (not a second full run). Pictures in artifacts/b-series/b02d/ (seed 2026): two thin
lines still cross the far lake at about 650 to 700 and 1,050 to 1,090 blocks from the eye; the data under it is one level (41); likely the
far view's level seams on water (at High, levels 2 and 3 begin near 600 and 1,000 blocks); not confirmed.

Round 2 (about 04:35 UTC), head 6bb0776a: merged b5d7304d (B03b); B03b's tours had lake-basin stops the lake change moved (BiomeTourSeedsTests failed B02d's first gate, merge reset): r52 15-lake-basin 1204,4462 to 1212,4470 (look 180,-4); r53 17-lake-basin 5268,942 (look 90,-4) to 5276,946 (look 180,-4); other stops unchanged. Full suite 12,787 passed, 0 failed.
Gate (about 05:05 UTC): `b32e7ec4`, 12,787 passed, 0 failed: the final gate of feature/biomes before the merge into main.
