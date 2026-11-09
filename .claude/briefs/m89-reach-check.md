You are implementing the core milestone **M89, the open-fallback fuzz test's reach check made deterministic**, in the Vocaterra repository
(C#, .NET 10). The language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` tests and tools; the game is
not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `M89: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m89`, already on the branch `M89-reach-check` (from
`feature/german`; no build yet). cd there first; never edit another worktree. Commit after each step that builds and passes its tests;
never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and
finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:**
every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m89/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m89/` for your logs, **10515 passed, 3 skipped** as the count before
you, and these changes: skip the scripted runs (no game file changes); add `bash scripts/license-scan.sh --offline` (PASSED). Edit only
`tests/Vocaterra.Nlp.Fuzzing.Tests` and, if it lists the test's known limits, its `README.md` and `docs/nlp/fuzzing-plan.md`. Never weaken
the oracle; never lower a threshold to make it pass; no `System.Random`, no clock.

## Why
The row: `grep -n '^| M89 ' docs/nlp/implementation-plan.md`. `PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone_And
OtherwiseKeepsTheBetterOfTwoReadings` (`tests/Vocaterra.Nlp.Fuzzing.Tests/PipelineFuzzTests.cs`, about line 157) asserts that each of ten
fallback branches is reached at least 5 times in 300 randomly seeded iterations. Once, `fallback:second-chosen` came up 3 times (8 to 12
in four reruns): a gate failure by chance. A reach miss must mean the generator changed, never luck.

## What to do (each committed)
1. **Find how the seeds are drawn** today and how the stream digests got deterministic in M57 (grep `M57` and `digest` in the fuzzing
   project and `docs/nlp/fuzzing-plan.md`).
2. **Make the reach assertion deterministic**, keeping the oracle (every iteration's property check) exactly as it is and still run on
   random seeds: either a fixed, committed seed set for the reach count (chosen so each branch is reached comfortably above the floor,
   with the margin recorded in a comment), or a generator per branch that targets it. Prefer the smaller change that keeps the random
   oracle's coverage.
3. **Prove it:** run the test 5 times; the reach counts must be identical each run. Say the minimum count per branch and its margin.

## Report (half a page)
Branch, final SHA, worktree clean; what changed and why that choice; the per-branch counts; test counts (`scripts/test.sh full`); the
gate's results.

Commit messages end with
```
Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>
```
