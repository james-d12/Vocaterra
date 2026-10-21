# Implementation Plan — Voice Recognition & Feedback System

Derived from `HIGH_LEVEL_DOCS.md` (Oct 18, 2026) and `SPECIFICATION.md`. This file is the
working plan for building the deterministic voice pipeline in C# / .NET. It is maintained by the
orchestrator; milestone agents update only the **Status** column of their own milestone.

## 1. Decisions taken for this plan

| Question (from HLD) | Decision | Rationale |
| --- | --- | --- |
| Language | **C# on the current .NET LTS SDK**, pinned in `global.json` | HLD recommendation; official ONNX Runtime, Vosk and sherpa-onnx NuGet packages |
| Engine | **Not a core concern.** The core is engine-agnostic class libraries plus a console sample; the game engine is chosen and documented in the Vocaterra repository (owner, 2026-10-21) | The core must stay consumable from any .NET host |
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
| M17b | Intent expansion, game part: planned and built in the Vocaterra repository (not tracked here since 2026-10-21) | M17b-core | — | moved (Vocaterra) |
| M17b-core | The core part of M17b: `[WORD:slot]` grammar token, generic sample intents (confirm, deny, farewell, thank, introduce_self), blacksmith confirming state, fuzz support | M19a | M | done |
| M18 | Part-of-speech disambiguation in the analyzer (triggered: `ayer compré un libro` yields a false `conj.tense` on main after M17a) | M17a | M | done |
| M19a | Standalone core, step 1: boundary ADR, rename `Slg.*` → `Vocaterra.Nlp.*` (solution, projects, namespaces, packages), game-agnostic context and agent naming, opaque outcome kinds, `docs/API.md`, README reframed as a library (see §8) | M18 | M | done |
| M19b | Standalone core, step 2: NuGet packaging of `Slg.*`, content bundle packaging, `samples/HostConsole` consuming the packages (see §8) | M19a | S | done |
| M19c | Standalone core, step 3: new game repository scaffold with the game spec, game content bundle and a host project on the packages (see §8) | M19b | S | done |
| M17c | Evaluator polish from the CLI-examples review: near-miss recast, `vocab.wrong` expected value, plural/accent corrections, leading-adverb grammars (see §5b) | M16b | S | done |
| M19d | Post-split core follow-ups from the game host author: repeat-count helper and next-turn context, prompt `text` in `PromptSpec`, ContentBuilder as a dotnet tool `vocaterra-content` | M19c, M17b-core | H | done |
| M19e | Apache-2.0 licence (D3), submodule consumption route (D5), core/game documentation isolation (D12) | M19d | H | done |
| M19f | Recast bug from the fuzz suite (O5, seed `1diyRPH9lxJ1`): a `conj.tense` error after a leading `ayer` on an irregular verb (`ayer tengo sufrir`) is detected but the recast repeats the wrong form instead of `tuve` | M19e | H | done |
| M26 | Game requests, batch 1 (CR-002…CR-013, triaged 2026-10-21 into M26a–f below; rows marked `accepted` in the game's `docs/CORE_REQUESTS.md`) | M19f | — | in-progress |
| M26a | CR-012: repeat counts mean "before this utterance" on `ConversationContext`; the pipeline applies `RepeatCounts.Next` itself before `FeedbackPolicy.Plan` and `RepairFor`; docs and sample host follow | M19f | H | done |
| M26b | CR-013: accent-insensitive lookup also offers the accented readings of a form the lexicon has (`esta`/`está`, `si`/`sí`, `mas`/`más`, `el`/`él`, `tu`/`tú`…) and the reading resolver chooses | M19f | S | done |
| M26c | CR-002 analysis on the result, CR-004 answer match on the result, CR-007 host-owned bundle files (`ContentBundle.TryReadHostFile`), CR-008 threading and load-cost contract in `docs/API.md` | M26a | H | done |
| M26d | CR-003 host slot values (`SlotMap` on the context, template access, replay record and `HostStateHash`), CR-005 host-named skills in the learner update | M26c | S | todo |
| M26e | CR-006 speech-out readiness: Kokoro and Parakeet file hashes pinned from real downloads, `KokoroSynthesizer.FromStore`, Spanish speaker ids documented (D2: private builds only) | M19f | H | done |
| M26f | CR-010 `cefr-check` reads prompt text; CR-011 sample `ATTRIBUTION.md` describes the sample only (content procedure: hash re-pin, goldens) | M26d | H | todo (next) |
| M27a | Content lint (owner chose lint first, 2026-10-21): `vocaterra-content lint` — example coverage per intent, confusion within each prompt's allow-list, grammar hazards, reachability, CEFR incl. prompt text; examples may carry expected error tags; `validate` warns, `--strict` fails; per-intent waivers (see §10) | M26f | S | todo (next) |
| M27b | Content layering and `std-v1`: manifest `base` pinned by id, hash and path; whole-item replacement with explicit `replaces`/`removes`; lexicon and CEFR unioned; one level; `content/v1` becomes `std-v1` plus a blacksmith overlay (see §10) | M27a | S | todo |
| M27c | Format de-duplication: defaulted redundant fields, any-state transitions, `<np:x>` macros, generated article aliases; old files load identically (see §10) | M27b | H | todo |

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
- **M18 results:** `ReadingResolver` (new) picks one reading per token before the checks run: local
  syntax first (hard constraints on words whose every reading is of the triggering kind), then the
  slot-grammar bindings (`SlotGrammarMatch.Bindings`, with tied parses keeping either reading), then
  local syntax again, then a fewest-errors fallback (exhaustive up to 256 combinations, coordinate
  descent beyond). Bindings before syntax hid real mistakes (`yo lanza` read as `[NOUN:spell]`), hence
  the order. 121 new tests, 96 of them real-lexicon homograph sentences; `ayer compré un libro` and
  `cuánto cuesta la espada` score 1 again and `cuánto cuesta el espada` still gives `gender.agree`.
  Fuzz generators now draw homographs (gender-homograph exclusions 347 → 0; verb-form skips 1–7 → ≤1
  per 200), suite time unchanged, O4/O5 green at 200 000 iterations. Unresolved: verbs sharing a form
  (`fue` is `ir` and `ser`) and sentences correct under one reading and wrong under another with
  nothing to prefer. The resolver is the seam a trained tagger (M20) would replace.
- **M17c results:** the near-miss inconsistency was a sentinel, not the edit budget: a fuzzy match
  exactly at the budget edge reported confidence 0.5, which the communicated margin (0.6 ± 0.02)
  rejected, so `espado` and `yelo` failed while `ezpada` (phonetic distance 0) passed. Confidence is
  now 1 − d/length and all three are communicated; a fuzzy match's unknown word carries the canonical
  word in `expected` and the recast uses it ("Ah, espada"). `vocab.wrong.expected` is the answer
  set's single canonical answer or empty. Recasts restore lexicon accents (`buenas dias` → "buenos
  días") without touching accentless spellings that are words themselves. `offer_item` gained a
  `tener` keyword rule, so `ayer tengo dos espadas` resolves by Rules and keeps `conj.tense`. Bundle
  hash `1adcf251fb31189f0cb42875135217e65f4ee3d8e4c81dff19b5e0073ee1d294`. Known limit: `espadas`
  for `espada` on an answer-only prompt is a Fuzzy match with no `noun.number` tag (no grammar to
  check number). The `docs/CLI_EXAMPLES.md` prose for the `fuego` example (expected now empty) is
  fixed in M19a's docs pass.
- **M19a results:** 516 files renamed or edited; solution `Vocaterra.Nlp.sln`, 27 projects under
  `Vocaterra.Nlp.*`; env vars `VOCATERRA_MODELS_DIR` and `VOCATERRA_FUZZ_ITERATIONS*` with the `SLG_`
  names read as a deprecated fallback; `ConversationContext` (AgentId, AgentState, HostStateHash),
  `AgentStateMachine` and friends in `Decision.ConversationAgents`, `ResolvedEffect`, effect kinds as
  opaque non-empty strings with unique effect ids; content `npcs/` → `agents/` and JSON keys `npcId` →
  `agentId`, `gameEvent` → `hostEvent`, `gameStateHash` → `hostStateHash`; replay logs recorded before
  M19a no longer replay (accepted: pre-release developer artefacts; the golden was re-recorded).
  Bundle hash `d45b8012dde06bdd54039c4857849709a88e96e11802b960f5c5b36795a26b3f`. Kept as data:
  `slg-vosk-runtime` cache folder, `vosk-runtime-1` hash input, bundle id `v1`, sample ids. New
  `docs/API.md` (snippets compiled in a scratch host) and ADR 0009. Follow-ups taken into M19b: the
  literal rename produced `Vocaterra.Nlp.Nlp` (to become `Vocaterra.Nlp.Language`); O15 lost its
  `kind` enum mutation and needs an empty-kind mutation instead; the parked workflow now reads the
  repository variable `VOCATERRA_RUN_MODEL_TESTS` (rename it on GitHub if `SLG_RUN_MODEL_TESTS` was set).
- **M19b results:** 11 packages `Vocaterra.Nlp.*` 0.1.0 (nupkg + snupkg, deterministic, no licence
  expression while D3 is open) via `scripts/pack.sh` into the local feed `artifacts/feed`;
  `samples/HostConsole` restores from that feed with package references only and passes its two
  checks (`scripts/sample-host.sh`, verified on main). `ContentBuilder pack`/`verify-pack` produce and
  check a deterministic bundle zip with its hash. Project `Vocaterra.Nlp.Nlp` renamed to
  `Vocaterra.Nlp.Language`. O15 gained an empty-required-string mutation (effect id, intent, kind;
  prompt id). Parked workflow has a `pack` job with the GitHub Packages publish step commented out
  (D5). The Asr and Response packages carry the sherpa-onnx dependency with GPL espeak-ng inside
  (D2): do not distribute them until that is settled. Suite: 2603 passed, 5 skipped.
- **M19c results:** `james-d12/Vocaterra` has its root commit: the game design (`docs/SPECIFICATION.md`),
  `docs/ARCHITECTURE.md`, bundle `content/game-v1` (a copy of the core sample with id `game-v1`, same
  hash), an engine-agnostic console host on the seven `Vocaterra.Nlp.*` packages with a text loop
  that carries agent state, learner model and repeat counts across turns, 4 smoke tests, a parked
  workflow, and `scripts/content.sh` that drives the core checkout's ContentBuilder. No API gap: every
  type the host needs is public. Core follow-ups noted by the host author: a helper for repeat counts
  (the host re-implements the rule), prompt text for what an agent says before evaluation (`PromptSpec`
  has none), and packaging the ContentBuilder as a dotnet tool. `SPECIFICATION.md` left the core.
- **Sandbox keyword refusals (process):** agent sandboxes refuse command lines containing the shell
  builtin words `eval` and `hash`, which are also the CLI and ContentBuilder subcommands; briefs must
  say to use a wrapper script outside the repo, and the orchestrator runs the ContentBuilder `hash`
  step itself when an agent is blocked.
- **M17b-core results:** `[WORD:slot]` matches one all-letter token (accents and ñ included; no
  digits, punctuation or hyphens), binds the normalized text, and the bound word carries no reading:
  never `vocab.unknown`, no agreement check, no recast. Intents `confirm`, `deny`, `farewell`, `thank`,
  `introduce_self` with grammars, examples and four answer sets; prompts `confirm-open`,
  `farewell-open`, `thank-open`, `introduce-open`; blacksmith `confirming` state (`trading` →
  `buy_item` → `confirming`; `confirm` → `done` with host event `trade.confirmed`; `deny` → `trading`).
  Bundle hash `3b1d1f02dff448c830d0cfdbaf1f5e330fc66922a7b7507abc4b56d78afbc657` (23 files),
  reproduced by the orchestrator on main; golden `e2e-confirm-open` added. Two core behaviour changes:
  among grammars with equal explicit-token counts the one binding more literal words now wins (before,
  `gracias`, also a noun, resolved to `cast_spell`), and the embedding rung's reported gap is clamped
  at 1. Fuzz found three more bugs (a name spelled like an irregular verb got `conj.irregular`; a
  recast conjugated a name that is a verb form; gap above 1), all fixed with seeds in the fuzz README.
  Known limits: recasts print names in lower case; hyphenated names do not match; `nos veo` carries
  no tag (no clitic-person check). `cefr-check` now lists `moneda` and `nada` (A2 words in A1
  templates) and `herrería`; reword or accept. Suite: 2663 passed, 5 skipped.
- **M19d results:** `RepeatCounts.Next(before, verdict)` in `Vocaterra.Nlp.Pipeline` (ordinal-sorted
  immutable result; verdict tags and `not_communicated` count `before+1`, everything else is dropped,
  counts below 1 are treated as absent, saturates at `int.MaxValue`) and
  `ConversationContext.Next(PipelineResult, StoredTimestamp)` (keeps world seed and agent id, takes
  the decision's next agent state and learner model, the new counts and the given time; a host with
  its own state digest must recompute it). `PromptSpec.Text` (optional, JSON `text`, omitted when
  null; validator refuses empty, whitespace-only or untrimmed text; O15 mutates it) and a line for all
  18 sample prompts. ContentBuilder packs as the dotnet tool `vocaterra-content`
  (`Vocaterra.Nlp.ContentBuilder` 0.1.0, 12 packages from `scripts/pack.sh`); the tool nupkg is
  218 MB because `PackAsTool` bundles every native runtime (onnxruntime for all platforms, libvosk,
  sherpa-onnx with espeak-ng, D2) through `Content` → `Asr`/`Response`/`Pronunciation`; it is for
  local installation from the feed only. Bundle hash
  `cedb97c4d068221c58b86c4a69dc23fa081d6eda8ecb96dc068361bc5c3afad1` (23 files), reproduced by the
  orchestrator on main; goldens changed only in their `contentHash` lines (`PromptSpec` is not in the
  replay record). Bug found by the rename: `VoskRuntimeModel.IsComplete` accepted a cache directory
  whose symlinks dangled into the old `SpanishLearningGame` path (`File.Exists` is true for a dangling
  link); fixed to resolve links, with a regression test. `cefr-check` scans dialogue templates only,
  not prompt text (follow-up if prompt text should be level-checked). Suite: 2682 passed, 5 skipped.
  Follow-ups: README still says 14 prompts (there are 18); the tool has no package readme; trimming
  the tool's natives means splitting content loading from the model adapters. For the game repo:
  switch `scripts/content.sh` to the installed tool, update the `../SpanishLearningGame` defaults in
  `nuget.config`, `content.sh` and docs to `../Vocaterra.NLP`, replace `NextRepeatCounts` with
  `RepeatCounts.Next` (the host copy can overflow), show `PromptSpec.Text`, re-sync `game-v1`.
- **M19e results:** `LICENSE` (Apache-2.0, byte-identical to the Debian copy of the apache.org text,
  sha256 `cfc7749b…523d30`) and `NOTICE` (copyright 2026 james-d12; the Wiktionary lexicon stays CC BY-SA
  4.0); `PackageLicenseExpression` Apache-2.0 and both files shipped in all 12 packages;
  `docs/PACKAGING.md` documents the decided consumption route (the game includes this repository as a
  git submodule and uses the local feed from `scripts/pack.sh`; hosted feeds are not planned).
  Isolation audit: README, `HIGH_LEVEL_DOCS.md`, `TOOLS.md`, `docs/PACKAGING.md`, `docs/PACKAGE_README.md`
  and `docs/LEXICON.md` now describe the core only; ADRs 0001–0009 and the research report keep their
  bodies with a note that game matters are decided in the Vocaterra repository; text not already in
  the game's docs went to Vocaterra `docs/NOTES_FROM_CORE.md`. No code or content changed (bundle hash
  unchanged). Found by the agent's second test run: fuzz oracle O5 fails with seed `1diyRPH9lxJ1`
  (`ayer tengo sufrir`: `conj.tense` expected `tuve`, recast repeats `tengo`); reproduced by the
  orchestrator on main before the merge, so it predates M19e; scheduled as M19f. Follow-ups:
  `content/v1/ATTRIBUTION.md` still says "the game loads at run time" and "licence not chosen" (a
  content edit, needs the bundle procedure; fold into the next content milestone); the game's
  `docs/ARCHITECTURE.md` and `nuget.config` still name the old feed path (game side).
