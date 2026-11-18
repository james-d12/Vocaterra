# B04b report (Sonnet 5.5, medium; 2026-11-15)

Branch `B04b-game-consumers`, head `58a4f710` (one commit on 47f51d07). Agent's suite: 11,660 passed, 3 skipped (10 new). Not yet gated:
it conflicts with B05 in DenseGridMembersAllowListTests (both removed entries and changed the reason pattern); the agent was resuming
to merge feature/biomes when the owner stopped the work, and its half-done merge was aborted (the branch is back at 58a4f710).

- Built: renderer, ChunkVersionTracker and ChunkViewSet keyed by VoxelChunkCoord, reading only through TryGetChunk, PeekBlock and
  Limits (GridBlockSource peeks; a non-resident chunk reads as air); tracker and renderer are IChunkResidencyListeners (forget and
  release on eviction); the view set rebuilds on eye chunk, EditCount and ResidencyVersion; `Due(IEnumerable<VoxelChunkCoord>)` for
  streamed grids. Mesher floor reads Limits.MinY. `ChunkLightIndex` replaces FindLights (by chunk column, updated on installs,
  evictions and block changes; same order, bit-identical glow; `ChunksReachedBy` marks chunks due). ColumnCover sparse by column.
  Allow-list: the four B04b entries gone; VoxelGame.cs ComputeDigestHex left with reason B05 (B05 has since removed that use).
  VoxelGrid.AddListener/RemoveListener made public.
- Tests: KeyedChunkConsumersTests (10): light index equals the dense scan; peeking meshes vertex-identical within 80 blocks of the
  spawn; view set equals the dense list; rain cover equals HighestBlock; streamed installs/evictions; late neighbour; a placed and
  removed post torch; lantern chunk install/evict; rain cover never generates.
- Scripted runs: 31 of 32 identical (including r01); r17-speak one voice-timing line, which did not reproduce in two re-captures.
- Frame time (r01, loaded machine): scene draw 2.8 ms before, 3.0 after; frame 3.0 / 3.2 (noise).
- Finding for B12: the player's placed torch (`antorcha`, BlockId.Torch) is not a light kind (only Lantern and PostTorch glow), so "a
  placed torch lights a cave" needs Torch made a light, which changes r01's picture.
- Not fixed: no caps on chunks, meshes or tiles (B07); the light index misses FillColumn writes made after it is built (only the scene
  build uses FillColumn); a non-resident neighbour meshes as air until it installs (B07's 26-neighbour gating).
