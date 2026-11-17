B24 report (agent af25b90c9b9cf6fe1, Sonnet 5.5 medium, 2026-11-17 about 12:45 UTC)

Branch `B24-guards-cleanups`, head `12b3fc49` (one commit on a8560f35), worktree `.claude/worktrees/biomes/.claude/worktrees/b24`
(nested by the orchestrator's mistake; the brief named `.claude/worktrees/b24` under the main checkout). Build 0 warnings; full suite
12,529 passed, 0 failed, 5 skipped (12,531 less 9 removed with dead code plus 7 new); format clean; content valid; world unchanged.
- A1: `ProceduralWorldPurityTests` scans all of `World/Generation`, `World/Layout` and `World/Pass` (bans Thread, Task, lock,
  Interlocked, DateTime, float, double, Random in code); a new test fails for any `World/` folder neither pure nor named with a reason
  (only `Lod` today; `World/Drainage` must join when B02c merges: it holds one `lock`, its arithmetic is IntMath).
  `ArchitectureRulesTests` bans threads, Parallel, tasks, timers, file IO and environment reads in Game.Core, allow-list with reasons:
  TurnWorker, ValleyUnderground (B24a's two threads), SaveFile, LocaleBundle, GameBundle, SpeechEngine, BuildCommit, SoundMixer's log,
  SceneLayout.Load (dev options). No use needed fixing.
- A2: removed `ChunkStreamer` (with `StreamPolicy`), `VoxelGrid.ResidentCoordinates` and their tests (9); stale comments fixed.
- C8: a real bug: `Request` ran before `Evict` and at the cap set the cursor to the plan's end, so after a walk or jump the old place's
  chunks held the cap and the new place got nothing (0 resident at the new eye after `Settle` returned true). `Evict` now runs first and
  the cap waits for the plan's drops; documents describe an admission cap. Tests: a walk (fails without the fix), a cap lowered in flight.
- T1: B07f and B21 already covered most; added a persistence read during writes (3 reader threads against 1,500 rewrites, never torn).
- T2: timing tests on process CPU time (`CpuTime`, fastest of three, timing collection), budgets twice the measured cost: procedural chunk
  6.4 to 6.9 ms (14), all-stone 13, deep 13, column of 24 17.5 to 20.6 ms (42), underground ratio 4.95 (10, was 40); streamed column band
  5.2 to 5.6 ms (12), layers 21, per chunk 2.3 to 2.5 ms (5), plan 3; `PristineSnapshotTimeTests` 2.7 s (5.5 s; was 20 s wall). B07f's hang
  guards replaced by `SettleGuard`: two-minute slices, failing only on a slice with no progress (runaway cap 30 slices).
- Docs: architecture.md threads (JobPool, the turn worker, ValleyUnderground.Lay), comments. For the orchestrator: biomes-plan lines 152,
  160, 403, 533 and the B06 row name `ChunkStreamer` (now `BandStreamer`); 155 and 413 name `ChunkPrefetcher` (now `JobPool` lanes); the
  residency cap is an admission cap. Core: `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` needs the same CPU-time treatment.