- **M19f results:** the O5 failure was not a recast bug. The pattern `[WORD:item]? [VERB] [ADJ]?
  [VERB:sufrir]` bound `ayer` as the optional name; a name carries no reading, so the past-time marker
  vanished, no `conj.tense` was raised and the recast had nothing to apply. Fix in the matcher:
  `[WORD]` binds only a letter word with no reading or with at least one open-class reading
  (`ReadingClasses.IsClosedClassReading`: article, numeral, determiner, pronoun, preposition,
  conjunction, adverb, interjection). Contract change: a spelled numeral (`dos`) no longer binds
  `[WORD]`, so `mi nombre es dos` is now unresolved rather than a name. Regression fact in
  `Vocaterra.Nlp.Language.Tests`; seed `1diyRPH9lxJ1` passes on main; O5 passed on four more seeds;
  `me llamo Ana`, `soy Ana` and the README demo unchanged. Suite: 2683 passed, 5 skipped.
- **Open requests from the game (read 2026-10-21, end of day):** the game orchestrator ran G1 and G2
  (its main `59b8630`) and filed CR-002 to CR-013 in `~/Dev/Vocaterra/docs/CORE_REQUESTS.md` (CR-009 is
  not listed there). All are `open`; none is accepted yet. Two are bugs in the core and go first:
  CR-012, the repeat counts mean "including this utterance" to `FeedbackPolicy.Plan` and `RepairFor`
  but `RepeatCounts.Next`/`ConversationContext.Next` (M19d) produce the counts before the next
  utterance, so the host cannot supply the right value (decide one meaning, fix the pipeline side,
  document it in `docs/API.md`); CR-013, accent-insensitive lookup is only a fallback, so `esta`
  never reads as `está` and `dónde esta el arbol` gets a false `gender.agree` and a wrong recast.
  CR-011 and CR-010 are the ATTRIBUTION.md and cefr-check follow-ups already listed under M19d/M19e.
  CR-006 unblocks the game's speech milestone P8 and must respect D2 (private builds only). The next
  core session triages them into M26 (and M27 if the result-surface work is large), marks each row
  `accepted (M26)` or answers why not, and keeps one agent per batch of disjoint modules.
