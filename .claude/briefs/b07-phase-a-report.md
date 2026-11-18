# B07 phase A report (Sonnet 5.5, high; 2026-11-16, no game windows)

Branch `B07-streamed-world`, head `11cad482` (four commits on 660586a9). Agent's suite: 11,993 passed, 0 failed, 3 skipped (92 new); build
0 warnings; format clean; validate passes; `SceneTests.GoldenDigest` unmoved. The full run came before the last small edits.

- Core: `SurfaceBands`/`ColumnBand` (`ProceduralChunkSource.SurfaceBand`: a column's ground layers from the shaped heights, nothing
  generated); `BandStreamer`/`BandStreamPolicy`/`BandPlan` (each column holds the hull of its 3 x 3 neighbours' bands plus a layer each
  way, so band chunks have their 26 neighbours; everything under the player within 4 chunks, 6 underground; nearest first; eviction
  beyond keep plus a column and a layer); `StreamedWorld`/`GridResidency`/`Scene.Streaming` (the one place a host installs and evicts;
  duplicates dropped; changed chunks kept); `World/Far` (`FarTerrainSampler`, `FarSample`: height, water, top block, snow, forest from the
  pure model or a region's chunks); `BodyBarrier` (the edge wall: `VoxelGrid.BlocksBody` only, synced to the pass each tick, at session
  start and after a load); `PristineChunks` (the guide's snapshot reads non-resident chunks as generated without installing them).
- Game: `Streaming/JobPool` (at most 2 workers for residency and far tiles; inline mode); `ChunkResidency` (bands on workers, cached;
  columns generated on workers or inline in a 3 ms budget; installs and evictions on the loop only, tested; caps; `Settle`);
  `StreamedWorldHost`; `ChunkViewSet` lists a streamed chunk only with its 26 neighbours resident, capped; far plane
  `FarPlaneFor(view, 384)`; the far view (`FarViewSettings` 32 x 32-cell tiles, levels 4/8/16/32/64 blocks a cell; `FarTileSelector`
  quadtree; `FarTileMesher` with slope shading, block colours, flat sea, snow, forest tint, skirts; `FarView` cache with built
  ancestors standing in; `FarTerrainRenderer` after the sky with its own projection, then a depth clear; one shared haze); launch options
  `--world streamed|valley` (default streamed; `sample` can join), `--prefetch 0..2`, `--far-view off|blocks`, `--stream-stats`; presets
  `FarRange` 4,096/8,192/16,384 (initial guesses); scripted frames settle first; residency counts only on a `stream:` line.
- Tests: Core 31 (bands, streamer, pristine chunks, far sampler, barrier, a slow time test); Game 59 (job pool, residency, view set,
  tile selector, mesher, far view, host, launch options); four residency/worker configurations give identical digests and saves.
- Measurements (Debug, one thread, not the GPU): band per column 1.17 ms; a column's band plus a layer each side 1.72 ms; install plus
  evict 5 us; a plan at radius 28 1.1 ms; far sample 17 us; a far tile (1,221 vertices, 2,304 triangles) 22 to 26 ms; 16 km: 491 tiles,
  about 1.0M triangles, about 11 s of Debug tile-build CPU on 2 workers; far sampler against the generated surface: height within 3
  blocks in 119 of 120 columns, water and top block 120 of 120; a cold guide snapshot outside the valley 1.3 to 2.1 s (nothing installed).
- Phase B (when windows are allowed): merge `feature/biomes` once B02b round 2 lands; scripted runs before (on 660586a9) and after
  (`--world valley` exact; streamed re-baselined); measurements at each preset with `--stream-stats` (target 60 fps at High, 16 km); set
  presets (candidates if short: a 128-block level, a faster climate sample, a smaller inner fraction); pictures in
  artifacts/b-series/b07/; full suite; an addendum.
- Not yet tried on the card: the stand-in tiles' depth bias, the voxel-over-far seam, the grey view not tinting far land. Far sampling
  could be about 4x faster with a single-lattice climate path (World/Climate, B02b's). A cold guide snapshot outside the valley still
  takes 1 to 2 s (`WorldWindowTests` pin the window).
- Outside the area: `Save/SaveGame.cs` (one line), `Session/VoxelSession.cs`/`.Pass.cs`, `Npc/WorldFacts.cs`, `World/Scene.cs`,
  `World/WorldFactory.cs`, `ProceduralChunkSource.cs` (one method); test helpers `RegionTestWorld.cs`, `ProceduralWorldPurityTests.cs`.
- Process slip: a Monitor wait loop with `pgrep -f`, stopped at once.
