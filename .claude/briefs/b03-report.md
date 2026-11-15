# B03 report (Sonnet 5.5, high; 2026-11-15)

Branch `B03-generation-pipeline`, head `74758cbf` (fed6b09a World/Streaming, 37c19ace pipeline/registry/source/tests, 74758cbf
allow-list). Gated at `3b0d7ef6`: 11,732 passed, 3 skipped (82 new in the agent's run).

- Built: `World/Streaming/` (`IChunkSource.Generate(VoxelChunkCoord)`, immutable `ChunkData` in VoxelChunk order with
  `WaterLevel.Encoded` levels, air holds no array); `World/Generation/Procedural/`: `GenerationPipeline.Standard` with column shapers
  (`gen/relief`, `gen/banks`, `gen/waterside`) and chunk decorators (`gen/surface`, `gen/water`, `gen/stone` to y -64, `gen/trees`,
  `gen/undergrowth`), `ColumnContext`, `ChunkBuilder`, `GenerationWorld`; `StrategyRegistry` (surface and decoration strategies, species,
  crowns; Default plain, undergrowth, oak/spruce/birch, round/small-round/narrow/cone; With* return new registries; `TreeStrategies`
  removed); `BiomeRow.SurfaceStrategy`/`DecorStrategy` (`surfaceStrategy`, `decorStrategy` in biomes.json and its schema); fade by
  Chebyshev distance to another biome; `ProceduralChunkSource` (pure; `Generate`, `GenerateColumn` for 24 layers with one window;
  climate by `Fill`; all-air and all-stone shortcuts). Window margin 4 with the shipped data (tested against a window 6 wider).
- Tests (about 82): ChunkData, pass order and names, chunk locality (4 places x 3 seeds, shuffled, wider window, column, shortcuts,
  threads), trees, water at rest and exitable edges over coast, river and lake of 3 seeds, no border step over 9, the registry with a
  biome, strategies, species and crown only in test data, 18 goldens (3 seeds x river, lake, coast, forest, mountain, plain), timing.
- Measurements: surface chunk median 1.3 ms (worst 1.6); all-stone 1.1 ms; air 1.1 ms (the window); a 24-layer column 1.6 ms median
  (worst 2.8); a surface ChunkData 8 KB; non-empty chunks per column: plains 7.8, river/lake 7.0, coast 7.2, forest 8.0, range 19.2.
- Fixed: 31 of 144 sites failed the exitable-edge scan (trunks in the bank column beside water): no trunk within one column of water;
  `BankCutReach` 12 (the valley's cut 4); a bank beside swimmable water at most 1 above the surface. `UnexitableEdges` treats a floor
  of leaves as a crown over the water, like a bridge (valley unchanged). A golden finder chose a speckled border; mutable primary-ctor
  captures; NoLiteralTextTests entry for the registry's author-facing error.
- Design choices: the biome is chosen on the height model's ground before strategies and banks; hash-priority competitors ignore
  viability (density a little under the row's percent); the deep-water bank cap is stricter than the valley's.
- Not fixed: the window is recomputed per Generate (use GenerateColumn); no reeds or lookout rules, no tree slope rule; a strategy that
  lowers a column under the sea keeps its dry biome; the shipped rows name no strategy; leaves over a lake can bump a swimmer.
