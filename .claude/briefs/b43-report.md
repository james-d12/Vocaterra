# B43 report (which stage of chunk generation is the slowest; Opus 5.5 high, at the owner's request)

**Answer: meshing, held back by the game thread.** About 80% of each frame's 4 ms mesh budget goes to `SkyExposure.Tile` (an 18x18 tile of
`NaturalSurfaceAt`, the full terrain shape, about 3.4 ms per new chunk column) in the mesh submit on the game thread.

Branch `B43-start-time`, head `1de3d297` (feature/world-next up to `9c55c16e` merged; the earlier agent's WIP probes reset away, none left;
probe diff kept at artifacts/tmp/b43/patch/probe-final.diff). Commits: `eb0c5965` the stage counters, `58da90ae` a findings entry,
`1de3d297` two test fixes (`NoLiteralTextTests` entry; the counters' residency test read a clock). Agent's full run before that fix:
12,697 passed, 2 failed (those two, fixed and their classes pass); expected 12,699 after; not re-run in full.

Counters: `src/Vocaterra.Game/Diagnostics/PipelineCounters.cs`, owned by `StreamedWorldHost`; per stage a total since the first frame and
per episode (the start, or an eye jump over 64 blocks) a count in 50 ms buckets; lock-free adds, no allocation after construction (tested).
Stages: generated (a column's chunks made, `ChunkResidency.Generate`), resident (in the grid), meshed (first mesh taken, timed at the
worker's finish, `ChunkMeshResult.FinishedAt`; all-air chunks never counted), gpu-ready (first upload with something to draw), visible
(first frame drawn inside the frustum). `--stream-stats` prints a `pipeline:` line. `PipelineCountersTests` (8).

Measurements (Release, High 400 blocks, 16 km far view, 8 workers, the owner's `--world streamed --pass open`, `--no-settle`, 60 fps cap,
fresh seed and `VOCATERRA_LOD_DIR` per run, limited.sh's 10 cores; medians of 3; seconds from the first frame):

| Case (load) | generated/resident 50/90/100% | meshed 50/90/100% | gpu-ready 100% | visible 50/90/100% | gen/mesh/visible per s |
|---|---|---|---|---|---|
| Start, branch (8.5-8.7) | 4.5 / 7.8 / 8.8 | 10.1 / 23.1 / 26.4 | 26.4 | 11.1 / 20.3 / 24.7 | 1,697 / 254 / 71 |
| Start, origin/main, interleaved (8.3-8.9) | 3.6 / 7.5 / 8.2 | 9.8 / 20.2 / 23.3 | 23.3 | 10.9 / 18.9 / 21.7 | 1,822 / 291 / 80 |
| 3 km teleport, branch (6-17) | 2.9 / 5.4 / 6.1 | 10.2 / 23.4 / 26.0 | 26.0 | 7.7 / 21.4 / 23.9 | 1,707 / 170 / 51 |

14,800 chunks resident, 6,700 meshes, 5,900 uploaded, 1,740 visible. Near view (64 blocks) complete: branch 4.0 s, main 3.8 s, teleport
2.1 s. The owner's 10 to 15 s is the visible terrain: half by about 11 s, 90% by about 20 s. **B35 to B40 did not change the voxel start;
it was never limited by generation. The branch is about 3 s slower to the whole view than main: its game thread spends 1.2 s more in the
streaming phase (B38's ring copies resident chunks on the game thread).**

Where the time goes: meshing used only 6 to 7 worker-seconds (residency 11 to 13); the 8 workers mostly build far tiles (145 to 160 s of CPU
during the start). Meshes per second = fps x 4 ms budget / about 1.1 ms of game-thread cost per mesh, of which the mesh options call
(almost all `SkyExposure.Tile`) took 6.2 to 7.1 s in total, the snapshot copy 0.2 s, the hand-off about 0. Uploads and hand-back are not a
limit (0.5 s; gpu-ready trails meshed by under a frame). Residency (about 9 s at 1,700 chunks/s) is the next limit.

Direct tests: sky tile skipped (probe): whole view 15.5 s against 26.4, meshes 439/s against 254, game-thread meshing 3.2 s against 8.3.
Far view off: 24.4 against 26.4 s. Strict near-first (earlier probe, load 13 to 18; no far work until the whole view is made): 34 against
51 s; far work held only until the 64-block near view: near view 5.6 against 8.6 s. B38's order reverted: no change to the voxel view, far
coverage worse (40 against 22 s). B40's nice raise off: worse (63 against 51 s; workers at normal priority take the game thread's CPU).
Mesh budget 12 / 30 ms: whole view 39 / 29.5 against 51 s.

Ranked fixes (none implemented):
1. **The sky tile off the game thread:** the residency's column job computes the 16x16 natural surface heights on the worker and caches
   them with the band, so `SkyExposure.Tile` becomes lookups plus the short downward walk. Meshes at least 1.7 times faster (probe 26 to
   15.5 s whole view), near view about 1 s sooner. M, low risk (identical values; a byte-for-byte mesh test).
2. **A larger mesh budget while the view is incomplete** (the catch-up 10 ms for as long as many meshes are pending, not just 2 s after a
   jump): whole view about 40% shorter. S; frames of 10 to 15 ms while loading.
3. **After 1, the mesh pipeline's caps during catch-up** (`MaxSubmitsPerFrame` 16 and `MaxInFlight` 24 cap it at about 960/s). S, low.
4. **Near-first gate for far work:** no ring or tile submits until the 128-block view is meshed, and B38's resident-chunk copy off the game
   thread: -3 s near view, -10 to -17 s whole view under load. S-M; the far horizon fills later.
5. **Residency in-flight cap 16 to 32 during catch-up:** -2 to -4 s on the resident stage, the floor after 1 to 3. S.
Do not revert B38's order or B40's nice raise.

Caveats: scripted `--no-settle` runs with live budgets, a 60 fps cap, limited.sh's 10-core cap and other sessions' load; not live play.
Run logs: artifacts/tmp/b43/out/ (and old/out-prev/); summariser artifacts/tmp/b43/sum2.py.

For running.md: "Since B43 it also prints a `pipeline:` line after each `whole view resident and meshed` line and at the end of a scripted
run: for the start or the latest jump (and the seconds since it), per stage of a voxel chunk's way to the screen (generated, resident,
meshed, gpu-ready, visible), the chunks that reached it, when 50, 90 and 100 percent did (seconds, to 50 ms), the rate over the whole time
and over 10 to 90 percent, then the totals since the first frame. Each chunk counts once per stage; the counters stay on and cost no
allocation." For architecture.md: `PipelineCounters` (Diagnostics), owned by `StreamedWorldHost`, fed by `ChunkResidency.Counters` and
`VoxelRenderer.SetPipelineCounters`.
