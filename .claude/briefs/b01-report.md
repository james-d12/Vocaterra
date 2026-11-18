# B01 report (Sonnet 5.5, high; 2026-11-15)

Branch `B01-noise-climate-biomes`, head `40cfff0c` (3e0813ca noise, 32453379 climate/height/biomes/data, 40cfff0c allow-list, docs).
Gated at `94bea060` on `feature/biomes`: 11,597 passed, 3 skipped (98 new).

- Built: `World/Noise/` (TerrainNoise moved unchanged, GradientNoise 2D/3D quintic, NoiseStack Fbm/Ridged/Warp, Spline from data,
  LatticeSampler2/3, FieldScale); `World/Generation/Terracing.cs` (the valley's Terrace, shared unchanged); `World/Climate/`
  (ClimateSample, IClimateSampler with At, TemperatureAt and Fill, ClimateSampler, ClimateLapse); `World/Generation/Procedural/`
  (IHeightModel, ColumnShape with Height, NaturalHeight, Water, WaterLevel, RiverWeight, LakeWeight, HeightModel); `World/Biomes/`
  (BiomeRow, BiomeData, BiomeTable with Classify and Surface, BiomeValidator, TreeStrategies); WorldGenData and WorldGenValidator;
  `game/world.json`, `game/biomes.json` and their schemas, loaded into GameData.WorldGen and GameData.Biomes.
- Tests: NoBiomeIdsInSourceTests, ProceduralWorldPurityTests (readonly fields, no mutable statics, token ban), and noise, spline,
  lattice, climate, height, biome, world-data, world-shape and timing tests.
- Measurements: 256 x 256 columns of climate + height + biome in 120 ms Debug, 60 ms Release (climate fill 25 / 10.5 ms); one chunk
  column 0.49 / 0.46 ms; one ClimateSampler.At about 12 µs (use Fill).
- Fixed: NoLiteralTextTests (the validators' English problem messages, added to its validator allow-list); biome ids `river`, `lake`
  (and `forest`) clashing with src literals, renamed; cliffs of 14 to 15 blocks (ridges noise to 1024 cells, 2 octaves; squared river
  carve, width 40, fade 60 to 84), held to 9 by a test over 3 seeds.
- Design choices: lakes in the height model; spline Catmull-Rom clamped, points [input, output] in thousandths; fields fixed point
  -4096..4096 (ridges 0..4096; river signed); biome ids river-channel, lake-basin, ocean, beach, jagged-peaks, stony-peaks,
  snowy-slopes, grove, meadow, taiga, plains, birchwood, dense-woodland, woodland (catch-all); border dither +-50 thousandths per column.
  Seed 42 over a 24,000-block square: water 17%, rivers 2.5%, heights >= 190 about 2%, the tallest 298, every biome present, lakes 0.4%.
- Not fixed: peaks not faded over the sea (islands on rugged coasts); terrace and crag risers up to 8 to 9 blocks.
- Outside the area: SceneLayoutParser.BlockNamed made internal; TerrainGenerator calls Terracing; GameData's two parameters;
  provenance.json rows; Game.Core README and architecture.md lines.
