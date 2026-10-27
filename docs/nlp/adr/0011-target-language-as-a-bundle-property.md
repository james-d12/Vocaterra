# ADR 0011: Target language as a bundle property

- Status: accepted
- Date: 2026-10-27
- Deciders: repository owner (decision D14, the language-agnostic goal); design in `docs/nlp/research/2026-10-22-multilingual.md` sections 4.1 to 4.6 and 4.9,
  as amended by `docs/nlp/research/2026-10-26-engine-and-language-packs.md`; milestone M32 of `IMPLEMENTATION_PLAN.md`
- Relates to: [ADR 0009](0009-core-and-game-boundary.md) (the boundary), [ADR 0012](0012-one-engine-assembly-and-language-packs.md) (one engine, packs beside it).
  Number 0010 is reserved for D9's determinism record.

## Context

Everything the core reads or produces as the learner's language is Spanish today, in static classes (`TextNormalizer`, `SpanishNumbers`,
`SpanishPhoneticKey`, `SpanishG2p`, the punctuation fixer, the repair lines, the grammar defaults, the linter's pronoun list). The owner wants a second
language to be possible beside Spanish. Two things have to be settled before any code can be made neutral: which language a bundle is in, and how the
engine finds the code for it. Spanish must stay byte-identical while this is done (the Phase A safety rule, research 4.9).

## Decision

1. **The language is a property of the bundle.** `manifest.json` gains an optional `"language"`, a BCP-47 tag (`es`, `es-MX`, `de-DE`), and an optional
   `"supportLanguage"`. One bundle has one target language. An absent `language` means `es`, so `content/game-v1` needs no edit and keeps its hash.
   `ContentManifest.Language` and `ContentManifest.SupportLanguage` are `string?` with `JsonIgnore(WhenWritingNull)`, appended as optional trailing
   record members, so positional construction and deconstruction of the five old members compile and behave as before.
2. **The bundle hash includes the language only when the field is present.** With a language it is
   `Combine("language", tag, path1, sha1, ...)`, otherwise exactly `Combine(path1, sha1, ...)` as before. The tag is hashed as written, so `es` and `es-MX`
   give different hashes (they are different manifests; the pack is the same). `Combine` frames every part with its length, and in the old form the second part is a 64-digit hexadecimal
   digest while a tag has a hyphen after at most eight letters or is at most eight letters long, so a bundle with a file named `language` cannot make
   the two forms collide. `ContentManifest.ComputeBundleHash(files, language)` is the one place that implements the
   rule; the loader's verification, `ContentManifest.Compute` and therefore the ContentBuilder `hash` command all use it. `Compute` keeps the language
   fields of an existing manifest unless an argument overrides them, and `hash` takes `--language TAG` and `--support-language TAG`.
3. **`supportLanguage` is recorded and passed through, never interpreted, and is not hashed.** The core does not process the learner's first language,
   so no behaviour depends on it, and the hash identifies the behaviour of the bundle for replay. Hashing it would change the bundle hash, and with it
   every verdict stamp and replay record, for a change that alters no output. The same holds for `bundleId` and `contentVersion`, which are not hashed
   either; the manifest is not hashed as a file, only its language is folded in. The loader does not validate its syntax (it never reads it); the
   ContentBuilder does not either.
4. **A registry resolves the pack.** `LanguagePacks` (`Vocaterra.Nlp.Language.Packs`) maps a primary language subtag to an `ILanguagePack`.
   `LanguagePacks.Default` holds `es`; `LanguagePacks.Of(...)` and `With(...)` build another set; `TryResolve(tag)` reads the primary subtag of a
   well-formed tag and finds the pack, ordinal and case-insensitive on the subtag (`es-MX`, `ES` and `es` find the same pack). A host passes its own
   registry through `ContentLoadOptions.LanguagePacks`. The registry type knows no Spanish except the `Default` property, so M53 can move the Spanish
   registration out of the engine without touching it. A pack's id must be a lower case primary subtag; `With` refuses anything else.