- **M26 triage (2026-10-21, second session):** CR-002…CR-013 accepted and split by module so batches
  that touch disjoint directories can run in parallel: M26a (Pipeline, CR-012) and M26b (Language,
  CR-013) first, in parallel; M26c (Pipeline result surface and Content, CR-002/004/007/008) after M26a;
  M26d (CR-003/005, cross-module: context, templates, replay record, learner model; Sonnet) after M26c;
  M26e (models manifest and Response/Speech, CR-006) when disk allows (two model downloads, ~1 GB);
  M26f (CR-010/011, a content edit that re-pins the bundle hash) last so the re-pin happens once.
  CR-012 decision: `ConversationContext.RepeatCountsByTag` means the counts before this utterance (what
  `ConversationContext.Next` and `RepeatCounts.Next` already produce), and the pipeline computes the
  counts including this utterance itself; the alternative (documenting that the host passes the
  "including" counts) is impossible for a host, which cannot know the tags before it evaluates. The
  rows in the game repository's requests file were edited in its working tree and left for the game
  orchestrator to commit (its checkout had uncommitted plan work).
- **M26a results (CR-012):** the pipeline computes `RepeatCounts.Next(context.RepeatCountsByTag, verdict)`
  once after the verdict and passes it to `FeedbackPolicy.Plan` and `RepairFor` (the `Math.Max(1, seen)`
  guard is gone: a not-communicated verdict always carries its count). `yo tiene dos espada` three times
  now gives recast, hint, hint (was recast, recast, hint); the repair ladder for three failures is
  AskAgain, SlowRepeat, OfferChoices (was AskAgain, AskAgain, SlowRepeat). The CLI's `--repeat` now
  means counts before the utterance (README, `docs/PIPELINE.md`, `docs/CLI_EXAMPLES.md` examples shifted
  by one, outputs unchanged). Fuzz oracle `PipelineFuzzTests.CheckDecision` updated to the new contract
  (still exact equality). `samples/HostConsole` threads `ConversationContext.Next` over three repetitions
  and checks the ladder. Caveat: a replay log recorded before M26a with non-empty counts replays with
  different feedback (none is committed). Suite: 2685 passed, 5 skipped (one duplicate theory row
  removed, three facts added). The agent cleared the shared `~/.nuget/packages/vocaterra.nlp.*` 0.1.0
  cache while testing the sample; checkouts re-extract from their own feeds.
