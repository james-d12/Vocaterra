# B11 report (Sonnet 5.5, high; 2026-11-16)

Branch `B11-underground-generation`, head `db82e1c9` (186d4d78, db82e1c9 on a9dae891); then a merge of feature/biomes for two conflicts.
Agent's suite: 12,054 passed, 0 failed, 3 skipped (about 90 new); build 0 warnings; format clean; validate passes; GoldenDigest unchanged.

- Data: content/game-v1/game/underground.json with content/schema/underground.schema.json, `UndergroundData`, `UndergroundValidator`;
  `GameData.Underground` nullable (a bundle without it gets plain stone); `ProceduralChunkSource` takes an optional `UndergroundData`.
- Types (World/Underground): `IStrataSelector`/`StrataTable` (stone to y 8, slate below 0, a hash dither between, granite and limestone
  blobs); `BedrockFloor` (1 to 4 ragged from y -64); `OreTable` (triangular spread, veins inside their chunk and band,
  `WorldRng.For(seed, "ore:"+id, cx, cz)` mixed with the layer, listed rock only); `ICaveField`/`CaveField` (cheese and spaghetti on the
  4 x 8 x 4 lattice; `LatticeGrid` skips impossible cells and fills certain ones; a chunk equals block-by-block); `EntranceMask`,
  `UndergroundWorld`. Passes after `gen/stone`: `gen/strata`, `gen/floor`, `gen/caves`, `gen/lava`, `gen/ores`, then trees and
  undergrowth; `CavePlan` once per chunk column.
- Water rule: a cave stays 3 blocks (cube) from any water cell and `bedClearance` 16 blocks under a water bed (data; the literal rule
  would forbid caves at any depth under water: the owner may want it infinite); caves stay 6 under the ground except at entrances.
- Entrances: only in mask zones on dry procedural land at least 4 over the sea, no water within 3 columns, never in a region or its
  64-block margin, only over a cave; a shaft to the highest cave air within 10 blocks under the cave top; no tree or plant in a zone.
- Under the valley: the procedural underground with caves capped at y -9; `CompositeChunkSource` gained `EntranceColumns`.
- Preview: `slice --mode blocks`, an `entrances` command. Docs: Game.Core README, content/README.md ("Editing the underground").
- Tests: World/Underground (data, strata, floor, ores, caves, water rule over rivers, lakes and coasts for seeds 42, 7, 2026 with 0
  violations, entrances, lava, under the valley), `NoOreIdsInSourceTests`, chunk locality and 8 threads, preview tests.
- Pins: `ProceduralGenerator.Version` 1 to 2; all 18 `ProceduralGoldenTests` re-recorded; `AtmosphereSimulationTests` PinnedFresh
  24e4820f..., PinnedAfterWalking 9b4cb0a9... (the version bump); save-v9.json bundle hash and state digest d8b772c9...; tests fitted:
  `CompositeChunkSourceTests`, `ProceduralChunkSourceTests`, `GenerationPipelineTests` (13 passes), purity, `NoLiteralTextTests`.
- Measurements (Debug, one thread): surface chunk median 1.41 to 2.13 ms; deep chunk 1.14 to 1.96 ms; a 24-layer column 1.8 to 6.4 ms;
  worst 8.7 ms a chunk, 16.1 ms a column. B07 and B12 should re-measure (Release is lower).
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b11/ (index.md): seed 42 slices through a range, under lakes, rolling land and
  the valley's edge, two entrance maps, a height map (no real sea within 4 km of the origin on seed 42 yet).
- Known-not-fixed: ragged mouths where the cave below is patchy; no gravel on cave floors or river beds; spaghetti "winding" tested
  loosely; the valley's own stone is B13's.
- Outside the area: `GenerationWorld` (optional `Regions`, `Underground`, margin), `ColumnContext`, `TreePass`, `UndergrowthPass`,
  `GenerationPipeline`, `ProceduralChunkSource`, `WorldFactory`, `GameData`/`GameDataValidator`, `ProceduralGenerator.cs`,
  content/game-v1/provenance.json and manifest.json.