5. **The loader validates and resolves.** A present `language` must be well-formed (RFC 5646 syntax: subtags of 1 to 8 ASCII letters or digits joined
   by single hyphens, the first of 2 to 8 letters; not checked against the IANA registry) and its primary subtag must have a pack. A malformed tag and an
   unregistered language are each a `ContentValidationException` with one problem that starts with `manifest.json:` and names the tag (and, for the
   unregistered one, the registered ids). This check runs before the hashes are verified and also when `VerifyHashes` is off, so a wrong language is
   reported as such and not as a hash mismatch. An absent language is looked up as `es`, so a registry without Spanish refuses a bundle without a language.
6. **`ContentBundle.Language` and `ContentBundle.LanguageId`.** `Language` is the resolved `ILanguagePack`; `LanguageId` is its id, the primary subtag
   (`es` for a manifest language of `es-MX`). The manifest keeps the tag as written.
7. **The pack interface, text side only.** `ILanguagePack` has `LanguageId`, `Normalizer` (`ITextNormalizer`), `Folding` (`ILexiconFolding`: lookup key,
   lenient key and the matching key), `Numbers` (`INumberWords`), `PhoneticKey` (`IPhoneticKey`), `Punctuation` (`IPunctuation`), `G2p` (`IG2p?`, null
   when pronunciation scoring is off), `GrammarDefaults` (optional subject pronoun, the pronoun list, the trailing polite words), `Lint`
   (`LintVocabulary`: subject pronouns, polite phrase, article and numeral homographs) and `RepairFallback` (`RepairLines`: ask again, slow repeat, give
   up). These are the members the call sites of M33 are routed through. **Left out, for M34:** `CreateMorphology`, `CreateAnalysisRules`,
   `CreateTemplateFunctions`, `LexiconProfile`, `ErrorTagSet` and the recast policy. They wrap types (`MorphologyGenerator`, the analyzer rules, the
   template engine) whose construction depends on the lexicon and whose neutral interfaces do not exist yet; adding them now would freeze interfaces
   that M34 has to design with the German needs in view.
8. **`SpanishLanguagePack.Instance` wraps the statics and moves nothing.** Each member forwards to the static it names; the statics keep their public
   entry points, their behaviour and their Spanish meaning, so the game compiles unchanged. A test per member checks the forward returns exactly what the
   static returns. The pack does not own the Spanish word lists: the pronoun list comes from `WordClasses` (made enumerable, in listing order), and the
   polite words and homographs are literals in the pack beside the literals in `LintCoverage`, `SlotGrammarMatcher` and `AnalysisSlotFinder`. M33 removes
   those duplicates when it routes the call sites. M33 routed the call sites (see the amendment below).
9. **Layering.** The packs live in `Vocaterra.Nlp.Language.Packs`, in the `Language` folder, but wrap code of `Phonology` and `Response`, which
   `Language` may not use (ADR 0012). `LayerRules` therefore gives the packs a rule of their own (they may use `Core`, `Language`, `Phonology` and
   `Response`; nothing else, so not `Content` or `Pipeline`) and the `Language` rule no longer covers `Language.Packs`. `Content` already depends on
   everything but `Pipeline`, so `ContentLoader` and `ContentBundle` may use the registry. Note for M33: `Response` is allowed to depend on `Language`,
   and the pack depends on `Response` for the punctuation fixer, so a `Response` call site that takes an `ILanguagePack` makes a namespace cycle that the
   rules permit; M33 should move the punctuation behind the interface, or keep the cycle on purpose, and say which. **Decided in M33: the cycle is removed** (see the amendment below).
10. **Replay needs no new field.** The bundle hash identifies the bundle and the bundle names its language, as research 4.6 says. The envelope's
    optional `language` is not added in this milestone.

## Consequences

- A Spanish bundle without `language` loads, hashes and replays exactly as before: `content/game-v1` keeps hash `aab525f9...`, the goldens and the
  Spanish identity baseline are untouched.
- A bundle may now declare `language`; today only Spanish tags load. Adding `"language":"es"` to a manifest changes its bundle hash (and so every
  verdict stamp), which is the intended property: a language can never be changed silently under a hash. Hosts that want the old hash leave the field out.
