M83 report (Sonnet 5.5 medium, 2026-11-07). Branch M83-o7-seed (local) at e55009e0.

Cause: none of the brief's three guesses. The ladder, the analyzer's match and «ayuda» as the item noun are right. Scores for «ayuda por
favor por favor»: request_item `[ART]? [NUM:qty]? [NOUN:item] [ADJ]? por favor` explicit 3, literal 4, named 4; ask_help `ayuda (por
favor)?` explicit 3, literal 5, named 5 (it outranks, as the oracle says). The matcher binds the trailing polite phrase's words to
LiteralNodes, so they count as literal and named tokens; SlotGrammar.MaxLiteralTokens/MaxNamedTokens count only the pattern's own
elements (3 for ask_help); IntentResolver.OutsideGrammar and MatchScore.CanBeBeatenBy use those bounds to skip matching an outside grammar,
judged ask_help unable to beat 4 literals and never matched it. The oracle stands, unchanged.
Fix: only src/Vocaterra.Nlp/Language/Intent/IntentResolver.cs: CanBeBeatenBy takes politeWords; both bounds add
GrammarDefaults.TrailingPoliteWords.Count. No public type changed; api.md untouched.
Tests: IntentResolverCatalogReadingTests: TheFuzzSeed8eu5g1sTe_A8ReadsAsAskHelpOffThePrompt (seed input, game-v1);
TheTrailingPolitePhraseCountsAsLiteralWordsWhenAGrammarOutsideThePromptIsWeighed (two spellings, local catalog);
ALineTheOutsideGrammarDoesNotOutrankStaysWithTheAllowedOne («pan por favor»). Fuzz README "Bugs found and fixed" entry.
CLI before = after at stall-need and fisher-open: «ayuda, por favor», «ayuda por favor por favor» -> ask_help; «quiero agua, por favor»,
«pan, por favor» -> request_item at stall-need, unknown at fisher-open (neither prompt shows the bug: it needs request_item without ask_help).
Goldens and digests: none moved. Gate: build 0 warnings, format clean, validate 0 errors; full suite 9937 passed, 3 skipped, 1 failed
(LintTests.TheBundleLintTakesUnderFiveSeconds 5.55 s beside T9; 3 s alone) -> 9941 with its 3 tests. O7 passes with the seed and 5 more runs.
