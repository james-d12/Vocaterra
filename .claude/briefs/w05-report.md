# W05 report (Haiku 5.5, 2026-11-06)

Branch `W05-procedures-data` (local), commits ae843c5c (the work), c0d2a5ad (lexicon-rule tests by infinitive), cdc375a6 (format), on 43848fe7.

- Schemas: procedure, procedures, stations, actions; phrases.schema.json accepts `does`.
- Bundle: game/procedures.json, stations.json, actions.json, all empty; provenance and README; manifest and save-v1/save-v4 fixtures re-hashed.
- `does` kind: PHRASES.md documents it; PhraseSheet.IsInstruction; GrammarStringsExtractor treats does rows as NPC text; no rows added.
- Loaders in Game.Core/Activities: ProcedureBook (+ records), StationBook, ActionTable; GameData loads them.
- Validator (CheckActivities): station prop/NPC/fixtures; verbs in lexicon with tú imperative (mood imp, 2, sg); items; bonus vs step count;
  nouns, fixtures, action pairs; does rows with recorded audio; chunks contiguous and disjoint; goal row; level held.
- Tests: ActivityBooksTests (the design's pan.json verbatim), ActivityValidationTests, ActivitySchemaTests, host PhraseSheetTests rule.

Values chosen / design disagreements: optional step `use` (the sacar pair is mano/horno); design's `flour` refused (not an item until W10);
`anyOrder` a string group name; `what` an item noun or a fixture, `where`/`from`/`with` fixtures; `mano` must become a catalogue noun in W10.
Level check lemmatizes `saca` (CefrChecker), separate from the lexicon rule, pinned by a test.

Core gaps: none. Gate (agent): full loop 9613 passed, 3 skipped (before the lemma test; fast 9227 after); format clean; build 0 warnings;
validate and A2 lint clean; scripted runs 25 of 26 identical, r30-no-classes declined.png bytes differ, also between two after-captures
(nondeterministic). CI case without models skipped on the orchestrator's instruction.
