You are implementing B29b, "No wall-clock asserts", for the Vocaterra repository (C#, .NET 10). This milestone changes tests (and, only
where a test needs it, a wall-clock timeout's default in the game's code); it changes no game or core behaviour.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b29b (an absolute path; cd there first), already on the local
branch `B29b-wall-clock`, cut from `B29a-throughput` at BASE `8e372d3e` (B29a, not yet gated, runs beside you; it adds the
`Category=Timing` trait, `scripts/test.sh timing` and Release test builds). Commit after each item that builds and passes its tests, with
the prefix `B29b: `. Do not push. Never push or merge feature/meta_improvements, B29a-throughput or main. Leave the worktree clean on its
branch. Another agent (B29c) works beside you on other files; do not touch the save fixture, scripts/ or .claude/.
Resources, waiting, environment and the sandbox's word filter: exactly as in .claude/briefs/b29a-throughput.md (in the worktree
/home/james/Dev/Vocaterra/.claude/worktrees/meta-improvements), its paragraphs "Resources" and "Environment"; scratch under
/home/james/Dev/Vocaterra/artifacts/tmp/b29b/. Up to three agents share the machine: one heavy command at a time, never raise a cap.
Read `scripts/test.sh`'s header first: tests now build in Release by default; `full` and `project` leave out the timing tests and the fuzz
suite; `timing` runs only the timing tests.

Why: the throughput review docs/research/2026-11-18-throughput-review.md, section 2 (the row on timing tests) and section 4, item 4: wall-
clock asserts are the main cause of failed gates. Your row: docs/game/implementation-plan.md section 4.14, B29b (its first part; the hot
spots and the hook are B29c). New evidence: CI on `main` failed on 2026-11-18 (run 44298795061) on
`Vocaterra.Game.Tests.Streaming.StreamedWorldHostRingTests.Given_ARingPlannedForOnePlaceAndNotFinished_When_TheEyeMovesAndTheHostSettles_Then_EveryFootprintInTheNewRadiusIsReal`:
it ran 7 m 16 s on the GitHub runner (about 4 times slower than this machine) and `Assert.True(host.Settle(...))` was false, because
`Settle` gives up after a wall-clock timeout (`src/Vocaterra.Game/Streaming/ChunkResidency.cs` `Settle`/`SettleNear`, 120 s by default;
`src/Vocaterra.Game/Graphics/Far/FarView.cs` `Settle`, 180 s; follow the host's own `Settle`). The same test passed in PR #70's CI run.

Read first: CLAUDE.md; the review's sections named; the two `TimingCollection`s (tests/Vocaterra.Game.Core.Tests/TestSupport/TimingCollection.cs,
tests/Vocaterra.Nlp.Tests/TimingCollection.cs) and one class in each to see how a CPU-time budget is measured (search `CpuTime`) and how
`CI=true` multiplies budgets by 4 (B30); `tests/Shared/TestSpeed.cs`; `ArchitectureRulesTests` in the game's tests (its timer allow-list).

Rules: never weaken a test's meaning or remove one. A test that checks speed holds a CPU-time budget (twice its Release cost) in a
`TimingCollection` with the `Category=Timing` trait; a test that checks an outcome must not fail because the machine is slow. A wall-clock
timeout may stay only as a hang guard (generous, multiplied under `CI=true` like the budgets, and its expiry reported as a hang, not as a
wrong result), never as the thing the test asserts. Do not edit CLAUDE.md, `.claude/`, scripts/, `.github/workflows/`, either
implementation-plan.md or either decisions.md. The world, the goldens and the fixture do not change.

Deliverables:
1. **Find every wall-clock assert in the tests**: an assert on a `Stopwatch`, `MonotonicClock`, `DateTime`/`Environment.TickCount*`
   difference, or on the bool of a wall-clock-limited wait (`Settle`, `SettleNear`, `WaitFor...`, `Task.Wait(timeout)`, `SpinWait.SpinUntil`
   with a timeout), outside the timing collections. The review's candidates (check each): `ProceduralReliefRegionTests` (line about 397, a raw
   `Stopwatch`; failed 2 of 4 gates), `DrainageNetworkTests`, `FarTerrainSamplerTests`, `GuideTurnTests`, `WorldFactsTests`,
   `SpeechUxFrontendTests` (11 asserts), `LintTests`, plus the `Settle` callers above. List them with your treatment for each in the report.
2. **Move each**: a speed check becomes a CPU-time budget in the timing collection (budget twice the Release cost, say each); an outcome
   check behind a wall-clock wait becomes a hang guard per the rules (for `Settle`, for example, a timeout scaled under `CI=true` and an
   assert message that says "hung after N s"), or waits on a count of steps instead of time if the code allows it without a behaviour change.
3. **An architecture test** that fails when a test file outside the timing collections asserts on a wall-clock measurement (a source scan
   like the existing architecture tests is fine; an allow-list with a reason per entry for the hang guards). It must catch a re-added raw
   `Stopwatch` assert in `ProceduralReliefRegionTests` (show it does, then revert).
4. **Run** each moved test 3 times under load (`scripts/test.sh project <project> --filter ...` while a full run of another project goes:
   say what you did) and once with `CI=true`; then `scripts/test.sh full` once and `scripts/test.sh timing` once.

Gate (from your worktree root): scripts/limited.sh dotnet build Vocaterra.sln -c Release -warnaserror -m:4 (0 warnings);
scripts/test.sh full and scripts/test.sh timing pass (give the counts); scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes
clean. Do not commit bin/, obj/ or artifacts/.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDc2yqvjCy1ntkujnVS
Report, one page: branch, head commit, worktree; the list of asserts found and the treatment of each (budgets: Release cost and new budget);
the architecture test and its proof; the runs under load and with CI=true; test counts; any production default changed and why; what the
plan should say; anything left. No file dumps or long logs.
