# B04a report (Sonnet 5.5, high; 2026-11-15)

Branch `B04a-sparse-grid`, head `871fce93` (bc280010, 17349b14, 871fce93), merged with feature/biomes at `10cca9d8` by the orchestrator
(architecture.md conflict with B01: both notes kept). Agent's full suite: 11,552 passed, 3 skipped (54 new).

- Built: `VoxelGrid` sparse by VoxelChunkCoord (air chunks hold no array, a last-chunk read cache, a slot index on bounded grids);
  bounded mode (the old constructor) and streamed mode `new VoxelGrid(WorldLimits, IChunkGenerator?)` (WorldLimits chunk-aligned;
  Default x/z +-30,000, y -64 to 320); outside the limits solid for collision, air for lookups, writes throw; dense members throw on a
  streamed grid. Residency (internal): InstallChunk, TryEvictChunk (unchanged only), ResidentCoordinates; installs move neither
  EditCount nor block-change listeners; ResidencyVersion; public IChunkResidencyListener; peeks TryGetChunk, IsResident, PeekBlock,
  ResidentChunkCount. Generate-on-read: `IChunkGenerator { ChunkContent? Generate(VoxelChunkCoord) }` owned by Voxel/. PristineDigest
  and IsChanged (lazy before the first write; an undone edit is unchanged); SealPristine() (nothing calls it yet). IVoxelReader and an
  internal IVoxelWriter; many readers converted (BlockRay, LineOfSight, GroundFinder, VoxelCollider, PlayerBody, water, WalkPlanner...).
  Negative y via Limits.MinY; "none" is MinY - 1 (-1 on bounded grids). WorldFacts keyed by coordinate (origin-only until B06).
- Tests: SparseVoxelGridTests, ChunkResidencyTests, DenseGridMembersAllowListTests (fails also when a listed use disappears).
- Scripted runs: 33 of 35 match. r01-route differs in sounds.log only (voice-end tick 2174 / 2131 / 1950 across three captures; two
  captures of the same code disagree, so load-dependent speech timing); r10 (order-only) one stdout line. Not proven by a second
  baseline capture.
- Measurements (Debug, B01 building alongside): scene build 612-625 ms before, 690-700 after (+10 to 12%); valley grid memory 36.8 MB
  before, 28.5 MB after; 27-neighbour reads over 196 x 8 chunks 5.6 s before, 3.3 s after.
- Allow-list: B05 Save/SaveGame.cs, SaveMigration.cs, VoxelGridDiff.cs, Simulation/VoxelSimulation.cs (ComputeDigest); B06
  Needs/FireRings.cs, Session/VoxelSession.cs, Land/LandRules.cs (BlocksOfKind); B04b ColumnCover, ChunkVersionTracker, ChunkViewSet,
  VoxelRenderer, VoxelGame.cs (ComputeDigestHex; the B04b brief moves it to B05).
- Fixed while building: WalkPlanner keyed by a dense int index (breaks for negative y, overflows on a 60,000-wide world); keyed by
  BlockPos now, same tie order.
- Not fixed: dense by-origin code stays valley-only (LandRules mask, RiverCurrents.Build, WorldFacts loops, SleepWalk); valley chunks
  count as changed until SealPristine is called (B05 decides); listeners fire during reads that generate (must not read recursively).
