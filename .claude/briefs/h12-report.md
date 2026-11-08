H12 report (Sonnet 5.5 medium, 2026-11-08). Branch H12-lake-bank at 4ff7a7fe (a8edcc9d the fix, 4ff7a7fe r31-lake), worktree clean.
Checks: build -warnaserror clean, format clean, scripts/test.sh full 10394 passed, 3 skipped (was 10393); content/game-v1 untouched.

Bad edges: the scan (tests/.../TestSupport/UnexitableEdges.cs, with PlayerBody, Immersion, StepHeight, SwimMotion.HopReach) found 582
before the fix. Cause: every shore had a rim of one-block-deep water (0.875, not swimmable) under a bank 2 over the bed; the bank began
at (level+1)*16 in all three carvers. Lake 303 edges, the whole rim (x 168..260, z 263..322); river about 275 single columns from the
pool to the mouth; the waterfall pool's rim (x 251..265, z 112..124); the bridge landings (x 228..236, z 200..210). Ponds none.

Fix (TerrainGenerator.cs): BankHeight (the bank starts at the water's level, eased up by weight squared; all three carvers);
BankBesideWater (a pass in Finish: each dry column beside water into [the adjacent water's top, 1 over the bed of adjacent shallow
water]; the floor wins so water never spills; paths kept; faces more than 4 above left (cliffs, plateau); reads heights as they were).
Scan exemptions: a bridge floor; a bank column holding back higher water (the river staircase steps at 258,139 and 253,152). The scan
ignores walls over 12 and the 5..12 range the pass leaves. Water cells' count and hash, spawn, points, NPCs, props, Path and Bridge
counts identical; 3514 columns (2.4%) changed along lake, river and pool, none near the village.

Tests: Water/WaterEdgesAreExitableTests (zero edges); WalkerLeavesTheLakeTests (the walker leaves at 212,262); TerrainTests (banks level
with the water or above). Moved: SceneTests.GoldenDigest b699324f68f071a6 (was 3eba51d3); save-v1/v4 state digests; no end-to-end golden;
bundle hash unchanged; water at rest 0 cells changed.

Scripted runs: before /tmp/claude-1000/h12/runs-before, after runs-after, compare.txt; 13 differ, all from the world: digests and
hostStateHash (r01, r09, r20, r21, r26, r27, r30), mesh stats (r01), --teleport 234,222 lands at y 35 not 36 (r20, r25, r30), r27's walk
to 279,208 38 turns not 36 (same 2439 ticks, arrival 0.03 off; village pixels in r27, r30), distant bank pixels (r01, r07..r09, r12,
r14, r15, r21, r25, r26; r07, r01, r25, r27 inspected). New r31-lake (scripted-runs.sh, scripted-runs.md): water.png, land.png,
land-north.png. Left: no synthetic-grid unit test of the scan; no pictures of the river and pool banks.
