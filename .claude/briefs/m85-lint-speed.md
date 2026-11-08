You are implementing the core milestone **M85, the lint's speed again**, in the Vocaterra repository (C#, .NET 10). The language core is
`src/Vocaterra.Nlp` (the engine), `src/Vocaterra.Nlp.Es` (the Spanish pack) and the `Vocaterra.Nlp.*` tests and tools; the game
(`Vocaterra.Game*`, `Vocaterra.Host`) is not yours and must not change. Do the work yourself; do not spawn subagents. Commit prefix `M85: `.

## Where to work, and committing
Work only in the git worktree `/home/user/Dev/Vocaterra/.claude/worktrees/m85`, already on the branch `M85-lint-speed` (from
`feature/bread-pilot` at its tip; it has no build yet). cd there first; never edit `/home/user/Dev/Vocaterra` or another worktree
(`h12` runs beside you; `adaptive`, `german` are not yours). Commit after each step that builds and passes its tests; never end your
turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and finish in one
turn (no background commands, monitors or wait loops).

## The rules, environment, resources and gate
Read `/home/user/Dev/Vocaterra/.claude/briefs/w07-station-view.md`, the sections **Output and context**, **Resources**, **Environment**,
**Rules** and **Gate**: they apply to you unchanged, with `/tmp/claude-1000/m85/` for your logs, and **10393 passed, 3 skipped** as the
count before you. Another agent builds beside you: every heavy command goes through `scripts/limited.sh` (it shares two slots); check
`df -h /home` before a build and stop and report if it falls under 6 GB. Build the core first (`Vocaterra.Nlp.slnf`); the whole solution
only for the final gate. Do not edit `content/game-v1`, `docs/nlp/decisions.md` or either implementation plan; say in the report what they
should say. Read `docs/nlp/changing-the-core.md` before changing the engine.

## The problem
- CI on PR #40 (2026-11-08) failed one test: `tests/Vocaterra.Nlp.Tests/Content/LintTests.cs`, `TheBundleLintTakesUnderFiveSeconds`,
  "the lint took 00:00:05.5409105" (line 190). Everything else passed. The GitHub runner is about four times slower than this desktop.
- The same test failed once before: **M60** (`grep -n '^| M60 ' docs/nlp/implementation-plan.md` and its §5b entry, `grep -n 'M60, the
  lint' docs/nlp/implementation-plan.md`). M60 found the confusion check analysing each example once per rival intent and made
  `LintContext` read each text once, match each grammar once and complete once per winning grammar: the lint went from 1610 ms to 398 ms
  (Debug) with byte-identical output. It kept the 5 s budget.
- Since then the bundle grew (the village's phases, the bread pilot's lessons, W04b) and the core changed (M74/M75's grammar rung "reads
  the whole catalog", M80 to M84). Something made the lint about three times slower again.
- The row: `grep -n '^| M85 ' docs/nlp/implementation-plan.md`.

## What to do (each committed)
1. **Measure.** The lint's time on the shipped bundle now (Debug, the test's way: the bundle read before the clock), several runs, and
   where it goes (per check; a temporary stopwatch per check or `dotnet-trace` only if already installed). Find the commit or commits
   that made it slower (`git log` on `src/Vocaterra.Nlp/Content/Lint*` and what it calls; a quick timing at M60's merge `95bd7bf` and at
   a few points after is fine if builds allow). Say whether the growth is the bundle's size, a check's cost per example, or both.
2. **Fix it at the source**, as M60 did: no repeated analysis, no repeated grammar or catalog scans, caches scoped to one lint run. The
   lint's output must stay byte-identical: compare `Format()` and `ToJson()` on the bundle at levels `a1` and `a2` (and the content gate's
   `lint content/game-v1 --strict --level a2 --fail-on-level --host-pools 'guide.*'`) before and after.
3. **The budget stays 5 s.** Target the Debug time on this desktop at about 1.25 s or less (a four-times margin for the runner). Do not
   widen the budget, mark the test slow or skip it. If the target is out of reach without changing what the lint reports, stop at what
   you reach and say why in the report.
4. **A regression check as a C# test** if one fits (for example, a check that counts analyses per lint run and pins it to the number of
   distinct texts), not a script.

## Report (half a page)
Branch, final SHA, worktree clean; the lint's time before and after (Debug, several runs) and per check; the cause (which commit, which
check); what you changed; the output comparison; test counts before and after (the whole suite once with `scripts/test.sh full`); what the
orchestrator should record in §5 and §5b; anything left.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
