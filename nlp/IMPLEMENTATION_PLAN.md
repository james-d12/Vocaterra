# Implementation Plan — Voice Recognition & Feedback System

Derived from `HIGH_LEVEL_DOCS.md` (Oct 18, 2026) and `SPECIFICATION.md`. This file is the
working plan for building the deterministic voice pipeline in C# / .NET. It is maintained by the
orchestrator; milestone agents update only the **Status** column of their own milestone.

## 1. Decisions taken for this plan

| Question (from HLD) | Decision | Rationale |
| --- | --- | --- |
| Language | **C# on the current .NET LTS SDK**, pinned in `global.json` | HLD recommendation; official ONNX Runtime, Vosk and sherpa-onnx NuGet packages |
| Engine | **Deferred.** Everything is engine-agnostic class libraries + a console host | Godot .NET and MonoGame both consume plain .NET libraries; no engine code in this plan |
| Min-spec | 8 GB RAM, 4-core x86-64, no GPU, **CPU-only inference** | HLD performance table; grading paths must be CPU-only anyway |
| Default dialect | Latin American neutral (seseo, yeísmo) for NPC speech; scoring accepts **all** dialect variants | HLD "Dialect bias" risk |
| Free conversation (Phase 3) | Out of scope for this plan | Optional per HLD |
| Mic privacy | Audio never persisted by default; replay logs are opt-in and local | HLD open question |
| Test framework | xUnit v2 (2.9.x) with plain `Assert`; no FluentAssertions | License stability |
| Package policy | Only packages listed in `Directory.Packages.props`; agents request additions, never add them | Keeps the license scan meaningful |
| Project prefix | `Slg.*` (Slg = SpanishLearningGame) | Short, unambiguous |
| License file | **Not chosen** — the repo owner must pick one | Not an engineering decision |

## 2. Repository layout

```
SpanishLearningGame/
  global.json                  pinned SDK
  Directory.Build.props        TFM, nullable, warnings-as-errors, deterministic build
  Directory.Packages.props     central package versions (the only place versions live)
  SpanishLearningGame.sln
  .editorconfig  .gitignore
  .github/workflows/ci.yml
  src/
    Slg.Core/             contracts between stages, determinism primitives, verdict, taxonomy
    Slg.Audio/            PCM clip construction: WAV IO, resampler, gain, energy VAD, clip hash
    Slg.Phonology/        Spanish G2P (D0), dialect config, predicted-error variants
    Slg.Nlp/              normalization, answer matching, lexicon, linguistic analysis, intent
    Slg.Pronunciation/    phoneme recognizer adapter, alignment, GOP, v1 fallback scoring
    Slg.Asr/              word recognizer adapters (Vosk, sherpa-onnx), fakes, model manifest
    Slg.Evaluation/       verdict assembly (pure D0)
    Slg.Decision/         NPC state machines, learner model (BKT + FSRS), feedback policy
    Slg.Response/         template engine + morphology, TTS adapters, game-effect tables
    Slg.Content/          content bundle schema, loader, hashing, versioning
    Slg.Pipeline/         end-to-end orchestration, replay log, replayer
  tools/
    Slg.Cli/              demo host: wav + prompt -> verdict -> response
    Slg.Bench/            ASR benchmark harness (WER, learner-error preservation, latency)
    Slg.ContentBuilder/   build-time pipeline (Wiktionary import, validate, hash, CEFR check)
  tests/
    Slg.<Project>.Tests/  one xUnit project per src project
    Slg.Golden.Tests/     G2P golden set, replay golden set, end-to-end worked example
  content/v1/             versioned, hashed content bundle (JSON)
  golden/                 golden learner audio manifest (+ recording protocol); audio is gitignored
  models/                 gitignored; models/manifest.json (names, versions, SHA-256) is committed
  docs/adr/               architecture decision records
```

Dependency direction (no cycles): `Core` <- `Audio`, `Phonology`, `Nlp`, `Pronunciation`, `Asr`,
`Evaluation`, `Decision`, `Response` <- `Content` <- `Pipeline` <- tools.
`Response` may reference `Nlp` (morphology) and `Phonology` (phones for TTS).
`Pronunciation` may reference `Phonology`. Nothing else references a sibling.

## 3. Determinism rules every agent must follow

1. **No `System.Random`.** Use `Slg.Core.Determinism.SeededRng` seeded by
   `SeededRng.ForInteraction(worldSeed, npcId, interactionCounter)`.
2. **No wall clock in evaluation paths.** `DateTime.Now/UtcNow`, `Stopwatch`, `Environment.TickCount`
   are banned in `src/`. Time arrives as a `StoredTimestamp` parameter (Unix seconds from the save).
   Latency measurement is allowed only in `tools/Slg.Bench`.
3. **Stable hashing only.** `DeterministicHash` (SHA-256 over canonical UTF-8/LE bytes). Never
   `string.GetHashCode()` or `HashCode` for anything persisted or logged.
4. **Ordered collections.** Never let `Dictionary`/`HashSet` enumeration order affect output.
   Sort with `StringComparer.Ordinal`; serialize maps with sorted keys.
5. **Invariant culture everywhere.** `CultureInfo.InvariantCulture`, `StringComparison.Ordinal`,
   Unicode NFC. `InvariantGlobalization` is **off** (decided after M0: invariant mode leaves
   decomposed accents un-composed, which breaks NFC). ICU-backed normalization is used; culture
   behaviour must never leak in because every comparison is ordinal. Pinning an app-local ICU for
   shipped builds is an open ADR item (M15).
6. **Margins on every threshold.** `Margin(threshold, epsilon).Evaluate(score)` returns
   `Accept | Reject | Uncertain`; every caller handles all three. Fixed summation order; no
   parallel reductions in grading paths.
7. **D1 behind interfaces.** Every model is an adapter implementing a `Slg.Core` interface.
   Tests use deterministic fakes. Real-model tests are `[Fact(Skip=...)]`-style skipped unless
   `SLG_MODELS_DIR` is set and the manifest hashes verify. Thread count = 1, CPU provider.
8. **No N-class code in `src/`.** No LLM packages. Build-time LLM work is Phase 3 and lives in
   `tools/Slg.ContentBuilder` only, under the Freeze pattern, and is not part of this plan.
9. **Pinned versions.** Every model file has a SHA-256 in `models/manifest.json`; every
   `VersionStamp` carries `ContentHash` and sorted `ModelHashes`.
10. Build hygiene: `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
    `dotnet format` clean, every public type documented with a one-line `///` summary.

## 4. Cross-stage contracts (owned by `Slg.Core`, milestone M1)

Downstream agents program against these. Field names are normative; agents must not rename them.

