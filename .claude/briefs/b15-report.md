# B15 report (Sonnet 5.5, high; 2026-11-15)

Branch `B15-towns`, head `65db5475` (on 06e018a5: a01557b6, f695d5ab, 65db5475); then a merge of feature/biomes for a README conflict.
Agent's suite: 11,897 passed, 1 failed (its own new preview test, fixed; the preview project re-run 24/24), 3 skipped; 19 new tests;
build 0 warnings; format clean; validate passes; `SceneTests.GoldenDigest` unchanged.

- `IGroundShape` (`World/Generation/GroundShape.cs`) in `TerrainGenerator`: `HandDrawnGround` (the old relief code moved unchanged) and
  `ProceduralGround` (the world's height at the layout's world column, shifted by the placement's floor; no noise). `SceneBuilder.Build`
  has an internal overload taking the strategy.
- `RegionPlacement` (chunk x, layer, z; `ToWorld`, `ToLocal`); `RegionPoint` (name, local and world block).
- `ProceduralReliefRegion : IRegion`: `Create(id, layout, placement, data, seed)` builds the layout on `ProceduralGround` and freezes it
  through `AuthoredReliefRegion.Freeze` (now with a first layer); bounds from the layout at the placement; `Fit` picks the layer;
  `Points`, `Point(name)`, `Origin`, `Seed`, `TemplateDigest`, `Blocks`, `EdgeHeight`, `SmoothedEdgeHeight`; ground levelled only by the
  layout's sites and paths; refuses a river, lake or waterfall in the layout (B16), ground under the sea or outside [4, ceiling], a
  water level above 4. `RegionCatalog`, `RegionBlend` and `CompositeChunkSource` unchanged.
- `WorldFactory.Create` takes `towns` (streamed worlds only, validated, seed must match); template digests folded only when towns exist.
- Parser: `terrain.relief` optional (flat at `valley` without it).
- Sample town: tests/Vocaterra.Game.Core.Tests/Fixtures/sample-town.json (96 x 96 x 96, no words: two houses, a square, two lamps, a
  fence, a road). Preview tool: `--region <layout> --region-x --region-z [--region-layer]`.
- For B16: towns from world.json (id, layout file, chunk x/z, layer), each town's digest listed separately, water in a layout, the
  surface block from the biome, the exit corridor (anchored at the valley) not read by `ProceduralGround`.
- Measurements (Debug): a 96-cube town builds, freezes and profiles in 41 ms warm; border columns equal the world's ground (within 1)
  384/384 at chunks (79,6), (56,-25), (98,30); largest step 2, 1, 6 (bound 9); 324 of 9,216 columns levelled.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b15/ (index.md): 512-block heights and biomes round the town on seed 42, a
  slice, a hillier town, the free world.
- Known-not-fixed: a town on mountains is refused when its ground passes the layout's ceiling; a river bed is copied (only ground under
  the sea is refused); towns only in streamed worlds.
