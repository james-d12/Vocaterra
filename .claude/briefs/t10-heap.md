You are implementing **T10, Game.Core's managed heap since the water merge**, in the Vocaterra repository (C#, .NET 10, a MonoGame
first-person voxel game that teaches Spanish). The work is in the game's tests (`tests/Vocaterra.Game.Core.Tests`) and, only if a test's
memory comes from game code, `src/Vocaterra.Game.Core`. Do the work yourself; do not spawn subagents. Commit prefix `T10: `.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/t10`, already on the branch `T10-heap` (from
`feature/bread-pilot` at `e71846be`, which holds the water merge, T9 and M84; it has bin/ and obj/ from an earlier build). cd there
first; never edit `/home/user/Dev/Vocaterra` or another worktree (`adaptive`, `german` are not yours). Commit after each step that builds
and passes its tests; never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in
the foreground and finish in one turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**,
**Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/t10/` for your logs, about 12 GB of disk free, and **10361
passed, 3 skipped** as the count before you (M84's run; its gate saw 10360 and the one failure below). No content edit.

## The problem
- The water series' H08a (`e71af2a2`; read `git show e71af2a2`) capped the Game.Core test host's managed heap with
  `System.GC.HeapHardLimit` 3 GB (`tests/Vocaterra.Game.Core.Tests/runtimeconfig.template.json`; the csproj comment says 2.25 GB, fix the
  comment), sized for water's tests, and made whole-world kind scans lazy (`TestSupport/GridScan.cs`).
- Since the bread pilot's tests were merged with it (`bc881c58`), a full run throws `System.OutOfMemoryException` now and then: in
  `Npc/GuideTurnTests.TheCursorTileIsNamedWhenTheGuideIsAskedWhatThisIs` in 2 of 3 full runs (the log of the last:
  `/home/user/Dev/Vocaterra/.claude/gate-logs/test-20261108-053958.log`). The failing test is probably only where the heap ran out, not
  what fills it.
- **The owner (2026-11-08, `docs/game/decisions.md` D-game-23 item 6): fix it at the source; the 3 GB cap stays, and xUnit's
  `maxParallelThreads` stays 3.** The plan entry: `docs/process/test-performance-plan.md`, section T10.

## What to do
1. **Measure.** Find which test classes hold the most managed memory in the Game.Core run and why (whole scenes or worlds built per
   test or per class instead of shared, fixtures that keep grids, the language pipeline or bundles alive, caches that never clear,
   per-test copies of the shipped bundle). Use what works without new packages: `GC.GetTotalMemory`/`GC.GetGCMemoryInfo` logged per
   class in a temporary fixture, `DOTNET_gcServer=0`, `dotnet-counters` or `dotnet-gcdump` only if already installed. Run Game.Core's
   assembly alone through `scripts/test.sh project tests/Vocaterra.Game.Core.Tests` (under the cap). Remove temporary instrumentation
   before committing, or keep it only as a real test.
2. **Fix at the source**, as H08a did: share the expensive immutable fixtures (`IClassFixture`/`ICollectionFixture` or a static lazy
   instance of the shipped scene, bundle or pipeline where the tests do not mutate it), build smaller hand-made grids where a test does
   not need the shipped world, release what a test keeps. Never weaken a test's assertion, skip it or mark it slow to hide memory; a test
   that must stay heavy may be moved into a non-parallel collection if that is the cause. Never change the heap cap, the threads or
   `scripts/limited.sh`.
3. **A regression check as a C# test** if one fits (for example, a shared fixture is built once), not a script.
4. **Proof:** the Game.Core assembly's peak managed heap before and after (from your measurement), and **three** full Game.Core runs and
   **one** `scripts/test.sh full` with the models, with no `OutOfMemoryException`; give the margin under 3 GB.

## Report (half a page; the orchestrator reads it)
Final SHA, worktree clean; the measured top classes and their memory, before and after; what you changed and why; the proof runs with
counts and log paths; anything left heavy and why.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
