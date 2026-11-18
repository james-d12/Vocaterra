# B10 report (Sonnet 5.5, medium; 2026-11-15)

Branch `B10-underground-blocks`, head `79479428` (three commits on d6bacf4c). Agent's suite: 11,945 passed, 0 failed, 3 skipped (72 new
in `UndergroundBlockTests` and `UndergroundBlockMeshTests`); build 0 warnings; format clean; validate passes; GoldenDigest unchanged.

- Blocks (after Counter 56): Slate 57, Granite 58, Limestone 59, Gravel 60, Lava 61, Bedrock 62, IronOre 63, TinOre 64, CopperOre 65.
  Rock and ores `Material.Stone`, gravel `Soil`; all solid, not gatherable, no drop, item or noun, except lava (`Solid=false`,
  `Liquid=true`, `StopsBody`, `Glows`, no harm). New `BlockDefinition` flags `StopsBody`, `Glows`, `Indestructible` (bedrock), helpers
  `StopsBodyOf`, `IsLight`, `IsIndestructible`.
- Lava: `VoxelGrid.BlocksBody`, SleepWalk's and the mesher's copies honour `StopsBody` (stopped from the side, lands on top from
  above); not solid for sight (`LineOfSight`); `ChunkLightIndex` builds its light kinds from `Glows` (Lantern, PostTorch, Lava); drawn
  as an opaque cube with its own tile.
- Bedrock: `VoxelChunk.Set` and `FillColumn` refuse to change a bedrock cell (every edit path ends there; chunk installs are the only
  writer); `SetBlock` returns false on a refused write.
- Art: nine `BlockCells` drawn in code in `BlockArtNature`, before `IconBowl` (atlas mips); `ColourRows` 13 to 14; grey copies; the
  mesher's `KindFlags` table sized `BlockKinds.Capacity`.
- Fixed to fit the new blocks: `Footsteps.SurfaceOf` (threw for unknown blocks), `SoundEventMapper` materials, stale limits in
  VoxelGridTests, GreyscaleTests, UndergrowthTests, VillageTests.
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b10/tiles.png (8x, stone, cobble, water and the nine) with index.md.
- Known-not-fixed: lava collides like a solid block, so a body can stand on a lava surface (a two-high collider would hover a body
  over a lava lake; the owner may decide); lava is sun-shaded, not self-lit (its glow lights neighbours); the new cells shift atlas
  positions and top mips slightly (no test pins it).
- Outside the area: content/schema/nouns.schema.json (its block-name list must equal `BlockDefinition`; nine names added).