- All additions are public API and additive (docs/nlp/API.md section 1, "The bundle's language"): `ContentManifest.Language`, `.SupportLanguage`,
  `.DefaultLanguage`, `ComputeBundleHash(files, language)` and two optional parameters of `Compute`; `ContentLoadOptions.LanguagePacks`;
  `ContentBundle.Language` and `.LanguageId`; and the types of `Vocaterra.Nlp.Language.Packs`. The `ContentBundle` constructor is internal.
- `ContentManifest` is a positional record: `with` expressions, equality (now including both fields) and `ToJson` handle the new members; a consumer
  that deconstructs the manifest into five variables would break (none found in the core, the tools, the tests or the game).
- `LanguagePacks.TryGetPrimarySubtag` is a syntax check only. A tag such as `zz` is well-formed and fails as "no pack registered", not as malformed.

## Alternatives considered

- **Hash the language always (`Combine("language", "es", ...)`).** It would change the hash of every existing bundle and every recorded verdict and
  replay, and force a re-record of goldens and the identity baseline. Rejected by the Phase A rule.
- **Put the language in the options of the loader instead of the manifest.** A host could then load a German bundle as Spanish. The manifest, and the
  hash that covers it, make the language part of the bundle's identity.
- **Hash the normalized primary subtag instead of the tag as written.** It would make `es` and `ES` one bundle, but two manifests that differ are two
  bundles to a reader of the files, and the tag is data the bundle author controls. The tag is hashed as written.
- **Interpret `supportLanguage` now** (support templates keyed by tag). Research 4.4 treats it as optional and later; nothing needs it.
- **Include the analysis-side members in `ILanguagePack` now.** See decision 7.

## Amendment, M33: the text-side seams (2026-10-27)

M33 routed every text-side Spanish call site of the evaluation path, the response path and the content tools through `bundle.Language`, with
Spanish output byte-identical (the identity guard `scripts/identity-check.sh f4694ff` passes, and the baseline was not re-recorded).

**What M33 routed.**
- *Pipeline* (`UtterancePipeline`): the normalizer, the answer matcher and the answer corrections (`Language.Folding.MatchingKey`, the phonetic key),
  the intent resolver (`IntentResolver(catalog, language, embeddings, options)`), the analyzer (`LinguisticAnalyzer(lexicon, [morphology,] language)`),
  the digits and G2P of pronunciation scoring (no pronunciation scoring when `G2p` is null), the speech requests (`SpeechRequests.Create(text, voice,
  language)`), the recast (`RecastBuilder.Build(text, analysis, lexicon, language)`), the answer phrases of the recognizer grammar
  (`AnswerSet.GrammarPhrases(language)`) and the repair lines.
- *Response*: `TemplateEngine(lexicon, morphology, language)` reads numbers, number agreement and punctuation from the pack; `RecastBuilder` reads
  the number words, the agreement and the accent policy from it.
- *Grammar defaults*: `SlotGrammarMatcher.Match(tokens, grammar, language)` adds the implicit subject pronoun only when
  `GrammarDefaults.OptionalSubjectPronoun` is set, and matches the trailing polite phrase word by word from `TrailingPoliteWords`.
  `AnalysisSlotFinder.FirstNoun` skips that phrase through the pack.
- *Content tools*: `TextGrader`, `ContentLinter` (its `LintContext`), `LintCoverage` (the subject pronouns, the polite phrase and the article and
  numeral homographs of `Lint`), `CefrChecker` (`Check(dialogue, lexicon, language, allowed, level, prompts)`), `LintConfusion`, the validator's
  template engine, and the ContentBuilder through them.
- *Bench and CLI*: `BenchMetrics.Tokens` and `ErrorsKept` take the pack; the bundle's content supplies it.
- *Kokoro*: `KokoroSynthesizer(modelDir, speakerId, speed, modelHash, lang = "es")` and `FromStore(..., lang = "es")`. `lang` is folded into the
  model hash only when it is not `es`, so the Spanish `ModelHash` is unchanged.
- *Model tables*: the CLI and Bench name the language of each model they run (`ModelLanguages`), and refuse a model whose language is not the
  bundle's before opening it.

