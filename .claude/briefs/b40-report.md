# B40 report (the small ones)

Branch `B40-small-ones`, head `25665a0b` (feature/world-next at 73f55d0e merged). Agent's gate: build 0 warnings; full 12,674 passed,
0 failed, 9 skipped; timing 23 passed; format clean; validate 0 errors. No pin, golden or limit changed.

0. **Bench group rows** (`7b6149b7`): tiered JIT. `Warm()` ran `GenerateColumn` only twice, so the group, footprint and residency paths ran
   at tier 0. `Warm()` now runs every row's path for 2.5 s; `--quick` gives 0.81 and 7.7 ms for the group rows, the full run 0.74 and 6.9 ms.
1. **Allocation:** `ColumnScratch` (Core, plain arrays) and `ColumnScratchPool` (Game, instance-owned): a column cut from a group takes its
   8 window arrays (about 170 KB) from the caller's scratch; new overloads `GenerateLayers(..., group, scratch)` and `CreateScratch()`;
   `ChunkResidency.Generate` and `LodRing.MakeColumns` rent and return in try/finally. No static or `[ThreadStatic]`. `ColumnScratchTests`:
   dirty reused scratch equals fresh arrays byte for byte. At 8 workers: bytes per column 353,400 to 187,400 (-47%); gen-0 per second 105 to
   58; columns per second 5,143 to 5,606 (+9%). FarScope left alone (B37 not merged). `DOTNET_GCgen0size=0x6000000` (96 MB, environment
   variable only; the runtimeconfig keys showed no clear effect) cut gen-0 to 23 a second and gave about +20% columns per second: not
   shipped, the owner's choice. Server GC untouched.
2. **Worker priority:** `BelowNormal` does nothing on Linux (both read the inherited nice). A thread cannot lower its nice value, so
   `WorkerPriority.LowerCurrentThread()` (libc `gettid`, `getpriority`, `setpriority`; Linux only) raises it by 7, capped at 19; `JobPool.Work`
   calls it at thread start (nice 7 from a normal start, 17 under limited.sh). `WorkerPriorityTests` reads /proc/self/task. Not read on a
   live game run.
3. **Drainage:** a landmass is read under a lock keyed by its start node (a `ConcurrentDictionary` of lock objects); the old `_build` lock is
   a short publish step; two threads starting from different nodes of one landmass both read it (pure) and the first publishes. New warm
   rows (`WarmRow`, `WarmStep`, `HeightModel.WarmRow`, `FarTerrainSampler.WarmDrainageRow`); `FarView.WarmDrainage` submits one job per row on
   `JobLane.Ring` over the far range at the first update and after the eye moves a quarter of the range. `DrainageNetworkWarmTests`:
   identical shapes forward, reverse, on 6 threads in both orders, and on demand. The purity test's lock allowance comment updated (test
   code unchanged). First-read and teleport times not measured (needs a game run).
4. **HighestBlock:** `IChunkGenerator.HighestPossibleLayer` (default interface method); `CompositeChunkSource` answers from the surface band;
   `VoxelGrid.HighestBlock` skips non-resident layers above it; resident layers still count (edits found); cached per column, bounded.
   `HighestBlockFromTheBandTests` over 255 columns, seeds 42 and 2026, with blocks placed above and the top dug out. Bench: 19.7 to 4.8 ms
   per new column.

For running.md: bench `--workers <n>` runs the residency path (4x4 groups) on n threads for 4 s with allocated and caller-owned window
arrays and reports columns a second, bytes a column and gen-0 a second (default 0, not run); the bench warms every row's path for 2.5 s
first; new rows: HighestBlock of a new column, from the band and layer by layer. For architecture.md: ColumnScratch and ColumnScratchPool;
WorkerPriority; per-start-node landmass locks with a publish lock; FarView's drainage warm-up; `IChunkGenerator.HighestPossibleLayer`.
