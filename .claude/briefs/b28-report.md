B28 report (agent a8c1efedc45e3ae95, Sonnet 5.5 medium, 2026-11-17 about 21:00 UTC)

Branch `B28-test-speed`, head `14be3b56` (87952df5 code, 14be3b56 docs), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b28`.
Split (shared fields and helpers in a `<Name>Base`; every test and assertion kept): `StreamedWorldHostTests` into nine classes (each test
its own GUID folder), `FarViewTests` into six, `LodStoreTests`, `TerrainTuningTests`, `HeadwaterTests`, `ProceduralChunkSourceTests`,
`DrainageNetworkTests`, `UndergroundWorldTests` and Game.Tests' `ChunkResidencyTests` into two to four each. Game.Core.Tests threads 3 to 6
(Game.Tests unlimited, left); the full run's peak memory 5.5 GB. NamePlatesTests' slow test starts two game processes one after the other;
now side by side (411 to 346 s, loads differed). About 35 classes over a minute marked Slow: `scripts/test.sh fast` 187 s for 11,830 tests.
Suite at load 8 to 13, before / after: Game.Tests 38.6 / 19.5 m; Game.Core.Tests 34.3 / 31.5 m (bound by single tests over 5 minutes, e.g.
the chunk-order tests and `LodStoreRingEditRaceTests`); whole run 42.3 / 31.5 m. 12,567 passed, 1 failed (the core lexicon wall-clock
test, passing alone); no test added or removed. Next levers: optimised builds under test (the owner's item 50 A), cheaper samples in the
longest single tests. docs/process/test-performance-plan.md has a T11 section.
