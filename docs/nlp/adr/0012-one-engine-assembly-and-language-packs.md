# ADR 0012: One engine assembly, and language packs beside it

- Status: accepted
- Date: 2026-10-26
- Deciders: repository owner (2026-10-26: "move all the `src/Vocaterra.Nlp.*` projects into one Nlp project", and the language-agnostic goal;
  research `docs/research/2026-10-26-engine-and-language-packs.md` section 3); decision D14 in [decisions.md](../decisions.md)
- Supersedes: the layering bullet of [ADR 0003](0003-engine-agnostic-core.md) (the layering stays; the project boundary changes)

## Context

The core was eleven library projects (`Vocaterra.Nlp.Core`, `Audio`, `Asr`, `Phonology`, `Pronunciation`, `Language`, `Evaluation`, `Decision`,
`Response`, `Content`, `Pipeline`) and eleven unit test projects. Nothing is published, and every consumer (the game's core projects, the host, the
three tools) takes all eleven. The project boundary therefore enforced nothing a consumer could choose, and it cost MSBuild nodes, disk and IO on
the build machine.

The owner also wants the core to become language-agnostic, so that a second language can be added beside Spanish. A single project with Spanish
inside it would make that goal harder: the compiler would no longer stop neutral code from calling Spanish code. The boundary that should remain
is the one between the language-neutral engine and each language pack, not between the pipeline stages.

## Decision

1. **One engine assembly.** `src/Vocaterra.Nlp/Vocaterra.Nlp.csproj` replaces the eleven libraries. Each former project is a folder of the same
   short name (`Core/`, `Audio/`, `Asr/`, `Phonology/`, `Pronunciation/`, `Language/`, `Evaluation/`, `Decision/`, `Response/`, `Content/`,
   `Pipeline/`), and every namespace is unchanged (`Vocaterra.Nlp.<Folder>`). The folders were moved with `git mv`, so history follows the files.
   One `Vocaterra.Nlp.AssemblyMarker` replaces the eleven markers.
2. **The layering is enforced by a test, not by the compiler.** `tests/Vocaterra.Nlp.Tests/Architecture/LayeringTests.cs` (NetArchTest.Rules,
   MIT) encodes the project graph of 2026-10-26 as namespace rules over the `Vocaterra.Nlp` assembly:
   - `Core` depends on no other stage;
   - `Audio`, `Phonology`, `Language`, `Evaluation`, `Decision` and `Asr` depend only on `Core`;
   - `Pronunciation` depends on `Core` and `Phonology`;
   - `Response` depends on `Core`, `Asr`, `Language` and `Phonology`;
   - `Content` depends on everything except `Pipeline`;
   - nothing but `Pipeline` depends on `Pipeline`.

   The test also checks that every stage has types (a rule over an empty namespace would pass for nothing), and it has a negative fixture: a
   core-like type that depends on the pipeline must be reported by the same helper. A rule that does not bite fails the build.
3. **The project boundary is engine and pack.** The engine is the language-neutral part. Its Spanish data and rules (today inside
   `Phonology`, `Language` and the bundle's lexicon) are to move out in the later milestones: the pack seams come in M32 to M35 (under
   `src/Vocaterra.Nlp/Language/Packs/`), and the Spanish pack is extracted to `src/Vocaterra.Nlp.Es` (`ILanguagePack`, research section 5.2,
   milestone M53). A German pack, if started, goes to `src/Vocaterra.Nlp.De`. This record does not move any Spanish code; M52 moves none
   between projects either. The engine must not depend on a pack, and `LayeringTests` gains that rule with the first pack. (M53 did the extraction: see the amendment at the end.)
4. **Build settings follow the name.** `VocaterraCore` in `Directory.Build.props` is true for the engine `Vocaterra.Nlp` and for every project
   named `Vocaterra.Nlp.*` (tests and tools), so the XML documentation, `CS1591`, the package metadata and the native-trim exclusion still apply.
   The engine's `InternalsVisibleTo` is `Vocaterra.Nlp.Tests`.
5. **The tests are merged, the suites that differ in purpose stay apart.** The eleven unit test projects become `tests/Vocaterra.Nlp.Tests/`, one
   folder per area, with each `Data/` folder moved under its area unchanged. The eleven `SmokeTests` collapse to one. The fuzz suite
   (`Vocaterra.Nlp.Fuzzing.Tests`) and the golden suite (`Vocaterra.Nlp.Golden.Tests`) stay separate, because they differ in purpose, in
   `VOCATERRA_RECORD_GOLDEN` and in run time, and they see only the public API.
6. **Tests that depended on assembly isolation are not parallel.** Tests that load a native speech or ONNX runtime, or read the models folder
   through the process-wide gates, run in the `NativeRuntime` collection (`DisableParallelization`). The sherpa-onnx library and the managed
   ONNX Runtime each need their own `libonnxruntime.so`, and both files are named the same; `NativeOnnxRuntimes.cs` loads the sherpa copy first
   and maps the managed package's `onnxruntime` to a copy in `ort-managed/`, so both run in the one test process.

