# B50 report (far scheduling fixes)

Branch `B50-far-scheduling`, head `9ec84342` (feature/lod-lifecycle merged; briefs only). Agent: Sonnet 5.5, medium. Commits `9008cb74`
(finding 3), `f88013b4` (findings 4, 11), `8a922188` (finding 10), `5abf9a67` (finding 6). Release build 0 warnings after the merge.
The agent's `scripts/test.sh full` was killed for low memory (the owner's Minecraft launcher, 16 GB, and another session's tests); timing,
format, validate and the `--stream-stats` measurement were not run by the agent: the merge gate ran them.

Built:
1. Finding 3: `JobLane.Warm = 4` in `JobPool`, taken only when the tile and ring lanes are empty, and `JobPool.Waiting(lane)`;
   `FarView.WarmDrainage` submits there (one line). Tests: `JobPoolTests` warm-last; `FarViewLifecycleTests` `..._ItWarmsTheDrainage_...`
   (old: "warm 0, ring 33, tiles 860", failed).
2. Finding 4: `LodRing` counts failures per footprint and re-plans after `RetryAfterUpdates` = 30 updates, doubling; after `MaxAttempts` = 3
   it gives the footprint up (not planned again, `Awaits` false); `Pending` counts a due retry. Tests with a disk whose `Save` throws (once,
   always); both failed on the old code.
3. Finding 11: `Awaits` measures from `_plannedFor` (origin and radius), from the eye only before the first plan. Test: eye moved 24
   blocks, footprint (4,0) not awaited; failed on the old code.
4. Finding 10: `NearFirstGate.IsNearViewMeshed(view, centre, chunks, hasMesh)`: true when the view set is non-empty and has no chunk near
   the eye (an empty view set is still not done); `VoxelRenderer.IsNearViewMeshed` wraps it and `SceneDrawer`'s gate uses it;
   `IsMeshedAround` unchanged (the `--stream-stats` move probe unchanged). Two `NearFirstGateTests`.
5. Finding 6: `DrainageNetwork`'s unbounded `_reading` lock table (B40) replaced by a `_claims` table: each land node claimed by the reader
   that found it first; a reader meeting a claim takes the holder over (if it started from a smaller key and the holder has not committed)
   or stops, releases its claims and waits for the holder's `Done`, then looks again; commit by compare-exchange before the flood; claims
   removed when a read ends. Counters `LandmassReads`, `LandmassReadsGivenUp`, `ClaimedNodes` (also on `HeightModel`).
   `DrainageNetworkWarmTests` (8 threads, seeds 42 and 2026): old code 9 and 13 flooded reads against 2 and 3 on one thread; new code equal
   to one thread, 0 claims left, 6 repeated runs. 72 drainage, `FarSectionDigest`, `HeightModel` and `LakeLevels` tests pass unedited.

Measurements: B49's flight test unchanged (covered at frame 113, 0 uncovered frames, 128 tiles built against 129); that harness has no
failing footprint or warm-up contention. No `--stream-stats` run.

Known, not fixed: finding 7 untouched (the claims make concurrent publishes of one landmass rarer); a landmass cut at `MaxNodes` may still be
re-read from another start node; the Warm lane can starve behind a stream of tile jobs (intended: a tile needing an unread landmass reads it
itself); a footprint given up by the ring is not retried in that run.

Files outside the far view: `src/Vocaterra.Game.Core/World/Drainage/DrainageNetwork.cs`, `.../Procedural/HeightModel.cs` and
`DrainageNetworkWarmTests.cs` (finding 6 needs them).

Orchestrator's review of the claims (2026-11-27): no deadlock (a reader waits only after its `finally` released its claims and set its own
`Done`; every reader sets `Done`), takeovers go only to a smaller start key (finite chains), commit and takeover race on one
compare-exchange of `Winner`. `ManualResetEventSlim` objects are not disposed (no wait handle unless contended; left to the GC).

Text for docs/game/architecture.md: "The job pool has a fifth lane, Warm, taken only when no residency, mesh, tile or ring job waits; the
drainage warm-up rows go there. LodRing re-plans a failed footprint after 30 updates (doubling), gives it up after 3 failures, and Awaits
measures from the plan's origin and radius. The near-first gate counts a view set with no chunk within 8 chunks of the eye as done.
DrainageNetwork reads each landmass once at a time: readers claim land nodes, a reader that meets another's claim yields or takes over by
start-node key, and the claim table empties when reads end."
