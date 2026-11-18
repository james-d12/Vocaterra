You are implementing B29a, "The throughput review's script changes", for the Vocaterra repository (C#, .NET 10). This milestone changes
how the work is built, tested and gated: `scripts/limited.sh` (only if needed), `scripts/merge-gate.sh`, `scripts/test.sh`, the test
projects' traits and build settings, CI's workflow `.github/workflows/ci.yml` (named here, so you may edit it), the timing budgets of the
CPU timing tests and `docs/process/test-performance-plan.md`. It changes no game or core behaviour.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29a (an absolute path; cd there first), already on the local
branch `B29a-throughput`, cut from `feature/meta_improvements` at BASE `aac46848`. Commit after each deliverable that builds and passes
its tests, with the prefix `B29a: `. Do not push your branch. Never push or merge feature/meta_improvements or main. Leave the worktree
clean on its branch.
Resources (the machine has crashed from overload): run every dotnet restore, build, test, format and run, every scripts/content.sh call
and the licence scans through scripts/limited.sh (builds `-m:4`, tests `-m:2`). scripts/test.sh calls it itself: run it directly. One
heavy command at a time (no `&`, no background builds, no parallel test runs). **Waiting:** run a long command with the Bash tool's
`run_in_background: true`, end your turn and wait for its notification; never poll with `sleep`, `until ...; do sleep` or `pgrep -f`
loops, never `nohup ... &`. Heavy output to log files under /home/james/Dev/Vocaterra/artifacts/tmp/b29a/ (not /tmp; removed at the end),
at most 30 lines printed. Exit 137 or 143 means a cap killed the command: lower the parallelism, never raise a cap.
Environment: prefix every command inline with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; model-gated tests
need VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The sandbox refuses a command line containing the words `eval` or `hash`
(use a wrapper script outside the repository if you need one).

Why: the throughput review docs/research/2026-11-18-throughput-review.md (read sections 1, 4 and 5; it is the plan) and the owner's
decisions in docs/game/decisions.md, D-game-34 items 57 (the review approved, B29 split), 58 ("timing tests in the gate and CI only; the
fuzz suite in CI only") and 59 (CI runs on every push to the open pull request #70). Your row: docs/game/implementation-plan.md section
4.14, B29a. The earlier full brief, .claude/briefs/b29-throughput.md, is background only: its parts B (token policy) and C (stamp skip)
are deferred and are NOT part of this milestone; its part 5 (hot spots) is B29b's; its part 6 was done by B30.

Read first: CLAUDE.md; `.claude/agents/orchestrator.md` ("Machine resources", "The milestone loop" step 4, "CI"); `scripts/limited.sh`
(its whole header, including the re-entrancy through `VOCATERRA_LIMITED=1`); `scripts/merge-gate.sh`; `scripts/test.sh`;
docs/process/test-performance-plan.md (the T sections); `.github/workflows/ci.yml`; the two `TimingCollection`s
(tests/Vocaterra.Game.Core.Tests/TestSupport/TimingCollection.cs and the one in tests/Vocaterra.Nlp.Tests) and the classes in them
(search `[Collection(` for their name), and how `TestSpeed.Trait` is defined and used. Read the sections named, never whole long files.

The machine: at launch (12:3x UTC, 2026-11-18) nothing else runs (load under 1, no test hosts). The other workstream (world-next) may
start agents later from its own branch. **Deliverable 1's two measurement runs need a quiet machine:** before each, check `uptime` and
`pgrep -fc 'MSBuild.dll|VBCSCompiler|testhost'`; if the 1-minute load is over 3 or another session's test hosts run, say so in the report
with the numbers (do not wait in a loop; measure and note it, or run Debug and Release back to back so they share the conditions).

Rules: **the gate's protection must not weaken** apart from what the owner decided (item 58): it still builds with warnings as errors,
runs the whole suite except the fuzz suite, runs the timing tests (as their own step), checks format, the content gate, the grammar replay
and the licence scans. **The machine's safety stays**: limited.sh keeps its slice and caps exactly (three slots, 10 GB and 10 cores a
command, 16 GB and 14 cores in all, no swap). Never weaken or remove a test or a fuzz oracle; a timing budget re-set from Release times is
twice the measured Release cost, so it still catches a doubling. Do not edit CLAUDE.md, `.claude/agents/`, `docs/process/milestone-briefs.md`,
either implementation-plan.md or either decisions.md: put the exact replacement text in your report and the orchestrator applies it. The
world, the goldens and the save fixture's state digest do not change. Only the files named above change, plus test files for traits and
budgets; say any other.

Deliverables:
1. **Release tests, measured first** (the review's item 1). Build the solution in Release and run the full suite in Release once and in
   Debug once (both with the fuzz suite, so the numbers compare with the review's): per-assembly wall times, the 20 slowest classes,
   peak memory (limited.sh prints it), the load. Check the 18 procedural goldens, `SceneTests.GoldenDigest`, the end-to-end goldens
   (tests/Vocaterra.Nlp.Golden.Tests) and the save fixture pass unchanged in Release. **If Release is at least 1.5 times faster and every
   golden holds**, switch `scripts/test.sh` (every mode), `scripts/merge-gate.sh` and CI's `build-and-test` job to Release (build
   `-c Release -warnaserror`, test `-c Release --no-build`), and re-set every CPU timing test's budget to twice its Release cost (list each:
   old budget, Release cost, new budget). CI runs with `CI=true`, which multiplies the CPU budgets by 4 (B30): keep that. If Release is not
   clearly faster or a golden moves, give the numbers and leave Debug. Say whether anything still needs a Debug build (for example a
   `#if DEBUG` assertion or a test that checks one) and whether the gate keeps a Debug build for it. Say what Release does to CI's time
   (its job has a 120-minute timeout) as an estimate; the orchestrator reads the real CI run after the push.
2. **The gate holds one limited.sh slot for its whole run** (item 2). `merge-gate.sh` re-runs itself once under `scripts/limited.sh`
   (or is wrapped so the whole gate is one limited command); its inner limited.sh calls then run directly through the re-entrancy, so the
   gate never queues for a slot after it starts. Check the whole gate fits one command's caps (10 GB, 10 cores): the suite peaked at
   5.5 GB; say the gate's peak. The merge, the commit message and the log names stay as they are. Its header says so.
3. **Failed tests re-run alone and labelled** (item 3). When the suite (or the timing step) fails, the gate collects each failed test's
   full name from the test results (use a TRX or similar logger written under `.claude/gate-logs/`, not by scraping console text if a
   logger works), re-runs each alone in its project (`--filter "FullyQualifiedName=..."`; theory cases by method), and prints one line per
   test: `FLAKY <name>` (passed alone) or `FAILED <name>`. The gate passes when every failure is FLAKY and fails if any is FAILED; the
   FLAKY list is printed in the gate's summary and kept in a file in `.claude/gate-logs/` so the orchestrator records it. Cap the re-runs
   (for example at 20 tests; more failures than that fail the gate without re-running). Test it on a throwaway branch with a test that fails
   once and then passes (a temporary marker file), and one that always fails; do not commit those tests.
4. **Timing tests in their own step; the fuzz suite in CI only** (item 7a). Give every test in the two `TimingCollection`s a trait (for
   example `[Trait(TestSpeed.Timing, ...)]` or a `Category=Timing` trait; keep `Speed=Slow` where it is). `scripts/test.sh` fast, full and
   project leave out the timing tests and `tests/Vocaterra.Nlp.Fuzzing.Tests` (its assembly trait already says `Speed=Slow`); a new
   `scripts/test.sh timing` runs only the timing tests (for agents whose milestone changes performance or a timing test). `merge-gate.sh`
   runs the suite without fuzz and timing, then the timing tests as their own step, alone, after the suite; a failure there gets
   deliverable 3's re-run. CI's `build-and-test` runs the whole suite, the fuzz suite and the timing tests included (check it does after your
   changes; if it calls `scripts/test.sh`, give it a mode that keeps fuzz and timing). `scripts/test.sh full` stops saying it runs the suite
   "as the gate and CI run it". Say how many tests each of fast, full, timing and the gate's two steps now run.
5. **A test-time report in the gate** (item 5). After the test steps, print each assembly's wall time and its slowest class's time next to
   the last gate's (read from the newest earlier report kept in `.claude/gate-logs/`, a small machine-readable file you write each run),
   and a warning, never a failure, when the suite's time grew by more than 10 percent or a class passed 5 minutes. With no earlier report
   it prints the times alone.
6. **Documents:** docs/process/test-performance-plan.md (a T12 section: the Release and Debug numbers, the gate's new shape, the test
   modes), the headers of the scripts changed. In the report give the exact replacement text for: CLAUDE.md's "Merge gate" bullet (the
   sentence "The fuzz suite is a gate; never weaken an oracle to make it pass" becomes "The fuzz suite runs in CI; never weaken an oracle
   to make it pass"), its "Build, test, format" and "CI is" bullets if Release changes them; `.claude/agents/orchestrator.md`'s "Test
   tiers" bullet ("the gate and CI always run the full suite" no longer holds) and milestone-loop step 4; `docs/process/milestone-briefs.md`
   lines 24 to 26 (the template's test rules: agents' runs leave out the fuzz suite and timing tests; `scripts/test.sh timing` when the
   milestone changes performance or a timing test) and line 98 (the fuzz suite is a merge gate).
7. **Measured at the end:** a dry run of the new `merge-gate.sh` merging your branch into a throwaway copy of feature/meta_improvements
   (`git branch b29a-gate-scratch feature/meta_improvements`, `GATE_TARGET=b29a-gate-scratch`, run from a scratch worktree on that
   branch; never feature/meta_improvements itself); its total time and each step's, compared with the review's section 5 table ("after
   B28": test phase 25 to 29 min, whole gate about 27 min). Remove the scratch branch and worktree after.

Gate (from your worktree root): scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4 (0 warnings, in the configuration the
scripts now use); scripts/test.sh full passes (12,787 passed on feature/biomes' last gate including the fuzz suite and timing tests; give
the new counts per mode); scripts/test.sh timing passes; scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes clean;
scripts/limited.sh scripts/content.sh validate passes; bash scripts/license-scan.sh --offline PASSED; the gate dry run of deliverable 7
passes. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS
Report, one page: branch, head commit, worktree; deliverable 1's numbers (Release against Debug per assembly, the slowest classes, peak
memory, the load) and whether you switched, with the budgets re-set; the gate's single slot and its peak; the re-run and its test; the
test counts per mode and step; the time report's output on the dry run; the dry run's times against the review's table; the replacement
texts of deliverable 6; what the plans and decisions should say; anything left or found (bugs, flaky tests seen). No file dumps or long logs.
