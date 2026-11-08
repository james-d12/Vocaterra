M85 report (Sonnet 5.5 medium, 2026-11-08). Branch M85-lint-speed at 00d02639 (one commit), worktree .claude/worktrees/m85 clean.

Time (Debug, shipped bundle, bundle read before the clock, 4 runs): before 3.0-3.2 s cold, 2.4-2.6 s steady (confusion 2100-2300 ms,
coverage 120, expectations 150, cefr 45); after about 1.2 s cold, 0.6-0.9 s steady (confusion 300-560 ms).

Cause: LintConfusion resolves 20,448 (example, rival) pairs (33 intents, about 640 examples); since M74/M75/M83 each Resolve scans the
whole catalog (136 grammars) in OutsideGrammar: 2.2M Better calls, about 2,500 matching; each grammar match recomputed per pair. Both the
bundle's growth (quadratic) and the per-resolve cost. Also CheckPair matched with the pack's default AnalysisRules while the resolver
uses the bundle's, so TokenFacts' one-entry cache alternated and rebuilt facts twice per pair. No bisect.

Changes: SlotGrammarMatcher (TokenFacts.Matched: one match per grammar per facts, reference-keyed, locked); IntentResolver
(PlausibleGrammars: the catalog's grammars that pass CanMatch, once per analysis, ConditionalWeakTable; AllowedGrammar and OutsideGrammar
visit only those; catalog order kept); LintConfusion (rival matches first pass, resolver pairs second; facts built once per rules set).
Production resolver touched; pipeline not timed. Test: SlotGrammarTests, a grammar is searched once per facts.

Output: Format() and ToJson() at a1, a2, the content gate's options and no level: byte-identical (through the API).
Tests: 10394 passed, 3 skipped (was 10393); format of Vocaterra.Nlp.slnf clean; no solution-wide -warnaserror build run.
For §5b: a catalog-wide rung must reuse per-analysis TokenFacts; watch point: CheckPair matches with AnalysisRules.For(language), not
the bundle's rules (left to keep output identical).