```csharp
// Slg.Core.Audio
public sealed record PcmClip(short[] Samples, int SampleRate, string ClipHash);   // mono, 16 000 Hz
public sealed record AudioBuffer(short[] Samples, int SampleRate);

// Slg.Core.Verdicts
public enum ResolvedBy { Exact, Alias, Fuzzy, Grammar, Rules, Embedding, Unresolved }   // fixed order
public readonly record struct TextSpan(int Start, int Length);                           // char offsets in normalized text
public sealed record ErrorTag(string Tag, TextSpan Span, string Expected, string Actual);
public sealed record PhoneScore(TextSpan GraphemeSpan, string Expected, string Actual, double Score);
public sealed record VersionStamp(string ContentHash, IReadOnlyDictionary<string, string> ModelHashes);
public readonly record struct IntentId(string Value) { public static IntentId Unknown; public bool IsUnknown; }
public sealed class SlotMap : IReadOnlyDictionary<string, string> { /* ordinal-sorted, immutable */ }
public sealed record Verdict(
    IntentId Intent, SlotMap Slots, bool Communicated, double Correctness,
    IReadOnlyList<ErrorTag> Errors, IReadOnlyList<PhoneScore> Pronunciation,
    double Confidence, ResolvedBy ResolvedBy, VersionStamp Versions);

// Slg.Core.Taxonomy — stable IDs, v1 set exactly as in the HLD table
public static class ErrorTags { VocabWrong="vocab.wrong", VocabUnknown="vocab.unknown", ConjPerson="conj.person",
  ConjTense="conj.tense", ConjIrregular="conj.irregular", GenderAgree="gender.agree", NounNumber="noun.number",
  SerEstar="ser_estar", WordOrder="word_order", PronVowel="pron.vowel", PronR="pron.r",
  PronSilentH="pron.silent_h", PronStress="pron.stress"; All (ordered) }

// Slg.Core.Determinism
public static class DeterministicHash { string Sha256Hex(ReadOnlySpan<byte>); string OfText(string); string Combine(params string[]); ulong Seed64(params object[]); }
public sealed class SeededRng { static SeededRng ForInteraction(ulong worldSeed, string npcId, long counter); int NextInt(int exclusiveMax); double NextDouble(); T Pick<T>(IReadOnlyList<T>); }
public enum MarginDecision { Accept, Reject, Uncertain }
public readonly record struct Margin(double Threshold, double Epsilon) { MarginDecision Evaluate(double score); }
public readonly record struct StoredTimestamp(long UnixSeconds);
public static class CanonicalJson { string Serialize<T>(T); T Deserialize<T>(string); }   // sorted keys, invariant doubles ("R"), LF, no indentation

// Slg.Core.Prompts
public enum RecognitionMode { Constrained, Open }
public enum Difficulty { Beginner, Intermediate, Advanced }
public sealed record PromptSpec(string PromptId, RecognitionMode Mode, Difficulty Difficulty,
    string AnswerSetId, IReadOnlyList<string> AllowedIntentIds, string? NpcId, IReadOnlyList<string> SlotGrammarIds);

// Slg.Core.Stages — inputs/outputs of each tier
public sealed record RecognitionRequest(RecognitionMode Mode, IReadOnlyList<string> GrammarPhrases, int MaxAlternatives);
public sealed record AsrWord(string Text, double StartSeconds, double EndSeconds, double Confidence);
public sealed record AsrHypothesis(string Text, double Score);
public sealed record AsrResult(string Transcript, IReadOnlyList<AsrHypothesis> NBest, IReadOnlyList<AsrWord> Words,
    double Confidence, string ModelHash, RecognitionMode Mode);
public interface IWordRecognizer { AsrResult Recognize(PcmClip clip, RecognitionRequest request); string ModelHash { get; } }

public sealed record Token(string Text, TextSpan Span, string Original);
public sealed record NormalizedText(string Text, IReadOnlyList<Token> Tokens);
public enum MatchType { Exact, Alias, Fuzzy, None }
public sealed record MatchResult(string? AnswerId, MatchType Type, int Distance, bool LikelyPronunciation, double Confidence);

public sealed record LexicalFeatures(string Lemma, string Pos, string? Person, string? Number, string? Tense, string? Mood, string? Gender);
public sealed record AnalyzedToken(Token Token, IReadOnlyList<LexicalFeatures> Readings);   // empty => unknown word
public sealed record SlotValue(string Name, string Value, TextSpan Span);
public sealed record AnalysisResult(IReadOnlyList<AnalyzedToken> Tokens, IReadOnlyList<SlotValue> Slots,
    IReadOnlyList<ErrorTag> Errors, string? MatchedGrammarId, double Confidence);

public enum IntentRung { Grammar, Rules, Embedding, Unresolved }
public sealed record IntentResolution(IntentId Intent, SlotMap Slots, IntentRung Rung, double Margin, double Confidence);
public interface IEmbeddingModel { float[] Embed(string text); int Dimension { get; } string ModelHash { get; } }

public sealed record PhonePosteriors(IReadOnlyList<string> Vocabulary, float[] LogProbs, int FrameCount, double FrameSeconds); // row-major [frame, phone]
public interface IPhonemeRecognizer { PhonePosteriors Recognize(PcmClip clip); string ModelHash { get; } }
public sealed record PronunciationResult(IReadOnlyList<PhoneScore> Phones, IReadOnlyList<ErrorTag> Errors,
    double WordScore, double Confidence, string ModelHash);

public sealed record SynthesisRequest(string Text, string VoiceId, IReadOnlyList<string>? Phones);
public interface ISpeechSynthesizer { AudioBuffer Synthesize(SynthesisRequest request); string ModelHash { get; } }

// Slg.Core.Replay
public sealed record ReplayRecord(string ClipHash, string PromptId, string GameStateHash, VersionStamp Versions, Verdict Verdict);
```

## 5. Milestones

Agent tier: **H** = Haiku 5.5 (mechanical, well-specified), **S** = Sonnet 5.5 (design judgement,
algorithms). Status: `todo | in-progress | done | blocked`.

| ID | Milestone | Depends on | Tier | Status |
| --- | --- | --- | --- | --- |
| M0 | Solution scaffold: SDK pin, props, all project stubs, CI skeleton | — | H | done |
| M1 | Core contracts + determinism primitives + taxonomy | M0 | S | done |
| M2 | Audio front-end (WAV IO, resample, gain, energy VAD, clip hash, push-to-talk session) | M1 | S | done |
| M3 | Spanish G2P with dialect config, alignment, predicted-error variants, golden set | M1 | S | done |
| M4 | Text normalization + answer matching (exact/alias/fuzzy + phonetic key) | M1 | S | done |
| M5 | Lexicon schema + seed lexicon + morphology generator + linguistic analysis + slot grammar | M1 | S | done |
| M7 | Verdict assembly (pure D0) incl. worked example | M1 | S | done |
| M10 | Decision layer: NPC state machine, learner model (BKT + FSRS), feedback policy | M1 | S | done |
| M6 | Intent resolution (grammar → rules → embedding w/ margins) + repair strategy | M5 | S | done |
| M8 | Pronunciation scoring: v1 greedy+edit distance, v2 CTC Viterbi + GOP, wav2vec2 adapter | M3 | S | done |
| M9 | ASR adapters: Vosk constrained, sherpa-onnx Parakeet open, fakes, model manifest + fetch script | M2 | S | done |
| M11 | Response realization: templates + morphology, recasts, TTS adapters + cache, effect tables | M5, M3 | S | done |
| M12 | Content bundle: schema, loader, hashing, ContentBuilder CLI (kaikki import), sample content v1 | M4, M5, M6, M10, M11 | S | done |
| M13 | Pipeline orchestration, replay log + replayer, CLI demo, bench harness, golden layout | M2–M12 | S | done |
| M13b | Integration fixes from the demo: answer-match communication, spell grammars, blacksmith idle transitions | M13 | S | done |
| M14 | CI matrix (x64/ARM64/Windows/macOS), golden tests, license scan, format check (workflow parked in `.github/workflows-pending` until the push token has `workflow` scope) | M13 | H | done |
| M15 | README, ADRs, CONTRIBUTING, determinism checklist | M13 | S | done |
| M16a | Combinatorial fuzz testing, stage 1: project, CsCheck, generators, harness, language oracles (see `docs/FUZZING_PLAN.md`) | M13b | S | done |
| M16b | Combinatorial fuzz testing, stage 2: signal, decision, content and pipeline oracles | M16a | S | done |
| M17a | Vocabulary expansion: Wiktionary (kaikki.org) lexicon import filtered by a frequency list, provenance, coverage gates (see §7) | M16b | M | done |
| M17b | Intent expansion: ten new intents, two new scenes (innkeeper, quest giver), prompts, dialogue, effects, goldens (see §7; after the split in §8 the scenes are game content and move to the game repo) | M19c | L | todo |
| M18 | Part-of-speech disambiguation in the analyzer (triggered: `ayer compré un libro` yields a false `conj.tense` on main after M17a) | M17a | M | in-progress |
| M19a | Standalone core, step 1: boundary ADR, rename `Slg.*` → `Vocaterra.Nlp.*` (solution, projects, namespaces, packages), game-agnostic context and agent naming, opaque outcome kinds, `docs/API.md`, README reframed as a library (see §8) | M18 | M | todo |
| M19b | Standalone core, step 2: NuGet packaging of `Slg.*`, content bundle packaging, `samples/HostConsole` consuming the packages (see §8) | M19a | S | todo |
| M19c | Standalone core, step 3: new game repository scaffold with the game spec, game content bundle and a host project on the packages (see §8) | M19b | S | todo |
| M17c | Evaluator polish from the CLI-examples review: near-miss recast, `vocab.wrong` expected value, plural/accent corrections, leading-adverb grammars (see §5b) | M16b | S | todo |

