# B07 phase B part 1 report (Sonnet 5.5, high; 2026-11-16, windows allowed)

Branch `B07-streamed-world`, head `5f047d16`. Fast tier 11,722 passed after all changes; format clean; full suite not re-run (part 2).

- Merge `67b01296` of feature/biomes at 4fc0a00f (B10, B10b, B11, B15, M96): `BlocksBody` keeps the barrier and B10b's lava rule;
  `WorldFactory` keeps B15's towns and the barrier. B11's real strata broke `SurfaceBandsTests` and `PristineChunksTests`: a layer above
  the band is all air (tested); the guide's world is the surface (region chunks plus each column's band) and the player's changes
  (`PristineChunks.Sees`), so residency never changes a guide answer. `GenerateLayers` on the procedural and composite sources decorates
  only the asked layers (each equal to the chunk alone; `GenerateLayersTests`): band plus a layer each side 3.5 ms against 6 ms a column.
- Seen on the RTX 3070 (about 20 shots in /tmp/claude-1000/b07-runs/shots/): far land to the horizon, the voxel-to-far boundary, depth,
  haze and sky at 16 km correct; no cracks, gaps or z-fighting seen. Not fixed: the far land a little more saturated than the voxel land;
  mild haze (fog from 35% of the range); far water, snow and forest not yet looked at closely; no grey-world tint on the far land.
- Behaviour: a walk east from (375, 208) stops at x 383.70 with the pass closed and reaches 400.05 with `--pass open`; teleporting outside
  with the pass closed reports no ground (the barrier is in `BlocksBody`); negative script coordinates wait for B14.
- Fixed: the plan now starts from the near columns' bands (radius 10) and grows (was about 3 s of nothing after a start or jump; tested);
  `--no-settle` to measure the live path; the view set rebuilt at most every 8 frames on a residency change alone; `--stream-stats` gives
  frame p50/p95/p99 and counts over 16.7 and 33.4 ms; stale column jobs and `FarTileKey.Parent` recursion fixed.
- Measurements (Debug, 1280 x 720, shared card; (450, 208), `--pass open`): settled median 4.8 ms (low, 224, 4,096), 5.5 (medium, 304,
  8,192), 8.9 (high, 400, 16,384); live walk of 250 blocks, 2 workers: p50/p95/p99 5.9/17.6/32.2 (low), 6.7/17.3/31.2 (medium),
  9.8/21.1/34.9 (high), frames over 33.4 ms 0.8 to 1.2%; at high the streaming update p99 5 ms (max 28), remesh p99 15 ms (max 78);
  17,254 resident chunks, 559 far tiles (10.4 MB, 1.14 M triangles, 10.3 s of worker CPU).
- Valley: all 32 `--world valley` runs against 4fc0a00f: 31 identical, r01-route's sounds.log the known timing noise.
- Known not fixed: `VoxelCollider.Collides` ignores the barrier for a lava cell outside the wall; settling a 16 km far view at High costs
  seconds on the first scripted frame; the longest frame (2.6 to 5 s) is a start or jump, where the simulation reads chunks residency has
  not made yet.
