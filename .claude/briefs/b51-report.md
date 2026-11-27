# B51 report (LOD seams)

Branch `B51-lod-seams`, head `ffc82541` (feature/lod-lifecycle at 6b54f176 merged). Agent: Sonnet 5.5, high. Files: `FarTileMesher.cs`,
`FarGranules.cs`, `FarView.cs` (5 lines, `FarTile.RunsOf` appends skirt runs when the tile has windows); tests `FarSeamTests`, `SeamProbe`,
`FarViewLifecycleTests` (+ a flight seam case), `FarTileGranuleTests` and `FarViewStandInClipTests` adapted.

Built: a stand-in drawn inside windows (B47) had no wall along a window's edge, so sky showed through where the finer ground or the chunks
beside it stood lower (the review's five-block opening; the owner's stepped sky line). `FarTileMesher` now hangs a wall of ground voxels
under every granule edge of a cut tile, a skirt's depth under the lower of the two columns' ground (2 cells textured, 2x the tile skirt
flat), also beside a neighbour's crown, deep water, air under the ground and hollows under overhangs; the faces go into a new fourth bucket
`FarBucket.Skirt` (after the water), drawn only for tiles with windows, never with a whole tile (memory and upload, no draw cost there).
A ground voxel's tile-edge face is no longer culled by a neighbouring crown or trunk (whole tiles change by 1 to 3 quads, area unchanged).
Lanterns' ground joins the skirt bucket (no glow there). `Volume.Ground` cached per column (speed only).

Failing first: `FarSeamTests` (3 seeds, stand-in levels 1 to 6 over leaves 0 to 5, 97,344 blocks of edge): old mesher 54,254 blocks open
(worst 2, 4, 8, 20, 32, 64 blocks at levels 1 to 6), new 0 under the stand-in. Flight case (real workers, the ring, 77 sampled frames,
about 1.65 million edge blocks): 0 open (44 open, worst 50 blocks, before the hollow walls).

Zone-edge overlap: already there at the game's voxel radius (400 to 576): leaves not wholly inside 0.96 of the radius draw whole behind the
chunks (at least 16 blocks plus the straddling leaf); the sky line was the stand-in cut edge, now walled. An `InnerRadius` change for small
zones broke B42's rule in `FarViewChunkZoneTests` and `FarViewStandInClipTests` and was reverted (`ffc82541`); under 400 blocks the
overlap stays under a chunk (12 blocks at 304, 9 at 224).

Tests: full run about 14,012 passed (4 Game.Tests failures from the reverted change; Game.Tests rerun alone after the revert: 1,848 passed);
timing 23; build, format, validate clean.

Known, not fixed: a fine leaf standing higher than a stand-in 3 or more levels coarser: its own skirt is too short (level 4 over 0/1: 23% /
22% of edge blocks open, worst 8 / 6 blocks; level 5 over 1: 16%, 4; level 6 over 2: 40%, 24; level 6 over 3: 6%, 8); the test asserts 0
only up to 2 levels apart and prints the rest. Deeper fine skirts would add about 1,000 quads per level-0 tile; the better fix is per-side
skirt buckets drawn only beside a different level (not built). Crown gaps at joins not treated as seams. **Floating pale slabs** over forest
and lakes at the mountain spot (-200,1600), before and after: not investigated (ice or water at the wrong height?), worth a QA look.

Measurements: skirt quads per tile (memory only) level 1 385 (+16%), level 3 about 750, level 4 about 1,250 to 2,170; whole-tile drawn
quads unchanged within 1 to 3. Scripted run (seed 7900, High, view distance 576, far detail 200%, 8 workers, `--no-settle`, fresh store):
last-frame scope 1,118,457 quads before and after; far pass processor median GLSL 6.41 to 7.08 ms, effects 7.39 to 8.54 ms; card 4.91 to
5.04 ms (GLSL), 3.72 to 3.96 ms (effects); noisy, covers the whole fill. Pictures `artifacts/b-series/b51/` in the b51 worktree (not committed).

Text for docs/game/architecture.md (far view): "A stand-in drawn inside windows has no wall along a window's edge in its ground data, so
the mesher of a tile built cut (`cut: true`) hangs a wall of ground voxels under every granule edge inside the tile, a skirt's depth under
the lower of the two columns' ground (it also covers crowns, deep water, air and hollows under overhangs there). Those faces go into their
own bucket, `FarBucket.Skirt`, after the water; `FarTile.RunsOf(Opaque)` draws them only for a tile with windows, never for a tile drawn
whole, so they cost memory and upload, not draw time. A stand-in's wall closes steps down to the finer ground beside it; a finer leaf's own
skirt still has to cover a step up to a stand-in that is 3 or more levels coarser, which it does not always (see `FarSeamTests`). Tests:
`FarSeamTests` (mesh probe over seeds and levels), `FarViewLifecycleTests` (flight with window edges)."
