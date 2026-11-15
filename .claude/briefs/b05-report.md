# B05 report (Sonnet 5.5, medium; 2026-11-15)

Branch `B05-saves-v9`, head `7d3d97b7` (5b6795ae work, 7d3d97b7 import order and a test). Gated at `707864aa`: 11,748 passed, 3 skipped.

- Digest: `World/WorldStateDigest.cs` and `WorldStateHeader.cs` (in World/, since Voxel/ must not depend on World/ or Content/). SHA-256
  of seed (u64), region template digest (32 bytes; zero until B06), `ProceduralGenerator.Version` (i32,
  `World/Generation/ProceduralGenerator.cs`), the generation-data hash (world.json, biomes.json, underground.json when present; append to
  `WorldStateHeader.GenerationFiles`), the changed-chunk count, then X, Y, Z and the chunk digest per changed chunk sorted by layer, row,
  column. `VoxelGrid.ChangedChunks()` (internal); `VoxelSimulation.Create(..., stateHeader)`; `VoxelSession` passes
  `WorldStateHeader.For(bundle, seed)`. `hostStateHash`, the save's check and VoxelGame's start-up line use it; `VoxelGrid.ComputeDigest`
  stays the dense test aid behind `SceneTests.GoldenDigest`. SceneBuilder calls `SealPristine()`.
- Saves v9: `ChunkEdit(X, Y, Z, Runs, Water)` sorted; WorldDims and SaveDocument.World gone; Current and Oldest 9; v8 refused.
  `VoxelGridDiff.Capture(grid)` / `Apply(target, edits)`; SaveGame and SaveService lost their pristine parameters. Restore: build as a new
  game, then Apply via SetBlock; a grid with an IChunkGenerator regenerates on first touch (tested on a streamed grid with edits at
  negative Y). B06 must pass the region template digest to `WorldStateHeader.For(bundle, seed, templateDigest)`.
- Tests: 15 new (round trip, undone edit, residency independence, eviction, sorting, header, v8 refused, first save < 8 MB).
- Pins: AtmosphereSimulationTests PinnedFresh a505b99e...7f99, PinnedAfterWalking 51f3083b...000d; save-v9.json stateDigest
  e1be1a7f...e7d5; end-to-end goldens and SceneTests.GoldenDigest unchanged.
- Scripted runs: only version and digest fields differ (compare-runs.py does not mask them); r01 sounds and r17-speak voice order are
  timing noise.
- Times: save 1.2 ms (first save < 8 MB), load about 56 ms, a 57 KB save for one edited chunk (before: first save 0.7 s and 38 MB).
- Allow-list: the five B05 entries removed. Outside the area: scripts/content-loop.sh message, docs/nlp/changing-the-core.md and
  content/README.md (fixture rename), docs/game saves/activities/content/village plans, Game.Core README, the SealPristine line.
