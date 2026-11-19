# I-fuzz report (the CI fuzz failure on PR #71)

The agent's report, saved by the orchestrator (2026-11-19). Sonnet 5.5 at high effort; brief `i-fuzz-cuanto.md`. Gated at `a9c4d923`
(13,981 passed, 9 skipped; timing 23); the agent ran `scripts/test.sh ci` (with the fuzz suite): all pass (Fuzzing 142, Nlp 4,202, Nlp.Es
2,317, Golden 17, Game.Core 5,411, Game 1,836, Host 189, WorldPreview 32).

CI failed `HomographRerollTests.TheCuantoPatternNoLongerRealisesANounThatReadsAsADeterminer` (seed 1892: 'sueño biblias 1 cuanto transporta
por favor'). Cause: a generator gap the inventory content exposed (no content entry wrong): the lexicon has `cuanto` as a noun and as the
quantifier adjective (cuánto); `LexiconCatalog.ReadsAsDeterminer` excluded only `DeterminerAdjectiveLemmas`, which lacks it, and M97's
re-roll fires only before a noun, so the noun stayed in the pool; the series' new nouns changed the draws so seed 1892 reached it. Fix
(5cb21652, core test tooling only): `ReadsAsDeterminer` also treats an adjective reading with lemma `cuanto`/`cuánto` as a determiner, so
`NounExclusion` drops it everywhere; new test `TheNounCuantoIsNoLongerInTheNounPool`; the oracle unchanged, no seed skipped; one lemma of
coverage lost. Lesson: since B29a the local gate skips the fuzz suite, so a content change can fail CI's fuzz run only.