### Delegation waves

- **Wave 0 (sequential):** M0 → M1. Critical path; everything else programs against M1.
- **Wave 1 (parallel, each in its own worktree):** M2, M3, M4, M5, M7, M10.
- **Wave 2 (parallel):** M6, M8, M9, M11.
- **Wave 3 (sequential):** M12 → M13.
- **Wave 4 (parallel):** M14, M15.
- **Wave 5 (sequential):** M16a → M16b (fuzzing; added 2026-10-20 at the owner's request, modelled on CodeGuard's rule-fuzzing suite).

Each parallel agent touches only its own `src/<project>` and `tests/<project>.Tests` directories
(plus `content/` or `golden/` where the milestone says so). The orchestrator merges branches to
`main` and pushes after each wave once `dotnet build` and `dotnet test` are green.

### Milestone specifications

#### M0 — Solution scaffold (H)
- `global.json` pinning the installed LTS SDK (`rollForward: latestPatch`).
- `Directory.Build.props`: TFM = installed LTS (`net10.0`), `Nullable`, `ImplicitUsings`,
  `TreatWarningsAsErrors`, `Deterministic`, `ContinuousIntegrationBuild` when `CI` is set,
  `InvariantGlobalization=false` (see §3.5), `GenerateDocumentationFile` for `src/`, `LangVersion latest`.
