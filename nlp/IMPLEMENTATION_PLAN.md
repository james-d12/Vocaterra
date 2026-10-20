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
| M13 | Pipeline orchestration, replay log + replayer, CLI demo, bench harness, golden layout | M2–M12 | S | in-progress |
| M14 | CI matrix (x64/ARM64/Windows/macOS), golden tests, license scan, format check (workflow parked in `.github/workflows-pending` until the push token has `workflow` scope) | M13 | H | done |
| M15 | README, ADRs, CONTRIBUTING, determinism checklist | M13 | H | todo |

### Delegation waves

- **Wave 0 (sequential):** M0 → M1. Critical path; everything else programs against M1.
- **Wave 1 (parallel, each in its own worktree):** M2, M3, M4, M5, M7, M10.
- **Wave 2 (parallel):** M6, M8, M9, M11.
- **Wave 3 (sequential):** M12 → M13.
- **Wave 4 (parallel):** M14, M15.

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
- **Build output size:** each full build writes ~3 GB of native-package output per checkout;
  worktrees are cleaned after merge. Trimming runtime packs is a follow-up.

## 6. Definition of done (every milestone)

1. `dotnet build` with warnings as errors and `dotnet test` green for the whole solution.
2. `dotnet format --verify-no-changes` clean.
3. New public types have `///` summaries; D1 adapters are gated and skipped without models.
4. No violations of §3; no new packages outside `Directory.Packages.props`.
5. Committed on the milestone branch with a message starting `M<id>:`; the agent reports
   branch name, commit SHA, test counts, and any contract gaps found in `Slg.Core`.
