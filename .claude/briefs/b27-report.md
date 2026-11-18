B27 report (agent a9f2399728dac471a, Sonnet 5.5 medium, 2026-11-17 about 19:40 UTC)

Branch `B27-drainage-cost`, head 12679b5e (merge of feature/biomes 93a74756 into dd1bbf3b), worktree `/home/james/Dev/Vocaterra/.claude/worktrees/b27`.
Where the time went (a temporary profile, Debug, CPU, a 24x24 column window): Shape with the network 3.8 ms against 0.6 without; `River()`
2.9 ms of it, from a per-column scan of 9x9 = 81 nodes round the column's own, each a `Locate` and a flag test, almost none holding a
river (about 47k `Locate` calls a window). Fix (`DrainageNetwork.cs`): each node's river reaches within the look window computed once and
kept in its tile (`DrainageCell.Near`, `RiverSegment` structs, in the old scan order); a column runs the same arithmetic over those few
reaches. Byte-identical: goldens, `SceneTests.GoldenDigest`, the fixture's digest and the atmosphere pins unchanged; purity and allocation
tests pass; the lock, the dry scope, `IsRead` and `Warm` untouched. `River()` 2.9 to 0.2 ms a window; Shape 3.8 to 1.0 ms.
CPU before / after (Debug, fastest of 3): surface chunk 13.9 / 6.7 ms (B02c's base 6.4 to 6.9); all-stone 13.6 / 6.4; deep 13.1 / 6.4;
column of 24 24.2 / 17.0; streamed band 15.4 / 6.4; band layers 20.4 / 11.2; snapshot 5,780 / 2,930 ms. Budgets halved to twice the new
cost (surface 14, stone 13, deep 13, column 41; band 13, made 23, per chunk 6, plan 2; snapshot 5,900 ms).
Suite at load 6 to 12: Game.Core.Tests 4,359 (36 m 26 s), Game.Tests 1,413 with 1 failure (`JobPoolTests...ItIsResized...`, B07d's
thread-timing test, 16 of 16 alone) (35 m 53 s); the rest passed; build, format, validate clean.
Left: the first landmass read (0.3 to 0.7 s) still blocks the first chunk; `Lake()` (0.14 ms a window) could take the same cache.
