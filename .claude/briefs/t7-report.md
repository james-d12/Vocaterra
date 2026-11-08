T7 report. Branch `T7-test-memory` in /home/user/Dev/Vocaterra/.claude/worktrees/t7, final commit e49eb771 (local, not pushed). Worktree is clean and no process of mine is still running.

**What held the memory.** The cause was undisposed turn-worker sessions, not a cache or the number of seeds (the tests use one seed, 42).
- `StartWorker` starts a thread that holds the session, its scene clone (38 MB) and a language engine until the session is disposed.
- `ConversationSurfaceTests` built ten such sessions without disposing them. `SecondTryTests`, `CheckProgressTests`, and Game.Tests' `SpokenFrontendTests` and `SessionLogTests` did the same.
- I measured this with a temporary probe that forced a GC every 0.6 s and logged the running tests. The live heap went from 250 MB to 1.9 GB in about 2 s, in steps of about 450 MB, one per `ConversationSurfaceTests` case. With the sessions disposed, live never passed 0.8 GB.
- The tests also allocate 79 GB in total, about 38 MB each from the scene clone. The GC lets the heap reach about 4.5 GB (75% of the cap) before collecting hard, so the peak followed the leak.

**What changed:**
- Five test classes dispose their sessions (`using var`). `ConversationSurfaceTests` is now `IDisposable` and disposes the sessions its `Screen()` helper made.
- New `tests/Vocaterra.Game.Core.Tests/xunit.runner.json` sets `maxParallelThreads: 3`, copied to the output by the csproj. After the leak fix, ten threads still peaked at 4.0 to 4.5 GB. Three threads give 2.4 to 2.5 GB with about the same wall time.
- No test, assertion or budget changed, and no production code. `scripts/test.sh` was not needed.
- The plan doc has the numbers in its Results section and a new "### T7" section after T6.

**Peaks, before to after** (the cgroup peak `limited.sh` prints):

| Assembly, alone | Before | After |
|---|---|---|
| Game.Core.Tests | 5.0 GB (2 timing tests failed in that run) | 2.4 to 2.5 GB (73 to 81 s) |
| Game.Tests | 1.6 GB | not re-measured alone |
| Host.Tests | 0.3 GB | not re-measured alone |
| Nlp.Tests | 2.8 GB | untouched |
| Es.Tests | 1.3 GB | untouched |
| Golden.Tests | 0.4 GB | untouched |
| Fuzzing.Tests | 2.5 GB | untouched |

- **Full run, before:** `scripts/test.sh full` was killed by the cap (exit 143) at 87 s. This reproduces the original problem.
- **Full run, after:** passed three times, peaks 4.6, 4.7 and 4.8 GB, wall times 274, 255 and 238 s (build included).
- **The full run still peaks at 4.6 to 4.8 GB**, below the 6 GB cap but with less room than "comfortably" suggests. The rest comes from overlap with Nlp.Tests (2.8 GB) and the fuzz suite (2.5 GB), which are core and untouched. If you want more headroom, the next step would be a change to those assemblies.

**Counts:** unchanged at 9586 passed and 3 skipped with models (Game.Core.Tests 2384, Game.Tests 646, Host 188, Es 2292, Nlp 3942 passed plus 3 skipped, Golden 17, Fuzzing 117).

**Gate:**
- The build with `-warnaserror` was clean (`test.sh` builds that way on every run).
- `dotnet format --verify-no-changes` is clean.
- CI's case (empty models folder): full run passed, 5.2 GB, 234 s, 28 + 17 model tests skipped.

**Rejected after trying:**
- A GC heap hard limit lowers the peak but gives `OutOfMemoryException` when the live set exceeds it (3 GB failed 3 tests at 4 threads; 2 GB failed 301 at 2 threads).
- `GCConserveMemory`, `GCgen0MaxBudget` and non-concurrent GC did not help.

**Left undone:** nothing in scope.