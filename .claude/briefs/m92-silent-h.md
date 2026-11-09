You are implementing the core milestone **M92, the silent-h scorer double report**, in the Vocaterra repository (C#, .NET 10). The
language core is `src/Vocaterra.Nlp`, `src/Vocaterra.Nlp.Es` and the `Vocaterra.Nlp.*` tests and tools; the game is not yours and must not
change. Do the work yourself; do not spawn subagents. Commit prefix `M92: `.

## Where to work, and committing
Work only in the git worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m92`, already on the branch `M92-silent-h` (from
`feature/german`; no build yet). cd there first; never edit another worktree. Commit after each step that builds and passes its tests;
never end your turn with uncommitted work. Do not push; never push or merge `feature/*` or `main`. Run everything in the foreground and
finish in one turn (no background commands, monitors or wait loops). Never use `git stash`. **Other sessions build on this machine:**
every heavy command goes through `scripts/limited.sh`; if it waits for a slot, let it wait.

## The rules, environment, resources and gate
Read `/home/james/Dev/Vocaterra/.claude/worktrees/m92/.claude/briefs/w07-station-view.md`, the sections **Output and context**,
**Resources**, **Environment** and **Gate**: they apply to you, with `/tmp/claude-1000/m92/` for your logs, @@COUNT@@ as the count before
you, and these changes: skip the scripted runs (no game file changes); add `bash scripts/license-scan.sh --offline` (PASSED) and the core
demo (`docs/process/milestone-briefs.md`, "Repository facts": exactly `conj.person` and `noun.number`, through a wrapper script outside
the repository because the sandbox refuses the word `eval`). Read `docs/nlp/changing-the-core.md` first. Edit only `src/Vocaterra.Nlp`,
`src/Vocaterra.Nlp.Es`, their tests, the fuzzing project's README (its bugs-found list) and `docs/nlp/` (not the implementation plan, not
`decisions.md`). Never weaken a fuzz oracle (O9 stays exactly as it is); goldens are not re-recorded unless the fix moves them and you
explain why each moved; no `System.Random`, no clock.

## The bug
Found by M36's merge gate (2026-11-09); the row: `grep -n '^| M92 ' docs/nlp/implementation-plan.md`.
`PronunciationFuzzTests.SpokenPhrases_ScorePerfectly_OrYieldExactlyTheInjectedPredictedError`, reproduce with
`CsCheck_Seed=8DQuns3HnRh8`:
```
text="ah humana" dialect=1 error=378 shape=FrameShape { FramesPerPhone = 3, BlankFramesBetween = 1, EdgeBlankFrames = 0 } mode=Greedy difficulty=Advanced
[O9] Injected pron.silent_h at TextSpan { Start = 1, Length = 1, End = 2 } (heard [a h u m a n a] for [a u m a n a]) but the scorer
reported [pron.silent_h@{Start = 1} '' -> 'h'; pron.silent_h@{Start = 3} '' -> 'h'].
```
One pronounced h was injected (the h of "ah"); the learner said "humana" correctly, yet the scorer also blames the h of "humana". The
likely cause is the alignment of one heard [h] between two silent-h letters across a word boundary (the heard phones `a h u` fit both
"a(h)" and "(h)u"); the code is around `src/Vocaterra.Nlp/Phonology/PredictedErrorVariants.cs`,
`src/Vocaterra.Nlp.Es/Phonology/` (`WordPipeline`, `PhoneMapper`, `SpanishG2p`) and the predicted-error matcher. Find the real cause first.

## What to do (each committed)
1. **A failing regression test** in the owning module's test project (not only the fuzz seed): the case above as a `[Fact]`, plus the
   neighbours (h injected only on "humana"; both; neither; "ah" alone; a word-internal pair if one exists in the lexicon).
2. **Fix at the source**: one heard phone is blamed on at most one letter; which one is decided deterministically and by the evidence
   (prefer the span that the injected variant's alignment chose; say how).
3. **Prove it:** the seed passes; the fuzz suite runs 3 times green; goldens and `SpanishIdentityTests` unchanged (or explained).

## Report (half a page)
Branch, final SHA, worktree clean; the cause; the fix; the tests; test counts (`scripts/test.sh full`); the gate's results; the fuzzing
README entry.

Commit messages end with
```
Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
```
