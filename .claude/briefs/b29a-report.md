# B29a report (agent, 2026-11-18, about 14:00 UTC)

Branch `B29a-throughput`, head `c6b416a8`, four commits on `aac46848`, worktree `.claude/worktrees/b29a` (clean). Scratch branches removed.

1. **Release against Debug** (full suite with fuzz, 12,788 tests, models present; both passed 12,788 with 9 skipped):
   Game.Core.Tests 26.4 m / 332 s; Game.Tests 17.6 m / 202 s; Nlp.Tests 241 / 199 s; Fuzzing 127 / 119 s; WorldPreview 53 / 25 s;
   Golden 19.6 / 11.3 s. The run 26.4 m / 532 s (8.9 m): **Release 3.0 times faster**. Every golden held (18 procedural, SceneTests.GoldenDigest,
   17 end-to-end, the fixture). Peak 5.6 GB Release, 5.7 GB Debug. Load: Debug from 0.6; Release from about 9 (its own build). Slowest Release
   classes: StreamedWorldHostFarBuildTests 167 s, OpenFallbackMeasurementTests 167 s, StreamedWorldHostSaveEditTests 158 s,
   StreamedWorldHostOtherSaveTests 155 s, LodStoreRingEditRaceTests 139 s, TerrainTuningCoastPoolsTests 138 s, TerrainTuningRiverLinesTests 131 s,
   LodRingTests 123 s; none over 5 minutes (top 20 in T12). **Switched** test.sh, merge-gate.sh and CI's build-and-test to Release;
   `VOCATERRA_CONFIG=Debug` restores Debug. Nothing needs Debug (no `#if DEBUG`, `Debug.Assert` or `[Conditional]`).
   Budgets old to new (Release cost): ProceduralChunkTime surface 14 to 2.2 ms (1.10), stone 13 to 2 (0.98), deep 13 to 2 (0.97), column 41
   to 6.2 (3.1); StreamedWorldTime band 13 to 2.6 (1.25), made 23 to 3.7 (1.81), per chunk 6 to 0.9 (0.43), plan 2 to 0.8 (0.4);
   PristineSnapshot 5,900 to 910 (451); SceneBuild 7,300 to 1,450 (720); ValleyUnderground 1,000 to 1,060 (526); GeneratedLexiconLoad
   1,082 to 1,280 (592 to 639 alone). Three wall-clock asserts left for B29b: ProceduralTimingTests, RegionTimeTests, ChunkIndexTests.
   CI estimate 15 to 20 min of tests plus build (was 43 min Debug).
2. **One slot:** merge-gate.sh re-runs itself under limited.sh after the cheap checks; gate peak 6.5 GB.
3. **Re-run:** TRX under `.claude/gate-logs/trx-<stamp>/`, `scripts/gate-tests.py` lists failures; each re-run alone, `FLAKY`/`FAILED`; cap 20;
   FLAKY list in the summary and `.claude/gate-logs/flaky-<stamp>.txt`. Tested on a throwaway branch (fails-once, always-fails, theory row):
   FLAKY, FAILED, FAILED. Fixed: TRX assembly names are lower case.
4. **Modes:** fast 12,001 passed / 6 skipped, 93 s (187 s before); full 12,631 / 9, 421 s; timing 16, about 45 s; ci everything (12,788);
   project without timing and fuzz. `Category=Timing` on nine classes, `Category=Fuzz` on the fuzz assembly. CI runs unfiltered.
5. **Time report:** per-assembly time and slowest class against the newest `timing-*.json`; warns on +10% or a class over 5 minutes.
7. **Dry run** (quiet machine): new gate 779 s (13.0 m) PASS (build 41 s, suite 10.0 m, timing 51 s, the rest 1.7 m); the old script on
   the same tree 1,705 s (28.4 m). **The first gate after B29a runs the target's old script**: merge B29a first, then run the gate.

Replacement texts (applied by the orchestrator): CLAUDE.md Merge gate, Build/test/format, CI bullets; orchestrator.md Test tiers and
milestone-loop step 4; milestone-briefs.md lines 24 to 26 and 98. See the commit that applies them.

Left: the 1 ms budgets are at 2× (CI factor 4 covers CI); a new timing class must carry `Category=Timing`; an OOM kill now ends the whole
gate. Extra files: `scripts/gate-tests.py`, the TestSpeed constants, FuzzOptions' assembly trait.
