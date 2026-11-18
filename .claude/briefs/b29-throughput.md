You are implementing B29, "The throughput changes", for the Vocaterra repository (C#, .NET 10). This milestone changes how the work is
built, tested and gated: the scripts `scripts/limited.sh`, `scripts/merge-gate.sh`, `scripts/test.sh`, the test projects' build settings,
CI's workflow `.github/workflows/ci.yml` (named here, so you may edit it), and a few tests. It changes no game or core behaviour.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29 (an absolute path; cd there first), on the local branch
`B29-throughput` (cut from `feature/biomes` at BASE). Commit after each step with the prefix `B29: `. Do not push. Never push or merge
feature/biomes or main. Leave the worktree clean on its branch. **Nothing else runs on the machine while you work** (the owner asked for
these changes once the other work had drained), so you may use all three limited.sh slots; still one heavy command at a time unless a step
says otherwise.
Waiting: run a long command with the Bash tool's `run_in_background: true`, end your turn and wait for its notification; never poll with
`sleep` or `until` loops, never `nohup ... &`. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b29/ (not /tmp), removed at the end.
Environment: prefix every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1; model-gated tests need
VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models. The sandbox refuses the words `eval` and `hash` in a command line (use a wrapper
script for `scripts/content.sh hash`).

Why: the throughput study (docs/research/2026-11-17-ai-development-throughput.md: read sections 2.3 to 2.6, 4 and 5) and the owner's
decision D-game-34 item 50 ("For the meta agent analysis can we kickoff [it] once all current active work is done"): yes to (A) to (D)
below. B28 already split the long test classes and made the fast tier fast (the gate's test phase 26 minutes, from about 50); B24, B24a
and M100 moved timing tests to CPU time. Your row: docs/game/implementation-plan.md section 4.14, B29.

Read first: CLAUDE.md; `.claude/agents/orchestrator.md` ("Machine resources", "The milestone loop", "CI"); `scripts/limited.sh` (all of its
header); `scripts/merge-gate.sh`; `scripts/test.sh`; `docs/process/test-performance-plan.md` (T1 to T11); `.github/workflows/ci.yml`;
the timing tests (search for `CpuTime` and the timing collections in tests/Vocaterra.Game.Core.Tests, tests/Vocaterra.Game.Tests,
tests/Vocaterra.Nlp.Tests).

Rules: **the gate's protection must not weaken**: it still builds with warnings as errors, runs every test (or proves the exact tree was
tested, C), checks format, the content gate and the licence scans. **The machine's safety stays**: limited.sh keeps the slice's caps (14 of
16 cores, 16 GB in all, 10 GB a command, no swap); the owner's limits (five agents, three slots) are what you implement, re-expressed.
Do not edit CLAUDE.md or `.claude/agents/`. Never weaken or remove a test; a timing budget re-set from Release times is twice the measured cost, so it still catches a doubling. Never
edit either implementation-plan.md or either decisions.md (say in the report what they should say). The world, the goldens and the
fixture's state digest do not change.

Deliverables:
1. **(A) Release tests, measured first.** Build the solution in Release and run the full suite in Release once, and in Debug once, both with
   nothing else running (note the load): per-assembly times, the 20 slowest classes, peak memory. Check the 18 procedural goldens,
   `SceneTests.GoldenDigest`, the end-to-end goldens and the fixture pass unchanged in Release. **If Release is at least 1.5 times faster
   and every golden holds,** switch `scripts/test.sh` (fast, full, project), `scripts/merge-gate.sh` and CI's `build-and-test` job to
   Release (build `-c Release -warnaserror`, test `-c Release --no-build`), and re-set every CPU timing test's budget to twice its Release
   cost (say each). If not, say the numbers and leave Debug. Either way the gate still builds Debug once if some check needs it (say).
2. **(B) The slot policy in `scripts/limited.sh`.** Tokens instead of plain slots: five tokens; a full test suite (`scripts/test.sh full`,
   or a `dotnet test` of the whole solution) takes two, every other heavy command one; one token is reserved for the gate (the gate sets
   `VOCATERRA_GATE=1` and may use it; others may not), so a gate never waits behind agents for its first token. The preflight waits on
   CPU pressure (`/proc/pressure/cpu`, `some avg10` under a stated threshold) and free memory (as today), not the 1-minute load. Keep the
   slice and its caps exactly. Keep `VOCATERRA_HEAVY_SLOTS` working as a way to go back to plain slots (`=3` the old behaviour) and say so
   in the header. Test it: a small shell test (bats is not installed; a script under `scripts/tests/` run by hand and from a C# test if
   simple) that starts dummy commands and shows the counting, the reservation and the release on exit; and one real check: two full suites
   and a build at once stay within the slice.
3. **(C) The gate skips a test run already done on the exact tree.** `scripts/test.sh full` writes a stamp when the whole suite passes:
   the tree hash (`git write-tree` of a clean tree, or refuse to stamp a dirty one), the configuration, the test counts and the date, in
   a folder outside the repository (`~/.cache/vocaterra/test-stamps/`). After merging, `merge-gate.sh` computes the merged commit's tree;
   if a passing stamp exists for exactly that tree and configuration, it skips the test phase with a clear line in its log (and still
   builds, checks format, the content gate and the licences); otherwise it runs the suite as today. A merge that changed anything gives a
   new tree, so the skip applies only when the branch already held feature/biomes' head. Test it on a throwaway branch.
4. **(D) A test-time report in the gate.** After the test phase, print each assembly's time and its largest class's time next to the last
   gate's (kept in `.claude/gate-logs/`), and a warning (not a failure) when the suite grew by more than 10 percent or a class passed 5
   minutes.
5. **Hot spots** (the study's item 6): the save-v9 fixture stops pinning the content bundle's hash. The tests that read it check its
   `bundleHash` against `content/game-v1/manifest.json` (or ignore it with a comment) while the state digest stays pinned. A small wrapper
   `scripts/rehash-bundle.sh` runs the content hash and validate in one step (outside the sandbox's word filter: name it so the command
   line does not contain the refused word). Say how many files a content edit now touches.
6. **Two small test fixes:** `JobPoolTests.Given_APoolWithWorkers_When_ItIsResized_Then_ThreadsStartAndEndWithoutLosingAJob` counts the
   threads before they start (it failed twice under load: "Range (2 - 8) Actual 1"): wait on the condition, not a sleep. And name
   `src/Vocaterra.Game/Streaming/FileLodPersistence.cs` in `ArchitectureRulesTests.OnlyTheRealFrameClockReadsATimer`'s allow-list with its
   reason (B07d's fsync timer reads `Environment.TickCount64` to pass it; say whether the test should catch `TickCount64` too).
7. **Documents:** docs/process/test-performance-plan.md (a T12 section with the numbers) and the headers of the three scripts. Do not
   edit CLAUDE.md or `.claude/agents/orchestrator.md`: put in your report the exact replacement text for CLAUDE.md's "Machine resources"
   line and the orchestrator's "Machine resources" and "The milestone loop" items (the orchestrator applies them).
8. **Measured at the end:** the gate's time on a real merge of your branch run by you as a dry run of `merge-gate.sh` into a throwaway
   copy of feature/biomes (`GATE_TARGET` a scratch branch, never feature/biomes), and the full suite's time.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (COUNT passed on feature/biomes at your base, plus your new tests);
format clean; content validate; the licence scans. Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, half a page: branch, head commit, worktree; A's numbers and whether you switched; B's policy and its test; C and its test; D's
report; the hot spots; the two test fixes; the documents changed; the gate's and suite's times before and after; what the plans and
decisions should say; anything left. No file dumps or long logs.