- **M26b results (CR-013):** `Lexicon.LookupWithAccentVariants` (exact readings plus the readings of
  accented forms that fold to an unaccented lexicon form; `Lookup` unchanged for its other callers) and a
  resolver step 0, `AccentVariantChoice` (rule tables in `AnalysisTables`): the typed reading is the
  default and an accented reading wins only when no typed reading fits its neighbours (an article,
  demonstrative or possessive with no noun phrase after it, a pre-verbal pronoun with no verb, a
  conjunction or preposition with nothing after it; a determiner after `dónde/donde/cómo` becomes the
  verb; `como` + pronoun + verb or + a finite verb becomes `cómo`); grammar-bound words and names keep
  their reading; up to 3 passes. Accent policy: nothing is reported to the learner (no accent tag, as
  for `arbol`); a recast made for another reason restores the accent (`AccentRestoration.SpellingsFor`
  in `RecastBuilder`). Corpus of 1102 sentences: 4 false positives removed (`donde esta el arbol`,
  `como esta el herrero`, `aqui esta el herrero`…), 10 real mistakes now found that the old reading hid
  (`el esta simpatico` → `ser_estar`, `tu tengo` → `conj.person`…). The first hand-back added a false
  `conj.person` on `soy el herrero y tu como te llamas`; sent back and fixed (`ed5cde8`) before the gate.
  Fuzz: new O3 fact drops accents where the neighbours leave no doubt; realiser re-rolls
  determiner/preposition/pronoun patterns that a reader can take either way
  (`skip:accent-variant-homograph`, within the homograph budget); five seeds that already failed on
  main were narrowed with reasons in the fuzz README (`2to272FZCXD3`, `belmI3pADeia`, `fApfampI1fQ3`,
  `8OeIOEx_Hfm3`, `1RTZdjQ2tFb2`). Suite: 2749 passed, 5 skipped; fuzz 57 facts. Known, not fixed:
  `tu hablas`/`tu ayudas` still read as possessive + noun (`noun.number`); `esta hecho` gives
  `gender.agree`; the slot-grammar matcher still sees typed readings, so `[VERB:estar]` does not bind
  `esta` (the game keeps its supplement entry); **follow-up:** in a grammar with an alternation and a
  `[WORD]` name, a mis-conjugated verb can bind as the name and its error is lost (seed `1RTZdjQ2tFb2`,
  fuzz narrowed, source not fixed).
