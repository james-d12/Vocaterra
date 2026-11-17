M99 report (agent a2f52aa0714ddc14d, Sonnet 5.5 medium, 2026-11-17 about 13:50 UTC)

Branch `M99-silent-h`, head 96c6330b, worktree `/home/james/Dev/Vocaterra/.claude/worktrees/m99`, clean.
Cause: in «ah hambrientos» the h ending «ah» and the h starting «hambrientos» both insert an [h] at the same phone index, so
`PredictedErrorVariants` makes two variants with identical phones and different spans; `PredictedErrorMatcher` checked each variant
against the heard insertions without using one up, so both matched. Fix (`Pronunciation/PredictedErrorMatcher.cs`): a heard insertion is
claimed by one variant only, variants taken in text order: one extra [h] gives one `pron.silent_h`, at the earlier word; two give two.
No public API change. Oracle (`PronunciationFuzzTests`): an [h] injected at «hambrientos» gives the same heard phones as at «ah», so the
oracle expects, among injectable variants with the same tag and phones, the earliest span; still exactly one tag with the right tag,
phones and span (the orchestrator reviewed it: a necessary correction, not a weakening). Tests: four facts in `GreedyPhoneScorerTests`.
Three fresh fuzz runs 138/138; the seed passes on replay. Full suite 12,535 passed, 0 failed; goldens unchanged; format clean.
Note (orchestrator): preferring the word whose h is word-initial (the usual learner error) would read better in feedback than the earlier
word; a later refinement if the owner wants it.
