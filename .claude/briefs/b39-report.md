# B39 report (shared windows and the strata pass)

Branch `B39-shared-windows-strata`, head `edf1b1d8` (feature/world-next with B36 merged before the full run). Agent's gate on the merged
tree: build -warnaserror 0 warnings; `scripts/test.sh full` 12,665 passed, 0 failed, 9 skipped; timing 23 passed; format clean; content
validate 0 errors. No golden, pin, save-fixture digest or timing budget changed. The walk-and-edits test: workers (shared windows) and
inline (no windows) give identical state digests and saves.

Figures (Release, seed 42, single thread, CPU time, fastest of 3, base and branch interleaved in one scratch script at load 8 to 15; 256
columns, 144 footprints):

| Measure | Before (aed35164) | After |
|---|---|---|
| Residency column (band then layers, band ±1) | 1.92 ms | 0.62 ms (-68%) |
| Ring footprint (bands plus 4 columns, no ingest) | 9.6 ms | 5.6 ms (-41%) |
| Strata pass per chunk | 114 µs | 61 µs (-46%) |

The ring is now bound by its 90x90-column window (about 4 ms of 5.6). Two group rows were added to the `bench` command; one `--quick`
run on the loaded machine read about 8 times higher than the scratch program, so the agent reports the scratch figures.

Design: `ColumnGroup` (public, immutable) holds the shaped window of an N x N square of chunk columns (one climate fill, one shape, one
shapers run); a column cuts its own 42x42 window out of it by row copies, already shaped; trees, caves, biomes and surfaces stay per
column and lazy. New API: `ProceduralChunkSource.ShapeGroup`, `SurfaceBand(cx, cz, group)`, `GenerateLayers(..., group)`, passed through
the composite source, `SurfaceBands` and `StreamedWorld`; old overloads unchanged; `Climate` and `HeightModel` untouched.
Residency: 4x4 groups aligned to the chunk grid; the band job runs per group at the distance of its nearest wanted column, makes all 16
bands and keeps the window in `ColumnGroupCache` (128 groups, about 64 MB, oldest first); each column's layers stay their own job and cut
the cached window or make their own. The nearest column never waits on other columns' layers; its group's band job costs about 4 ms
instead of 0.9. A run with no workers keeps the per-column path. Ring: one window per footprint (4x4 columns when 3 or more outer bands
are missing, else the footprint's 2x2). `LodRing.MakeColumns` is public for tests. Memory: about 1 MB a job while shaping.
Strata: whole-chunk skip when no stone reaches it; `StrataTable.Fill` returns whether anything differs from stone; exact skips of pocket
rows that cannot occur at a height; a per-row mask for `Array.IndexOf`. The noise evaluation is the remaining cost.

Tests: `ColumnGroupTests` (15; group-made chunks and bands equal solo ones on all 24 layers, seeds 42 and 2026, corners, edges, unaligned
groups, a one-column group, 4 threads, valley columns); `ColumnGroupStreamingTests` (6; cached group layers equal solo, cache bounded,
ring footprint the same through wide, small and no window).

Left: biomes and surfaces not shared across a group; a larger ring unit; B40's pooled arrays would cut the copy and allocation cost; the
near view after a jump not measured end to end (no game run). docs/game/architecture.md could name `ColumnGroup`, `ColumnGroupCache` and
the group band job.