**What M33 added to the pack (narrowest members).**
- `IRecastPolicy` with `RestoredSpellings(text, analysis, lexicon)` and the member `ILanguagePack.Recast`. The Spanish policy (`SpanishRecastPolicy`)
  holds the accent restoration that was in `RecastBuilder` (the lexicon's accented spelling of a folded word, then the analysis's accented reading);
  the caller still drops a restoration that overlaps an error edit.
- `INumberWords.Agree(words, gender)`: the number agreement (`uno` becomes `un` or `una`).

**The repair lines (M33).** `RepairStrategy.Next(attempt, prompt, retry, choices, RepairLines lines)`. The pipeline gives attempt 1 the line of the
reserved pool `repair.ask_again` and attempt 2 the line of `repair.slow_repeat` when the bundle has the pool (one variant drawn with the
interaction's seed; a bundle without the pools draws nothing), else the pack's `RepairFallback`. The give-up line is always the pack's. The pool
names are reserved (`ReservedPools.RepairAskAgain`, `RepairSlowRepeat`), so the linter does not call them unused. `content/game-v1` has no such
pool and speaks the same lines.

**The cycle decision (point 9).** The punctuation fixer moved from `Response/Templates` into `Language/Packs` (`PunctuationFixer`, internal, the
implementation of the Spanish `IPunctuation`), and so did the number agreement (`NumberAgreement`, the implementation of `INumberWords.Agree`). The
pack therefore uses no `Response` code; `Response` uses the packs (`Response` may use `Language`). The namespace cycle is removed, not kept.
`LayerRules` gives the packs `Core`, `Language` and `Phonology` and no `Response`, and `LayeringTests` checks the absence of the cycle
(`ThePacksAndTheResponseStageHaveNoCycle`).

**The Spanish compatibility entry points.** `TextNormalizer`, `SpanishNumbers`, `SpanishPhoneticKey`, `SpanishG2p`, `MorphologyGenerator`,
`RecastBuilder.Build(text, analysis[, lexicon])`, `SpeechRequests.Create(text, voice[, dialect])`, `TemplateEngine.CreateDefault()` keep their
public signatures and their Spanish meaning; the game calls some of them (`GuideFacts`, `WordProgress`, `NounCatalog`, `GuideSession`) and compiles
unchanged. Each carries an XML remark that names it as a Spanish compatibility entry point. `SpanishCallSiteTests` (the grep guard) allows the
Spanish statics only in the pack, in their own files and in these entry points.

**Left for M34 (listed by file in the guard's allowlist).** The morphology generator, the analysis rules (`ReadingResolver`, `AgreementChecker`,
`ReportedReadings`, `AccentVariantChoice`, `ReadingClasses`, `AnalysisTables`), the typed matching of the slot grammar (`TextFolding` and
`WordClasses` in `SlotGrammarMatcher` and `SlotGrammar`), and the lexicon's own Spanish fold (`Lexicon` and the intent catalog's lemma fold). These
need `IMorphology`, `IAnalysisRules` and `LexiconProfile`: the lexicon's indexes are built with the Spanish fold at load, so its lookups cannot take a
pack's fold before the lexicon has a profile. The offer line of the repair ladder ("¿Quieres decir «…» o «…»?") is Spanish text in `RepairStrategy`
and is not a pack member yet, and the pronunciation scorer's Spanish-tuned predicted-error tags are M34's too (research 2026-10-22 section 1.2,
item 14).

## Amendment, M34: the analysis-side seams (2026-10-27)

M34 puts the analysis side of the language behind the pack. After it, no engine code outside the Spanish pack, the Spanish statics' own files and the
documented compatibility entry points calls Spanish code (`SpanishCallSiteTests` checks it). Spanish output is byte-identical: `content/game-v1`
(bundle hash `aab525f9...`), the goldens, the identity baseline and the 96 homograph sentences are unchanged. Everything below is additive except the
signatures marked breaking in `API.md`; the game compiles and passes unchanged.

