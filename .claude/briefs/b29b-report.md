# B29b report (agent, 2026-11-18, about 14:30 UTC)

Branch `B29b-wall-clock`, head `c8a3d272`, four commits on `8e372d3e` (architecture test, CPU budgets, HangGuard, import order); 59 files,
all under tests/.

**Speed checks to CPU budgets** (`tests/Vocaterra.Game.Core.Tests/Timing/SpeedChecksTimingTests.cs`, `Collection(Timing)`,
`Category=Timing`, `CpuTime.Fastest(3, ...)`, ×4 under CI; measured at load 12 to 17): ProceduralReliefRegion 563 to 978 ms, budget 2000 (was
5000 wall); GuideTurn 1.4 to 2.6 ms a turn, budget 6 (was 250); WorldFacts snapshot 16 to 36 ms, budget 80 (was 1500); WorldFacts six searches
under 0.01 ms, budget 2 (was 3000); WorldEditsSave 0.5 to 1.0 ms, budget 2.5 (was 2000); DrainageNetwork dry column under 0.05 ms, budget 1
(was 100); FarTerrainSampler 943 columns 34 to 46 ms, budget 100 (was 20 s). LintTests' "under five seconds" moved to
`Nlp.Tests/Content/LintTimeTests.cs`, 1103 ms fastest, budget 2300 (was 5000). Outcome checks stay in the source tests; three renamed (their
names promised speed); the save and sampler tests gained outcome asserts. Three costs are under the CPU clock's 10 ms resolution (mean of 200
to 2000 repeats; small fixed budgets).

**Waits to hang guards:** new `TestSupport/HangGuard.cs` (linked into Game.Tests): factor 4 under `CI=true` or `GITHUB_ACTIONS`, or
`VOCATERRA_TIMING_FACTOR`; `Scale`, `DeadlineAfter`, `Passed`, `Hung` ("hung after N s (a hang guard, not a speed check)"), `Settled`.
`StreamedWorldHostRingTests` (CI's failure on main) and every other `Settle`/`SettleNear` caller (58 call sites) pass 600 s × factor; `MeshingRig`,
`SettleGuard`, the `TickCount64` deadline loops and the `Wait*` calls are scaled. `StreamedWorldHostDisposeTests` (`took < 15_000`) and
`FileLodPersistenceTests` (`took < 1500` after `Shutdown(1 ms)`) became 60 s hang guards on a background thread; the shutdown test still
asserts the call returns without draining (`finished`, `IsWriterStopped`), only its 1.5 s bound is gone. No production default changed.

**Architecture test** `tests/Vocaterra.Game.Core.Tests/Architecture/WallClockRulesTests.cs`: scans every test source (literals and comments
stripped, timing files skipped) for a clock read outside `ClockReadAllowed` (10 entries with reasons), an Assert on a clock read not through
HangGuard, a wait with a literal limit not through HangGuard, and stale entries. Proof: a re-added Stopwatch assert in
ProceduralReliefRegionTests failed it, naming the file; reverted.

**Runs:** the moved sets 3 of 3 under load (a full Nlp.Tests run beside them, load 12 to 14) and once with `CI=true`; `scripts/test.sh full`
12,631 passed, 9 skipped, 10.1 min, 5.9 GB (before the import-order commit); `scripts/test.sh timing` 23 passed; Release build 0 warnings;
format clean.

**Left:** `ChunkIndexTests`, `RegionTimeTests`, `ProceduralTimingTests`, `ProceduralChunkTimeTests` (in the timing collection) still use a wall
Stopwatch, not CpuTime; read the next CI run to confirm the Ring test passes on the runner.
