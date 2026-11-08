# M81 report (Haiku 5.5, 2026-11-05), saved by the orchestrator

Branch `M81-describe-slotgrammars`, commit ae047f9a, on feature/adaptive_design at 5820f1e6.

- Cause: the loader is right; the fuzz tolerance was short. An intent with examples is reachable with no grammar through the embedding rung
  (`IntentResolver.TryEmbedding`); only the lint's coverage check rejects it (27 `coverage.uncovered` for `describe` without grammars). The other
  rule-less intents of game-v1 (1, 6, 7, 12, 16, 17, 25, 27, 30, 32) all validate with their `slotGrammars` removed.
- Fix: `KnownTolerances.AcceptedGrammarlessIntent` (was `AcceptedRuleOnlyIntent`): `RuleOnlyIntent` with rules, the new `ExampleOnlyIntent` with
  examples, else null. Fuzz README finding and the O15 tolerance text updated. No loader code changed.
- Tests: `OracleInfrastructureTests.AGrammarlessIntentIsToleratedOnlyWhenItHasRulesOrExamples`,
  `IntentCatalogTests.AnIntentWithOnlyExamplesLoadsAndKeepsThemForTheEmbeddingRung`; the seed replay passes. 9160 passed, 3 skipped.
- For the owner: the tolerance covers every example-bearing intent (exact in reason, broad in reach). Known, not fixed: an intent with no
  grammar, rule or example loads (`OptionalMembersMayBeOmitted` pins a bare intent); whether the loader should refuse it is the owner's call.
