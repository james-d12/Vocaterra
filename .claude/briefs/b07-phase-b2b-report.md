B07 part 2b addendum (agent a1713ee1fce44cccd, 2026-11-16 about 07:10 UTC)

Branch B07-streamed-world, head 50ee8383, worktree clean, nothing pushed. `scripts/test.sh full`: 12,267 passed, 3 skipped, 0 failed. Format and content validate clean.

1. Merged feature/biomes c2785332 (B02b round 3): clean.
2. `FarTerrainSampler` reads the per-column water: a column's water top is its own `WaterLevel` (`ColumnShape.Water`), the surface `Classify(..., water)` with the underwater kind; above the sea, the maximum level at the eight points 3 columns away stands in for `WaterEdgePass.EvenLevels`; the bank turn and falling cells are left out.
3. Tests: `FarTerrainSamplerTests` water assertions; a test comparing the sampler's river and lake columns above the sea with the generator's water top (58 of 60 equal, widest gap 3 blocks); one save-count assertion in `ChunkResidencyTests` relaxed from 8 to the range 4 to 8 (where the ground already holds the block an edit is none; terrain-dependent). Core README: the sampler's water.

Seam: `river-valley-midday.png` shows a near-ring river continuing as far water at the same height; lakes above the sea are flat sheets at their own level (`summit-*`). A river narrower than a far-tile cell does not show in far tiles (known limit, in index.md).

Measurements (Release, seed 42, `--pass open`, `--no-settle`, 250-block walk along the pass road, load 1.1 to 1.5, peak memory 0.6 to 0.8 GB):

| Preset | p50 | p95 | p99 | over 16.7 ms | resident chunks | far tiles | far triangles | meshing p99 |
|---|---|---|---|---|---|---|---|---|
| low (4 km) | 1.4 | 5.7 | 9.4 | 4 of 4,396 | 5,547 | 315 in 2.6 s | 611k | 7.7 |
| medium (8 km) | 1.7 | 7.6 | 9.5 | 3 | 9,247 | 381 in 3.1 s | 763k | 7.0 |
| high (16 km) | 2.5 | 7.8 | 9.6 | 3 | 14,466 | 549 in 4.6 s | 1.14M | 6.6 |

Experiments at high: far 32 km p95 7.9 ms but 1,186 tiles, 22 MB, 10 s of building (kept 16 km); view distance 576 p50 4.3, p95 10.6, 27k chunks (too heavy); far 8 km with view 304 no better. Start: one 626 to 670 ms frame (synchronous near zone) and one 124 to 162 ms; after it the worst about 50 ms; none over 100 ms outside start and jump. Presets unchanged (far 4,096 / 8,192 / 16,384). Picture runs at high: frame median 5.4 to 7.1 ms.

Scripted runs (base a build of c2785332): `--world valley` all identical but r01-route's sounds.log voice timing (22 lines). Streamed (scripted-runs.sh's default): all 32 differ, all exit 0: the new `world: streamed` and voxel stats lines in stdout, the far land and haze in every picture with sky, r01's sounds.log (64 lines, gains of far and near chunks), r24-followups "order only". No test or golden re-recorded; the bundle untouched.

Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b07/ (14 files, index.md): river-valley at midday, dawn, dusk, greyscale, medium, low; river-along-valley; summit-sea-lake at midday, dawn, dusk; summit-north-far-coast; rim-looking-at-chain-spine. No clean "chain spine at 16 km from open ground" shot (needs a hand-picked viewpoint).

Known, not fixed: rivers too narrow for far tiles beyond the near ring; the start's 600 and 150 ms frames by design; negative `--teleport` in script steps (B14); `--teleport` outside the valley needs `--pass open`.
