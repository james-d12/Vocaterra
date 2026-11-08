T10 report (Sonnet 5.5 medium, 2026-11-08). Branch T10-heap (local) at c6274cfe from e71846be; worktree clean; tests only.
Measured (temporary xUnit hook, removed): live heap 400 MB -> 1.5-1.67 GB and held; unforced peak 2955 MB of the 3 GB cap; process 3.9 GB.
Cause 1: TestSupport/RouteScene.cs cached one built scene per seed for the run (~10 seeds x ~110 MB = ~1.1 GB). Cause 2:
Activities/TeacherLoopTests' Rig started a turn worker and never disposed it, keeping each test's session and scene (~30 MB a test;
the class alone 194 -> 1122 MB over 29 tests).
Fix: RouteScene keeps seed 42 for the run and the last 2 other seeds (rebuild ~0.6 s; no wall-time cost seen); Rig is IDisposable,
`using var rig` everywhere; RouteSceneCacheTests (2 tests: at most 2 other seeds cached; separate copies per call); csproj comment 3 GB.
Cap, threads and limited.sh unchanged; no assertion weakened.
After: live peak 563 MB (was 1.67 GB); unforced peak 1065 MB, heap 1191 MB (~1.9 GB under the cap); process 2.1 GB (was 3.9 GB).
Proof: three Game.Core runs 2873 passed each; scripts/test.sh full with models 10363 passed, 3 skipped, 0 failed (logs /tmp/claude-1000/t10/).
Still heavy: ~110 MB scene clone per concurrent test; speech-model tests in the ~400 MB baseline.
