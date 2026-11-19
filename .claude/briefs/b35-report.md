# B35 report (stand-ins for the inner ring)

Branch `B35-inner-ring-standins`, head `9324a390` (merge of feature/world-next over two B35 commits). Changes in
src/Vocaterra.Game/Graphics/Far/FarView.cs and Scripted/ScriptedRunEnd.cs only; no World, store or pin change.

- `RequestStandIns`: a needed fine leaf (level below 4) with no ancestor within `FallbackSteps` built, requested or failed gets its
  level-4 ancestor requested (1 to 4 steps from levels 0 to 3, so `Assemble` finds it). Asked once, in `_wanted` while needed, then an
  ordinary cache entry: drawn while a leaf under it waits, dropped under the cap like any tile.
- **Not in the brief:** fine tiles' priority gets `+ Range/4`, so every coarse tile and stand-in goes before every fine one. Stand-ins
  alone left the horizon (L5 to L7) interleaved with fine tiles and coverage barely moved (115 s to 116 s). Same work, different order.
- `Assemble`: where two stand-ins nest only the coarsest is drawn. Diagnostics: `FarView.Uncovered`, `CoveredAfterMilliseconds`,
  `FilledAfterMilliseconds`; `--stream-stats` prints a `far coverage:` line. `Requested` and `Cache` made public for the tests.

Tests: new `FarViewStandInTests` (first frame: every leaf has a tile or a requested ancestor; stand-ins built before the first fine tile;
each tile built once). Three existing tests that counted leaves only (`FarViewWalkTests` x2, `FarViewReshapeTests`) now add the stand-ins.
Agent's gate after merging world-next: build 0 warnings; full 12,646 passed, 0 failed, 9 skipped; timing passes; format clean; validate.

Times (Release, High, 16 km, seed 42, 8 workers, fresh store, one run each on a loaded machine; from the far view's first frame):
no uncovered leaf 115 s before, 33 s after; every leaf on the card 117 s before, 82 s after. L0 to L3 (about 1,200 leaves) were uncovered
until 100 to 115 s; now covered by 8 to 16 s. Tile count +3%.

Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b35/ (index.md; about 5 s and 10 s after start, before and after): the middle
distance beyond the valley's mouth is filled with coarse grey blocks where before it was pale sky. The first 4 s of the far view (the
start's near-zone wait) are unchanged; stand-ins are blocky until the fine tiles arrive. For running.md: `--stream-stats` gains a
`far coverage:` line.
