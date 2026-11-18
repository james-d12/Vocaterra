You are implementing B30, "CI ready for the merge into main", for the Vocaterra repository (C#, .NET 10). Do the work yourself; do not
spawn subagents. Where to work: the worktree /home/james/Dev/Vocaterra/.claude/worktrees/b30 (absolute; cd there first), on the local
branch `B30-ci-ready` (cut from `feature/biomes` at 21f632e0). Commit after each step, prefix `B30: `. Do not push; never push or merge
feature/biomes or main. Scratch under /home/james/Dev/Vocaterra/artifacts/tmp/b30/, removed at the end.
Resources and waiting: every dotnet command through scripts/limited.sh (builds `-m:4`, tests `-m:2`); one heavy command at a time, no
`&`; run long commands with the Bash tool's `run_in_background: true` and wait for the notification; never poll with `sleep` or `until`
loops, never `nohup ... &`. Other agents build at the same time. Environment: prefix every command with PATH="$HOME/.dotnet:$PATH"
DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1.

Why: the owner wants feature/biomes merged into main by the morning of 2026-11-18 (he merges the pull request; main has not moved since the
branch point). A pull request runs `.github/workflows/ci.yml` (**named here: you may edit it**): `build-and-test` (restore, build
`-warnaserror`, `dotnet test Vocaterra.sln --no-build` with no models, the offline grammar replay) and `languagetool`, each with
`timeout-minutes: 30`. Two problems: (1) the full suite now takes about 26 minutes on this 16-core machine (the gate on 2026-11-17, with
B28's split), and the GitHub runner (4 cores) is about four times slower (orchestrator.md "CI"; M60), so the job times out; (2) the CPU
timing tests (tests/*/TestSupport/CpuTime.cs and the timing collections: `ProceduralChunkTimeTests`, `StreamedWorldTimeTests`,
`PristineSnapshotTimeTests`, `ValleyUndergroundTimeTests`, `Seed42SceneBuildsWithinItsCpuBudget`, the core's `GeneratedLexiconTests`)
have budgets of twice this machine's cost, so they fail on the slower runner. The orchestrator cannot read CI results (403), so you must
make CI pass by measurement here. Read: docs/process/test-performance-plan.md (T1 to T11), the timing tests, `.claude/agents/orchestrator.md`
"CI", docs/game/building-and-testing.md.

Rules: **do not weaken what CI checks**: it still builds with warnings as errors and runs every test (the full suite, no Slow filter),
unless a step below says otherwise; do not remove or loosen a test locally. Do not edit scripts/merge-gate.sh, scripts/limited.sh, either
implementation-plan.md or either decisions.md, CLAUDE.md or .claude/agents/.

Deliverables:
1. **Timing tests on a slow runner:** a single factor applied to every CPU timing budget when the tests run on CI (`CI=true` or
   `GITHUB_ACTIONS=true`; default 1 locally, 4 on CI, overridable by `VOCATERRA_TIMING_FACTOR`), in one place each test project uses
   (the core and the game's CpuTime helpers); the local budgets unchanged. Say each test's local budget and CI budget. A test that the
   factor reads the environment.
2. **CI's time:** estimate the runner's time for the suite by running it here as CI does (no models: `VOCATERRA_MODELS_DIR` an empty
   folder; Debug or Release as CI builds; restricted to 4 cores with `taskset -c 0-3` inside limited.sh), and set `timeout-minutes` for
   `build-and-test` with a wide margin (at least 1.5 times the estimate, at most 240). If a Release build would bring CI's time under
   the margin and every golden passes in Release, use `-c Release` for CI's build and test (say the numbers); otherwise keep Debug.
3. **Anything else CI would trip on:** tests that need a display, a GPU (`VOCATERRA_GL_TESTS` stays unset on CI, so live-GL tests skip),
   the far store folder (`VOCATERRA_LOD_DIR`), game processes (NamePlatesTests starts the game: does it skip or pass headless on CI?),
   the models; check by reading the tests' guards and by the 4-core, no-model run above. Fix any that would fail on CI (skip with a clear
   reason only where the test needs hardware CI lacks, as the live-GL tests already do).
4. docs/game/building-and-testing.md: a short "CI" note (the factor, the timeout, what skips on CI).
Gate: build -warnaserror (0 warnings); scripts/test.sh full (12,766 passed on feature/biomes at your base, plus your tests); format clean.
Commit messages end with
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDbkPTqABsu7sLVD9h6
Report, a few lines: branch, head, worktree; the factor and each budget; the estimated CI time and the timeout; Release or Debug; anything
else fixed for CI; the suite's count.
