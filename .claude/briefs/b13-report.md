# B13 report (Sonnet 5.5, medium; 2026-11-16)

Branch `B13-valley-underground`, head `3599eb94` (four commits on d26c7a45). Agent's suite: 12,100 passed, 0 failed, 3 skipped (12 new);
build 0 warnings; format clean; validate passes.

- `World/Generation/ValleyUnderground.cs`: `SceneBuilder.Build(layout, seed, ground, underground)` lays B11's passes (strata, caves, lava,
  ores; same fields and seed) into the valley's stone after the scene is complete; the two-argument `Build` stays plain stone.
  `TemplateBlockIndex.Scan` runs before the underground. `WorldFactory` passes `ValleyUnderground.Create(data.Underground, seed)`; chunks
  computed in parallel and written back with `VoxelGrid.ReplaceBlocks` (not an edit).
- Reach: only plain stone changes, at or under a per-column reach (sliding-window minima; cube distances). Margins in a `valley` section
  of underground.json (schema, `ValleySpec`, validator checking the named site, paths and props exist): 8 from air, 16 from water, 12
  columns from the edge; sites, points, NPCs and spawn 12; paths 10; props 10; the village 16; the pass 24 (`pass.rise`, `pass.floor`,
  `pass`, `rockfall`). No entrance in the valley. Seam: `caves.underRegionCeiling` -9 to -1, so the caves under the valley meet the
  valley's own from the same field.
- Tests: `ValleyUndergroundTests` (12) with an exact 3D distance transform: nearest changed block 9.0 from air, 17.0 from water; none in
  any margin; only plain stone changed, no exposed face, every column top equal; 1,537,377 changed blocks (538,543 cave air); seam 0
  mismatches over 16,018 open columns; bounded equals streamed; repeatable; timed. VillageTests, PassTests, PassLockTests unchanged.
- Pins: `SceneTests.GoldenDigest` b189ee0a6783 to 83af36d375d7 (the one valley golden move); bundle hash to 1ddef7455677; save-v9.json
  state digest d8b772c9fd59 to e770d6d4e81a; the template digest moves (not pinned). Unmoved: atmosphere pins, procedural goldens,
  end-to-end goldens. Tests fitted: `UndergroundUnderRegionTests` (checks above the data's ceiling), `ChunkMesherTests` skippable buried
  chunks > 2,000 to > 1,500 (caves open about 400 buried chunks; face-count bounds hold).
- Build time: Debug 843 to 1,047 ms; Release 151 to 227 ms (+76 ms: reach about 65 ms Debug, parallel chunk work 130, write-back 20).
- Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b13/ (before/ and after/, five slices: the village at z 218, the lake, the
  north peaks, the pass and rockfall, the camp ledge and waterfall).
- Known-not-fixed: cube distances slightly conservative at corners; the reach assumes the only hollows are props and water falls (the
  distance test would catch it); water distance 16 (B11's clearance; the brief said 8); cells between the 12-column strip and the
  procedural land left plain; no lava in the valley (level -54); more mesh faces at buried cave walls (not run in the game).
- Outside the area: `Voxel/VoxelGrid.cs` (`ReplaceBlocks`), `Content/GameDataValidator.cs`, `World/WorldFactory.cs`, Game.Tests csproj.