- **M26c results (CR-002/004/007/008):** `ResolvedWord(Token, WordRole {Word, Name, Unknown}, LexicalFeatures?
  Reading)` in Core.Stages, carried as an optional trailing `Words` on `AnalysisResult` and on
  `PipelineResult` together with `Answer` (`MatchResult?`); additive, existing constructions compile.
  The core `ReplayRecord` is unchanged (both are derived from the recorded inputs); they travel in the
  envelope's `result` and `Replayer` compares `/words` and `/answer`, so a resolver change that keeps the
  verdict still shows on replay (logs recorded earlier report them as differing; none committed). The two
  e2e goldens gained the new fields (additions only). Host files: any manifest-listed file the loader does
  not own (`ContentPaths.IsLoaderOwned`), bytes kept from the load's hash check (never re-read from disk),
  `ContentBundle.TryReadHostFile(path, out ReadOnlyMemory<byte>)` and `HostFilePaths`; strict relative
  path rule; none exposed when loaded with `VerifyHashes=false`. `docs/API.md` §8 states the threading and
  load-cost contract. Thread-safety findings, documented not fixed (follow-up candidates):
  `VoskWordRecognizer.Recognize` checks `_disposed` without a lock (dispose racing a call can use a freed
  model; rule: one instance per thread, no dispose in use); `DirectorySynthesisCache.Put` uses a fixed
  `<key>.wav.tmp` (two concurrent misses on one key can throw); `VoskRuntimeModel.Prepare` is not
  synchronized per model directory (construct recognizers of one model on one thread); Wav2Vec2 and ONNX
  embedding adapters give no guarantee. Suite: 2776 passed, 5 skipped.