**The pack grew five members.**
- `LexiconProfile LexiconProfile` (`Vocaterra.Nlp.Language.Lexicon.LexiconProfile`): the closed vocabularies of the lexicon files (genders, numbers,
  persons, tenses, moods) and, for later languages, `Cases`, `VerbForms`, `Declensions`, `Particles` and `Registers`. An empty list means the language does not
  use the feature, and an entry that sets it is refused. `LexiconProfile.V1` is the schema v1 and the Spanish profile: a Spanish lexicon file with gender
  `n` is still refused (`unknown gender 'n'`), and so is a `case` key (`feature 'case' is not used by this language`). The loader reads the file with the
  pack's profile (`Lexicon.ReadFile(json | bytes, profile)`), and the lexicon cache keys carry the language id, so one file read under two languages is two entries.
- `ErrorTagSet ErrorTags` (`Vocaterra.Nlp.Core.Taxonomy.ErrorTagSet`): the tags of a language and their ranks (`Rank(tag)`: the position, or `Count` for an unknown
  tag). `ErrorTagSet.V1` lists `ErrorTags.All`, which stays the frozen v1 Spanish set in its order (the game's `EnglishText.MissingErrorTags` reads it). The analyzer
  and the verdict assembler (through the new optional `VerdictInputs.ErrorTags`) order errors with the pack's ranks. The pronunciation scorer's predicted-error tags
  are still Spanish-tuned (research item 14): `PredictedErrorMatcher` keeps `ErrorTags.All`, and a language with other `pron.*` tags needs its own scorer (M54+).
- `IMorphology CreateMorphology(Lexicon)`: `IMorphology` (nouns, articles, adjectives, conjugation and regularized forms: `Pluralize`, `Singularize`, `InflectNoun`,
  `Article` x2, `AgreeAdjective`, `Conjugate`, `IsRegularizedIrregular`, `TryGetRegularization`). `MorphologyGenerator` implements it; `RegularizedForm` is public.
- `IAnalysisRules CreateAnalysisRules(Lexicon, IMorphology)`.
- `ITemplateFunctions CreateTemplateFunctions(Lexicon, IMorphology)`.

**What moved behind each.**
- *`IAnalysisRules`*: the closed word classes and lists that the shared code consults (`IsDeterminerAdjective`, `IsSubjectPronoun`, `IsClitic`, `IsPreverbalPronoun`,
  `IsVerbSeekingQuestionWord`, `IsNounModifyingAdverb`, `IsInterrogativeQuantifier`, `IsInterjectionFirst`, `IsNearFutureAuxiliary`, `IsNearFutureLinker`, `IsCopula`,
  `IsCopulaOrAuxiliary`) and `CreateAgreementChecker(resolved, errors)`, which returns an `IAgreementChecker` (`Run`, `HasPastTimeMarker`). The Spanish tables
  (`SpanishAnalysisTables`, from `AnalysisTables`) and the Spanish checks (`SpanishAgreementChecker`, from `AgreementChecker`) moved into `Language/Packs` with
  `SpanishAnalysisRules`. `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`, `AccentRestoration`, `ReadingClasses`, the typed matching of the slot grammar and
  `LinguisticAnalyzer` stay shared and take the rules (and the pack's folding) instead of calling the statics. `ReadingClasses` and `WordClass` are public, so a pack in
  another assembly can write its checker with them. The shape of the resolver's local-syntax rules (what follows a determiner, a subject pronoun, a preposition) stays
  shared: it is written over word classes, and the language-specific lemmas it needs are the predicates for the near future and the copula.
- *`ITemplateFunctions`*: the inflection behind `article()`, `words()`, `conj()` and `plural-if()` (`ResolveVerbLemma`, `Conjugate`, `InflectNoun`, `InferNumber`, `GenderOf`,
  `Article`). `TemplateEngine` still parses and validates placeholders and renders; the Spanish gender heuristic and the lexicon reads moved to
  `SpanishTemplateFunctions`. The `conj` tense is checked against the pack's `LexiconProfile.Tenses`.
- *The lexicon's fold*: `Lexicon.FromEntries(entries, folding, profile)` and `Lexicon.Load(json, folding, profile)` build the indexes with the pack's `ILexiconFolding`
  (`Key` for forms and lemmas, `LenientKey` for the accent-insensitive index); `Lexicon.Folding` exposes it and every lookup uses it. `IntentCatalog.Load(json, folding)` and
  `SlotGrammar.Parse(pattern, id, folding)` lower-case their lemmas and literals with the pack's `Key`; with no folding they use the language-neutral key (NFC and invariant
  lower case, `TextKey`), which is the Spanish key. The loader passes the pack's folding to all three.
- *The offer line*: `RepairLines` gained `Offer`, an `OfferLineFormat` (prefix, quotes, separators, suffix) with `Format(choices)`; `RepairStrategy.OfferFormat` is the Spanish
  one and gives the same text as before. A `RepairLines` without `Offer` speaks Spanish. No reserved pool overrides it: a pool line is a template without a choices
  placeholder, and the offer needs the list, so the wording is a pack member only.

**`ContentBundle.Morphology` (the decision).** `bundle.LanguageMorphology` (`IMorphology`) is the new neutral property, and the engine (the analyzer, the template engine, the
pipeline, the grader, the linter) uses it. `bundle.Morphology` keeps its type, `MorphologyGenerator`, because the game's `NounCatalog`, `GuideSession` and book pages call
its Spanish members; it returns `LanguageMorphology` as a `MorphologyGenerator` and throws `NotSupportedException` for a bundle whose pack creates another
implementation. It throws on access, not at load, so a non-Spanish bundle loads and runs; only a host that asks for the Spanish type fails, with a message that names
`LanguageMorphology`. For a Spanish bundle both properties are the same object. `bundle.AnalysisRules` is the third new property.

**Optional features of the reading and of the file.** `LexicalFeatures` and the file-side `EntryFeatures` gained trailing optional members `Case`, `VerbForm`, `Declension`,
`Particle` and `Register`, all null by default and omitted from JSON while null (`JsonIgnore(WhenWritingNull)`), so Spanish readings, lexicon files, `Lexicon.ToJson()` and
every hash serialize as before and record equality is unchanged while they are null. The lexicon reader accepts the keys `case`, `verbForm`, `declension`, `particle`
and `register`, and the profile of the pack decides whether they are allowed. Sorting compares them last, so entries without them keep their order.

**`NormalizedText.FinalMark`.** Added: `?`, `!` or null, set only when `NormalizationOptions.CaptureFinalMark` is true (default false), looked up after trailing white space and
closing quotes or brackets (the full-width marks map to `?` and `!`). It is omitted from JSON while null, taken into equality always and into the hash only when set, so every
Spanish result and hash is unchanged. The pipeline does not set the option (a later milestone that needs the mark does).

**Left out, and why.**
- A reserved dialogue pool for the offer line (above).
- `PredictedErrorMatcher`'s tag order: pronunciation scoring is language-specific until a second language has a scorer.
- The seed lexicon's Spanish default: `Lexicon.Seed`, `Lexicon.Empty` and the overloads without a folding use the Spanish folding, because the embedded seed is Spanish and 32
  test files and the ContentBuilder call them. A bundle's lexicon never uses it. The guard allows exactly that one reference (`Lexicon.cs`, `SpanishLanguagePack`).
- The shape of `ReadingResolver`'s rules is shared, not pluggable per language; a language whose syntax does not fit it (verb-second order, separable prefixes) needs the rule set
  extended in M54+, with its own homograph sentences.

**The guard after M34.** The allowlist lost every M34 entry: `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`, `AnalysisTables`, `AgreementChecker`, `ReadingClasses`,
`SlotGrammar`, `SlotGrammarMatcher`, `IntentCatalog` and the `Lexicon.cs` fold. Two kinds remain, each with a reason in the test: the three `MorphologyGenerator.cs` entries (the Spanish
`IMorphology` implementation is a Spanish static class in its own file, and moves into the Spanish pack project in M53) and `Lexicon.cs` for `SpanishLanguagePack` (the seed default).
Two patterns were added (`new MorphologyGenerator(` and `SpanishAnalysisTables.`) so a neutral file cannot construct the Spanish morphology.

**For M53 and M35.** Every seam a pack needs is a public type with a summary: no `internal` engine member is required to implement `ILanguagePack`. The Spanish classes the pack
owns (`SpanishAnalysisRules`, `SpanishAgreementChecker`, `SpanishTemplateFunctions`, `SpanishAnalysisTables`, `WordClasses`) are internal to the engine assembly today and move with the
pack. M35's toy pack implements all five new members to prove that nothing Spanish is left on the analysis path.

## Amendment, M35: the toy language (2026-10-27)

M35 wrote a toy pack, `xx`, against the engine's public API only (`tests/Vocaterra.Nlp.Fuzzing.Tests/Languages/Xx`), with a three-word bundle loaded through a
registry that holds no Spanish pack. Every member is trivial and non-Spanish. The pipeline, the loader and the neutral oracles (O1, O2, O7 and a neutral O14)
run on it, and the Spanish suite runs the same bodies on Spanish (`FuzzLanguage`, research 4.8). What the toy proved, and what it found:

- **The seams hold, on the paths the oracles reach.** A pack that implements every member, with no Spanish code behind it, loads and runs the pipeline, the
  validator and the replay, and the neutral oracles pass on it. The toy run does not prove the absence of Spanish code on every path: the Spanish statics are
  still guarded by name (`SpanishCallSiteTests`), and a Spanish literal in neutral code is caught only when a path reaches it.
- **Three hidden dependencies, fixed at the source** (regression tests named in the findings log, section 5b):
  1. *The Spanish fold threw on a lone surrogate.* `TextKey.Of` (behind `TextFolding` and the parsers that take no folding) called `string.Normalize` on raw text.
     It now reads a lone surrogate as U+FFFD, as `Accents.Strip` does. Valid text is unchanged; the Spanish identity guard is green.
  2. *The content validator rendered every dialogue template with Spanish words.* The sample slots (`tener`, `espada`, `fuego`, `tengo`, `tiene`, `gender.agree`)
     were fixed in `SampleSlots`. `ILanguagePack.Samples` (`SampleWords`) now gives the validator the pack's words; the Spanish values are unchanged.
  3. *The recast spelled any digit run of up to six digits.* The bound was the Spanish one (999 999), not `INumberWords.MaxValue`. A digit run above the pack's
     bound now stays as digits, the same rule `TemplateEngine` applies to template numbers.
- **Not a dependency, recorded.** A transcript with a lone surrogate does not survive canonical JSON byte for byte (the known finding of the Spanish O14, README).
  The neutral O14 replays it in memory and does not round-trip it.
- **Not fixed, by design.** `ReadingResolver`'s local-syntax rules are still shared, not pluggable (M54 onward, as M34 recorded); the toy language's analysis
  rules say no word is a clitic or a copula, so the shared rules see nothing to do. The pronunciation scorer's predicted-error tags stay Spanish-tuned: the toy
  has no G2P, so the path is off for it, and the Spanish O9 covers the scorer.

**For M53.** The toy pack is the proof that the engine runs without the Spanish pack. M53 moves the Spanish pack out of the engine assembly; the toy suite then
needs no change, and `LayeringTests` should keep the engine free of a reference to `Vocaterra.Nlp.Es`.

## Note, M53: the Spanish pack is a project of its own (2026-10-27)

`SpanishLanguagePack` and the Spanish rules behind it moved to `src/Vocaterra.Nlp.Es` (namespaces `Vocaterra.Nlp.Es.*`; [ADR 0012](0012-one-engine-assembly-and-language-packs.md), amendment M53). Where this record says
that `LanguagePacks.Default` holds `es` or that the engine's compatibility entry points keep their Spanish meaning, read: the default registry is empty, a host registers the pack
(`LanguagePacks.Default.WithSpanish()`, `SpanishContent.Load`), and the entry points are the pack project's. An absent manifest `language` still means `es`, resolved from the host's registry;
an unregistered `es` is the usual `ContentValidationException`. The seams, the hash rule and the toy language are unchanged.
