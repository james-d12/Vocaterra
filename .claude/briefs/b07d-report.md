B07d report (agent a804fbfcef7ba51d8, Sonnet 5.5 high, 2026-11-17 about 16:50 UTC)

Branch `B07d-far-remainder`, head 538364a5 (db915f24 code and tests, 4b0291e5 docs, 538364a5 merge of 08bbfeb0 plus an fsync-timer fix),
worktree `.claude/worktrees/biomes/.claude/worktrees/b07d` (nested). Full suite on the pre-fix tree: all green but PristineChunksTests
(wall clock, 23.5 s at load 20, passing alone) and an ArchitectureRules hit (FileLodPersistence read a Stopwatch; now
Environment.TickCount64); after the fixes only build, format and the affected classes re-run.
1. "Worker threads" (Auto or 1 to 8, saved as `workers`; `--workers auto|N`); `JobPool.MaxWorkers` 8; `JobPool.Resize` while running (no
   job lost); `WorkerPlan` Auto = cores minus 4 in 1..8 (8 here); scripted runs, tests and `--prefetch` at 2 or less; the world identical
   at 1, 2, 8 and inline (store bytes and grid).
2. Far view distance (Auto or 1 to 16 km), detail (50 to 200%), trees up to, textures, real land to: saved, launch options, live; in the
   Graphics group above B17b's Shaders rows.
3. `--pregen <blocks> [--pregen-at x,z]`: the ring alone on all workers, windowless, resumable; 1 km 3,224 footprints in 29 s; 4 km 51,494
   in 340 s, 1.05 GB; 16 km 823,657 (estimated about 48 minutes and 15 GB from a 10-minute probe).
4. A 256 MB LRU of real sections (edited ones kept) reloaded from disk; the ring plans on a worker; fsync on a 2 s timer; region
   compaction at start and after pregen; pooled arrays; no put-then-get; near plane 0.05 to 0.1; a surface-only ring (tiles identical; skips
   only about 23% of layers, not the review's 5 to 10x).
5. Measured (Release, High, 16 km, cold, load 15 to 25, one round): fill after a start / a 1.9 km teleport: 2 workers 307 / 272 s; 4
   workers 221 / 147 s; 8 workers 180 / 110 s. Frame times dominated by load. Walks made 30 to 75 MB of real sections (the cap never
   dropped in play; unit tests show it). Working set 2.0 to 3.4 GB at a settled 16 km view. Pictures in artifacts/b-series/b07d/.
Left: a second interleaved round on a quiet machine; the old ring against the new on fill time; persisting the model's sections if a
16 km view must be complete at open (only the ring's real radius is stored, about 1 to 2 km).
Sent back (about 16:55) to merge B24 (conflicts in JobPool.cs, QualityPreset.cs, architecture.md) and run the full suite.
