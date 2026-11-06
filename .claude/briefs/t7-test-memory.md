You are implementing **T7, the game tests' memory**, a follow-up of the test-performance plan (`docs/process/test-performance-plan.md`) in the Vocaterra repository (C#, .NET 10; the language core `Vocaterra.Nlp*`, the game `Vocaterra.Game*`, `Vocaterra.Host`). Do the work yourself; do not spawn subagents.

## Why
`Vocaterra.Game.Core.Tests` (2372 tests) now peaks at **5.4 GB** when run alone (`limited.sh` prints the peak of the command's cgroup). With xUnit's `MaxParallelThreads=2` it still peaks at 4.8 GB in the same 72 s, so a few tests or caches hold the memory, not the parallelism. At test-performance phase T1 (2026-10-29) the same assembly peaked at 2.80 GB. `scripts/test.sh full` runs two test assemblies at once (`dotnet test -m:2`) inside one `limited.sh` command capped at 6 GB, so when Game.Core.Tests overlaps a second assembly (for example the fuzz suite) the kernel's OOM killer ends the run (exit 143, "Test host process crashed"; journal: `oom-kill` in `vocaterra-heavy.slice`). On 2026-11-06 this failed a content loop twice in a row; on 2026-11-05 it killed one of N09's runs. The merge gate passes or fails by which assemblies happen to overlap. The owner's rule: the fix is lower memory or lower parallelism, **never a higher cap**.

## Where to work, and pushing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/t7`, already on the branch `T7-test-memory` (from `feature/adaptive_design`). cd there first; edit nothing elsewhere.
- **Commit often, push never** (the owner, 2026-10-30 and 2026-11-05): commit after each step that builds and passes, prefix `T7: `. Never push anything and do not merge. Finish with the worktree clean on your branch.

## The work
1. **Measure, then find the holders.** Measure the peak of each test assembly alone (`scripts/limited.sh dotnet test tests/<project> --no-build -v q`; the last line is the peak) and of `scripts/test.sh full`. Then find what Game.Core.Tests holds: run it by namespace or class (`--filter "FullyQualifiedName~Vocaterra.Game.Core.Tests.Village"` and so on) and compare peaks; look at the static caches in its test support (`RouteScene`, `TestGame`, `VoxelTestSupport`, anything `static`/`Lazy`/`ConcurrentDictionary`/`ConditionalWeakTable` under `tests/Vocaterra.Game.Core.Tests/TestSupport/` and `tests/Shared/`), the number of distinct seeds and layouts the tests build (T1 counted ten seeds at about 38 MB each), the village scene's size, sessions that keep a whole scene alive in a static, and tests that build a fresh scene or session per case instead of a clone. `dotnet-counters` and `dotnet-gcdump` are not installed; do not install global tools. Measuring by filter is enough; a short sampling of the test host's `VmHWM` in `/proc/<pid>/status` is fine too.
2. **Bring Game.Core.Tests under about 3.5 GB alone**, and `scripts/test.sh full` comfortably under its 6 GB cap (report the peaks). Prefer the test side: share one built scene per seed (clones, as T1), cut the number of seeds that need a full route build where a test only needs *a* seed, release per-test sessions, move a heavy class into a collection that runs after the rest (as T1 did with `TimingCollection`) only if that is what fits. Production code may change only where a test helper needs an `internal` hook (as T1's `Clone()`), never the game's behaviour.
3. **If memory alone cannot get there**, change `scripts/test.sh` so the full tier cannot put Game.Core.Tests beside another heavy assembly (for example: Game.Core.Tests on its own, then the rest at `-m:2`), and measure the wall time against today's. Keep `scripts/test.sh`'s interface (`fast|full|project`, the options after the mode, the trx results folder) and the merge gate unchanged.
4. **Keep every test and every assertion.** No test deleted, skipped or weakened; the counts stay equal (9546 passed, 3 skipped with models on `feature/adaptive_design` at fe986862 or later). The timing tests keep their budgets.
5. Add the T7 numbers (before and after, per assembly and for the full run, and the wall time) to the end of the "Results" section of `docs/process/test-performance-plan.md` and a "### T7" section after T6 saying what was done; this one document you may edit.

## Output and context (the cost of a milestone is its turns times its context)
- Send every heavy command's output to a log file under `/tmp/claude-1000/t7/`, print at most 30 lines.
- Keep `sed -n` ranges under about 150 lines; `grep -n` first.
- Wait for a long command in the foreground. No background commands, no Monitor, no wait loops left behind.
- **Disk:** about 12 GB free; build only what you need while iterating.

## Resources (the desktop has crashed from overload)
- Every `dotnet restore|build|test|format|run` and `scripts/content.sh` call through `scripts/limited.sh` (`-m:4` builds, `-m:2` tests). `scripts/test.sh` and `scripts/content-loop.sh` run directly. Never start two heavy commands at once.
- You are the only agent building; the orchestrator may run one game screenshot now and then. A command killed with 137 or 143 met a cap: that is the subject of this milestone, so record it; never raise a cap in `scripts/limited.sh`.

## Environment
- Prefix every command inline with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_NOLOGO=1 VOCATERRA_MODELS_DIR=$HOME/.local/share/vocaterra/models`.
- The sandbox refuses a command line containing the words `eval` or `hash`; use a wrapper script under `/tmp/claude-1000/t7/`.

## Rules
- **What this milestone edits:** the game's test projects (`tests/Vocaterra.Game*.Tests`, `tests/Vocaterra.Host.Tests`, `tests/Shared` where only the game's tests use the file), `internal` test hooks in `src/Vocaterra.Game.Core` if needed, `scripts/test.sh` (step 3 only), and `docs/process/test-performance-plan.md`.
- The core (`src/Vocaterra.Nlp*`, `tests/Vocaterra.Nlp.*`, `tests/golden`) as it is: if a core test assembly is the heavy one, report it with its numbers; do not patch it.
- Do not edit either `implementation-plan.md`, either `decisions.md`, `scripts/merge-gate.sh`, `scripts/limited.sh`, `content/`, anything under `.claude/` or `.github/workflows/`.
- A regression check is a C# test, never a script with text baselines.

## Gate (yours, before reporting)
- `scripts/limited.sh dotnet build Vocaterra.sln -warnaserror -m:4`, 0 warnings.
- `scripts/test.sh full` **three times**, all passing each time, with the peak of each run; the same counts as before.
- `scripts/limited.sh dotnet format Vocaterra.sln --verify-no-changes`.
- CI's case: the full suite once without models (`VOCATERRA_MODELS_DIR` an empty folder).
- Do not commit `bin/`, `obj/` or `artifacts/`; `git status` clean at the end.

## Commit
Each message ends with:
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01F9jKDeFsoWE1itA1oaKnYf
```

## Report (one page, under 500 words)
- The branch and the final commit SHA (local).
- The peaks before and after: each assembly alone, the full run (three runs), and the wall time.
- What held the memory (with the measurement that showed it) and what you changed (files, types).
- The test counts before and after; CI's case; anything left undone and why.

Before your final message, make sure no process you started is still running (`pgrep -fa 'dotnet|testhost|content-loop'`, kill only your own).
