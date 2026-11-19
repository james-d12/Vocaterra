You are implementing B40b, "WorkerPriorityTests flaky", for the Vocaterra repository (C#, .NET 10; the game is Vocaterra.Game.Core,
Vocaterra.Game, Vocaterra.Host; the core Vocaterra.Nlp* is not yours). Do the work yourself; do not spawn subagents.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b40b (absolute; cd there first), on the local branch
`B40b-worker-priority-test` (already created there, cut from `feature/world-next` at 61a8cace). Commit when it builds and passes, prefix `B40b: `. Do not push;
never push or merge feature/world-next or main.
Resources and waiting: every dotnet restore, build, test and format, every game or WorldPreview run through scripts/limited.sh (builds
`-m:4`, tests `-m:2`); one heavy command at a time, no `&`; long commands with the Bash tool's `run_in_background: true`, then end your
turn and wait for the notification; never `sleep`/`until` polls, `pgrep -f` loops or `nohup ... &`. Two other agents build beside you
(the slots in limited.sh queue you; that is expected). Game runs: a Release build, `scripts/limited.sh timeout 900 <command>`, one batch
script rather than many small runs, few and short windows (the owner may be using the machine). Iterate with `scripts/test.sh fast` plus
the classes you touch (`scripts/test.sh project <test project> --filter ...`); **merge `feature/world-next` into your branch before your
one `scripts/test.sh full`**,. Exit 137 is the memory cap:
lower the parallelism, never work around the cap. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; game runs need DISPLAY=:0 and VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The
sandbox refuses the words `eval` and `hash` in a command line.
Rules: a game milestone edits only the game projects (src/Vocaterra.Game.Core, src/Vocaterra.Game, src/Vocaterra.Host, tools/Vocaterra.WorldPreview),
their tests and docs/game/, and takes the core (Vocaterra.Nlp*) as it is. A regression check is a C# test. Never edit .github/workflows/,
either implementation-plan.md, either decisions.md, docs/game/biomes-plan.md, README.md, docs/game/running.md or docs/game/architecture.md
(give their text in your report). `World/` stays pure (ArchitectureRulesTests: no clock, no System.Random, no `[ThreadStatic]` or static
caches without an allow-list line, IntMath); determinism unchanged; **never loosen a test's limit or re-record a pin unless this brief
says so**. Do not commit bin/, obj/ or artifacts/.
Gate: build -warnaserror (0 warnings); scripts/test.sh full (the count on feature/world-next at your base, plus your tests); timing tests
pass; format clean; scripts/content.sh validate passes. Commit messages end with
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS

The fault: tests/Vocaterra.Game.Tests/Streaming/WorkerPriorityTests.cs, `NiceOf(name)`, enumerates /proc/self/task and reads every
thread's `stat` file to find a thread by name. The test process has many other threads (xUnit, the thread pool); one that exits between
the directory listing and the read makes `File.ReadAllText` throw (DirectoryNotFoundException, FileNotFoundException or IOException), and
the test fails at random. It failed twice in B38's runs. The code under test (src/Vocaterra.Game/Streaming/WorkerPriority.cs) is fine.

Deliverables:
1. Fix the test, not the code under test: read only the two test threads (have each record its own tid, for example through
   `WorkerPriority`'s existing `gettid` if it is reachable, or a small libc `gettid` import in the test), or skip a task whose files have
   vanished. Keep every assertion as strong as it is (the nice value rises by `WorkerPriority.Raise`, capped at 19; the other thread's
   does not move; the returned value equals the read one).
2. Run the test class 30 times in a row (`scripts/test.sh project tests/Vocaterra.Game.Tests --filter WorkerPriorityTests` in one loop
   inside one script, under limited.sh once) with no failure; say how many runs.
3. `scripts/test.sh fast`, then merge feature/world-next and run `scripts/test.sh full` once.
Report, a few lines: branch, head, worktree; the fix; the 30 runs; the test counts.
