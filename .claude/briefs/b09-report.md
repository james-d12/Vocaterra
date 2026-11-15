# B09 report (Sonnet 5.5, medium; 2026-11-15)

Branch `B09-kind-masks`, head `3bfad532` (88b3d020 UInt128 masks; 3bfad532 accumulator, guard, above-63 tests, README). Agent's suite:
11,885 passed, 0 failed, 3 skipped (6 new); build 0 warnings; format clean; `SceneTests.GoldenDigest` unmoved.

- Representation: `UInt128` everywhere (`BlockKinds.Capacity = 128`, `BlockKinds.Bit(BlockId)`, `Bit(int)`). The two 4,096-block loops
  (the mesher and `VoxelChunk.KindMask`) use `BlockKinds.Accumulator` (two `ulong` halves): `UInt128` operators there cost about 20% of
  Debug meshing time.
- Changed type: Game.Core `Voxel/BlockKinds.cs`, `VoxelChunk.cs`, `VoxelGrid.cs`, `Npc/WorldFacts.cs`, `World/Scene.cs`,
  `World/Regions/TemplateBlockIndex.cs` (type and `new int[64]` sizes), `Language/Words/WordColours.cs`, `GreyscaleWorld.cs`; Game
  `Graphics/World/ChunkMesher.cs`, `ChunkLightIndex.cs`, `ChunkVersionTracker.cs`, `VoxelRenderer.cs`; tests `LookTests.cs`,
  `GreyscaleTests.cs` (type only); Game.Core README "The kind mask (B09)".
- Tests: `Voxel/KindMaskWidthTests.cs` (5: the guard fails at any `BlockId` >= 128; ids 64, 100, 127 for distinctness, union, any-of,
  order, the accumulator, a chunk's mask, `BlocksOfKind`) and one in `GreyscaleTests` (`KindBit`, `IsGrey` above 63).
- Measurements (Debug, one thread, the valley's 3,536 non-empty chunks, best of 5): meshing about 4,250 to 4,370 ms before, 4,270 to
  4,420 after (UInt128 alone: 5,050 to 5,270); `ChunkLightIndex` build 1.2 to 1.3 ms both; `VoxelChunk` object 88 B to 96 B (alignment).
- Known-not-fixed: the mesher's tables are indexed by block id and throw for ids with no block, so mesher coverage above 63 is the mask
  helpers only; B10's real blocks will exercise it.
- Process slip: the agent started a `pgrep -f` wait loop against the brief and stopped it at once; no effect on builds or tests.
