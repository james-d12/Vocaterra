You are implementing the core milestone **M94, the resolver ranking a disallowed grammar wrongly**, in the Vocaterra repository (C#, .NET 10). The
language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must not
change. Do the work yourself; do not spawn subagents. Commit prefix `M94: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m94`, already on the branch `M94-resolver-ladder` (from
`feature/german`; no build yet). cd there first; never edit another worktree. Commit after each step that builds and passes its tests;
never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and
finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:**
every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m94/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m94/` for your logs, **11196 passed, 3 skipped** (plus this failure) as the count before
you, and these changes: skip the scripted runs (no game file changes); add `bash scripts/license-scan.sh --offline` (PASSED) and the core
demo (`docs/process/milestone-briefs.md`, "Repository facts": exactly `conj.person` and `noun.number`, through a wrapper script outside
the repository because the sandbox refuses the word `eval`). Read `docs/nlp/changing-the-core.md` first. Edit only `src/Vocaterra.Nlp`,
`src/Vocaterra.Nlp.Es`, their tests, the fuzzing project's README (its bugs-found list) and `docs/nlp/` (not the implementation plan, not
`decisions.md`). Never weaken a fuzz oracle (O7 stays exactly as it is); goldens are not re-recorded unless the fix moves them and you
explain why each moved; no `System.Random`, no clock.

## The bug
Found by M38's merge gate (2026-11-09); the row: `grep -n '^| M94 ' docs/nlp/implementation-plan.md`.
`IntentResolverFuzzTests.Resolve_NeverCrashes_AndRespectsTheLadderAndTheAllowedIntents`, reproduce with `CsCheck_Seed=6-6oGJLVh3o1`:
the text "yo vendo partos por favor" at the prompt `lesson-luz-will` (Constrained, Beginner, allowed intents `ask_help`, `state_action`,
`dont_know`, agent `fisher`). O7 reports: the analyzer matched `state_action`'s grammar `… [VERB:vender] [ART]? [NUM:qty]? [NOUN:item] …`,
which `offer_item`'s grammar `(yo)? (te|le)? vendo [ART]? [NUM:qty]? (bloque de|bloques de)? [NOUN:item]` outranks, but the resolution is
Grammar `state_action`. Read O7 (`tests/Vocaterra.Nlp.Fuzzing.Tests/Oracles/LanguageOracles.cs`, `CheckResolution`) to see exactly which
ladder rule it states, and the resolver's ladder (`IntentResolver`, `SlotGrammarMatcher`, `CanMatch`).

**First suspect:** the line ends in the polite phrase `por favor`, and M88 (merged at `b8897ef2`) changed polite handling in
`SlotGrammarMatcher`, `CanMatch` and `IntentResolver` (`PolitePositions`, `MaxPoliteTokens`). Bisect first: in a scratch worktree
(`git worktree add /tmp/claude-1000/m94/pre 8a98162c`, removed afterwards), build the fuzzing project and replay the seed. If it passes
there, M88 introduced it; say which change. If it fails there too, it is older; find the cause anyway.

## What to do (each committed)
1. **A failing regression test** in the owning module's test project (not only the fuzz seed): the case above as a `[Fact]`, plus the
   neighbours (the same line without `por favor`; with `por favor` leading; an allowed grammar that truly outranks; the polite phrase on
   another intent).
2. **Fix at the source**, so the ladder O7 states holds; Spanish behaviour on the goldens unchanged (if a golden moves, stop and explain
   why the old output was wrong before re-recording anything).
3. **Prove it:** the seed passes; the fuzz suite runs 3 times green; goldens and `SpanishIdentityTests` unchanged.

## Report (half a page)
Branch, final SHA, worktree clean; the bisect result; the cause; the fix; the tests; test counts (`scripts/test.sh full`); the gate's
results; the fuzzing README entry.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