- `Directory.Packages.props` with exact latest stable versions (query nuget.org, do not guess):
  `xunit` 2.9.x, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`,
  `Vosk`, `org.k2fsa.sherpa.onnx`, `Microsoft.ML.OnnxRuntime`.
- Every project in §2 created as an empty class library / console / xunit project; each test
  project references its src project; `InternalsVisibleTo` for tests; all added to the `.sln`.
- `.editorconfig` (4 spaces, LF, `dotnet_diagnostic` defaults), `.gitignore` (dotnet + `models/*`
  except `manifest.json` + `golden/audio/`), README skeleton, `.github/workflows/ci.yml`
  (ubuntu, `dotnet build -warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`).
- Acceptance: `dotnet build` and `dotnet test` green from a clean clone; `dotnet format --verify-no-changes` clean.

#### M1 — Core contracts (S)
- Implement §4 exactly, in `Slg.Core`. `SlotMap` is immutable and ordinal-sorted.
- `SeededRng`: SplitMix64 seeding → xoshiro256** generator; `ForInteraction` seed =
  `DeterministicHash.Seed64(worldSeed, npcId, counter)` (first 8 bytes of SHA-256, little-endian).
- `CanonicalJson`: System.Text.Json, properties in declaration order, dictionaries emitted with
  ordinal-sorted keys, doubles via `R` invariant, enums as strings, no indentation, LF.
- `ErrorTags.All` ordered as the HLD table. `IntentId.Unknown` serializes as `"unknown"`.
- Tests: golden values for `DeterministicHash` (known SHA-256 vectors), golden first-10 sequence
  of `SeededRng` for a fixed seed, `Margin` three-way behaviour incl. exact boundary, `Verdict`
  round-trip through `CanonicalJson` is byte-identical, `SlotMap` ordering.
- Acceptance: all downstream projects can compile against these types without edits to Core.

#### M2 — Audio front-end (S)
- `WavFile.Read/Write` (PCM16 and IEEE float, mono/stereo, any rate). Fixed-kernel resampler
  (windowed-sinc, documented taps, integer math where possible) to 16 kHz mono; fixed downmix
  `(L+R)/2`; `GainNormalizer` to a target peak with a hard cap; `EnergyVad(frameMs=20,
  thresholdDb, hangoverFrames)` trimming leading/trailing silence; `ClipHasher` = SHA-256 of LE
  sample bytes; `PushToTalkSession` (Begin/Append/End → `PcmClip`); `ICaptureSource` +
  `WavFileCaptureSource`. No engine or OS capture adapter in v1.
- Tests: synthetic sine/silence signals with exact expected sample arrays and hashes; VAD trims
  exactly N frames; resampler output for a 48 kHz sine matches a committed golden array.

#### M3 — Spanish G2P (S)
- Stages: normalize → digraph grouping (`ch ll rr qu gu gü`) → syllabification (Spanish onset/coda
  rules, diphthongs/hiatus with accents) → stress assignment (written accent; else
  vowel/n/s → penultimate, else final) → context-sensitive phone mapping → cross-word allophones
  (`b d g` → `β ð ɣ` intervocalic/after continuants, nasal assimilation, `s` voicing before voiced
  consonants) → exception lexicon (JSON: `México`, `subrayar`, loanwords).
- `DialectConfig { Seseo, Yeismo, LlSound, SAspiration }`; `Phonemize(text, dialect)` returns all
  accepted variants, primary first, each with per-syllable grapheme alignment and stress index.
- `PredictedErrorVariants(text)` for English speakers: `ɾ/r → ɹ` (pron.r), pronounced `h`
  (pron.silent_h), vowel reduction/diphthongization `a→ə, e→eɪ, o→oʊ` (pron.vowel), stress shift
  (pron.stress); each variant tagged with its `ErrorTags` id and grapheme span.
- Golden set: `tests/Slg.Golden.Tests/Data/g2p-golden.jsonl` with ≥ 300 words/phrases and IPA
  (authored carefully; to be regenerated from Wiktionary by M12). Unit tests per rule.

#### M4 — Normalization and matching (S)
- `TextNormalizer`: NFC, lowercase, punctuation strip (keep `ñ`, accents, `¿¡` removed), number
  words ↔ digits 0–999 999 incl. `y`, `un/una/uno`; optional `al/del` expansion; spans back to the
  normalized text. Written accents kept for lookup; a separate `StripAccents` for matching.
- `SpanishPhoneticKey`: `b/v→b`, `ll/y→y`, `z/c(e,i)/s→s`, silent `h` removed, `qu→k`, `c(a,o,u)/k→k`,
  `gu(e,i)→g`, `j/g(e,i)→x`, accents stripped, double letters collapsed except `rr`.
- `AnswerSet { AnswerSetId, Answers[{ AnswerId, Canonical, Aliases[], Dialects[] }] }` (JSON).
- `AnswerMatcher.Match(NormalizedText, AnswerSet, Difficulty)` → `MatchResult`: exact → alias →
  fuzzy (Damerau–Levenshtein on phonetic keys, max distance by length with `Margin`); a fuzzy hit
  within phonetic-key distance 0 but orthographic distance > 0 sets `LikelyPronunciation`.
- Tests: `coche/carro/auto` aliases, `espado`→`espada` fuzzy, number words, uncertain band.

#### M5 — Lexicon and linguistic analysis (S)
- Lexicon JSON schema: `{ form, lemma, pos, features { person, number, tense, mood, gender } }`.
  Seed lexicon `content/v1/lexicon/seed.json`: ≥ 150 beginner words incl. full present-indicative
  and preterite paradigms of 20 common verbs (ser, estar, tener, querer, comprar, vender, ir, hacer,
  poder, dar, saber, comer, hablar, vivir, necesitar, buscar, ver, decir, venir, poner), articles,
  numerals 1–20, ~40 nouns (items, animals, places) with gender, ~20 adjectives with 4 forms.
- `Lexicon` with ordinal-sorted index; `MorphologyGenerator`: `Pluralize`, `Article(definite/
  indefinite, gender, number)`, `AgreeAdjective`, `Conjugate(lemma, person, number, tense)` for
  regular `-ar/-er/-ir` plus the lexicon's irregular entries; regularized-irregular detection
  (`sabo` → `conj.irregular`).
- `LinguisticAnalyzer.Analyze(NormalizedText, SlotGrammar?)` → `AnalysisResult`: unknown word →
  `vocab.unknown`; agreement rules: subject–verb (explicit pronoun), article–noun, noun–adjective,
  quantity–noun; `ser_estar` via a small adjective class table; `word_order` for post-nominal
  adjectives placed pre-nominally. Each `ErrorTag` carries span + expected form from the generator.
- Slot grammar DSL: `quiero [ART] [NOUN:item]`, `tengo [NUM:qty] [NOUN:item]`, `[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]`
  with optional tokens and lemma/POS constraints; deterministic longest-match chart parser; returns
  `SlotValue`s and `MatchedGrammarId`.
- Tests: one positive and one negative per grammar tag (`conj.person`, `conj.tense`,
  `conj.irregular`, `gender.agree`, `noun.number`, `ser_estar`, `word_order`, `vocab.unknown`) and
  the worked example `yo tiene dos espada` → `conj.person` (tiene→tengo), `noun.number` (espada→espadas).

#### M7 — Verdict assembly (S)
- `VerdictAssembler.Assemble(VerdictInputs)` pure function. `VerdictInputs` = `PromptSpec`,
  `AsrResult`, `MatchResult`, `AnalysisResult`, `IntentResolution`, `PronunciationResult?`,
  `EvaluationConfig`, `VersionStamp`.
- `EvaluationConfig` (JSON, per difficulty): weight per error tag, `communicatedThreshold`,
  `correctnessThreshold`, `pronunciationThreshold`, `epsilon`, required slots per intent.
- Rules: `communicated` = intent resolved AND required slots filled; `correctness` =
  `1 − Σ weight(tag)` clamped to [0,1]; `pronunciation` list = content-word phone scores;
  `confidence` = min of stage confidences; `vocab.wrong` emitted when the answer set misses but
  the word is known to the lexicon; `ResolvedBy` from match type, else intent rung; ties and
  ordering of `Errors` fixed (by span start, then tag ordinal).
- Tests: the HLD worked example end-to-end on fake stage outputs produces the exact HLD verdict;
  threshold boundaries use `Margin` (Accept/Reject/Uncertain); property test that the same inputs
  always produce byte-identical `CanonicalJson`.

#### M10 — Decision layer (S)
- `NpcStateMachine` from JSON: states, transitions keyed on `(intent, error-tag predicate)`,
  actions `{ dialogueIntent, gameEvent, variantPoolId }`; variant choice via `SeededRng.ForInteraction`.
- `LearnerModel`: Bayesian Knowledge Tracing per skill (skill = error-tag family or content skill)
  with fixed parameters; `Fsrs` scheduler (published default weights, deterministic, using
  `StoredTimestamp`); `ContentPicker` priority queue (lowest mastery first, ties by ordinal id).
- `FeedbackPolicy` rule table JSON `(tag, repeatCountBucket, difficulty) → FeedbackType
  { Recast, Hint, ExplicitCorrection, ModelPronunciation, SlowRepeat }` → `FeedbackPlan`.
- Tests: transitions, same seed → same variant, BKT update golden values, FSRS interval golden
  values for a fixed review history, policy lookup incl. fallback row.

#### M6 — Intent resolution (S)
- `IntentCatalog` JSON: intents with required slots, slot-grammar ids, lemma keyword rules
  (`comprar + item → buy_item`), example utterances.
- `IntentResolver.Resolve(AnalysisResult, PromptSpec, catalog)`: grammar match → keyword rules →
  embedding nearest-neighbour via `IEmbeddingModel` accepted only if `top1 ≥ t` and
  `top1 − top2 ≥ m` (both `Margin`s) → `Unknown`.
- `FakeEmbeddingModel` (deterministic character n-gram hashing to a fixed dimension) for tests;
  `OnnxEmbeddingModel` adapter for multilingual-e5-small (tokenizer + mean pooling, thread count 1,
  CPU) gated on `SLG_MODELS_DIR`.
- `RepairStrategy`: attempt 1 → `¿Cómo?`; attempt 2 → slow repeat; attempt 3 → 2–3 spoken choices
  and a `RecognitionRequest` in constrained mode for the retry.
- Tests: each rung resolves its own example; margin band yields `Unknown`; repair sequence.

#### M8 — Pronunciation scoring (S)
- `FakePhonemeRecognizer` builds posteriors from a given phone string (for tests).
- v1 `GreedyPhoneScorer`: argmax + CTC collapse → phone edit-distance alignment to each accepted
  G2P variant → best variant → per-phone score (match 1, substitution 0, deletion 0, insertion
  penalty) → predicted-error matching to tags (`pron.r`, `pron.silent_h`, `pron.vowel`) →
  `WordScore` average over content phones → `Margin`-based thresholds per phone class.
- v2 `GopScorer`: CTC forced alignment (Viterbi over the target variant with blanks) and GOP =
  mean log p(target) − mean max log p over the aligned segment; thresholds per phone class and difficulty.
- `Wav2Vec2PhonemeRecognizer` ONNX adapter (`wav2vec2-xlsr-53-espeak-cv-ft`): waveform
  normalisation, logits → log-softmax, espeak phone labels mapped to the `Slg.Phonology` inventory
  (mapping table committed, unknown labels flagged). Gated on `SLG_MODELS_DIR`.
- Tests: fake posteriors for `perro` with `ɹ` → `pron.r`; `hola` with `h` → `pron.silent_h`;
  identical inputs → identical scores; GOP golden values for a tiny synthetic posterior matrix.

#### M9 — ASR adapters (S)
- `VoskWordRecognizer` (grammar = answer-set phrases + `[unk]`, `SetMaxAlternatives`, `SetWords`,
  single-threaded), `SherpaOnnxOfflineRecognizer` for Parakeet TDT int8 (greedy, `num_threads = 1`,
  CPU). Both expose `ModelHash` from the manifest.
- `models/manifest.json`: `{ name, version, files[{ path, sha256 }], license, source }` for Vosk
  small Spanish, Parakeet TDT 0.6B v3 int8, wav2vec2 phoneme model, multilingual-e5-small, Kokoro.
  `scripts/fetch-models.sh` downloads and verifies; fill real hashes for whatever it can download.
- `ScriptedWordRecognizer` (test double) and `ReplayWordRecognizer` (returns recorded results keyed
  by `ClipHash`, used by the replayer).
- Tests: grammar construction, n-best ordering, model-gated integration test on a committed
  3-second synthetic/recorded WAV.

#### M11 — Response realization and effects (S)
- `TemplateEngine`: `{slot}`, `{item:plural-if(qty)}`, `{item:article(def|indef)}`,
  `{verb:conj(person,number,tense)}` using `MorphologyGenerator`; variant pools picked via
  `SeededRng`; `RecastBuilder` rebuilds the corrected sentence from the player's slots and the
  analysis `Expected` forms (`¿Tienes dos espadas? ¡Perfecto!`).
- TTS: `StaticLineStore` (pre-rendered WAV by line id), `CachedSynthesizer` (key =
  `hash(text, voice, modelHash)`), `KokoroSynthesizer` via sherpa-onnx offline TTS with fixed
  speaker id and speed (gated). Record the espeak-ng phonemizer licensing concern in a `// TODO(ADR)`.
- `EffectTable` JSON: verdict → effect with partial credit curves (spell power %, trade price factor).
- Tests: morphology in templates, seeded variant stability, cache hit/miss, effect interpolation.

#### M12 — Content bundle and builder (S)
- `content/v1/` layout: `manifest.json` (per-file SHA-256, bundle hash, schema version,
  `generated_by`/`approved_by` provenance), `lexicon/`, `answer-sets/`, `intents/`, `prompts/`,
  `npcs/`, `dialogue/`, `feedback/`, `effects/`, `cefr/`, `ATTRIBUTION.md` (CC BY-SA for Wiktionary).
- `ContentLoader` validates schema versions and hashes, exposes typed catalogs, returns
  `ContentHash`. Sample content: blacksmith NPC, 10 prompts (trade, spells, quiz), intents
  `buy_item`, `sell_item`, `offer_item`, `cast_spell`, `greet`.
- `Slg.ContentBuilder` CLI: `import-lexicon <kaikki.jsonl> <out>` (wiktextract Spanish entries →
  lexicon JSON, inflection tables), `validate <bundle>`, `hash <bundle>`, `cefr-check <bundle>`.
- Tests: loader rejects a tampered file; bundle hash golden; importer on a 20-line fixture.

#### M13 — Pipeline, replay, CLI, bench (S)
- `UtterancePipeline.Evaluate(PcmClip, PromptSpec, GameContext)` → `Verdict` + `DecisionOutcome`
  + `ResponsePlan`; ASR and phoneme recognition may run concurrently but results are combined in
  fixed order. `ReplayLog` writes `ReplayRecord` JSONL via `CanonicalJson`; `Replayer` re-evaluates
  and reports any diff.
- `Slg.Cli`: `eval --wav --prompt --content [--models]`, `replay <log.jsonl>`.
- `Slg.Bench`: `--manifest golden/manifest.jsonl` → WER, learner-error preservation rate
  (does the transcript keep the annotated error?), latency p50/p95 per model and mode.
- `golden/README.md`: recording protocol, consent, manifest schema; `golden/manifest.jsonl` seeded
  with entries for synthetic test clips.
- Tests: end-to-end worked example with fakes reproduces the HLD verdict; replay of the same record
  is byte-identical.

#### M16 — Combinatorial fuzz testing (S)
- Full design in `docs/FUZZING_PLAN.md`: CsCheck generators driven by the project's own catalogs
  (lexicon, taxonomy, slot-grammar DSL, intent/dialogue catalogs, phone inventory, content bundle),
  sixteen oracles (crash, determinism, mutation↔tag agreement, recast closed loop, metamorphic
  matching, replay sensitivity, schema violations), explicit tolerances, full soak by default with
  env-var deepening and `CsCheck_Seed` replay.
- M16a: `tests/Slg.Fuzzing.Tests` with options, corpus, text/sentence/mutation/slot-map generators,
  harness, tolerances, oracles O1–O7, O10, O12, O16. M16b: audio/phone/content generators and oracles
  O8, O9, O11, O13, O14, O15, CI job, README findings.

#### M14 — CI and determinism verification (H)
- Matrix: `ubuntu-latest`, `ubuntu-24.04-arm`, `windows-latest`, `macos-latest`; build, test,
  format check, golden tests; license scan script (`dotnet list package --include-transitive`
  against an allowlist; fail on GPL/AGPL); model-backed jobs skipped unless a secret opts in.

#### M15 — Documentation (H)
- README (build, run, fetch models, replay), ADRs 0001–0005 (language, determinism rules,
  engine-agnostic core, model pinning, dialect default), CONTRIBUTING with the §3 checklist.

## 5b. Findings log (decisions made during implementation)

- **Vosk dither (M9):** stock Vosk models apply time-seeded dither, so identical clips decoded
  differently run to run. `VoskWordRecognizer` loads a temp copy of the model with
  `--dither=0.0` in `conf/mfcc.conf`; results are now identical across runs and processes.
- **Invariant globalization (M0/M1):** off; see §3.5.
- **Normalizer articles (M4):** `NormalizationOptions.ConvertIndefiniteArticles` defaults to true;
  the pipeline must pass `false` so article–noun agreement and `[ART]` grammar tokens work.
- **Answer-only prompts (M13b):** an Exact/Alias/Fuzzy answer match counts as `communicated`
  even when no intent resolves (`Intent = unknown`, `ResolvedBy` = match type). A prompt whose
  `AllowedIntentIds` is empty skips the intent stage entirely (it is an answer-only prompt); the
  earlier resolver rule "empty allows every intent" no longer applies at the pipeline level.
- **espeak-ng inside sherpa-onnx (TOOLS.md audit):** the Apache-2.0 `org.k2fsa.sherpa.onnx` native
  library statically contains espeak-ng (GPL-3.0) symbols, and the Kokoro archive ships
  `espeak-ng-data/`. Any build that links sherpa-onnx (ASR *or* TTS) therefore carries GPL code,
  contrary to the HLD licensing table. Options for an ADR: build sherpa-onnx without TTS/espeak,
  run Parakeet through ONNX Runtime directly, or isolate TTS in a separate GPL-licensed process.
- **ARM64 natives (TOOLS.md audit):** the Vosk NuGet package ships no linux-arm64/win-arm64 native
  library, so ARM64 targets need a separate Vosk build (CI ARM jobs pass only because model-gated
  tests are skipped there).
- **e5 ONNX export (TOOLS.md audit):** `intfloat/multilingual-e5-small` already publishes
  `onnx/model.onnx` upstream, so `scripts/fetch-models.sh` can fetch it instead of requiring an export.
- **Fuzzing found (M16a, oracle O5):** `RecastBuilder` took the gender of un/una from the word after
  the numeral instead of the noun (`1 grande queso` → `una grande queso`). Fixed to look through
  adjectives to the noun; regression cases added to `RecastBuilderTests`.
- **Lexicon gap (M16a):** `cuál` is missing from `content/v1/lexicon/seed.json`, so the correct
  `ask_price` utterance `cuál es el precio de …` is tagged `vocab.unknown`. Fixing it means a content
  edit, bundle re-hash and golden re-record; assigned to M16b.
- **Analyzer is not part-of-speech aware (M16a):** verb/noun homographs (ayudas, lanza, vino, como, sé,
  ir/ser preterite) trip `conj.tense`, `conj.person` or `ser_estar` on correct sentences. The fuzz
  generators skip these words and count the skips. Design limit; candidate follow-up after M16.
- **Pronunciation scoring cost (M16a):** scoring grows roughly with the cube of transcript length
  (about 0.4 s at 400 characters, over 20 s at 1600). M16b bounds generator lengths and records the
  budget; a linear-time alignment is a follow-up.
- **Ill-formed UTF-16 (M16a):** `Accents.Strip` throws on a lone surrogate and `CanonicalJson`
  writes one as U+FFFD, so such strings do not round-trip. Input should be rejected or repaired at
  the normalizer boundary; M16b adds the rule or a tolerance.
- **Intermittent gate failure (M16a merge):** one unidentified test failed in the first full
  `dotnet test` run after the merge build; four later full runs and twelve fuzz-project runs were
  green. The merge gate now keeps its test log so a recurrence can be identified.
- **Fuzzing found (M16b):** three more source bugs, each fixed with a regression test in the owning
  project and its CsCheck seed in `tests/Slg.Fuzzing.Tests/README.md`: `Accents.Strip` and
  `SpanishPhoneticKey.Compute` (Slg.Nlp) and `G2pNormalizer` (Slg.Phonology) threw on a lone surrogate
  and now replace it with U+FFFD like `TextNormalizer`; the content loader threw
  `NullReferenceException` on a lexicon entry without `features` or a file without `entries`, now a
  `FormatException` reported as a validation problem. This closes the M16a UTF-16 finding.
- **Pronunciation scorer cost, root cause (M16b):** `PredictedErrorMatcher.Match` runs a full
  `PhoneAligner.Align` per predicted variant, cubic in phone count (`PredictedErrorsFor` likewise with a
  smaller constant). The fuzz oracles cap inputs at 20/40 phones and 150 characters. Follow-up: a
  single alignment shared across variants.
- **Content loader silent defaults (M16b):** missing number, flag or enum fields (prompt `mode` and
  `difficulty`, evaluation thresholds, feedback `type`, effect curve points) default silently instead
  of failing validation. Listed in `KnownTolerances.AcceptedContentField`. Follow-up: require them or
  make the defaults explicit in the schema.
- **Replayer coverage (M16b):** the replayer compares the core record (including `/verdict`) and the
  downstream decision and response, but not the envelope's duplicate `result/verdict` and `result/replay`
  copies, and it reads `promptId` and the phoneme model hash back as inputs rather than checking them.
  O14 replay sensitivity covers the compared paths only. Minor; a consistency check on the copies is a
  follow-up.
- **Resampler (M16b):** kernels are rebuilt on every call; a WAV header claiming 1 Hz makes the
  front-end throw `OverflowException` on a large clip and a header claiming 2 GHz costs about 2 s.
  Follow-up: validate the sample-rate range in `WavFile.Read`.
- **`cuál` added (M16b):** `content/v1/lexicon/seed.json` now has 1217 entries; bundle hash is
  `b076558f7d1280b20ae84844d6aa377467fae8d3e8a86fc9d924c89d535d4efb`; the end-to-end golden was
  re-recorded; `golden/manifest.jsonl` is unchanged. O3 now realises every catalog grammar.
- **Fuzz CI (M16b, parked):** `fuzz` job on push and pull request, `fuzz-soak` weekly (Sunday 03:17
  UTC) and on demand at 50 000/50 000/10 000/3 000/5 000 iterations (176 s measured locally); the
  existing jobs are guarded to push and pull request. Whole fuzz project: 49 tests, about 17 s at
  default depth.
- **Fuzz flake (M16b):** O9's injected `pron.silent_h` coverage minimum failed in 2 of 25 runs; the
  generator now produces about 76 such cases per run. The M16a-merge intermittent failure never recurred
  in twelve further full runs.
- **CLI-examples review (docs, after M16):** writing verified README examples exposed evaluator quirks,
  none fixed yet (candidate M17c): (1) `offer_item` has no keyword rule for `tener` and its grammars allow
  a leading pronoun but not an adverb, so `ayer tengo dos espadas` is not understood while `yo tengo dos
  espadas` is; (2) a fuzzy near miss tagged `vocab.unknown` is recast with the player's own wrong word
  (`ezpada` → "Ah, ezpada. ¡Perfecto!"), and near misses are inconsistently communicated (`ezpada` yes,
  `espado` no; `espadas` not communicated with no error and confidence 0.5); (3) `buenas tarde` is
  corrected to `buena` (adjective) rather than `tardes` (noun), and the recast keeps the player's missing
  accents (`buenas dias` → `buenos dias`); (4) `vocab.wrong` puts the answer-set id in `expected`
  instead of a word; (5) the `ser_estar` recast replays the whole sentence; (6) effects depend on the
  verdict only, so a purchase in the NPC's `done` state still emits `buy_item_price` (by design; the
  game layer gates it). Also confirmed: the four `pron.*` tags need the phoneme model, which has no
  ONNX export yet.
- **M17a results:** `content/v1/lexicon/wiktionary-es.json` (4.7 MB) holds the 3 000 most frequent
  lemmas from the kaikki.org Spanish extract (enwiktionary dump 2026-09-02, pinned by SHA-256) ranked by
  the OpenSubtitles-based `es_50k` list: 40 263 entries (1 847 nouns, 680 verbs, 701 adjectives); the
  merged lexicon is 41 078 entries, 32 494 forms, 3 372 lemmas (was 1 268 entries, 404 lemmas). 5 000
  lemmas failed the bundle-load gate (333–373 ms against 300 ms) and left no margin on suite time, so
  3 000 ships. Measured: bundle load 246–267 ms debug, full suite +21 %, fuzz suite 20 s. Bundle hash
  `8090fe859f5ad0f7eeec56d1aa710110b00a8de76deca8728887d21826bafb78`. Sources live in git-ignored
  `lexicon-sources/` (fetched by `scripts/fetch-lexicon-sources.sh`); `docs/LEXICON.md` has the
  regeneration command.
- **Hand-authored deference (M17a):** the importer's `--hand-authored` option drops imported readings
  that would override hand-authored ones (Wiktionary's masculine `espada`, the matador, would otherwise
  make `un espada` correct) and words spelled like hand-authored function words (`la` the note, `si`
  the noun). Curated readings win; the policy is in `docs/LEXICON.md` and ADR 0008.
- **Analyzer bugs fixed by the fuzz oracles on the new words (M17a):** an imperative spelled like the
  third person excused `tú habla`; the copula correction picked an imperative (`ella está alta` →
  `eres`); adjective lemmas sorted before their masculine form broke the ser/estar class check
  (`ocupada`); nouns readable as both numbers (`corazones`) tripped `noun.number`. All in
  `AgreementChecker`, regression tests in `AmbiguousReadingTests`.
- **Homographs after M17a (input to the M18 decision):** forms with more than one reading rose from
  89 to 7 216; 339 of the 1 583 generator-usable nouns (21 %) are also verb forms; 347 nouns are left
  out of the generators for gender homographs; 1–7 sentences per fuzz run skip as verb-form homographs.
  A lemma can also inherit a frequent word's count through a shared form (`casar` ranks 21st via
  `casa`), which part-of-speech data would fix.
- **Fuzz narrowing accepted (M17a):** O15 no longer walks files over 1 MB for structural mutations (a
  byte flip still reaches them, and the small lexicon files exercise the same parser), loads through an
  opt-in `LexiconCache`, and samples on several threads because its iterations are independent. Each is
  documented in the fuzz README's "narrower than the plan" list.
- **Licensing to confirm before release (M17a):** the frequency list's counts are CC BY-SA 4.0, but
  the terms of the underlying OpenSubtitles corpus were not confirmed; only a ranking derived from the
  counts is used and no counts or text ship. kaikki.org marks the per-language file as deprecated; the
  SHA-256 pin and the committed output cover it, and the raw all-languages extract (24 GB) is the
  fallback, untested.
- **Second M17a false positive (CLI-examples re-verification):** `cuánto cuesta la espada` now yields
  `gender.agree` (`cuánto` → `cuánta`, correctness 0.85) on every `ask_price` input, because the
  imported lexicon carries gendered readings under the lemma `cuanto` and the agreement checker ties
  the interrogative to the following noun. Forwarded to M18 as a required case; the `ask_price`
  example was removed from `docs/CLI_EXAMPLES.md` until M18 lands and is to be restored then.
- **Build output size:** each full build writes ~3 GB of native-package output per checkout;
  worktrees are cleaned after merge. Trimming runtime packs is a follow-up.

## 6. Definition of done (every milestone)

1. `dotnet build` with warnings as errors and `dotnet test` green for the whole solution.
2. `dotnet format --verify-no-changes` clean.
3. New public types have `///` summaries; D1 adapters are gated and skipped without models.
4. No violations of §3; no new packages outside `Directory.Packages.props`.
5. Committed on the milestone branch with a message starting `M<id>:`; the agent reports
   branch name, commit SHA, test counts, and any contract gaps found in `Slg.Core`.

## 7. M17: vocabulary and intent expansion (plan)

### 7.1 Why

The bundle understands a scripted slice of Spanish: 1 217 lexicon *forms* but only about 400 lemmas
(32 verbs, 156 nouns, 77 adjectives), six intents, one NPC. Any word outside the lexicon is tagged
`vocab.unknown` and skipped by every grammar check, and any utterance outside the six intents is "not
understood". Both limits are content, not code: the lexicon loader, the Kaikki importer
(`Slg.ContentBuilder import-lexicon`), the slot-grammar DSL, the NPC state machines and the fuzz
generators are all catalog-driven, so widening the catalogs widens the system without touching the
pipeline. M17 is therefore mostly content work with two small, contained tool changes.

### 7.2 Data sources and licensing (decided)

| Source | Use | Licence | Status |
| --- | --- | --- | --- |
| kaikki.org Spanish extract of English Wiktionary (wiktextract JSONL) | lemmas, gender, number, full inflection tables | CC BY-SA 4.0 (older revisions 3.0) + GFDL | named in the HLD (§Content, licensing table); `ATTRIBUTION.md` already carries the notice; importer and tests exist |
| hermitdave/FrequencyWords, `es_50k.txt` (OpenSubtitles 2018) | frequency ranking to select which lemmas ship | content CC BY-SA 4.0, code MIT; OpenSubtitles attribution | new; same share-alike terms as the Wiktionary data |

The HLD licensing table accepts share-alike on derived data files, so no new decision is needed:
the generated lexicon file is CC BY-SA and `ATTRIBUTION.md` says so. Source files are never
committed; they are fetched into a git-ignored `lexicon-sources/` directory like `models/`, and the
committed lexicon carries a sidecar with the source URL, retrieval date, SHA-256, size and importer
parameters so the import is reproducible byte for byte.

### 7.3 M17a: Wiktionary lexicon import (Sonnet, size M)

Deliverables

1. `scripts/fetch-lexicon-sources.sh`: downloads the Kaikki Spanish JSONL and `es_50k.txt` into
   `lexicon-sources/` (git-ignored), verifies or records SHA-256, prints sizes. Pinned URLs.
2. `import-lexicon` gains `--frequency <es_50k.txt> --top N`: lemma frequency is the sum of its forms'
   counts; the top N lemmas are kept (ties broken ordinally); output is sorted and canonical so the file
   is reproducible. Also `--sidecar <path>` writing `lexicon/wiktionary-es.source.json` (URL, date,
   hash, line counts, skipped-by-reason table, parameters, importer version).
3. `content/v1/lexicon/wiktionary-es.json` generated at `--top 3000`, `"generated_by":
   "wiktionary-import"`, listed in `provenance.json`; `seed.json` and `game.json` stay hand-authored.
   Numerals stay hand-authored (the importer skips them by design).
4. Merge policy made explicit and tested in `ContentLoader`: all `lexicon/*.json` are unioned; identical
   readings (form, lemma, pos, features) de-duplicate; different readings of one form coexist. The
   import report prints the overlap with the hand-authored files.
5. Coverage tests (new, cheap, permanent): every lemma used in intent grammars, keyword rules,
   examples, answer sets and dialogue templates resolves in the merged lexicon; every authored intent
   example analyses with no `vocab.unknown`; a hand-written list of about 50 common A1/A2 words is
   known. These tests are what keep M17b and later content honest.
6. Measurements recorded in the fuzz README and TOOLS.md: lexicon file size, bundle load time, full
   test-suite and fuzz-suite wall time, homograph skip counts of the fuzz generators before and after.
   If all gates below hold at 3 000 lemmas, regenerate at 5 000 and keep whichever is the largest
   that still passes.
7. Docs: README vocabulary section and regeneration recipe; TOOLS.md rows for kaikki.org,
   wiktextract, FrequencyWords and OpenSubtitles; `ATTRIBUTION.md` (frequency list);
   `docs/adr/0008-lexicon-sources-and-frequency-filter.md`.

Gates (all of §6 plus): fuzz suite green at default depth with the new lexicon, no oracle weakened
(O3, O4 and O5 now draw from thousands of lemmas, so any analyzer bug they expose is fixed at the
source); fuzz suite under 60 s; full suite not more than 25 % slower; bundle load under 300 ms;
lexicon file under 15 MB; `validate`, `hash`, golden re-record, CLI demo re-verified.

Non-goals: CEFR level per entry (the schema has none; the CEFR lists stay lemma lists for
`cefr-check`); proper names and multiword expressions; part-of-speech disambiguation (M18);
profanity filtering of the *recognised* vocabulary (recognition only; a blocklist is a one-line
follow-up if the owner wants it).

Risks: the Kaikki file is several GB and changes with every extraction (pin and hash it; stream it;
never commit it); Wiktionary gender and number tags are uneven (the importer already skips odd forms;
the spot-check test catches gross errors); verb and noun homographs multiply with the lexicon, so
the analyzer's not-POS-aware checks will produce more false tags on correct sentences. M17a must
report that rate (the fuzz generators count the skips); fixing it is M18, not M17a.

### 7.4 M17b: new intents and scenes (Sonnet, size L)

Two new NPC scenes chosen from the HLD's loop (spells, trade, quests, NPC dialogue): trade and
spells exist, quests and everyday dialogue are the gaps.

| Scene / NPC | New intents | Example utterances | Slots |
| --- | --- | --- | --- |
| Innkeeper (`innkeeper`) | `order_item` | quiero una cerveza; dos panes, por favor | item, qty |
| | `ask_directions` | ¿dónde está la herrería? | place |
| | `ask_name` | ¿cómo te llamas? | |
| | `introduce_self` | me llamo Ana; soy Ana | name (needs `[WORD:name]`, see below) |
| | `farewell` | adiós; hasta luego; hasta mañana | |
| | `thank` | gracias; muchas gracias | |
| Quest giver (`captain`) | `ask_quest` | ¿tienes trabajo para mí?; ¿hay una misión? | |
| | `report_quest` | he matado tres arañas; maté tres arañas | target, qty |
| Cross-cutting (every NPC) | `confirm` | sí; claro; vale, acepto | |
| | `deny` | no; no, gracias; ahora no | |

`confirm` and `deny` also drive the existing blacksmith: a new `confirming` state asks "¿quieres
comprar la espada por diez monedas?" and `confirm` leads to `trade_done`. The quest giver's machine
is idle → offering_quest (`confirm`/`deny`) → quest_active (`report_quest` with the right target and
qty) → quest_done.

Per-intent content checklist: intent definition with required slots, at least two slot grammars, a
keyword rule and at least five examples (one with a typical learner error); answer sets where a
constrained prompt needs them (`food-drink`, `places`, `yes-no`, `quest-targets`); open and
constrained prompts per scene (about twelve new prompts, with difficulty); dialogue pool
`<intent>_reply` with at least three variants using template filters, plus `greet_reply`,
`not_understood` and scene-specific pools for the new NPCs, all passing `cefr-check` at A1 and A2
(extend `cefr/a1.txt` and `cefr/a2.txt` by hand where a template needs it); NPC definitions
`npcs/innkeeper.json` and `npcs/captain.json` with variant pools; effects entries (`order_item` →
tavern price curve, `report_quest` → quest reward curve); no change to the feedback policy or the
evaluation config.

The one allowed code change: a `[WORD:slot]` token in the slot-grammar DSL that matches any single
token (for names), with tests in `Slg.Nlp.Tests`, support in the fuzz `PatternGen` and `SentenceRealizer`,
and a line in `docs/PIPELINE.md`. If it turns out larger than a day's work, `introduce_self` is dropped
and the rest of M17b ships.

Gates (all of §6 plus): coverage tests from M17a green (every new grammar, example and template word is
in the lexicon); fuzz suite green at default depth, every catalog grammar realised by the sentence
generator (O3 asserts it) or its skip documented; `cefr-check` clean at A1 and A2; `validate`, `hash`,
golden re-record, two new end-to-end goldens (`e2e-order-item-open`, `e2e-report-quest-open`); CLI
demo commands for both scenes verified and added to the README; prompt and intent count assertions
updated; README status row.

Known limitation to document: the DSL has no tense constraint, so `report_quest` accepts any form of
the verb; person agreement is still checked. Tense-aware grammars join the subjunctive/por-para
question the HLD leaves open.

### 7.5 M18 (candidate): part-of-speech disambiguation

Only if M17a's numbers say so. The analyzer reads each word as every part of speech it can be; with a
large lexicon, verb/noun homographs (`lanza`, `vino`, `ayudas`) make correct sentences fail the tense
and person checks. The fix is to prefer the reading consistent with the matched grammar slot and the
neighbouring determiners, measured by the fuzz generators' homograph skip count falling to near zero
and O3 staying green with the skips removed.

### 7.6 Order and effort

M17a then M17b, sequential (M17b's grammars rely on M17a's vocabulary and coverage tests). Both on
Sonnet in worktrees, the usual merge gate, bundle re-hash and golden re-record after each. M18 is
scheduled after M17a's report.

## 8. Standalone language core: splitting the game out (roadmap)

### 8.1 Goal and boundary

This repository becomes the language-processing core, consumed by a separate game repository. The
core owns everything from audio in to verdict, feedback, learner model and response text out, plus
the content formats and the tools. The game owns scenes, characters, items, spells, quests, prices,
rewards, rendering and its own content bundle.

The split is cheap. Game vocabulary in code is confined to `GameContext` (`NpcId`, `NpcState`,
`GameStateHash`), `Slg.Decision/Npc` (a verdict-driven state-machine engine plus its definitions,
about 400 lines) and `Slg.Response/Effects` (two named outcome kinds, `spell_power` and
`trade_price`). The remaining ~21 000 lines are language processing. `content/v1` is game-flavoured
(blacksmith, spells, items) but every loader and schema is generic.

| Stays in the core | Moves to the game repository |
| --- | --- |
| `Slg.Core` contracts: verdict, prompt spec, error tags, replay envelope | `SPECIFICATION.md`, the game design |
| Audio, Asr, Phonology, Pronunciation, Nlp, Evaluation | Character definitions (blacksmith, innkeeper, captain) and their dialogue pools |
| Decision: learner model (BKT), scheduling (FSRS), feedback policy, and the conversation state-machine *engine* (renamed from Npc to a generic conversation agent) | Items, spells, quests, prices and rewards: the effect tables and game-event names |
| Response: templates, recast, speech synthesis, and the effect *resolver* with opaque outcome names | The game's content bundles, built with the core's ContentBuilder |
| Content: bundle format, loader, validator, provenance, lexicon import, CEFR lists | Engine integration (Unity, Godot or MonoGame), rendering, device audio capture |
| Pipeline with a game-agnostic `ConversationContext` | Game-side glue mapping outcomes to prices, spell power and quest state |
| Tools (Cli, Bench, ContentBuilder), fuzz and golden suites, a sample bundle | |

The boundary is defined on the contracts (verdict, prompt spec, content formats), which stay stable
whatever the research report in `docs/research/SCALING_APPROACHES.md` changes inside the core.

### 8.2 Milestones

- **M19a, boundary and API (Haiku, M).** ADR 0009 "core and game boundary". Rename the solution to
  `Vocaterra.Nlp.sln`, every project, namespace, assembly and future package id from `Slg.*` to
  `Vocaterra.Nlp.*` (folders too; `git mv` so history follows), the test and tool projects alike, and
  every doc reference; the `Slg.Cli` demo becomes `Vocaterra.Nlp.Cli`. Rename `GameContext` to
  `ConversationContext` (`NpcId` → `AgentId`, `NpcState` → `AgentState`, `GameStateHash` →
  `HostStateHash`); `NpcStateMachine`/`NpcDefinition`/`NpcState` to `ConversationAgent*`, content
  folder `npcs/` → `agents/`; `EffectTable` constants removed, outcome kinds become opaque strings
  validated for uniqueness only (the sample content defines `trade_price` and `spell_power`). Game
  words scrubbed from identifiers and doc comments (Spanish example words may stay). `docs/API.md`
  documents what a host calls: load a bundle, build services, evaluate an utterance, plan a response,
  replay a log. README reframed as a standalone library with the CLI as the demo host. Fuzz harness,
  goldens and replay docs follow the renames; the bundle hash changes (content keys renamed) and the
  goldens are re-recorded. Gate: every suite green, demo verdicts unchanged apart from names.
- **M19b, packaging (Haiku, S).** NuGet packaging of every `Slg.*` project (package metadata in
  `Directory.Build.props`, version 0.1.0, symbols, a README per package), a `dotnet pack` job in the
  parked workflow on tags, `ContentBuilder pack` producing a zip plus manifest hash, and a local-feed
  recipe. `samples/HostConsole` consumes the packages from the local feed, not project references, and
  evaluates the demo utterances; this proves the public surface is complete.
- **M19c, game repository scaffold (Haiku, S).** A new repository (name: owner decision) with
  `SPECIFICATION.md`, the game content bundle (blacksmith scenes, spells, items, effects; later the
  M17b scenes), a host project on the core packages, a smoke test running the demo utterances through
  the packages, and a CI job. The core keeps the blacksmith bundle as its sample and demo unless the
  owner prefers a neutral one (see 8.3).

Re-sequencing: M17b's scenes are game content and move to the game repo after M19c; its core part
(the `[WORD:slot]` grammar token and generic `confirm`, `deny`, `farewell` and `thank` sample intents)
stays here. M17c (evaluator polish) stays in the core.

### 8.3 Owner decisions
- Repository names (decided 2026-10-20): the core is `james-d12/Vocaterra.NLP` (this repository,
  renamed from `SpanishLearningGame`); the game is `james-d12/Vocaterra` (created empty on 2026-10-20, cloned
  at `/home/user/Dev/Vocaterra`, token access confirmed). Decided (owner, 2026-10-20): M19a also renames the solution, project names,
  namespaces, assembly names and NuGet package ids from `Slg.*` to `Vocaterra.Nlp.*` before any package
  is published; environment variables keep the `SLG_` prefix only if renaming them would break the
  parked CI, otherwise they become `VOCATERRA_` with the old names accepted for one release.
- Package feed: GitHub Packages (the token also needs `write:packages`) or a local feed and release
  artifacts. Publishing from CI waits for the workflow scope in any case.
- Whether the conversation state-machine engine stays in the core (recommended: yes, it is
  content-driven interaction logic) or moves to the game.
- Whether the blacksmith sample content stays as the core's demo (recommended: yes; the goldens,
  docs and fuzz suite exercise it) or is replaced by a neutral one.

## 9. Research outcome (2026-10-20) and proposed re-plan

`docs/research/SCALING_APPROACHES.md` (Opus 5.5, 120 sources, two experiments run) concludes that
the hand-written grammar checks are worth keeping and that what breaks them is not knowing each
word's part of speech. A stock Universal Dependencies tagger tagged all four M17a false positives
correctly (`libro`, `cuánto`, `lanza`, `ayudas`), while LanguageTool caught only 9 of 20 learner
errors the current analyzer catches 16 of. Its target architecture keeps the verdict contract,
replay, content bundle and provenance, decision, learner model, response, G2P, the morphology
generator and the fuzz suite; replaces the analyzer's reading choice with a trained UD tagger (train
UDPipe 1 on UD Spanish AnCora, CC BY 4.0; stock spaCy and UDPipe Spanish models are GPL or
non-commercial); rewrites agreement checks over dependency arcs; generates intents, examples and
dialogue with an LLM at build time under the existing provenance approval fields; trains a small
classifier to ONNX for open prompts and retires keyword rules; benchmarks Vosk, Parakeet and
Whisper on the owner's own recordings; swaps the phoneme model for ZIPA (ONNX, MIT code, CC BY 4.0
weights); pins a GPL-free sherpa-onnx (upstream PR #4055, merged 2026-10-20) to unblock ADR 0007; and
adds an optional runtime LLM tutor whose output is recorded in the replay envelope and validated in D0.

Candidate milestones (not scheduled until the owner decides the items in 9.1; ids chosen to avoid
the split's M19a–c):

| Id | Milestone | Replaces / depends | Size |
| --- | --- | --- | --- |
| M20 | UD tagger behind the resolver seam: spike UDPipe 1 (AnCora-trained), Stanza sidecar and Catalyst on the homograph sentences; adapter interface; tagger filters lexicon readings; `HomographSentencesTests` from M18 become the first evaluation set | M18 (keeps its tests and seam, replaces its heuristics) | M |
| M21 | Build-time LLM drafting: `ContentBuilder draft`/`approve`, `llm-draft` provenance, loader refuses unapproved drafts | M19a | M |
| M22 | Tree-based agreement checks and an evaluation set with independent oracles (tagger, LanguageTool as a dev-time process, COWS-L2H sample, O4 mutations) | M20 | M |
| M23 | Open-prompt intent classifier (SetFit or e5 fine-tune, ONNX, pinned); keyword rules retired | M21 | M |
| M24 | Speech reality check: 100–200 owner recordings with deliberate errors; benchmark Vosk, Parakeet, Whisper small on error preservation; ZIPA phoneme adapter; GPL-free sherpa-onnx pin | none (owner records first) | L |
| M25 | Optional tutor: Claude Haiku over HTTP with a local fallback, D0 validator, envelope and save recording, asynchronous feedback, false-correction report | M24 | M |
| M17b | Re-scoped to drafting-then-review under M21 | M21, M19c | M (was L) |

M18 (in progress) is kept: its resolver seam and regression tests are what M20 plugs into, and it
removes the two false positives from `main` now.

### 9.1 Owner decisions raised by the report (with its recommendations)
- Online or offline: offline-capable core with the online tutor optional.
- Determinism: relax "N forbidden in core" and bit-identical cross-platform goldens to
  "replay-exact" (seeded RNG, stored time and hashing stay); write it up as ADR 0010.
- Licence stance: accept MPL-2.0 for unmodified libraries (UDPipe's C# binding); LGPL/GPL only as
  separate development-time processes (LanguageTool).
- A cloud cost cap for the tutor (about $0.30 per 1 000 utterances on Haiku-class models, estimated).
- Game engine: pick one now; the report suggests Godot .NET.
- Pronunciation ambition: decide after M24's speech check.
- Also noted: the design doc's GOP citation (arXiv 2309.07739) points at a different method; the
  matching CTC-GOP work is arXiv 2507.16838.

