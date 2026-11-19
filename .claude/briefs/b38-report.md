# B38 report (no double builds)

Branch `B38-no-double-builds`, head `6cd871f9` (feature/world-next with B40 merged; one conflict in `LodRing.MakeColumns`, B40's scratch
kept). Agent's gate: build 0 warnings; full 12,682 passed, 0 failed, 9 skipped; timing 21 + 2; format clean; validate. Two earlier runs hit
B40's `WorkerPriorityTests` once each (it enumerates /proc/self/task and reads each thread's stat; a thread that exits in between makes the
read throw); it passed in the final run.

Changes (src/Vocaterra.Game only):
- **Fine tiles wait for the ring** (`FarView`): a level 0 to 2 leaf over a footprint inside the ring radius and not yet real is not built;
  its B35 stand-in covers it until the footprints land, then it is built once from real data. `SetRingWait` wired to `LodRing.Awaits`;
  inner-zone leaves keep their 700 ms grace and are not gated; levels 3 and up unchanged; a leaf gives up waiting after 240 s.
- **Priority** (`JobPool.TakeLocked`): ring and tile jobs taken nearest first on one scale (the ring at footprint distance plus `FineBias`,
  Range/4, as fine tiles); the ring keeps its one-job-in-five floor; coarse tiles and stand-ins still first.
- **The ring ingests resident chunks:** at submit, `TakeResident` copies the footprint's unchanged resident chunks (edited ones left out);
  `MakeColumns` uses them, treats layers above a column's band as air, and generates only the runs between from the shared window.
- **Settle fix:** `LodRing.IsIdle` also requires the results taken; the host's `Settle` waits for `Far.Waiting == 0` (the 1/2/8/0-workers
  same-bytes test caught 89 tiles against 85 without it).
- `--stream-stats` prints `far fine tiles <band> blocks:` lines (median and p90 of the first and last build per distance band).

Tests: `FarViewRingWaitTests` (each tile built exactly once in a real ring-plus-view fill; twice without the wait; no leaf uncovered; a
held gate keeps fine leaves unrequested); `LodRingResidentTests` (ring-from-zone byte-identical to generating on 3 footprints; an edited
chunk not taken; ordering by distance); `JobPoolTests` (nearest first across lanes).

Measurements (Release, High, 16 km, seed 42, 8 workers, fresh store): far tiles built 2,761 to 2,809 before, 2,069 to 2,084 after (-26%,
deterministic). Wall times too noisy to claim (two back-to-back pairs disagreed: 25/52 s against 51/113 s, then 29.5/71 s against
47/109 s). From the in-run bands (median, seconds from the far view's first frame): under 400 blocks the real tile at about 31 s (before:
a model tile first, the real one at 51 s); 400 to 800 blocks real at 36 to 49 s with no model tile between (before: a model tile, then
the real one at 103 to 113 s); 800 to 1,000 blocks about 52 s, built once (before: 68 s and again 115 s).

Pictures: /home/james/Dev/Vocaterra/artifacts/b-series/b38/ (pair 2, 10 s and 30 s). The blue line in B35's picture is not a crack: a
one-cell grey pillar in a coarse stand-in with a water face on its side (a river or lake cell sampled on a mountain top at a coarse
level), gone once finer data lands: the far sampler at levels 4 and up (B37's area).

Left: the air shortcut and the resident ingest rest on tests, not a proof; the cold-start jump case (inner-zone leaves built from the
model in their grace period, few); no live-walk test of the wait. For running.md: the `far fine tiles` lines. For architecture.md:
`LodRing.Awaits`, `TakeResident`, `FarView.SetRingWait`.
