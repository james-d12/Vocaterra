You are implementing B28, "Test speed", for the Vocaterra repository (C#, .NET 10). The game is Vocaterra.Game.Core (the engine-free
logic), Vocaterra.Game (MonoGame) and Vocaterra.Host; the language core (Vocaterra.Nlp*) is not yours. This milestone changes how the
game's tests are organised and run so the full suite is much shorter, **without removing or weakening any test**.
Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b28 (an absolute path; cd there first), already on the local branch
`B28-test-speed` (cut from `feature/biomes` at 271a5882). Commit after each step that builds and passes its tests, with the prefix `B28: `.
Do not push. Never push or merge feature/biomes or main. Leave the worktree clean on its branch.
Resources and waiting: every dotnet restore, build, test and format through scripts/limited.sh (builds `-m:4`, tests `-m:2`; the script's
caps: 10 GB a command, 16 GB in all); one heavy command at a time, no `&`. Run a long command with the Bash tool's `run_in_background: true`,
end your turn and wait for its notification; never poll with `sleep` or `until` loops and never use `nohup ... &`. Other agents build at
the same time: expect waits. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b28/ (not /tmp), removed at the end. Environment: prefix
every command with PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1. No game windows (NamePlatesTests starts a game
process itself; that is fine inside a test run).

Why: the throughput study (docs/research/2026-11-17-ai-development-throughput.md; read its sections 2.3, 2.4, 2.5 and 4, items 1, 3, 9
and 10) found the full suite grew from 7 to 50 minutes in two days: `StreamedWorldHostTests` (13 tests run one after another, 45 minutes),
`FarViewTests` (33), `TerrainTuningTests` (23), `LodStoreTests` (12), `NamePlatesTests` (9), and Game.Core.Tests limited to three threads
when a command's cap was 6 GB (it is 10 GB now). xUnit 2 runs a class's tests one after another and classes in parallel; the "fast" tier
still holds 98% of Game.Tests' time because the slow classes are not marked Slow. Your row: docs/game/implementation-plan.md section 4.14.

Read first: CLAUDE.md; docs/process/test-performance-plan.md (the tiers, `TestSpeed`, the timing collection); scripts/test.sh; the test
projects' xUnit settings (`xunit.runner.json`, assembly attributes such as `CollectionBehavior`, `MaxParallelThreads`); the slow classes
named above and in the study's table (section 2.4).

Rules: a game milestone edits only the game projects, their tests, content/game-v1, content/schema, assets/ and docs/game/ (and
docs/process/test-performance-plan.md for the tiers) and takes the core as it is. Never edit .github/workflows/, either
implementation-plan.md or either decisions.md. **No test is removed, skipped, shortened in what it checks, or given a looser bound**; a
test split into several classes keeps every assertion. Do not change scripts/merge-gate.sh or scripts/limited.sh (the owner decides
those). The world does not change (goldens and fixture stay).

Deliverables:
1. **Measure first:** the full suite's per-assembly times and the 20 slowest classes on your base (one `scripts/test.sh full` with TRX or
   the per-test times; `scripts/test-timings.py` may help). Say the machine's load at the time.
2. **Split the long classes** so xUnit runs their tests in parallel: `StreamedWorldHostTests`, `FarViewTests`, `LodStoreTests`,
   `ChunkResidencyTests`, and any other class over about 5 minutes, into several classes each (by theme), sharing setup through a fixture
   where it is safe; tests that must not run together (a shared folder, a process-wide setting) stay in one collection, and say why.
3. **Threads:** raise Game.Core.Tests' parallelism (3 today) to 6, and Game.Tests' if it is limited, after measuring the run's peak memory
   (limited.sh prints it): keep a full run under 10 GB. If a test then fails because of shared state, fix the test's isolation, never by
   lowering the count again without saying why.
4. **NamePlatesTests (9 minutes, starts a game process):** find why it takes so long and shorten it without losing what it checks (for
   example one game process for the class instead of one a test).
5. **The tiers:** mark the slowest classes `[Trait(TestSpeed.Trait, TestSpeed.Slow)]` (about the 20 slowest, each over about a minute) so
   `scripts/test.sh fast` takes a few minutes on this machine; the full tier still runs everything. Say the fast tier's time before and
   after. Update docs/process/test-performance-plan.md.
6. **Measure again:** the full suite's per-assembly times and wall time before and after (interleave or note the load; other agents run),
   and the fast tier's.

Gate: build with -warnaserror (0 warnings); scripts/test.sh full (12,568 passed on feature/biomes at your base: the same count after the
split, since no test is added or removed unless you say so); format clean. A timing test that fails under load: rerun it alone and say so.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6

Report, a few lines: branch, head commit, worktree; the classes split; the thread counts and the peak memory; NamePlates; the fast tier;
the suite's time before and after per assembly; the test count; anything left. No file dumps or long logs.
