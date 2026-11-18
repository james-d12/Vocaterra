# M96 report (Sonnet 5.5, medium; 2026-11-15)

Branch `M96-porque-noun-reading`, head `88be4b73` (three commits on 6d4d50b4). Agent's first full run: 11,987 passed, 1 failed (the M65
(c) guard test, then fixed), 3 skipped; the fuzz project 126/126 after the fix; format clean; goldens unchanged (17/17).

- Root cause: `AccentVariantChoice` (src/Vocaterra.Nlp/Language/Analysis/), not the lexicon, tagger, agreement or realiser. A typed
  conjunction "needs a word after it"; in «túneles unos porque» `porque` is last, so the chooser fell back to the accented sibling, the
  noun `porqué` (singular); the number check then asked for `porqués` after `unos`, and the recast wrote it.
- Fix: a conjunction or preposition that falls back to a noun sibling stays as typed when the word right before it is a determiner of
  the other number (`DisagreesWithDeterminerBefore`). Every other fallback unchanged; no public API change.
- Tests: `tests/Vocaterra.Nlp.Es.Tests/Analysis/PorqueNounReadingTests.cs` (8: both seeds' inputs and «los porque» give no
  `noun.number`; «vebegoz unos porque» only `vocab.unknown`; unchanged neighbours «quiero pan porque tengo hambre», «no sé el porqué»,
  «no sé el porque», «no sé los porqués», «por qué porque sí»). `NarrowedMutationGuardTests.C_…` (M65 (c), seed `2ldq2rIKLrG2`) asserted
  a non-tense error that was this misreading; it now asserts every error is the tense slip (stricter; the `MutationGen` guard untouched).
  Both seeds pass with their `CsCheck_Seed`.
- Spot-check (CLI eval, before then after): «túneles unos porque» `noun.number` (0.85) to clean (1.0); «vebegoz unos porque»
  `vocab.unknown` + `noun.number` (0.45) to `vocab.unknown` (0.6); «quiero pan porque tengo hambre», «¿por qué? porque sí», «no sé el
  porqué», «no sé el porque», «los porqués» identical (clean).
- Findings entry appended to docs/nlp/findings.md. Plan row text proposed: "M96 done: `porque` with no word after it, after a plural
  determiner, stays the conjunction (`AccentVariantChoice`); seeds `dTc3W4dw5Cg2` and `7PcP-w7xauk1` fixed; the M65 (c) guard test now
  expects tense errors only."
- Left: the M65 (c) guard in `MutationGen` kept (maybe partly redundant; removing it would widen the oracle).
