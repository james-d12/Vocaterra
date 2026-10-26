# German and a multi-language core: research and design

Research report, 2026-10-22, written against `origin/feature/prototype` at `ecd646d` (M29j merged, M30 planned). It answers the owner's
request: "kickoff a research agent into the NLP project to see what it will take to make it also support German, and make the architecture
for it support multi lang". No code was changed and no `dotnet` command was run; every number below comes from reading the source with
`wc`, `grep` and `sed`, or from the web sources in [section 7](#7-sources). Numbers marked **est.** are mine. The LanguageTool report named
by M30 (`docs/research/2026-10-22-languagetool.md`) is not on this branch, so this report relies only on the M30 row and D10.

**The short answer.** About a quarter of the core is Spanish: some 6,400 of the 28,300 lines in `src/` and `tools/`, in about 48 files.
It is concentrated in five places: the G2P (`Vocaterra.Nlp.Phonology`, all of it), the analyzer's checks and resolver rules
(`Language/Analysis`), the morphology generator, the number words and the phonetic key. A further 40 or so call sites and constants leak
Spanish into otherwise neutral code (`¿` balancing in every rendered line, `¿Cómo?` in the repair ladder, the implicit `por favor` in every
slot grammar). The rest (the verdict, replay, content bundle, decision layer, learner model, intent resolver, embeddings, ASR adapters and
the CTC and GOP scoring) is already language-neutral. The right architecture makes the target language a property of the content bundle:
`manifest.json` names it, and the loader resolves an `ILanguagePack` that supplies every Spanish-specific part. A bundle without the
field is Spanish, so `content/v1`, its hash and every golden stay byte-identical.

German is a much harder second language than a Romance one would be. It has four cases, three genders, three adjective declensions,
separable verbs, V2 and verb-final word order, and capitalised nouns. The hand-written-checks approach is still viable for A1 and A2 with
closed prompts, if the German analyzer works on noun-phrase chunks with case unification rather than single words. A tagger (the M20
route) is a later option, gated on measured false positives. It is easier for German than for Spanish on licence grounds, because spaCy's
German model is MIT and Stanza's is Apache-2.0. Speech input is ready: Vosk has an Apache-2.0 small German model, and the pinned Parakeet
v3 already covers German. Speech output is the weak point. Kokoro v1.0 has no German voice; the CC0 Piper voices need espeak-ng
phonemes, so they run into the D2 GPL question again.

Staged plan: **Phase A**, five milestones that put Spanish behind interfaces with an identity guard (about 3 to 4 orchestrated days).
**Phase B**, eight milestones for a minimal A1 German pack and a sample bundle (about 2 to 3 weeks). **Phase C**, five milestones for A2
(about 2 weeks). In all, 18 milestones and roughly 13,000 new or moved source lines and 15,000 test lines (**est.**).

## 1. Inventory: every Spanish assumption

### 1.1 Headline

| Measure | Value |
| --- | --- |
| C# in `src` and `tools` | 254 files, 28,297 lines |
| Wholly Spanish code (would be rewritten for German) | about 5,200 lines in 28 files: Phonology 1,776, Analysis 2,055, Morphology 743, numbers and phonetic key 524, word classes and folding 116 |
| Spanish inside otherwise neutral files (seams to cut) | about 1,200 lines in about 20 files (normalizer, templates, recast, punctuation, slot-grammar defaults, lint, pronunciation tuning, importer, taxonomy, schema) |
| Language-neutral code | about 21,900 lines (77 %) |
| Spanish tests | Language.Tests 6,719 and Phonology.Tests 1,142 lines; about 3,700 of the fuzz project's 12,091 (the realiser, mutation, lexicon-catalog and text generators); goldens: `g2p-golden.jsonl` 1,788 lines, two end-to-end goldens 671 lines |
| Spanish content | `content/v1` (lexicon 4.7 MB, CEFR lists 996 lemmas, 11 intents, dialogue) and the seed lexicon embedded in `Vocaterra.Nlp.Language.dll` (`Lexicon.SeedResourceName`) |

### 1.2 Item by item

Effort is the cost of putting the item behind a language seam with Spanish unchanged (Phase A). The German replacement is costed in
section 5. S is under a day of agent work, M one to three days and L more (**est.**).

| # | Area | Files and types (lines) | What is Spanish | Seam effort |
| --- | --- | --- | --- | --- |
| 1 | Normalization | `Language/Normalization/TextNormalizer.cs` (198), `NormalizationOptions.cs` (17), `Accents.cs` (56) | Number words through `SpanishNumbers`; contractions `al`→`a el`, `del`→`de el`; `ConvertIndefiniteArticles` knows `un`/`una`; `Accents.Strip` keeps `ñ` but strips the diaeresis, so `ü`, `ä` and `ö` fold to `u`, `a` and `o`; the inverted marks `¿ ¡` are separators, and all punctuation is dropped (M29j's known limit) | S: an `ITextNormalizer` with the same signature; the tokenizer itself is neutral |
| 2 | Accent-insensitive lookup (M26b) | `Lexicon/TextFolding.cs` (68, the fold table `á é í ó ú ü`), `Lexicon/Lexicon.cs` (385; `_byFoldedForm`, `_variantsByPlainForm`, `LookupWithAccentVariants`), `LexiconAccentLookup.cs` (27), `Analysis/AccentVariantChoice.cs` (197), `Analysis/AccentRestoration.cs` (46) | The fold table; the variant-choice rules (`esta`/`está` after `dónde`, `como` + pronoun). Lookup keys are lowercased on load (`Lexicon.cs:311`), so cased forms are lost | S for the fold table and lookup (`ILexiconFolding`); the variant-choice rules move with the analyzer rules (item 7) |
| 3 | Numbers | `Normalization/SpanishNumbers.cs` (384); called by `TextNormalizer`, `AnswerSet.cs:158`, `TemplateEngine.cs:325,418`, `RecastBuilder.cs:181`, `SpeechRequests.cs:18`, `UtterancePipeline.cs:313`; `Templates/NumberAgreement.cs` (22, `veintiún`/`veintiuna`) | All of it | S: `INumberWords` (parse, to words, agree) |
| 4 | Phonetic key and answer matching | `Normalization/SpanishPhoneticKey.cs` (140); `Matching/AnswerMatcher.cs` (169), `AnswerSet.cs` (168), `AnswerCorrections.cs` (114) | The key (b/v, ll/y, seseo, silent h, `gü`); matching calls `Accents.Strip` and the key directly. `DamerauLevenshtein.cs` (55) is neutral | S: `IPhoneticKey`, plus the folding from item 2 |
| 5 | G2P and dialects | All of `Vocaterra.Nlp.Phonology`: `SpanishG2p`, `DialectConfig` (seseo, yeísmo, five presets), `Syllabifier`, `StressAssigner`, `DigraphGrouper`, `PhoneMapper`, `AllophoneRules`, `PhoneInventory`, `ExceptionLexicon`, `PredictedErrorVariants`, `G2pNormalizer` (letters `a…zñáéíóúü`) (1,776 of 1,922; `G2pResult` and `ListEquality` are neutral) | All of it. Called by `UtterancePipeline.cs:319` (pronunciation) and `SpeechRequests.cs:19` (TTS phones), both statically | S to put behind `IG2p` (`Phonemize`, `PhonemizeAll`, `PredictedErrors`); the German G2P is L (section 2) |
| 6 | Lexicon schema and import | `Lexicon/LexiconSchema.cs` (152): genders `m, f`; tenses `pres, pret, impf, fut, cond`; moods `ind, subj, imp`; no case. `LexicalFeatures` (`Core/Stages/AnalysisResult.cs`, seven positional members). `tools/.../KaikkiImporter.cs` (885): `lang_code` `es`, infinitive endings `ar/er/ir`, `es-noun` head templates, voseo tags, plural-lookalike rule (about 250 Spanish lines); `FrequencyList.cs` (187) and `LexiconSidecarBuilder.cs` (188) are neutral | Closed vocabularies; the import mapping | M: per-language closed vocabularies and optional features (section 4.5); the importer takes a language profile |
| 7 | Analyzer, resolver, ReportedReadings (M18, M26b, M29c, M29j) | `Analysis/LinguisticAnalyzer.cs` (259), `ReadingResolver.cs` (607), `ReadingClasses.cs` (97), `ReportedReadings.cs` (185), `AnalysisTables.cs` (117), `Lexicon/WordClasses.cs` (48) | The mechanism (three resolver steps, the fewest-errors fallback, reporting after resolution) is neutral. The rules are Spanish: clitics before the verb, prenominal `grande/bueno/malo`, `cuánto`, `sus`/`su`, `ser/estar/haber` clause-initial, `gracias` interjection-first, the `bueno día` and `mucho gracia` fixed plurals, the copula lemmas, subject pronouns `yo…ustedes`, `FeminineNounsTakingEl` (`agua`, `hacha`) | M: split the resolver into a neutral engine plus an `IAnalysisRules` rule set; the hardest Phase A item to keep byte-identical |
| 8 | Agreement and conjugation checks, error taxonomy | `Analysis/AgreementChecker.cs` (547): `CheckSubjectVerb`, `CheckTense` (past adverbs `ayer`, `anoche`, `anteayer`, time noun + `pasado`), `CheckSerEstar` (23 adjectives), `CheckNounPhrases`, `CheckWordOrder` (an adjective *before* its noun is the error). `Core/Taxonomy/ErrorTags.cs` (86): 13 tags; `ser_estar`, `pron.silent_h` and `word_order` (as defined) are Spanish; `All`'s order ranks errors in `VerdictAssembler.cs:218` and `LinguisticAnalyzer.cs:192` | All the checks | M with item 7. The taxonomy rule is in section 2.3 |
| 9 | Morphology generator | `Morphology/MorphologyGenerator.cs` (602), `SpanishOrthography.cs` (130), `Definiteness.cs` (11) | Plural rules (`-s`, `-es`, `z`→`ces`, written accents), `el/la/los/las`, `un/una`, `el` before stressed `a`, adjective `-o/-a`, regular `-ar/-er/-ir` paradigms, regularized irregulars (`sabo`). The public API (`Article(Definiteness, gender, number, nounLemma)`, `Conjugate(lemma, person, number, tense)`) has no case parameter | M: an `IMorphology` interface. `MorphologyGenerator` stays the Spanish implementation because the game calls it (section 4.5) |
| 10 | Slot grammar DSL | `SlotGrammar/SlotGrammar.cs` (440), `SlotGrammarMatcher.cs` (341), `GrammarNode.cs` (28); `Intent/AnalysisSlotFinder.cs` (77) | The parser and matcher are neutral. Every grammar gets an implicit optional leading subject pronoun (`DefaultSubjectNode`, pro-drop) and an optional trailing `por favor` (`DefaultPoliteNode`, `SlotGrammarMatcher.cs:58-59,176`); literals and lemmas match through the Spanish fold; the order is strictly linear | S: per-language `GrammarDefaults` |
| 11 | Intent resolver and keyword rules | `Intent/IntentResolver.cs` (340), `IntentCatalog.cs` (386), `KeywordRule.cs` (39), `IntentExample.cs` (202) | None in code: keyword rules are lemmas in content, and the e5 model is multilingual. Examples are normalized with the Spanish normalizer (`IntentResolver.cs:328`) | none beyond item 1 |
| 12 | Template functions and response planner | `Response/Templates/TemplateEngine.cs` (438): `article(def|indef)` with no case, `plural-if`, `conj(p,n,t)`, `words(m|f|slot)`, `cap`, the `HeuristicGender` fallback, and `TemplateEngine.Default` built on the Spanish seed lexicon; `PunctuationFixer.cs` (110) balances `¿?` and `¡!` in *every* rendered line; `ResponsePlanner.cs` (101) is neutral | Articles, number words, gender guess, inverted marks | M: an `ITemplateFunctions` provider plus `IPunctuation`. Today a German line `Was kostet das?` would render as `¿Was kostet das?` |
| 13 | Feedback, recasts, repair ladder | `RecastBuilder.cs` (275): span edits are neutral; accent restoration, digits as Spanish words and `un/una` before a noun are not; it works on lowercased normalized text. `Intent/RepairStrategy.cs` (133): `AskAgainLine = "¿Cómo?"`, `SlowRepeatLine = "Perdón, te lo repito más despacio."` are Spanish text in code. `Decision/Feedback/FeedbackPolicy.cs` (165) and `TagPattern.cs` (82) are neutral | Recast spelling restoration; repair lines | S: an `IRecastPolicy`; the repair lines become reserved dialogue pools with the constants as fallback |
| 14 | Pronunciation and speech adapters | `Pronunciation/PredictedErrorMatcher.cs` (173), `PhoneClass.cs` (81), `EspeakPhoneMap.cs` (94), `FakePhonemeRecognizer.ForSpanish` (205), `PronunciationThresholds.cs` (98): tuned to the Spanish inventory and the four `pron.*` tags. CTC, GOP, the aligner and the wav2vec2 adapter (about 1,400 lines) are neutral. `KokoroSynthesizer.cs:34` hard-codes `Lang = "es"`. Model ids: `tools/Vocaterra.Nlp.Cli/Program.cs:303` and `Bench/Program.cs:121` (`vosk-model-small-es-0.42`); `src/Vocaterra.Game/SpeechEngine.cs:23,32` (Vosk es, `ef_dora`) | Phone inventory, tags, voice language | S for the seams (the phone map comes from the pack's G2P); the German scorer depends on D13 |
| 15 | CEFR lists and `cefr-check` | `Content/CefrChecker.cs` (349), `LintCefr.cs` (46) | Mechanism neutral (lemma lists in content). It normalizes with the Spanish normalizer and keeps `una` as the article | S (follows item 1) |
| 16 | Content linter (M27a) | `Content/ContentLinter.cs`, `Lint*.cs` (about 1,000) | `LintCoverage.cs:12` lists the Spanish subject pronouns, `:85` the trailing `por favor`, `:107` `un`/`una`; the hazards check knows Spanish homograph shapes | S |
| 17 | Fuzz generators and oracles | `tests/Vocaterra.Nlp.Fuzzing.Tests/Generation/SentenceRealizer.cs` (1,205), `MutationGen.cs` (764), `LexiconCatalog.cs` (645), `SpanishTextGen.cs` (287), `SentenceModel.cs` (207), `SentenceGen.cs` (159), `PhoneGen.cs` (203), `AdversarialCorpus.cs` (199) | O3, O4, O5, O6, O8, O9 and O12 are Spanish models (O12 checks balanced `¿?`); O1, O2, O7, O10, O11, O13 to O16 are neutral apart from their input generators | M (section 4.8) |
| 18 | Goldens | `tests/Vocaterra.Nlp.Golden.Tests/Data/g2p-golden.jsonl` (1,788 lines), `e2e-offer-items-open.golden.json`, `e2e-confirm-open.golden.json`; `tests/golden/manifest.jsonl` (audio protocol) | All Spanish | S: a `Data/<lang>/` layout for new languages; existing paths unchanged |
| 19 | CLI and bench | `tools/Vocaterra.Nlp.Cli/Program.cs` (360), `Bench/Program.cs` (213) | Model ids; the default bundle `content/v1` | S: `--language` is read from the bundle; model ids come from a per-language table |
| 20 | Pipeline and bundle wiring | `ContentBundle.cs:37-38` builds `MorphologyGenerator` and `TemplateEngine` directly; `UtterancePipeline.cs:99-100,313,319` calls `TextNormalizer`, `AnswerMatcher`, `SpanishNumbers` and `SpanishG2p` statically; `ContentManifest.cs` has no language; `CanonicalJson.cs:25` mentions Spanish in a comment only | The construction sites | S: the bundle owns the pack; the pipeline asks the bundle |

### 1.3 What is already language-neutral

`Vocaterra.Nlp.Core` (contracts, determinism, `CanonicalJson`, `SeededRng`, `Margin`, `VersionStamp`, `SlotMap`), `Audio` (887 lines),
`Asr` (1,598; Vosk and sherpa-onnx adapters, `GrammarBuilder`, `ModelStore`, manifest verification), `Evaluation` (792; weights come from
content, and unknown tags already get `unknownTagWeight`), `Decision` (1,412; BKT, FSRS, the agent state machine, feedback policy,
`TagPattern`), `Pipeline` (1,736 apart from the four call sites in item 20), `Content` (loader, manifest, provenance, validator, layering
plans, `TextGrader`, host files), `Language/Embeddings` (672; multilingual-e5), `Language/Intent` (the resolver rungs and margins),
`Language/Matching` (edit distance and the match ladder), the lexicon *store* (sorted index, derived indexes, `LexiconCache`), the slot
grammar parser and matcher, the resolver's three-step engine, `Response/Effects`, the template parser, `DialogueCatalog` and variant
selection, the speech caches and `WavCodec`, and the CTC, GOP and alignment code. Replay (`ReplayEnvelope`, `Replayer`, `ReplayDiff`) is
neutral.

### 1.4 Findings that matter before any German work

These would break silently, not loudly, the day a second language loads:

1. `PunctuationFixer.Fix` runs on every rendered line and inserts `¿` and `¡`. A German question gains a Spanish mark.
2. `RepairStrategy` speaks Spanish from code (`¿Cómo?`, `Perdón, te lo repito más despacio.`). It is the only target-language text
   outside content.
3. `Accents.Strip` and `TextFolding` fold `ü` to `u`, and `Accents.Strip` folds `ä` and `ö` too. In German, `schon` (already) would then
   match `schön` (beautiful), and `Mutter` (mother) would match `Mütter` (mothers), so a `noun.number` error is lost.
4. Normalized text is lowercased, and recasts are built from it (`RecastBuilder`). A German recast would print `ich habe zwei schwerter`.
   Every noun loses its capital, which German readers see as a spelling error.
5. Every slot grammar silently accepts a missing subject and a trailing `por favor`. In German, a dropped subject is an error, and `bitte`
   can stand almost anywhere.
6. `ErrorTags.All` is the whole taxonomy. The game builds its English error lines from it and tests that every tag has one
   (`src/Vocaterra.Game/EnglishText.cs:485`, `tests/Vocaterra.Game.Tests/EnglishTextTests.cs:111`). Appending German tags to
   `All` would fail a game test and change the fuzz suite's random draws (`VerdictGen`, `TemplateFuzzTests` pick from `All`).
7. `CanonicalJson` writes nulls (`DefaultIgnoreCondition = Never`). A new nullable member on any serialized record changes every golden,
   unless it carries `JsonIgnore(WhenWritingNull)`, as `IntentLintWaivers` already does.

## 2. German: what each feature needs from the core

### 2.1 Feature by feature

| Feature | The German facts (A1/A2 scope) | What the core needs |
| --- | --- | --- |
| Genders and articles | `m`, `f` and `n`, and a plural with no gender. The definite article changes with case: `der/die/das/die` (nominative), `den/die/das/die` (accusative), `dem/der/dem/den` (dative), `des/der/des/der` (genitive). The indefinite set is `ein/eine/ein`, `einen`, `einem/einer`, `eines/einer`. `kein` and the possessives decline like `ein` | Gender `n` in the schema; a `Case` feature; `IMorphology.Article(definiteness, gender, number, case)`; the template filter `article(def,acc)`, where the case defaults to nominative so that Spanish `article(def)` is unchanged |
| Case | Four cases. A1 needs the nominative and accusative, plus the dative after `mit`, `zu`, `von`, `aus`, `bei` and `nach`. A2 adds the two-way prepositions (`in`, `auf`, `an`… with the accusative for direction and the dative for location) and dative objects (`helfen`, `geben`). The genitive is rare in speech at these levels | A noun-phrase chunker that unifies case, gender and number across determiner, adjective and noun, because single words are ambiguous: `der` alone has five readings. Then an expected case for each chunk, from its role: subject, verb object (a small valency table for A1 verbs) or preposition complement (a preposition-case table) |
| Adjective declension | Strong, weak and mixed endings (`guter Wein`, `der gute Wein`, `ein guter Wein`). The ending depends on the determiner as well as on case, gender and number | A `Declension` feature on adjective readings; generation from the lexicon's tables, with rules as a fallback; a new check and tag (`decl.adj`) |
| Noun plurals | About five plural classes (`-e`, `-er`, `-(e)n`, `-s`, no ending), often with umlaut (`Haus`→`Häuser`, `Apfel`→`Äpfel`); dative plural `-n` (`den Kindern`); the weak nouns (n-Deklination: `den Jungen`) | Plurals from the lexicon, because rules alone are unreliable. A rule fallback only for the predictable classes (`-ung`→`-ungen`), and a regularized-plural detector like `sabo` (`Hausen` for `Häuser`) with a tag (`noun.plural_form`) |
| Verb conjugation | Strong verbs change their stem vowel (`fahren`→`du fährst`, `sprechen`→`er spricht`); irregular verbs (`sein`, `haben`, `werden`, `wissen`); modal verbs with their own singular (`kann`, `will`, `möchte`). Perfekt is `haben`/`sein` plus a participle (`gekauft`, `gegangen`, `angerufen`); Präteritum for `sein`, `haben` and the modals by A2 | Paradigms from the lexicon (kaikki carries them); a participle `VerbForm`; Perfekt recognised as a two-word form (auxiliary and participle), which the Spanish single-token tense check cannot express; `conj.irregular` reused for `er fahrt`, `ich gehte`, `gegeht` |
| Separable verbs | `anrufen`: `ich rufe dich an` (main clause), `…dass ich dich anrufe` (subordinate), `angerufen`, `anzurufen` | A `Particle` feature; the lexicon's multi-word forms (`rufe an`) joined across a gap, which M29j's adjacent-only join cannot do; a slot grammar element that binds the verb and its particle; a `verb.separable` check; the template filter `particle` |
| Word order | V2 in main clauses (`Heute kaufe ich Brot`); verb-final after `weil`, `dass`, `wenn`, `ob`; the verbal bracket (`Ich möchte ein Brot kaufen`, `Ich habe Brot gekauft`); verb-first for yes/no questions and imperatives; adjectives before the noun | Clause segmentation by subordinator; counting constituents in chunks (a whole noun or prepositional phrase fills the first position); checks `word_order.v2`, `word_order.verb_final`, `word_order.bracket`. Spanish `word_order` (an adjective before its noun) is the opposite of German. Question versus statement needs the final `?`, which normalization drops today, so `NormalizedText` should keep the sentence's final mark as an optional member |
| Capitalised nouns | Every noun and nominalisation is capitalised (`das Essen` beside `essen`), and so is formal `Sie` | Capitals are information only in typed input. Vosk prints lower case, so speech grading must ignore them. A typed-only `orth.capital` check reads `Token.Original`, and a recast restores capitals from the resolved reading (a noun reading, the formal `Sie`, the sentence start); the lexicon need not store cased forms |
| Compounds | Productive (`Schwertschmied`, `Apfelsaft`); gender and plural follow the last part; linking elements `-s-`, `-n-`, `-en-`, `-er-`, `-e-` | Phase B relies on the lexicon (common A1 compounds are lemmas in Wiktionary). Phase C adds a splitter: an unknown word that splits into known parts is read with the head's features, not reported as `vocab.unknown` |
| ß and umlauts | Learners type `ae`, `oe`, `ue` and `ss` for `ä`, `ö`, `ü` and `ß`; Swiss Standard German writes `ss` throughout; `Masse` and `Maße` differ only in `ß` | A German folding key (`ä`→`ae`, `ß`→`ss`, applied to both sides) used where Spanish uses `StripAccents`. Like M26b's accent policy, the typed variant is read by context, nothing is reported to the learner, and a recast restores the spelling |
| du and Sie | `du` is 2sg; formal `Sie` takes the 3pl verb form and is capitalised; `sie` is she or they; `ihr` is 2pl, or `her` | Pronoun readings with a register feature; subject-verb agreement keyed on person and number as today. An optional `register` tag, driven by content: an agent declares `formal` or `informal`, and the check fires only when the bundle asks. Spanish could later use it for `tú`/`usted` |
| Numbers | Compound number words (`einundzwanzig`, `zweihundertdreiundvierzig`); `ein/eine/eins`; `zwei`/`zwo` | A German `INumberWords`; the template filter `words` with `eins` and `ein/eine` agreement |
| Contractions | `im`, `am`, `ins`, `zum`, `zur`, `vom`, `beim` (a preposition with an article, carrying case) | Normalizer expansion with the case kept (`zum` = `zu dem`, dative), like Spanish `al`/`del` |

### 2.2 The error tags German learners need

The taxonomy grows by appending, never by reordering or renaming. Spanish keeps its 13 tags with their ranks 0 to 12, so the verdict's
error order and every Spanish golden stay the same. German emits the shared tags where the meaning is the same.

| Tag | Meaning | Example (learner → expected) | Phase |
| --- | --- | --- | --- |
| `gender.agree` (shared) | Article or adjective gender | `der Tasche` (nom.) → `die Tasche` | B |
| `noun.number` (shared) | Singular or plural mismatch | `zwei Apfel` → `zwei Äpfel` | B |
| `conj.person` (shared) | Verb person or number | `ich hat` → `ich habe`; `Sie hast` → `Sie haben` | B |
| `conj.irregular` (shared) | Regularized strong or irregular verb | `er fahrt` → `er fährt`; `gegeht` → `gegangen` | B (present), C (participle) |
| `conj.tense` (shared) | Present tense with a past marker | `gestern kaufe ich` → `gestern habe ich … gekauft` | C |
| `case.prep` | Wrong case after a preposition | `mit der Hund` → `mit dem Hund` | B |
| `case.object` | Wrong case of a verb object | `ich habe ein Hund` → `einen Hund` | B |
| `case.subject` | Subject not in the nominative | `den Mann kommt` → `der Mann` | B |
| `decl.adj` | Adjective ending (strong, weak or mixed) | `ein gute Wein` → `ein guter Wein` | B (nominative and accusative), C |
| `noun.plural_form` | Regularized plural form | `die Hausen` → `die Häuser` | C |
| `word_order.v2` | Finite verb not in second position | `heute ich kaufe Brot` → `heute kaufe ich Brot` | B |
| `word_order.bracket` | Infinitive or participle not at the end | `ich möchte kaufen ein Brot` → `ich möchte ein Brot kaufen` | B |
| `word_order.verb_final` | Finite verb not last in a subordinate clause | `weil ich habe Hunger` → `weil ich Hunger habe` | C |
| `verb.separable` | Particle not separated, or misplaced | `ich anrufe dich` → `ich rufe dich an` | B |
| `verb.infinitive` | Finite verb after a modal | `ich kann spreche` → `ich kann sprechen` | B |
| `aux.choice` | `haben` or `sein` in the Perfekt (the German analogue of `ser_estar`) | `ich habe gegangen` → `ich bin gegangen` | C |
| `orth.capital` | Noun not capitalised (typed input only; weight 0 by default) | `ich habe einen hund` → `Hund` | B |
| `register` | `du` or `Sie` against the agent's declared register (content opt-in) | `Hast du Brot?` to a formal agent → `Haben Sie Brot?` | C |
| `pron.*` German set | For example `pron.umlaut`, `pron.vowel_length`, `pron.ich_ach`; only after D13 | — | C (conditional) |

`ErrorTags.Family` splits at the first dot, so `word_order.v2` and `word_order` share the family `word_order`, and `case.*` is one family
for the learner model (BKT) and for feedback patterns (`case.*`). `ser_estar` and `pron.silent_h` are simply never emitted by German.

### 2.3 Can hand-written checks over a lexicon carry German?

**Yes for A1 and A2 with closed prompts, provided the German analyzer changes shape.** The Spanish checks look at neighbouring words. For
German the unit must be the chunk. The recommended German analysis runs in four steps:

1. Read every token with all its lexicon readings, as today. German readings carry case, so `der` has five and an adjective such as
   `guten` about a dozen.
2. Chunk noun phrases (determiner, adjectives, noun) and prepositional phrases, keeping only the case, gender and number assignments that
   every member of the chunk allows. This is constraint intersection over small sets, so it stays D0 and fast. A chunk whose intersection is
   empty has an agreement error, and the generator gives the nearest correct form.
3. Assign each chunk a role: a preposition's complement, the subject (the nominative chunk that agrees with the finite verb), or the object
   of a verb in the valency table. The role gives the expected case.
4. Place the finite verb, the particle, and the infinitive or participle against the clause type for the order checks.

The resolver's engine (rules, grammar bindings, fewest-errors fallback) still applies. The slot grammar's bindings help more in German than
in Spanish, because the author's pattern fixes the order the learner is aiming at.

Where it strains: role assignment without a parser is a heuristic. Learners at A1 speak subject-verb-object, which keeps it tractable,
but `Den Hund sieht der Mann` and long middle fields would defeat it. Speech loses capitals and `?`, so `essen` and `Essen`, and a
statement and a question, look alike. The charitable fallback reads such cases as correct, which fits D-game-9's "never shame" rule.
Expect a false-positive rate above Spanish's at first. Phase B's gate must therefore measure it on a corpus of correct sentences, as M26b
did with its 1,102-sentence corpus.

**The tagger route (M20 and M22 for German).** German is better served than Spanish:

| Tool | Licence | German model | Accuracy | .NET route | Fit |
| --- | --- | --- | --- | --- | --- |
| UDPipe 1, stock UD 2.5 models | MPL-2.0 code [17]; models CC BY-NC-SA [17] | German-HDT, German-GSD | HDT: UPOS 97.8 %, UFeats 91.4 %, LAS 92.3 %; GSD: 91.7 % UPOS [17] | C# SWIG binding (MPL-2.0) | No: the stock models are non-commercial |
| UDPipe 1, self-trained on UD German-HDT | MPL-2.0 code; treebank CC BY-SA 4.0, 3.4 M tokens [15] | train it yourself | close to the stock HDT figures (**est.**) | as above | Yes, if D10 accepts MPL-2.0 and the model is treated as CC BY-SA (the lexicon already is) |
| Stanza `stanza-de` | Apache-2.0 [18] | ready | not checked here | Python sidecar | Yes, as a separate process |
| spaCy `de_core_news_sm` | MIT [19] | trained on TIGER, Tiger2Dep and WikiNER | UPOS 98.0 %, UFeats 90.7 % [19] | Python sidecar | Yes; unlike Spanish, where the spaCy model is GPL |
| UD German-GSD treebank | CC BY-SA 4.0 [16] | — | weaker training data (GSD UPOS 91.7 % with UDPipe 1) | — | prefer HDT |

UFeats around 91 % means about one word in eleven has a wrong case or gender feature. That is too noisy to grade case directly from the
tagger. As in the Spanish report, the tagger should *filter* lexicon readings (POS and the subject/object arcs), and the case check
should stay with the lexicon and the generator. **Recommendation:** build Phase B and C on chunked hand-written checks. Spike the tagger
(M47) only if the measured false-positive rate on correct sentences fails the gate. Its seam is the same `ReadingResolver` seam M20 plans
for Spanish, now per language. D10's MPL-2.0 half matters only if the spike chooses UDPipe; Stanza and spaCy run as separate processes
under the dev-time stance only if they stay out of the shipped game. Shipping a Python sidecar is a separate question (D8 and the
min-spec). LanguageTool covers German well and stays a dev-time content oracle (M30 extended), not a learner grader. The Spanish report
found it caught 9 of 20 learner errors.

## 3. Resources for German

All checked on 2026-10-22. Sizes are download sizes as published.

| Need | Resource | Licence | Size | Verdict | Sources |
| --- | --- | --- | --- | --- | --- |
| Lexicon and paradigms | kaikki.org German extract of English Wiktionary (`kaikki.org-dictionary-German.jsonl`) | Wiktionary text: CC BY-SA 4.0 and GFDL [5]; the page itself names no licence and asks for a citation of the Wiktextract paper | about 1.0 GB, 353,033 word forms; marked **deprecated**, "will be removed in the near future" [3] | Use it, like Spanish (ADR 0008): stream, pin by SHA-256, commit only the filtered output. Fall back to the raw all-language extract, as ADR 0008 already plans for Spanish | [3][5] |
| Lexicon (alternative) | kaikki.org extract of the German Wiktionary (de.wiktionary): `raw-wiktextract-data.jsonl` | CC BY-SA 4.0 [6] | 2.9 GB (295.8 MB gzip), dump of 2026-10-01 [4] | Richer German tables and IPA on most entries (**est.**), but a different tag schema; a second importer profile. Keep it as the fallback and as the IPA source for G2P | [4][6] |
| Frequency list | hermitdave/FrequencyWords `content/2018/de/de_50k.txt` | content CC BY-SA 4.0, code MIT [7] | 50,000 lines (**est.** a size like `es_50k`, 0.66 MB) | Same source and terms as Spanish; D4 (OpenSubtitles) applies equally | [7] |
| CEFR A1 list | Goethe-Zertifikat A1 *Start Deutsch 1* Wortliste | No licence in the PDF; it calls itself "Information und Referenz"; © Goethe-Institut by default | 29 pages, about 650 words [8] | **Do not import or redistribute.** Hand-author `cefr/a1.txt` as Spanish did (996 hand-written lemmas today), using the Goethe list only as a reference while writing; record that in `ATTRIBUTION.md` | [8] |
| CEFR A2 list | Goethe-Zertifikat A2 Wortliste | same terms (not opened; same publisher and format) | — | same | — |
| ASR, constrained | Vosk `vosk-model-small-de-0.15` | Apache-2.0 | 45 MB; WER 13.75 (Tuda-de), 30.67 (podcast) [1] | **Use**: the counterpart of the pinned `vosk-model-small-es-0.42` (39 MB) | [1] |
| ASR, large | Vosk `vosk-model-de-0.21` (1.9 GB, Apache-2.0); `vosk-model-de-tuda-0.6-900k` (4.4 GB, Apache-2.0); `vosk-model-small-de-zamia-0.3` (49 MB, **LGPL-3.0**, "not recommended") | as listed | as listed [1] | Too large for the min-spec's constrained path; avoid Zamia | [1] |
| ASR, open | NVIDIA Parakeet TDT 0.6B v3 (already pinned as `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8`) | CC-BY-4.0 | already pinned; German WER 5.04 (FLEURS), 4.84 (CoVoST) [9] | **Use**: no new model; one German test clip (the pinned archive already ships `test_wavs` in de, according to the manifest note) | [9] |
| ASR, alternative | Whisper (tiny 39M, base 74M, small 244M) | MIT, code and weights [10] | German in its language list [11] | M24's benchmark candidate, as for Spanish | [10][11] |
| TTS | Kokoro v1.0 (pinned) | Apache-2.0 | — | **No German voice.** v1.0 has 54 voices in nine languages (American and British English, Japanese, Mandarin, Spanish, French, Hindi, Italian, Brazilian Portuguese) [2] | [2] |
| TTS | Kokoro German fine-tune `Thorsten-Voice/Kokoro` | Apache-2.0 (card), CC0 data | PyTorch only (`model.pth`, voicepacks), no ONNX; misaki's German G2P, which relies on espeak-ng [12] | Not usable without an ONNX export and a phonemizer; watch it | [12] |
| TTS | Piper `de_DE` voices: `thorsten`, `thorsten_emotional`, `kerstin`, `eva_k`, `karlsson`, `ramona`, `mls`, `pavoque` [13] | `thorsten`: dataset CC0; `kerstin`: CC0; `pavoque`: **CC BY-NC-SA 4.0**; `eva_k`, `karlsson`, `ramona`: M-AILABS data under a custom licence ("See URL") [14] | VITS; the medium voice is 22,050 Hz [14] | **Use `thorsten` or `kerstin`** through sherpa-onnx's VITS path (sherpa-onnx has published German Piper builds, e.g. `vits-piper-de_DE-thorsten-medium` [20]). Exclude `pavoque`; check M-AILABS terms before `eva_k`, `karlsson` or `ramona` | [13][14][20] |
| TTS licence question | Piper's engine is GPL-3.0 because it embeds espeak-ng [21]; Piper voices are trained on espeak-ng phonemes; sherpa-onnx v1.13.8 (10 Sept 2026) is still the latest release, so the GPL-free build of PR #4055 is not out yet [22] | — | — | D2 applies unchanged: private builds only until the GPL-free pin; then the phones must come from a German G2P that emits espeak-ng-compatible IPA. Pre-rendered lines (the game's S9 route) sidestep run-time speech | [21][22] |
| Dev-time grammar oracle | LanguageTool | LGPL-2.1 or later [23] | — | German is among its strongest languages (**est.**); M30's HTTP client works for `de-DE` with a language parameter. The owner's dev-time stance (D10) covers it | [23] |
| Tagger (optional) | see section 2.3 | — | — | — | [15]–[19] |

## 4. The multi-language architecture

### 4.1 The language is a property of the bundle

- **`manifest.json` gains `"language"`** (a BCP-47 tag: `de-DE`; the pack is chosen by its primary subtag `de`) and an optional
  `"supportLanguage"` (`en`). When `language` is absent, the bundle is Spanish (`es`, with ADR 0005's Latin American default). So
  `content/v1` and `content/game-v1` need no edit and keep their hashes. One bundle has one target language.
- **The bundle hash** includes the language only when the field is present: `Combine("language", tag, path₁, sha₁, …)`, otherwise the
  current `Combine(path₁, sha₁, …)`. A German bundle can never share a hash with a Spanish one, and a Spanish bundle hashes as before.
- **The loader resolves an `ILanguagePack`** from a registry (`LanguagePacks.Default` holds `es`, and `de` once it ships; a host may pass
  its own through `ContentLoadOptions.LanguagePacks`). An unknown language is a `ContentValidationException`.
- **The pack owns everything in section 1.2 that is Spanish.** `ContentBundle.Language` exposes it; `ContentBundle` builds the morphology,
  the template engine and the analyzer through it; the pipeline, `TextGrader`, the linter and `cefr-check` ask the bundle, never a static
  Spanish class.

### 4.2 The interfaces

```csharp
namespace Vocaterra.Nlp.Language;

/// <summary>Everything the core needs to know about one target language. Pure D0 code; no clock, no randomness.</summary>
public interface ILanguagePack
{
    string LanguageId { get; }                    // "es", "de"
    ITextNormalizer Normalizer { get; }           // tokens, contractions, number words to digits, final punctuation kept
    ILexiconFolding Folding { get; }              // lookup key and lenient key (es: strip acute and diaeresis, keep ñ; de: ä→ae, ß→ss)
    INumberWords Numbers { get; }                 // parse, to words, agree (veintiún / ein, eine, eins)
    IPhoneticKey PhoneticKey { get; }             // sound-alike key for answer matching
    LexiconProfile Schema { get; }                // closed vocabularies: genders, cases, tenses, moods, verb forms, declensions
    IMorphology CreateMorphology(Lexicon lexicon);          // nouns, articles, adjectives, conjugation, regularized forms
    IAnalysisRules CreateAnalysisRules(Lexicon lexicon, IMorphology morphology);   // resolver rules, variant choice, checks, reported readings
    ITemplateFunctions CreateTemplateFunctions(Lexicon lexicon, IMorphology morphology);   // article(), words(), conj(), decl(), particle()
    IRecastPolicy Recast { get; }                 // spelling restoration (accents, capitals), numbers in words
    IPunctuation Punctuation { get; }             // es: balance ¿? ¡!; de: none
    GrammarDefaults GrammarDefaults { get; }      // es: optional subject pronoun, trailing "por favor"; de: optional "bitte" anywhere, no implicit subject
    IG2p? G2p { get; }                            // null: pronunciation scoring and phone-fed TTS are off for this language
    ErrorTagSet ErrorTags { get; }                // the tags this pack can emit, with their ranks
    LintVocabulary Lint { get; }                  // subject pronouns, polite phrase, article/numeral homographs
    RepairLines RepairFallback { get; }           // es: "¿Cómo?", "Perdón, te lo repito más despacio."
}
```

The existing classes become the Spanish implementations without moving: `SpanishLanguagePack` wraps `TextNormalizer`, `SpanishNumbers`,
`SpanishPhoneticKey`, `MorphologyGenerator`, the analyzer rules, `SpanishG2p` and `PunctuationFixer`. Their public static entry points stay
and keep meaning Spanish, so the game and `samples/HostConsole` compile unchanged.

### 4.3 Diagram

```
                        content bundle (one target language)
                        manifest.json: { "language": "de-DE", "supportLanguage": "en", ... }   absent => "es"
                                   |
                                   v
   ContentLoader --resolve--> LanguagePacks registry --> ILanguagePack ("es" | "de" | ...)
        |                                                   |
        v                                                   |  supplies
   ContentBundle { Language, Lexicon, Morphology(pack), TemplateEngine(pack), intents, prompts, dialogue, cefr, ... }
        |
        v
   UtterancePipeline (shared, D0 over recognizer output)
     ASR (IWordRecognizer: Vosk es|de, Parakeet multilingual)      <- model id chosen by the host per language
       -> Normalizer.Normalize ............................ pack
       -> AnswerMatcher (edit distance + Folding + PhoneticKey) ... shared engine + pack keys
       -> LinguisticAnalyzer: lexicon lookup (Folding) ...... shared store
            ReadingResolver engine ......................... shared
              + IAnalysisRules (rules, checks, chunker) ...... pack
       -> IntentResolver (grammar -> keyword -> e5) ......... shared (+ GrammarDefaults from pack)
       -> PronunciationScorer (CTC, GOP) + pack.G2p? ........ shared + pack (optional)
       -> VerdictAssembler (weights from content, tag ranks from pack) ... shared
       -> Decision: agent machine, BKT, FSRS, FeedbackPolicy  shared
       -> ResponsePlanner -> TemplateEngine + ITemplateFunctions + Punctuation + RecastPolicy ... shared + pack
       -> ISpeechSynthesizer (Kokoro es | Piper de | silent)   <- host
     Replay envelope (shared; language implied by the bundle hash)

   Shared projects: Core, Audio, Asr, Evaluation, Decision, Pipeline, Content, Language (engines), Pronunciation (CTC/GOP), Response (engines)
   Pack projects:   Language.Es (+ Phonology.Es)     Language.De (+ Phonology.De later)
```

### 4.4 Target language and learner language

- **L2, the language taught,** is the bundle's `language`. Everything the core reads or produces as the learner's language follows it:
  the lexicon, grammars, examples, dialogue, feedback pools (`feedback.<kind>[.<tag>]`), recasts, repair lines and speech.
- **L1, the support language,** is not processed by the core today and need not be. The game already writes its English support
  (D-game-13) in game content (`EnglishText.cs` maps every error tag to an English line). The core's job is to make that mapping safe for
  several languages:
  - a stable, per-pack tag set (`ILanguagePack.ErrorTags`), so a host can ask "which tags can this bundle emit?" instead of reading
    `ErrorTags.All`;
  - `supportLanguage` in the manifest, recorded and passed through, never interpreted;
  - optionally later, a `support/<l1>.json` file of L1 templates keyed by tag and feedback kind, rendered by the same template engine with
    the verdict's `expected`, `actual` and `recast` as plain strings. It needs no L1 morphology. It is additive and not needed for German.
- Feedback lines spoken by an agent stay in L2 (pools); explanations in L1 belong to the host or the optional support file.

### 4.5 Effect on the public API and on ADR 0009

The boundary in ADR 0009 is unchanged in kind: the core still knows nothing about game words, and a language is a content property, like
the lexicon. The changes are additive. ADR 0009 gains a note, and a new **ADR 0011, "Target language as a bundle property"**, records this
design (0010 is reserved for D9's determinism ADR).

| Public type or member | Change | Spanish effect | Game effect |
| --- | --- | --- | --- |
| `ContentManifest` | `Language` and `SupportLanguage` (string?, `JsonIgnore(WhenWritingNull)`); hash rule of 4.1 | none (absent) | none |
| `ContentBundle` | `Language` (`ILanguagePack`), `LanguageId` | none | none |
| `ContentBundle.Morphology` | stays `MorphologyGenerator` for Spanish; for another language it throws `NotSupportedException`, and `bundle.Language.CreateMorphology` (or a new `bundle.LanguageMorphology`) is the neutral route | none | `NounCatalog.cs:219` and `GuideSession.cs:188` keep working for Spanish; a German game would switch to `IMorphology` |
| `LexicalFeatures` | optional trailing members `Case`, `VerbForm`, `Declension`, `Particle`, `Register`, all `JsonIgnore(WhenWritingNull)` | none in JSON; equality unchanged when null. Positional deconstruction into seven variables would break (none found in `nlp/` or `game/`) | `ResolvedWord.Reading` shows the new features for German |
| `LexiconSchema` / lexicon file | per-language closed vocabularies (`LexiconProfile`): `n`, `nom/acc/dat/gen`, `part`, `strong/weak/mixed`; the file's `features` gains optional keys | Spanish profile identical; a Spanish file with `n` is still refused | none |
| `ErrorTags` | `All` stays the frozen v1 Spanish set; new constants for the German tags; `ErrorTagSet` per pack with ranks (v1 first, then appended) | ranks and order unchanged | `EnglishText.MissingErrorTags` keeps passing; a German game reads `bundle.Language.ErrorTags` |
| `NormalizedText` | optional `FinalMark` (`?`, `!` or null) for sentence type | absent for Spanish unless opted in (keeping M29j behaviour) | none |
| `TextNormalizer`, `SpanishNumbers`, `SpanishPhoneticKey`, `SpanishG2p`, `MorphologyGenerator`, `RecastBuilder.Build` | kept, documented as the Spanish implementations; neutral entry points through `bundle.Language` | none | `GuideFacts.cs:49` (`SpanishNumbers.ToWords`) and `WordProgress.cs:228,487` (`TextNormalizer.Normalize`) keep working; a German game uses `bundle.Language.Numbers` and `.Normalizer` |
| `RepairStrategy` | lines from reserved pools `repair.ask_again`, `repair.slow_repeat` when the bundle has them, else the pack's fallback | identical text, so the replay record is the same | none |
| `KokoroSynthesizer.FromStore` | optional `lang` parameter (default `es`); folded into `ModelHash` only when it is not `es` | `ModelHash` unchanged | none |
| New adapter `PiperSynthesizer` (sherpa-onnx VITS) | `FromStore(store, "vits-piper-de_DE-thorsten-medium", ...)` | none | a German game chooses it |
| `PipelineServices`, `UtterancePipeline`, `ConversationContext`, `PromptSpec`, `Verdict`, replay types | unchanged | none | none |
| `docs/API.md` | a section 1 subsection "The bundle's language" and German voice and model tables in section 2 | doc only | doc only |

The game must change nothing for Spanish. A German game would change its model ids (`SpeechEngine.cs`), its number and normalizer calls,
its article helper and its English error table, and would add a German bundle. That is game work and stays out of the core plan.

### 4.6 Determinism and replay

- Packs are D0 code under the same rules (ADR 0002): ordinal comparison, invariant culture, NFC, no clock, no `System.Random`. German
  folding uses explicit tables, never culture casing: `ToLowerInvariant` already maps `ẞ` to `ß`, and `ß` is never upper-cased.
- **Replay needs no new field.** The bundle hash identifies the bundle, and the bundle names its language. A Spanish record has no
  language and a Spanish bundle, so it replays as before. A German record's bundle hash already folds the language in. For readability,
  the envelope may carry `"language"` only when it is not `es` (`JsonIgnore(WhenWritingNull)`), and `Replayer` refuses an envelope whose
  language differs from the pipeline's bundle. Old logs have no field and are read as Spanish.
- `VersionStamp.ModelHashes` already distinguishes the German Vosk model by its own hash. No stamp change is needed.
- `HostStateHash`, `SeededRng.ForInteraction` and the interaction counter are language-free.

### 4.7 Project layout

> **Superseded 2026-10-26** (D14 amendment): the core becomes one neutral `Vocaterra.Nlp` plus one project per language
> (`Vocaterra.Nlp.Es`, later `Vocaterra.Nlp.De`). See `docs/nlp/research/2026-10-26-engine-and-language-packs.md`.

Two steps, so that the risky one is small:

1. **Phase A, in place.** The interfaces live in `Vocaterra.Nlp.Language` (namespace `Vocaterra.Nlp.Language.Packs`). `SpanishLanguagePack`
   sits beside the code it wraps. No file moves, no namespace changes, and the game's `using` lines are untouched.
2. **Phase B, a new pack project.** `src/Vocaterra.Nlp.Language.De` (normalizer, folding, numbers, phonetic key, morphology, chunker,
   checks, resolver rules, template functions, recast policy) with `tests/Vocaterra.Nlp.Language.De.Tests`. It references
   `Vocaterra.Nlp.Language` and `Core`; `Content` references it so that the default registry can resolve `de`. A later, optional milestone
   moves the Spanish code into `Vocaterra.Nlp.Language.Es` and `Vocaterra.Nlp.Phonology.Es` with the same namespaces (type identity is
   by assembly, so the game would add one project reference). Do it only if a third language arrives; it buys tidiness, not function.
   Language data stays content (lexicon, CEFR lists, grammars, dialogue); rule tables that are code today (valency, preposition case,
   copula adjectives) stay code in the pack, as `AnalysisTables` does, because the fuzz realiser models them.

Dependency direction after the change: `Core` ← `Language` (engines, interfaces) ← `Language.De` ← `Content` ← `Pipeline` ← tools.
`Response` references `Language` (interfaces) only. No sibling references are added.

### 4.8 Fuzz suite, goldens and lint per language

- **Fuzz.** The oracles stay. Their inputs become per-language: a `FuzzLanguage` record bundles the pack, the bundle, a sentence
  realiser, a mutation set and a text generator. The Spanish classes keep their names, seeds, generators and iteration counts unchanged,
  and German gets parallel classes under `De/` using the shared `Oracles/` infrastructure.
  - O3, O4 and O5 for German need a German `SentenceRealizer` (it is a model of the analyzer, so it is written with the checks).
  - O4's German mutations: swap an article's case or gender, change an adjective ending, move the finite verb out of second position, join
    a separable particle back on, use a finite verb after a modal, drop a noun's capital (typed), and transliterate an umlaut, which must
    yield no tag.
  - O6 (answer matching) perturbs with `ae`/`ä`, `ss`/`ß`, `v`/`f` and `dt`/`t`.
  - O12 checks "no inverted marks" for German instead of balanced `¿?`.
  - O8 and O9 wait for a German G2P.
  - O15 and O16 cover the new manifest and lexicon fields.
- **Goldens.** Existing files keep their paths. New ones go under `tests/Vocaterra.Nlp.Golden.Tests/Data/de/` (end-to-end goldens of the
  German sample bundle; a G2P golden only if a German G2P ships). `VOCATERRA_RECORD_GOLDEN` records per language.
- **Lint and CEFR.** `ContentLinter` takes its pronoun list, polite phrase and article/numeral homographs from `pack.Lint`. `cefr-check`
  is already content-driven. M30's LanguageTool check takes `language` from the manifest (`de-DE`) and keeps one cache and waiver file per
  bundle.
- **The merge gate** runs the German fuzz classes from the milestone that adds them. "Never weaken an oracle" applies per language.

### 4.9 The safety rule and its guard

Every Phase A milestone must keep Spanish byte-identical. The guard is **`SpanishIdentityGuard`**, built first (M31):

1. **A frozen input corpus.** The 96 real-lexicon homograph sentences of M18's `HomographSentencesTests` and the regression sentences of M26b, M29c and M29j (M26b's 1,102-sentence corpus is not committed; re-create it if it can be found), every intent example of `content/v1` and `content/game-v1`, every
   `docs/CLI_EXAMPLES.md` input, all 1,788 G2P golden words, and 5,000 sentences and mutations drawn once from the current fuzz generators at
   fixed seeds. The inputs are stored as data, so later generator changes cannot alter them.
2. **A frozen output baseline,** recorded on the pre-refactor commit. For each input it stores the SHA-256 of the canonical JSON of
   `TextNormalizer.Normalize`, `LinguisticAnalyzer.Analyze` (with the prompt's grammars), `AnswerMatcher.Match`, the `TextGrader` verdict,
   the full `PipelineResult` under the scripted recognizer with a fixed context, `RecastBuilder.Build`, every dialogue template rendered
   with sample slots, and `SpanishG2p.PhonemizeAll`. One JSONL line per input (about 10 MB of hashes, **est.**).
3. **A test** (`tests/Vocaterra.Nlp.Golden.Tests/SpanishIdentityTests.cs`) that recomputes every hash and fails on the first difference,
   naming the input and the stage.
4. **Gate rules for Phase A branches:**
   - `content/v1/manifest.json`'s `bundleHash` is unchanged;
   - no file under `tests/*/Data/`, `golden/` or `content/` changes;
   - the fuzz suite passes at the default depth and once at 10,000 grammar iterations;
   - the game's tests pass (M29f's lesson: build the whole solution).

The baseline is never re-recorded in Phase A. A later, deliberate Spanish behaviour change (a bug fix) re-records it in its own commit,
the way goldens are re-recorded today.

## 5. Staged roadmap

Milestone ids continue after M30. Tier H is Haiku 5.5 and S is Sonnet 5.5; sizes S, M and L as in the core plan. "§6" is the plan's
definition of done. All milestones run in worktrees through `scripts/merge-gate.sh`, and per CLAUDE.md the core's plan owns them. The
game is affected only where section 4.5 says so.

### Phase A: interfaces, Spanish unchanged

| Id | Milestone | Deliverables | Gate (beyond §6) | Tier | Size | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| M31 | Spanish identity guard | Corpus and baseline (4.9), `SpanishIdentityTests`, gate-rule script `scripts/identity-check.sh` | Baseline recorded on the base commit; the test passes on it; a deliberately changed template fails it | H | S | M29j (independent of M30) |
| M32 | Language id and pack registry | `ILanguagePack` and sub-interfaces, `SpanishLanguagePack` wrapping today's statics, `LanguagePacks` registry, manifest `language`/`supportLanguage` with the hash rule, `ContentBundle.Language`, loader validation, ADR 0011, API.md section | Identity guard green; `content/v1` hash unchanged; O15/O16 cover the new manifest fields | S | M | M31 |
| M33 | Text-side seams | Normalizer, folding, numbers, phonetic key, punctuation, recast spelling policy, repair lines (reserved pools with fallback), grammar defaults, lint vocabulary, `cefr-check`, pronunciation and speech-request G2P, Kokoro `lang`, CLI/bench model table; every static call site in the pipeline, content tools and response routed through `bundle.Language` | Identity guard green; game tests green; no `Spanish*` call left outside the Spanish pack and its compatibility entry points (grep check in the gate) | H | M | M32 |
| M34 | Analysis-side seams and schema | `IMorphology` (Spanish `MorphologyGenerator` implements it), `IAnalysisRules` (resolver rules, accent-variant choice, agreement checks, reported readings), `ITemplateFunctions`; `LexicalFeatures` optional members; `LexiconProfile` per language; `ErrorTagSet` with v1 frozen; `NormalizedText.FinalMark` (opt-in) | Identity guard green, including the full `PipelineResult` hashes; fuzz at 10,000 grammar iterations; the resolver's 96 homograph sentences unchanged | S | M | M33 |
| M35 | Per-language test infrastructure | `FuzzLanguage` harness with the Spanish suite plugged in unchanged; golden `Data/<lang>/` layout; a toy "xx" pack and a three-word bundle that loads and runs O1, O2, O7 and O14 through the pipeline, proving no hidden Spanish dependency remains | Spanish fuzz identical (same seeds give the same cases); the toy pack passes the neutral oracles | H | M | M34 |

### Phase B: a minimal German pack (A1)

| Id | Milestone | Deliverables | Gate | Tier | Size | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| M36 | German lexicon import | `KaikkiImporter` language profiles (`lang_code`, POS map, tag map for case, gender `n`, strong/weak/mixed, separable, auxiliary, participle); `fetch-lexicon-sources.sh` German pins (kaikki German, `de_50k`); `content/de-v1/lexicon/wiktionary-de.json` at the largest top N (try 2,000) that meets the gates; sidecar; LEXICON.md and ATTRIBUTION | Reproducible byte for byte; lexicon file ≤ 15 MB; bundle load ≤ 300 ms; spot-check of 50 A1 nouns (gender, plural) and 30 verbs (strong, separable, auxiliary); Spanish import output unchanged | S | M | M35 |
| M37 | German text layer | `GermanNormalizer` (contractions with case, `FinalMark`), `GermanFolding` (umlaut and ß, variant choice by context), `GermanNumbers` (0 to 999,999), `GermanPhoneticKey`, capital restoration in recasts, `orth.capital` from `Original` | Unit tests; O1 and O6 for German; no Spanish file changed | S | S | M36 |
| M38 | German morphology | `GermanMorphology`: articles (def, indef, `kein`, A1 possessives) by case, noun forms by number and case (lexicon first, n-Deklination), adjective declension (lexicon first, rule fallback), conjugation (present, imperative, Perfekt participle with auxiliary, Präteritum of `sein`/`haben`/modals), separable split, regularized strong forms (`fahrt`, `gehte`) | Paradigm tests against 100 lexicon lemmas; generated forms round-trip through lookup; no Spanish file changed | S | M | M36 |
| M39 | German analyzer (A1) | Chunker with case, gender and number intersection; preposition-case table; valency table for about 60 A1 verbs; subject-verb agreement (with `Sie`); V2 and bracket checks; separable particle and modal-infinitive checks; German resolver rules (article/pronoun homographs, `sie`/`Sie`, verb/noun pairs without capitals); reported readings; tags of section 2.2 for phase B | A hand-written corpus of 500 correct A1 sentences gives 0 false tags in typed form and at most 2 % in lowercase "speech" form; 200 learner-error sentences give the expected tag in ≥ 90 %; M26b-style report | S | L | M37, M38 |
| M40 | German grammar DSL and templates | `GrammarDefaults` for German (`bitte` anywhere, no implicit subject); a V2 option on grammars (`"order": "v2"` expands alternatives at load, matcher unchanged); separable verb element; template filters `article(def,acc)`, `decl(...)`, `particle`, German `words`; no inverted marks | Template and grammar tests; the Spanish DSL and grammar files parse to the same canonical patterns | S | M | M38 |
| M41 | German sample bundle | `content/de-v1`: greet, farewell, thank, confirm, deny, introduce_self, ask_price, buy and offer items; prompts, dialogue, feedback and repair pools, a hand-authored `cefr/a1.txt`, evaluation weights for the new tags; `lint --strict` and `cefr-check --level a1` clean; M30's LanguageTool pass at `de-DE` with waivers; German end-to-end goldens; CLI demo with `--bundle content/de-v1` | Lint strict clean; goldens recorded; the CLI demo's German examples documented; an LLM review pass recorded as provisional (the D-game-12 pattern) | H | M | M39, M40 (M30 for the LanguageTool step) |
| M42 | German fuzz | German `SentenceRealizer`, `MutationGen`, text and lexicon-catalog generators; O3, O4, O5, O6 and O12 for German; iteration minimums with several standard deviations of margin (M29e's lesson) | Green at the default depth and at 10,000 grammar iterations; any analyzer bug found is fixed at the source; Spanish fuzz untouched | S | M | M41 |
| M43 | German speech | Pin `vosk-model-small-de-0.15`; a German Parakeet test on the pinned model; `PiperSynthesizer` (sherpa-onnx VITS) with `de_DE-thorsten-medium` (CC0) pinned in `models/manifest.json`; the pronunciation stage off for `de` (no G2P yet); API.md voice table | Model-gated tests skip without models; deterministic decode (M26g's check); D2 stance noted: private builds only | H | M | M35 (parallel with M36 to M42) |

### Phase C: what A2 needs

| Id | Milestone | Deliverables | Gate | Tier | Size | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| M44 | Perfekt, past and subordinate clauses | Perfekt as a two-part form; `aux.choice`; participle regularization (`gegeht`); `conj.tense` with past markers (`gestern`, `letzte Woche`); Präteritum of the modals; `weil/dass/wenn/ob` clauses with `word_order.verb_final`; two-way prepositions (motion versus location verbs); reflexive verbs | Corpus gates as M39, at A2; fuzz mutations for each new tag | S | L | M42 |
| M45 | Compounds and an A2 lexicon | Compound splitter (linking elements, head features) for otherwise unknown words; lexicon at the largest top N within the gates; hand-authored `cefr/a2.txt` | Unknown-word rate on an A2 text sample; load and size gates | S | M | M44 |
| M46 | German G2P and pronunciation (conditional on D13) | Lexicon-first G2P from Wiktionary IPA (`sounds` in the kaikki data) with a rule fallback; German phone inventory and predicted-error variants (`pron.umlaut`, `pron.vowel_length`, `pron.ich_ach`); G2P golden; espeak-compatible phone output for Piper once the GPL-free sherpa-onnx is pinned | O8 and O9 for German; a G2P golden of 1,000 or more words | S | L | M45, D13, D2 pin |
| M47 | German tagger spike (conditional) | Only if M39/M44 false-positive gates fail: UDPipe 1 trained on UD German-HDT versus Stanza `de` versus spaCy `de_core_news_sm` on the M39 corpus, behind the resolver seam; reading filter only | A measured false-positive drop with no loss on the error corpus; D10 settled for the chosen tool | S | M | M44, D10 |
| M48 | Register (du and Sie) | Agent content declares `register`; a `register` tag; templates choose `du`/`Sie` forms by a host or agent slot | Content opt-in only; Spanish bundles unaffected | H | S | M41 |

### Totals and risks

| Phase | Milestones | New or moved source lines (**est.**) | Test lines (**est.**) | Orchestrated time (**est.**) |
| --- | --- | --- | --- | --- |
| A | 5 (2 S-tier, 3 H-tier) | about 1,500 moved or wrapped, about 800 new | about 1,500 plus the baseline data | 3 to 4 days |
| B | 8 (6 S, 2 H) | about 7,000 (pack 5,000, importer 600, speech 400, content tools 300, bundle data excluded) | about 9,000 (unit 4,000, fuzz 4,000, goldens and corpus 1,000) | 2 to 3 weeks |
| C | 5 (4 S, 1 H; 2 conditional) | about 4,500 | about 4,500 | about 2 weeks, plus M46 and M47 if triggered |
| All | 18 | about 13,000 | about 15,000 | 5 to 6 weeks of orchestrated work, plus owner and reviewer time for German content |

The riskiest items, in order:

1. **M39, the German analyzer.** Case checks on syncretic articles, plus role assignment without a parser, are where false positives
   will come from. The corpus gate and the charitable fallback contain it, and M47 is the escape hatch.
2. **M34, the analysis-side extraction in Phase A.** The resolver and checker are tightly coupled (the fallback's cost is "errors the
   checks report"), so extracting them is where an accidental Spanish change could hide. The identity guard over the full
   `PipelineResult` is the defence; the work suits Sonnet.
3. **German speech out.** No German Kokoro voice; Piper needs espeak-ng phonemes (GPL, D2); the GPL-free sherpa-onnx is still
   unreleased. Pre-rendered lines and private builds are the interim answer.
4. **Lexicon size.** German adjectives (three declensions times cases, genders and numbers) and verbs (separable and participle forms)
   bring many forms per lemma. Spanish needed 41,078 entries for 3,000 lemmas and met the 300 ms load gate only at that size. German may
   need a smaller top N or a compact paradigm encoding (**est.**: 60,000 to 90,000 entries at 2,000 lemmas).
5. **Content trust.** No German reviewer; the Goethe list cannot be shipped; LanguageTool and an LLM pass are the interim checks, as in
   D-game-12.

## 6. Decisions for the owner

| # | Decision | Options | Recommendation | Waiting on it |
| --- | --- | --- | --- | --- |
| 1 | German in the game, or in the core only | (a) core and a core sample bundle only; (b) also a German game mode later; (c) a German game now | (a) now: Phase A and B produce a tested core and `content/de-v1`. The game decides separately (a game decision; content, recordings, an English support table) | the game's planning only |
| 2 | Variety | de-DE standard; de-AT; de-CH | de-DE for content and the voice. Grading accepts Austrian and Swiss spellings and variants (ADR 0005's principle: Swiss `ss` for `ß` is not an error) | M37, M41 |
| 3 | `du` or `Sie` in content | `du` throughout (game tone); `Sie` for adults and strangers; per agent | Per agent with a default of `Sie` for shopkeepers and strangers and `du` for a guide or child. Real German usage splits this way, and it teaches both forms. `register` stays an opt-in tag (M48) | M41 |
| 4 | Tagger route and MPL-2.0 (D10's open half) | none (hand-written only); UDPipe 1 self-trained (MPL-2.0 binding, CC BY-SA model); Stanza or spaCy as a sidecar process | Hand-written for A1/A2. Decide D10 only if M47 triggers. spaCy `de` (MIT) and Stanza `de` (Apache-2.0) remove the licence obstacle that drove Spanish towards UDPipe | M47 |
| 5 | German TTS and its licence | Piper `thorsten`/`kerstin` (CC0 voice data) through sherpa-onnx with espeak-ng (GPL, private builds per D2); the same after the GPL-free pin with an in-house German G2P (M46); a community Kokoro fine-tune after an ONNX export; pre-rendered lines only | Piper `thorsten` (or `kerstin` for a female voice) for private builds now, with pre-rendered lines for anything distributed. Revisit at the GPL-free sherpa-onnx pin | M43, M46 |
| 6 | Whether the game ever mixes languages | one target language per bundle and per session; several bundles side by side (one per language) with one learner model; code-switching inside one utterance | One target language per bundle, and per conversation. A host may load several bundles. The learner model's skill ids should carry the language (`word.de.Baum`) if a player learns two. Mixing within an utterance is out of scope; the ASR could not grade it | ADR 0011 |
| 7 | (Consequence of 1) the CEFR lists | hand-authored using Goethe as reference; ask the Goethe-Institut for permission | Hand-authored, as Spanish was | M41, M45 |

D1, D4 and D8 to D13 are not changed by this report. D4 (OpenSubtitles terms) applies to `de_50k` exactly as to `es_50k`. D2 now blocks
German speech out as well as Spanish.

## 7. Sources

Accessed 2026-10-22.

**Speech**
1. Vosk models (German rows and licences; small Spanish row): https://alphacephei.com/vosk/models
2. Kokoro-82M `VOICES.md` (nine languages, 54 voices, no German): https://huggingface.co/hexgrad/Kokoro-82M/blob/main/VOICES.md
9. NVIDIA Parakeet TDT 0.6B v3 model card (25 languages including `de`; CC-BY-4.0; German WER): https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3
10. OpenAI Whisper README (MIT for code and weights; model sizes): https://github.com/openai/whisper
11. Whisper `tokenizer.py` (`"de": "german"`): https://raw.githubusercontent.com/openai/whisper/main/whisper/tokenizer.py
12. Thorsten-Voice German Kokoro fine-tune (Apache-2.0, PyTorch only, misaki German G2P on espeak-ng): https://huggingface.co/Thorsten-Voice/Kokoro
13. Piper voices, `de/de_DE`: https://huggingface.co/rhasspy/piper-voices/tree/main/de/de_DE
14. Piper German model cards: `thorsten/medium` (CC0, fine-tuned from lessac): https://huggingface.co/rhasspy/piper-voices/raw/main/de/de_DE/thorsten/medium/MODEL_CARD ; `kerstin/low` (CC0): https://huggingface.co/rhasspy/piper-voices/raw/main/de/de_DE/kerstin/low/MODEL_CARD ; `pavoque/low` (CC BY-NC-SA 4.0): https://huggingface.co/rhasspy/piper-voices/raw/main/de/de_DE/pavoque/low/MODEL_CARD ; `eva_k`, `karlsson`, `ramona` (M-AILABS, "See URL"): https://huggingface.co/rhasspy/piper-voices/raw/main/de/de_DE/eva_k/x_low/MODEL_CARD ; M-AILABS custom licence (search summary, not a primary source; **unverified**): https://catalogue.yorvoice.york.ac.uk/catalogue/IB9IDJXF
20. sherpa-onnx German Piper builds (`vits-piper-de_DE-thorsten-medium` in sherpa-onnx app packages): https://huggingface.co/csukuangfj/sherpa-onnx-apk/commits/179e0b5d21be81da1bf061825837849131788a8d/tts-new/1.10.23/sherpa-onnx-1.10.23-arm64-v8a-de-tts-vits-piper-de_DE-thorsten-medium.apk
21. Piper engine `piper1-gpl` (GPL-3.0, embeds espeak-ng): https://github.com/OHF-Voice/piper1-gpl
22. sherpa-onnx releases (latest v1.13.8; no GPL-free release yet): https://github.com/k2-fsa/sherpa-onnx/releases

**Lexicon, frequency and CEFR**
3. kaikki.org German extract (353,033 word forms, about 1.0 GB, deprecated): https://kaikki.org/dictionary/German/index.html
4. kaikki.org German Wiktionary raw data (2.9 GB, dump 2026-10-01): https://kaikki.org/dewiktionary/rawdata.html
5. English Wiktionary copyrights (CC BY-SA 4.0 and GFDL): https://en.wiktionary.org/wiki/Wiktionary:Copyrights
6. German Wiktionary licence (CC BY-SA 4.0): https://de.wiktionary.org/wiki/Wiktionary:Lizenzbestimmungen
7. hermitdave/FrequencyWords (CC BY-SA 4.0 content, MIT code; `de_50k.txt`): https://github.com/hermitdave/FrequencyWords
8. Goethe-Zertifikat A1 Start Deutsch 1 Wortliste (29 pages, about 650 words, no licence statement): https://www.goethe.de/pro/relaunch/prf/ca/A1_SD1_Wortliste_02.pdf

**Taggers and treebanks**
15. UD German-HDT (CC BY-SA 4.0): https://universaldependencies.org/treebanks/de_hdt/index.html
16. UD German-GSD (CC BY-SA 4.0): https://universaldependencies.org/treebanks/de_gsd/index.html ; German treebank comparison: https://universaldependencies.org/treebanks/de-comparison.html
17. UDPipe 1 models (German-GSD and German-HDT scores; CC BY-NC-SA models): https://ufal.mff.cuni.cz/udpipe/1/models
18. Stanza German model (Apache-2.0): https://huggingface.co/stanfordnlp/stanza-de
19. spaCy `de_core_news_sm` (MIT; TIGER, Tiger2Dep, WikiNER; UPOS 0.980, UFeats 0.907): https://huggingface.co/spacy/de_core_news_sm

**Grammar checking**
23. LanguageTool (LGPL-2.1 or later): https://github.com/languagetool-org/languagetool

**Repository (read at `ecd646d`)**
`docs/nlp/IMPLEMENTATION_PLAN.md` (§1–§5, §5b M26b, M29c, M29j, §7, §9, §10); `docs/nlp/DECISIONS.md`; `docs/nlp/API.md`; `docs/nlp/adr/0002`, `0005`,
`0007`, `0008`, `0009`; `docs/nlp/LEXICON.md`; `docs/nlp/FUZZING_PLAN.md`; `models/manifest.json`; `docs/nlp/research/SCALING_APPROACHES.md`;
`docs/game/DECISIONS.md` (D-game-9, 10, 12, 13, 16); `docs/game/ARCHITECTURE.md`; source files as cited in section 1.
