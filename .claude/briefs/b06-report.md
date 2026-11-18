# B06 report (Sonnet 5.5, high; 2026-11-15)

Branch `B06-regions-streaming`, head `030388fd` (on db61ef0e: a06420f4, 16ece290, 80c328e3, 6922ba99, 030388fd). Agent's suite: 11,879
passed, 0 failed, 3 skipped (about 94 new); build 0 warnings; format clean; validate passes; `SceneTests.GoldenDigest` and the
atmosphere pins unmoved.

- Built (Game.Core `World/Regions`, `World/Streaming`, `World`): `IRegion`, `RegionBounds`, `RegionCatalog` (with `Validate`);
  `AuthoredReliefRegion` (the valley built by the unchanged `SceneBuilder`, frozen into immutable arrays; bounds from the layout, 24 x 8
  x 24 chunks; any chunk origin; edge profile, a blurred copy, template digest, `TemplateBlockIndex`); `RegionBlend`,
  `BlendedHeightModel`, `BlendedClimateSampler` (optional extras of `GenerationWorld` and `ProceduralChunkSource`);
  `CompositeChunkSource`; `WorldFactory.Create(data, seed, WorldMode.Bounded|Streamed, layout?)` with `Compose`, `StreamedGrid`,
  `InstallColumn` (a whole column from one window); `ChunkStreamer` with `StreamPolicy`; `IPlaceInfo`, `WorldQuery`, `Scene.Places`;
  `WorldWindow`. Program.cs calls `WorldFactory.Create(..., Bounded, layout)` in place of its one `SceneBuilder.Build`.
- Blend (new `regionBlend` section of world.json): margin 64 (smoothstep from the nearest edge point's height to the free land); rivers
  and lakes fade by the same weight; edge smoothing (the agent's addition: the target eases over 16 blocks from the exact edge to the
  edge blurred over 16 columns; without it ridges combed straight out from the border); trees none within 3 columns, thinning across
  the margin (40, 172, 341 per 32-block band against 706 beyond); land bias: continentalness floor from 0.100 at the edge to -0.120 at
  512 blocks (0 sea columns within 512 for seeds 42, 7, 2026, against 2,360, 5,842, 3,451 without); exit corridor (383,208) to
  (479,208), 7 wide, 10-block bank, last 32 blocks give way, at most one block of rise a block, road y 58 to 60 (grass, not Path).
- Streamer: a disc of columns plus layers, nearest first, ties by layer, row, column; evictable beyond keep + 1.
- `WorldFacts` window (the region inside it; else radius 128, 64 below, 64 above, in whole chunks), used by `VoxelSession`. `FireRings`
  and `LandRules` read `Scene.CurrentBlocksOfKind` (template plus changed chunks); `FireRings` tracks later placements with a listener.
  The dense-member allow-list is empty (the test stays, with a pattern test and the reason check tightened). The state header carries
  the template digest.
- Tests (about 94, World/Regions and WorldPreview.Tests): the streamed valley equals the bounded one chunk by chunk (4,608 chunks) with
  the same facts and a session on the streamed grid; one walk with the same edits under three policies gives the same state digest and
  save; 8 threads; a 96 x 64 x 96 region at chunk (40, -30) and the catalog validator.
- Measurements (Debug, one thread): the largest step across border, margin and corners 6 (seeds 42, 2026), 8 (seed 7), bound 9; 3 x 3
  columns beside the valley 17 ms (1.9 ms a column), a surface chunk 1.5 ms; ring walk (up 1, down 3) 192 of 192 start columns for
  seeds 42 and 2026, 181 of 192 for seed 7; scene build 791 ms (index scan 37 ms), bounded factory 874 ms, streamed 891 ms.
- Scripted runs: not run (no game windows). Expected moves: version, digest and bundle-hash fields (bundle 375729f3...).
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b06/ (index.md): heights and biomes 2,048 blocks round the valley with the
  footprint, margin and land-bias outlines, a 768-block close-up, the free world for comparison, 4 slices for seed 42. The preview tool
  has `--world valley`.
- Bugs fixed: `FireRings` lost camp stones restored from a save (the save applies edits before the session exists; it now scans the
  changed chunks); `LandRules` sized its mask by the grid (overflow on a streamed grid; now by the layout); the flat test scene needed a
  template.
- Known-not-fixed: seed 7 has a 100-block ridge east of the valley that blocks about 6% of ring start points at step-up 1 (margin 96
  gives 97%, 128 gives 99.5%; data, the owner's call); B02b may move the coast (`shoreFloor` -120 may need a nudge; the land-bias test
  says); on a streamed grid a guide snapshot outside the valley generates the window's missing chunks on the game thread (up to about
  2.6k chunks, a few seconds cold; B07's residency); `LandRules`, `SleepWalk` and similar stay valley-only.
- Outside the area (allowed zones): world.json and its schema, manifest.json, save-v9.json (bundle hash, state digest), the preview
  tool, Game.Core README, Program.cs (one line), `NoLiteralTextTests` (an entry for `RegionCatalog`'s author-facing problem list).
