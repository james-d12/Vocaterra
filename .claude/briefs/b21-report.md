B21 report (agent af8cc8a100e55e149, Sonnet 5.5 high, 2026-11-17 about 08:00 UTC; isolation round about 08:05)

Branch `B21-meshing-off-thread`, head `c7b08ce5` (three commits, feature/biomes merged twice, last at `ef932990`), worktree clean.
- P1: `ChunkSnapshot` copies on the game thread what the mesher reads (the chunk and its 18-block layer, two layers under, the blocks
  near lights stand on, the river currents); `ChunkMeshPipeline` builds meshes and grass layers on a new `JobLane.Mesh` (one residency job
  after every four mesh jobs); uploads on the game thread within the frame budget, old and new buffers swapped in one step. A stamp per
  chunk (its edits, a neighbour's border, edge or corner edits, the chunk above, a light, the sky's reach) refuses and requeues stale
  jobs; nearest first; a grey-set or quality change moves a pipeline epoch. An edited chunk within 40 blocks of the eye (at most 4 a
  frame) meshes on the game thread in the edit's frame. No workers (`--prefetch 0`, tests): the same jobs inline within the budget. The
  bounded valley gets its own two-worker pool.
- P2: `ChunkViewSet` re-sorts only when a chunk gains its first block or loses its last; the pipeline walks a window of 512 a frame; an
  edit costs a handful of lookups. P5: steady-state frames allocate 0 bytes in the pipeline and residency (tested); `ContentOf`'s copy kept
  (region chunks are shared). C4: residency packages carry their jump's epoch. C5: `ApplyPreset` reconfigures the same residency. C11:
  `HighestBlock` reads resident chunks and asks the generator without installing; `VoxelGrid.ChunksGeneratedByReads` in `--stream-stats`
  (about 50 at a start, 101 to 110 over three teleports or a 250-block walk, none inside the near zone).
- Measurements (Release, 250-block walk, medians of 6, load 5 to 18): Medium frame p50/p95/p99 2.0/12.2/19.6 to 1.9/12.1/18.5 ms; High
  3.8/12.4/20.5 to 3.2/13.8/19.8; game-thread remesh p50 0.15 to 0.01 ms (max 360 to 127); game-thread allocation 528 to 248 KB a frame
  (Medium), 808 to 383 (High); near view complete after a jump 5.1 to 3.3 s (Medium), 7.2 to 3.1 s (High); whole view 9.6 to 8.1 s,
  15.2 to 12.3 s. With the 16 km far view filling, frames 11.6 to 14.5 ms before, 4.4 to 5.1 after. Fill still bound by the two workers
  (near about 2x, whole view 20 to 40% faster, not the review's 3 to 5x). Process allocation 200 to 240 MB/s both (the workers carry it).
- Isolation of a High teleport with the far view on (interleaved A/B x3, load 9 to 10): after equal or better every run (p99 32.9/31.0/29.8
  before, 23.3/26.9/27.7 after; frames over 16.7 ms 442/470/517 before, 187/300/286 after). The earlier 28 to 31 ms was other agents' load.
- Pictures (artifacts/b-series/b21/): every before/after pair pixel-identical; seven `--world valley` runs equal.
- Suite on the final tree: 12,509 passed, 1 failed (`GeneratedLexiconTests.LoadingTheBundleIsFastEnough`, passing alone).
- Left: `LodEdits` allocates through `HashSet.First()` per frame while edits are pending (B07f's file); `ChunkMeshResult` arrays not
  pooled. Base bugs seen: scripted `--place antorcha@467,61,153` shows no torch; after a guide dialogue opens, `--wait` never returns.
