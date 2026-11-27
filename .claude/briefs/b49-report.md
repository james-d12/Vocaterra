# B49 report (never retire a drawn LOD)

Branch `B49-never-retire-lod`, head `e6ee202f` (feature/lod-lifecycle at 9704bfab merged). Files: `src/Vocaterra.Game/Graphics/Far/FarView.cs`,
new `tests/Vocaterra.Game.Tests/Graphics/Far/FarViewLifecycleTests.cs` (Slow). Agent: Sonnet 5.5, high.

Built: `Invalidate` marks a drawn tile (stand-in, leaf or below a leaf; new `_shown` set) dirty instead of disposing it, and `Below` counts
dirty tiles as cover; `RetireChanged` after `Assemble` frees a dirty tile in the first assembly that no longer draws it and does not want it
(memory bounded by what is on screen); `Reselect` keeps in-flight ancestors (within 4 levels) of current and look-ahead leaves wanted (the
old reset let a worker drop the job as stale and leave the key "being built" forever); `KeepCovering` stops `Trim` and
`DropUnwantedPending` evicting ancestors (within 4 levels) of leaves not on the card. Finding 9 was already fixed by main's `dcc56e8a`
(stand-ins get no windows over chunk-shown leaves, so backdrop windows no longer exist). `Reselect` and `IsWanted` are public (no
InternalsVisibleTo in the game projects).

Failing first (test commit `d2ea9edb` on the old code; `JobPool(3)`, `RealRadius` 1,000 against 400, the real `LodRing`, the gate, frames
waiting on job completions): section change of 43 drawn stand-ins, 18 uncovered frames (peak 707 leaves) against 0; flight out and back
(300 + 300 frames at 3 blocks a frame), 4 uncovered frames (89 to 160 leaves) against 0; in-flight stand-ins dropped by re-selection 16 of
18 without the fix; Trim evicted a level-3 ancestor of a waiting leaf at frame 182 without the fix.

Tests: full 14,009 passed, 1 failed (`WallClockRulesTests`: the test read `Stopwatch`; removed in `e6ee202f`, the rule and the far tests
re-run green, the whole suite not repeated); timing 23 passed; build, format, validate clean.

Measurements: no scripted flight exists (only `--walk-to`, `--teleport`), so the test's flight stands in: far-view game-thread time p50
1.30 to 1.25 ms, p99 5.38 to 5.80 ms, max 9.12 to 9.38 ms (one noisy run each; the timing code was temporary). The real flight's p99 and
far pass need the owner's flight with `--stream-stats`.

Known, not fixed: a dirty stand-in still drawn is not rebuilt (shows old coarse data until its leaves are drawn, then retired); slow leaves
(ring wait up to 240 s) keep a stale stand-in that long, never a hole. No separate cause of holes found for B50 to B52 in these runs; the
test cannot reproduce 190 blocks a second on 8 workers.

Text for docs/game/architecture.md (far view): "A drawn tile is retired only once what replaces it is drawn. When the store reports changed
sections, a tile that the last assembly draws (a stand-in, a leaf or a tile below a leaf) is marked dirty and keeps drawing; a wanted and
needed one is rebuilt, any other is freed by `FarView.RetireChanged` in the first assembly that no longer draws it, so what is kept for
drawing is bounded by what is on screen. Re-selection keeps every in-flight tile that stands in for a current leaf in the wanted set
(`FarView.Reselect`), so a worker never drops a stand-in's job as stale. `Trim` and `DropUnwantedPending` never evict an ancestor, within
four levels, of a leaf that is not on the card. Tests: `FarViewLifecycleTests` (real workers, the ring's 1,000 blocks against a 400-block
voxel zone, a flight out and back, `Uncovered` asserted every frame after the first cover)."