## Consequences

- The compiler no longer enforces the layering. `LayeringTests` does, for the dependency direction. Visibility widens: about 156 `internal`
  declarations are visible across the former stages. That is accepted; the test covers the direction, the rest is review.
- A change inside the engine recompiles the whole engine (about 26,000 lines). That costs seconds and saves the eleven project overheads.
- Consumers reference one project: `src/Vocaterra.Game.Core`, `src/Vocaterra.Host` and the three tools. The game still never copies core source.
- Behaviour does not change. The `content/game-v1` bundle hash, the goldens and the Spanish identity baseline are untouched; the core demo gives
  the same output.
- The package table in [package-readme.md](../package-readme.md) now describes one package, `Vocaterra.Nlp`.
- ADR 0003's layering bullet is superseded by this record. Its other points (the engine-agnostic contracts, the interfaces for models and
  capture devices, the thin host) stand.
- ADR 0009's boundary is unchanged: the core is the `Vocaterra.Nlp` engine and its packs, and the game is still the other side of it.

## Amendment, M53: the Spanish pack as built (2026-10-27)

M53 extracts the Spanish pack. Point 3 above ("the engine must not depend on a pack") is now true by construction: `src/Vocaterra.Nlp.Es` references `src/Vocaterra.Nlp`, and the
engine references nothing of it. Behaviour is unchanged: the bundle hash, the goldens, the identity baseline and the core demo are the same as before the move.

**The project boundary as built.**

- `src/Vocaterra.Nlp.Es` (assembly and namespace root `Vocaterra.Nlp.Es`, folders `Analysis`, `Morphology`, `Normalization`, `Phonology`, `Text`) holds `SpanishLanguagePack` and the
  Spanish rules and data behind it: the normalizer (`SpanishTextNormalizer`), `SpanishNumbers`, `SpanishPhoneticKey`, the accent folding (`Accents`, `TextFolding`),
  `MorphologyGenerator` and `SpanishOrthography`, `SpanishAnalysisRules`, `SpanishAgreementChecker`, `SpanishAnalysisTables` and `WordClasses`, `SpanishTemplateFunctions`,
  `SpanishRecastPolicy`, `PunctuationFixer` (the `¿ ¡` balancing) and `NumberAgreement`, the repair lines (`SpanishRepairLines`, the constants of the former `RepairStrategy`), the whole
  Spanish G2P (`SpanishG2p`, `G2pNormalizer`, `DigraphGrouper`, `Syllabifier`, `StressAssigner`, `PhoneMapper`, `AllophoneRules`, `WordPipeline`, `ExceptionLexicon` and `exceptions.json`), and the
  seed lexicon resource (`../../content/game-v1/lexicon/seed.json`, same logical name as before). New in the pack: `SpanishLexicon` (the seed and the Spanish-default `Load` and
  `FromEntries`) and `SpanishContent` (`WithSpanish`, `Load`, `SpanishMorphology`, `CreateTemplateEngine`), the host-facing entry points (api.md, "The Spanish pack project (M53)").
- `tests/Vocaterra.Nlp.Es.Tests` holds the Spanish tests (Analysis, Lexicon, Morphology, Normalization, Phonology, the pack seams and the G2P golden with its data file, moved with `git mv`-equivalent
  renames). `VocaterraCore` already covers the name, so the pack has XML docs, `CS1591` and package metadata, and its tests see the pack's internals through `InternalsVisibleTo $(MSBuildProjectName).Tests`.
  There is **no** `InternalsVisibleTo` from the engine to the pack: every engine member the pack needed was made public with XML docs (`Utf16`, `TextKey`, `Lexicon.Derive`, and the new `TextTokenizer`,
  `TokenizedWord` and `InvariantLexiconFolding`).
- Both projects are in `Vocaterra.sln` and `Vocaterra.Nlp.slnf`; the licence scan reads the filter, so it covers them (the pack adds no package).

**What stayed in the engine, and why.**

- `DialectConfig`, `G2pResult`, `PhoneInventory` and `PredictedErrorVariants` (`Phonology`): the pronunciation scorer, the fake recognizer and `IG2p` read them. `PhoneInventory` is the shared IPA
  phone set that the CTC and GOP scoring work in, not a set of Spanish rules; a language that needs more phones extends it.
- `ErrorTags` and `ErrorTagSet.V1`: the frozen v1 taxonomy, listed by every pack and used by the verdict assembler as the default. `LexiconProfile.V1` and `LexiconSchema`: the frozen v1 closed
  vocabularies and the default of `Lexicon.ReadFile(json)`. Both are the contract of the content format, not Spanish rules; the pack picks them (`ErrorTags => ErrorTagSet.V1`).
