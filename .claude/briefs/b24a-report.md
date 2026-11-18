B24a report (agent a0a352faac0ea6bd0, Haiku 5.5, 2026-11-17 about 05:37 UTC)

Branch `B24a-valley-threads`, head `b98358b7`, worktree `.claude/worktrees/b24a`, clean; scratch removed.
- Thread bound: `Lay` runs on at most two threads (`Threads` defaults to 2, `DegreeFor` clamps to [1, 2]), replacing `Parallel.For` on up
  to eight. The result is identical at one and two threads (tested).
- Cost: the underground adds 2,500 ms of process CPU time to the valley build (837 ms without, 3,337 ms with) at load 10 to 12; wall clock
  763 ms before and 1,344 ms after on a busy machine (noisy).
- Timing test: moved to `ValleyUndergroundTimeTests` in the timing collection (runs alone after the assembly); CPU time, fastest of three,
  budget 5,000 ms (twice the measured cost). The old wall-clock test failed at 1,540 ms under load. Caveat: spin-waits under contention
  may inflate the CPU measure, so the budget is generous: a slowdown must roughly double the cost to fail.
- Build 0 warnings; format clean. Full suite: all passed but one core fuzz test, M97's fixed-seed coverage test
  (`OpenFallback_TheGeneratorReachesEveryPath_OnAFixedSeed`), which M98 is fixing.