- **M26d results (CR-003/005):** `ConversationContext.HostSlots` (`SlotMap?`) and `ExtraSkills`
  (sorted, distinct; ids non-empty without whitespace), set through `Create`; both per utterance (`Next`
  does not carry them), omitted from JSON and from the old snapshot hash when empty (existing hashes,
  envelopes and goldens unchanged), included in `HostStateHash` and the envelope's context when present.
  Host slots share the verdict's namespace and lose on a clash (the reserved feedback names win over
  both), so adding a host fact never changes a line that rendered before; formatters (`article(def)`,
  `words(m)`) work on them; a missing one behaves as a missing verdict slot. Extra skills join the
  pipeline's `intent.<id>` in the single `LearnerModel.Update`; `LearnerModel.StepSkills` steps named
  skills alone (shares one private `Apply`). CLI `--host-slot`, `--host-skill`. Bug fixed: the loader's
  render check gave every unknown slot the sample `espada`, so `{distance:words(m)}` failed to load;
  `TemplateEngine.SlotKinds` now infers number/verb slots from their filters (the "a `words` slot named
  like a noun" check is gone as a consequence). Fuzz O14 and the replay/serialization oracles cover host
  inputs. Caveat: a host that builds its own digest with the constructor and then sets `HostSlots` with
  `with` gets a stale hash (documented: use `Create`).
- **M26e results (CR-006):** both release archive digests matched; all extracted files pinned (Kokoro 377
  files, 401 MB; Parakeet 8 files, 671 MB; downloaded into the git-ignored `models/`).
  `KokoroSynthesizer.FromStore(store, id, speakerId, speed)`. Spanish voices from the model's
  `speaker2id` metadata: `ef_dora` 28 (female), `em_alex` 29 and `em_santa` 53 (male). Parakeet
  transcribes the bundled `es.wav` correctly (load 1.6 s, decode 0.67 s). Two bugs found: (1) native
  collision: `Microsoft.ML.OnnxRuntime` 1.31 (via Language) and sherpa-onnx ship different
  `libonnxruntime.so` into one output folder and sherpa fails (`VERS_1.28.2 not found`); fixed in
  Response.Tests with `ExcludeAssets="native"`, but **a host that loads both the embedding model and
  Kokoro will hit it** (needs one ORT version both packages accept; follow-up, package decision);
  (2) a reused sherpa-onnx Kokoro engine does not repeat its output for the same text; the synthesizer
  now creates and disposes the engine per call, deterministic but 1.5–3.7 s per line (use
  `CachedSynthesizer`); supersedes M26c's "loads on first Synthesize" for Kokoro. Response now references
  Asr (for `ModelStore`), so the Response package pulls the ASR package; moving `ModelStore` to Core is a
  follow-up. Suite after M26d+M26e: 2823 passed, 3 skipped (wav2vec2 ×2, e5 ×1, both need an ONNX export).
- **Content-authoring research (Opus, 2026-10-21):** the owner named content authoring (intents and the
  content folder) as the main cost. Report `docs/research/2026-10-21-content-authoring.md`; outcome in §10.
  It corrected the orchestrator's first proposal: the game's copies are unchanged rather than edited, and
  pattern suggestion generalises poorly, so a lint (coverage, confusion, reachability, CEFR, examples
  with expected errors) goes first, then layering with `std-v1`, then format de-duplication. Owner chose
  lint first. Side finding: the hand grammars hold 4 real cross-intent matches, one the `gracias` →
  `cast_spell` class hidden only by the tie-break (M27a will report them).
- **Two orchestrators (owner, 2026-10-21):** this repository is run by the core orchestrator only; the
  Vocaterra repository has its own orchestrator and plan. Protocol: the game writes requests for core
  changes to `docs/CORE_REQUESTS.md` in its repository; the core orchestrator reads that file at the
  start of every session, implements accepted requests as core milestones, and marks the row done with
  the core commit; the game orchestrator then bumps its submodule. The core never plans game content;
  the game never reaches into core source (packages only, API gaps go in the requests file).
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

### 7.4 M17b: new intents and scenes (moved to the Vocaterra repository)

The scene planning that stood here (innkeeper and quest-giver characters, their intents, prompts,
dialogue pools and state machines) is game content and moved to the Vocaterra repository on
2026-10-21 (owner decision: the core repository documents only the language core). The core part,
the `[WORD:slot]` token, the generic intents `confirm`, `deny`, `farewell`, `thank`, `introduce_self`
and the blacksmith `confirming` state, shipped as M17b-core (§5b).

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
| `Slg.Core` contracts: verdict, prompt spec, error tags, replay envelope | `SPECIFICATION.md`, the game design (moved to Vocaterra `docs/SPECIFICATION.md` in M19c) |
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
  `HostStateHash`); `NpcStateMachine`/`NpcDefinition`/`NpcState` to `AgentStateMachine`/`AgentDefinition`/`AgentState` (namespace `Decision.ConversationAgents`), content
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