- `NormalizationOptions`, `Token`/`NormalizedText`, and the engine's neutral `OfferFormat` of `RepairStrategy` (used when a pack gives none).
- `Lexicon.Empty` stays, now with the neutral `InvariantLexiconFolding`; the Spanish empty lexicon is `SpanishLexicon.Empty`.
- Remaining mentions of "Spanish" in `src/Vocaterra.Nlp` are comments, XML documentation examples (`Spanish: ...`) and one data field, `LexiconSourceInfo.SpanishEntries`, which names a
  JSON field of the importer's sidecar (the importer reads the Spanish Wiktionary extract); `SpanishCallSiteTests` scans the code (not the comments) and its allowlist is empty.

**The espeak phone map is neutral.** `espeak-to-vocaterra.json` and `EspeakPhoneMap` map the labels of the multilingual `wav2vec2-xlsr-53-espeak-cv-ft` vocabulary (length marks, vowel and
liquid variants) onto the shared `PhoneInventory`. Nothing in it names a Spanish rule or word, the model that produces the labels is multilingual, and the scorer and `VocabularyMapping` read it
without a pack. It stays in the engine with its logical name. A pack whose phones fall outside the inventory would add its own map; that is a later milestone's decision.

**`TextNormalizer` is split.** The old class mixed neutral tokenization with two Spanish rules. The neutral part is the engine's `TextTokenizer`: `Clean` (lone surrogates to U+FFFD), `Split` (letters,
digits and combining marks; hyphens between word characters; NFC and invariant lower case), `FinalMarkOf` (the final `?` or `!`) and `Build` (the single-space text and the tokens with their spans). The Spanish
part is `SpanishTextNormalizer`, which composes them with the number-word conversion (`SpanishNumbers`) and the `al`/`del` contraction expansion. `SpanishTextNormalizer.Normalize` returns exactly what
`TextNormalizer.Normalize` returned. `TextTokenizer.Tokenize(text, options)` normalizes with no language rule (it reads only `CaptureFinalMark`); the toy `xx` pack could use it.

**The registry.** `LanguagePacks.Default` is empty (`Default => Empty`). A manifest with no `language` still means `es` and is resolved from the host's registry; a registry without `es` gives the usual
`ContentValidationException`. Hosts register the pack through `ContentLoadOptions.LanguagePacks`: `LanguagePacks.Default.WithSpanish()` (an extension of the pack project),
`LanguagePacks.Default.With(SpanishLanguagePack.Instance)`, or `SpanishContent.Load(dir[, options])`. This breaks a bare `ContentLoader.Load(dir)` of a Spanish bundle; every caller in the repository (tools, tests, the game's
`GameBundle` and `Host`) was updated.

**Removals instead of Spanish defaults.** `ContentBundle.Morphology` (typed `MorphologyGenerator`) is removed; a host of a Spanish bundle calls `bundle.SpanishMorphology()` and any host uses
`bundle.LanguageMorphology`. `Lexicon.Seed`, `LoadSeed`, `ReadSeedJson`, `Load(json)` and `FromEntries(entries)` without a folding, `TemplateEngine.CreateDefault`, the pack-less overloads of `RecastBuilder.Build`
and `SpeechRequests.Create`, `ResponsePlanner()` and the pack-less `RepairStrategy.Next` overloads are removed from the engine; their Spanish forms are in the pack project. The M33 and M34 amendments' "Spanish
compatibility entry points" are therefore the pack's own members now.

**A bug found on the way.** `UtterancePipeline` built its `RepairLines` from the bundle pools and the pack fallback but left out the pack's `RepairLines.Offer`, so the offer line always used the engine's
default wording. It was invisible while the default was Spanish. The pipeline passes `fallback.Offer` now (regression test `ThePacksOwnOfferWordingReachesTheOfferedChoices`).

**Guards.** `LayeringTests` gained "no engine namespace depends on `Vocaterra.Nlp.Es`" (with a fixture that must be reported), and "the engine assembly's referenced assemblies exclude `Vocaterra.Nlp.Es`".
`SpanishCallSiteTests` now scans only `src/Vocaterra.Nlp`, looks for every moved name and the `Vocaterra.Nlp.Es` namespace, and its allowlist is empty. The toy `xx` pack
(`tests/Vocaterra.Nlp.Fuzzing.Tests/Languages/Xx`) is checked by `XxIndependenceTests`: a scan of its source files for any pack type and a reflection scan of every xx type's signatures. The fuzz project
references the pack because its Spanish suites need it; the scan is what keeps `xx` independent.

**Which test projects reference the pack, and why.** `Vocaterra.Nlp.Es.Tests` (it tests the pack). `Vocaterra.Nlp.Tests`, `Vocaterra.Nlp.Fuzzing.Tests` and `Vocaterra.Nlp.Golden.Tests` reference it because their tests
run the Spanish bundle `content/game-v1` (one bundle, R02) and must register the Spanish pack to load it; three engine tests that use engine internals with Spanish fixtures (`ReadingResolverTests`,
`ReportedReadingsTests`, `AccentVariantSentencesTests`) stayed in `Vocaterra.Nlp.Tests` and link the Es tests' `TestTokens.cs` fixture. The tools `Cli`, `Bench` and `ContentBuilder` reference it because they load and
build Spanish bundles (the importer is Spanish-specific).