Follow-ups after M19c (core part done in M19d, see §5b): `content/game-v1` in Vocaterra is a copy of the core sample taken before
M17b-core, so it lacks the generic intents and the `confirming` state; the game decides whether to
re-sync from the core sample or diverge (its first content milestone). Core follow-ups from the host
author: a repeat-count helper, prompt text in `PromptSpec`, the ContentBuilder as a dotnet tool.

### 8.3 Owner decisions
- Repository names (decided 2026-10-20): the core is `james-d12/Vocaterra.NLP` (this repository,
  renamed from `SpanishLearningGame`); the game is `james-d12/Vocaterra` (created empty on 2026-10-20, cloned
  at `/home/user/Dev/Vocaterra`, token access confirmed). Decided (owner, 2026-10-20): M19a also renames the solution, project names,
  namespaces, assembly names and NuGet package ids from `Slg.*` to `Vocaterra.Nlp.*` before any package
  is published; environment variables keep the `SLG_` prefix only if renaming them would break the
  parked CI, otherwise they become `VOCATERRA_` with the old names accepted for one release.
- Package feed: decided (owner, 2026-10-21): no hosted feed; the game repository includes this one as a
  git submodule and uses project references or the local feed from `scripts/pack.sh` (D5).
- Conversation state-machine engine: decided (owner, M19a): stays in the core (D6).
- Blacksmith sample content: decided (owner, M19a): stays as the core's sample and demo (D7).

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
| M24 | Speech reality check: 100–200 owner recordings with deliberate errors; benchmark Vosk, Parakeet, Whisper small on error preservation; ZIPA phoneme adapter; GPL-free sherpa-onnx pin (D2: decided 2026-10-21, the pin is a blocker on any distribution) | none (owner records first) | L |
| M25 | Optional tutor: Claude Haiku over HTTP with a local fallback, D0 validator, envelope and save recording, asynchronous feedback, false-correction report | M24 | M |
| M17b | Game scenes, planned in the Vocaterra repository; if M21 ships, drafting runs there against the game bundle | M21 | — |

M18 (in progress) is kept: its resolver seam and regression tests are what M20 plugs into, and it
removes the two false positives from `main` now.

### 9.1 Owner decisions raised by the report (with its recommendations; tracked in `docs/DECISIONS.md`)
- Online or offline: offline-capable core with the online tutor optional.
- Determinism: relax "N forbidden in core" and bit-identical cross-platform goldens to
  "replay-exact" (seeded RNG, stored time and hashing stay); write it up as ADR 0010.
- Licence stance: accept MPL-2.0 for unmodified libraries (UDPipe's C# binding); LGPL/GPL only as
  separate development-time processes (LanguageTool).
- A cloud cost cap for the tutor (about $0.30 per 1 000 utterances on Haiku-class models, estimated).
- Game engine: not a core decision; moved to the Vocaterra repository (owner, 2026-10-21).
- Pronunciation ambition: decide after M24's speech check.
- Also noted: the design doc's GOP citation (arXiv 2309.07739) points at a different method; the
  matching CTC-GOP work is arXiv 2507.16838.

## 10. M27: cheaper content authoring (owner: lint first, 2026-10-21)

### 10.1 Why
The owner: writing the intents and the content folder is expensive. Research report:
`docs/research/2026-10-21-content-authoring.md` (Opus, 2026-10-21). Measured: one new intent costs 70–100
lines across six files (the intent entry is 20–30 % of it); much is mechanical (all 107 dialogue line ids are
`<pool>.<n>`, all 58 agent actions repeat the pool name twice, the guide's 24 transitions hold 12 distinct
ones, 31 of 57 answer aliases are article + word). The game carries all 11 sample intents unchanged plus 4
of its own; 15 of 27 files are byte-identical; its only edits are 5 dialogue lines re-worded for A1 and the
notice; of its 1,606 hand-maintained lines about 357 are its own work (~445 copied lines are generic,
~680 blacksmith material its code does not use). The costly mistakes in the history are behavioural
(grammars too narrow, too broad, homographs; CEFR rewording), which the validator, checking only
references, does not catch. Pattern suggestion from examples was prototyped and rejected as a milestone:
held out one at a time it generalises to 23–28 of 79 examples and mis-tags at least 12 of 59 suggestions.
No reusable Spanish intent data exists (Home Assistant's 38 Spanish intents are smart-home, CC BY 4.0).

### 10.2 M27a: content lint (Sonnet, size S/M)
`vocaterra-content lint <bundle> [--strict]`, also run by `validate` as warnings; it runs the real analyzer
and `IntentResolver`:
1. Coverage: each example resolves to its own intent, reporting the rung (grammar or keyword rule).
2. Confusion per prompt, within the prompt's intent allow-list (what the resolver sees).
3. Grammar hazards: optional `[WORD]` next to an optional element; a literal or lemma the lexicon lacks;
   a `[VERB:x]` lemma whose forms are also noun forms.
4. Reachability: pools no agent uses, intents no prompt allows, answer sets no prompt names, agent states
   with no way in.
5. CEFR: dialogue, prompt text (M26f's `cefr-check`) and answer-set canonicals; `--fail-on-level`.
6. Optional example expectations: an example may be `{"text": "...", "errors": ["conj.person"]}` and the
   lint checks the verdict's tags (additive schema; string examples keep working).
Per-intent waiver (`"lint": {"allowOverlap": [...]}`); `validate` never fails on lint by default. For
uncovered examples the report may print a suggested grammar skeleton (the useful half of the rejected
suggestion idea). Success: on the sample it reports the `cast_spell`/`thank` overlap and nothing judged
false (or waived with a reason); on a fixture copy of the game bundle it reproduces the research's 4
confusions filtered by prompt allow-lists and lists the blacksmith pools as unreachable from the guide's
prompts; under 5 s on the sample; bundle hash unchanged unless examples gain expectations. Not a hard
merge gate until one round of waivers has been done; after that "never weaken an oracle" applies to it.

### 10.3 M27b: layering and `std-v1` (Sonnet, size M)
- Manifest `"base": {"bundleId": "std-v1", "bundleHash": "<sha256>", "path": "<relative dir>"}`; a host may
  also supply base directories through `ContentLoadOptions` (packaged `std`). Hash mismatch is a hard error.
- Merged hash `Combine("layered", baseHash, overlayBundleHash)`; `VersionStamp` records it; provenance and
  the replay header list both.
- Whole-item replacement by id (intent, prompt, dialogue pool, answer set, agent, effect); singleton
  replacement (feedback policy, evaluation config); union (lexicon, CEFR lists); `ATTRIBUTION.md` per layer.
- Explicit `replaces`/`removes` per kind in the overlay manifest; an undeclared collision or a declared id
  that does not exist is a validation error; `validate` prints the effective override table.
- `intents/`, `prompts/`, `dialogue/` accept several `*.json` files (ordinal path order, duplicate ids an
  error). One level only. `std-v1` passes A1 with zero unlisted lemmas.
- Success: `content/v1` = `std-v1` + blacksmith overlay with every golden verdict unchanged apart from
  `contentHash`; lint finds no new issue; a layered bundle packs, verifies and hashes; `docs/API.md` shows
  the route; the game is told through `CORE_REQUESTS.md`/its notes, never by editing its repository.
- Not to do: deep merge, array patches, multi-level bases, silent override, reading the game's host files.

### 10.4 M27c: format de-duplication (Haiku, size S)
Additive shorthand that loads to the same objects: line ids default to `<pool>.<n>`, an action's
`variantPoolId` defaults to its `dialogueIntent`, keyword rules' `requiresSlots` default to the intent's,
`anyState` transitions, named grammar fragments / `<np:x>` macros, answer sets with `"articles": true`
generating article aliases from the lexicon. Success: the sample rewritten in the shorthand gives
identical catalogs and goldens apart from the hash.

### 10.5 Later (not scheduled)
LLM drafting at authoring time = M21, with `lint --strict` and `cefr-check --fail-on-level` as its
acceptance gate; intents from examples via embeddings after D9 and the e5 export (lint data gives the
calibration sets); character templates when a third agent of an existing shape is planned; pattern
suggestion revisited after M20. Do not import Home Assistant or MASSIVE sentences into content.
