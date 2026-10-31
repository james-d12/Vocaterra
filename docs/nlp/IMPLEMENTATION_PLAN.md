# Implementation Plan — Voice Recognition & Feedback System

> **Layout note (R01, 2026-10-23):** the repository now has one root layout. `nlp/src`, `nlp/tests`, `nlp/tools` and `game/src`, `game/tests`, `game/tools` are `src/`, `tests/` and `tools/`; `nlp/docs` is `docs/nlp/`, `game/docs` is `docs/game/`; `nlp/content/v1` is `content/v1`, `game/content/*` is `content/`, `nlp/golden` is `tests/golden`, `nlp/models` is `models/`, `game/assets` is `assets/`, `nlp/scripts` is `scripts/`, `game/playtest` is `scripts/playtest`; `LICENSE` and `NOTICE` are at the root and cover the whole repository (Apache-2.0). The core is the `Vocaterra.Nlp.*` projects, the game is `Vocaterra.{Simulation,Game,MonoGame,Host,SpeechRender}`; `Vocaterra.Nlp.slnf` builds the core alone. Paths and links below were updated to match; the narrative is otherwise unchanged. `.github/workflows-pending/ci.yml` still uses the old `nlp/` paths.

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
  docs/nlp/adr/               architecture decision records
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
| M16a | Combinatorial fuzz testing, stage 1: project, CsCheck, generators, harness, language oracles (see `docs/nlp/FUZZING_PLAN.md`) | M13b | S | done |
| M16b | Combinatorial fuzz testing, stage 2: signal, decision, content and pipeline oracles | M16a | S | done |
| M17a | Vocabulary expansion: Wiktionary (kaikki.org) lexicon import filtered by a frequency list, provenance, coverage gates (see §7) | M16b | M | done |
| M17b | Intent expansion, game part: planned and built in the Vocaterra repository (not tracked here since 2026-10-21) | M17b-core | — | moved (Vocaterra) |
| M17b-core | The core part of M17b: `[WORD:slot]` grammar token, generic sample intents (confirm, deny, farewell, thank, introduce_self), blacksmith confirming state, fuzz support | M19a | M | done |
| M18 | Part-of-speech disambiguation in the analyzer (triggered: `ayer compré un libro` yields a false `conj.tense` on main after M17a) | M17a | M | done |
| M19a | Standalone core, step 1: boundary ADR, rename `Slg.*` → `Vocaterra.Nlp.*` (solution, projects, namespaces, packages), game-agnostic context and agent naming, opaque outcome kinds, `docs/nlp/API.md`, README reframed as a library (see §8) | M18 | M | done |
| M19b | Standalone core, step 2: NuGet packaging of `Slg.*`, content bundle packaging, `samples/HostConsole` consuming the packages (see §8) | M19a | S | done |
| M19c | Standalone core, step 3: new game repository scaffold with the game spec, game content bundle and a host project on the packages (see §8) | M19b | S | done |
| M17c | Evaluator polish from the CLI-examples review: near-miss recast, `vocab.wrong` expected value, plural/accent corrections, leading-adverb grammars (see §5b) | M16b | S | done |
| M19d | Post-split core follow-ups from the game host author: repeat-count helper and next-turn context, prompt `text` in `PromptSpec`, ContentBuilder as a dotnet tool `vocaterra-content` | M19c, M17b-core | H | done |
| M19e | Apache-2.0 licence (D3), submodule consumption route (D5), core/game documentation isolation (D12) | M19d | H | done |
| M19f | Recast bug from the fuzz suite (O5, seed `1diyRPH9lxJ1`): a `conj.tense` error after a leading `ayer` on an irregular verb (`ayer tengo sufrir`) is detected but the recast repeats the wrong form instead of `tuve` | M19e | H | done |
| M26 | Game requests, batch 1 (CR-002…CR-013, triaged 2026-10-21 into M26a–f below; all rows `done` in the game's `docs/nlp/CORE_REQUESTS.md`) | M19f | — | done |
| M26a | CR-012: repeat counts mean "before this utterance" on `ConversationContext`; the pipeline applies `RepeatCounts.Next` itself before `FeedbackPolicy.Plan` and `RepairFor`; docs and sample host follow | M19f | H | done |
| M26b | CR-013: accent-insensitive lookup also offers the accented readings of a form the lexicon has (`esta`/`está`, `si`/`sí`, `mas`/`más`, `el`/`él`, `tu`/`tú`…) and the reading resolver chooses | M19f | S | done |
| M26c | CR-002 analysis on the result, CR-004 answer match on the result, CR-007 host-owned bundle files (`ContentBundle.TryReadHostFile`), CR-008 threading and load-cost contract in `docs/nlp/API.md` | M26a | H | done |
| M26d | CR-003 host slot values (`SlotMap` on the context, template access, replay record and `HostStateHash`), CR-005 host-named skills in the learner update | M26c | S | todo |
| M26e | CR-006 speech-out readiness: Kokoro and Parakeet file hashes pinned from real downloads, `KokoroSynthesizer.FromStore`, Spanish speaker ids documented (D2: private builds only) | M19f | H | done |
| M26f | CR-010 `cefr-check` reads prompt text; CR-011 sample `ATTRIBUTION.md` describes the sample only (content procedure: hash re-pin, goldens) | M26d | H | done |
| M26g | Intermittent failure from M26e: `ParakeetFromStoreTranscribesTheSpanishSampleClipDeterministically` failed 1 of 5 isolated runs (two decodes in one process differ in a field the assertion message hides; transcript equal); find the nondeterminism in the sherpa-onnx adapter or its configuration and fix it at the source | M26e | H | done |
| M27a | Content lint (owner chose lint first, 2026-10-21): `vocaterra-content lint` — example coverage per intent, confusion within each prompt's allow-list, grammar hazards, reachability, CEFR incl. prompt text; examples may carry expected error tags; `validate` warns, `--strict` fails; per-intent waivers (see §10) | M26f | S | done |
| M27a2 | Lint follow-up on the sample content (needs `hash`): narrow `cast_spell`'s bare-noun grammar so it no longer takes `gracias` (or waive it), a `thank` grammar for `muchas gracias…`/`mucho gracias` (or `allowRuleOnly`), example expectations (`yo es Rosa`, `yo estás de acuerdo`, `yo no quieres`, `mucho gracias`, `yo tiene dos espada`); fold into M27b if that is sooner | M27a | H | proposed |
| M27b | Content layering and `std-v1`: manifest `base` pinned by id, hash and path; whole-item replacement with explicit `replaces`/`removes`; lexicon and CEFR unioned; one level; `content/v1` becomes `std-v1` plus a blacksmith overlay (see §10) | M27a | S | todo |
| M27c | Format de-duplication: defaulted redundant fields, any-state transitions, `<np:x>` macros, generated article aliases; old files load identically (see §10) | M27b | H | todo |
| M28a | Game requests, batch 2: CR-017 two-phase evaluation (analyse, then plan the response with host slots and skills computed from the analysis; one evaluation per turn) and CR-015 host-initiated agent turns (the agent speaks and moves on a host event, in the replay record and the interaction counter) | M26 | S | done |
| M28b | Game requests, batch 2: CR-016 replies that depend on a host fact (variant or pool chosen by a host slot, or a conditional that renders only when a slot is supplied) and CR-018 lists in templates (`join` with per-part count agreement) | M26 | S | proposed |
| M28c | Game requests, batch 2: CR-014 host-supplied expected slot values graded like an answer set (`vocab.wrong` on the span, correctness and `communicated` follow) | M26 | S | proposed |
| M28d | Game requests, batch 3 (the last rows of the game's former `docs/nlp/CORE_REQUESTS.md`, closed when the repositories merged on 2026-10-21): CR-019 `scripts/fetch-models.sh` copies `models/manifest.json` into the destination (or `ModelStore` takes a manifest path), so a shared models folder is self-describing; CR-020 a public WAV reader and writer (mono or multi-channel 16-bit PCM to `AudioBuffer`); CR-021 the two `libonnxruntime.so` (Microsoft.ML.OnnxRuntime and sherpa-onnx) coexisting without the consumer's `ExcludeAssets="native"` workaround; CR-022 an ASR confidence that tracks the audio (for example the mean or minimum of Vosk's per-word `conf`) as `AsrResult.Confidence`, or a recognizer option for it | M26 | S | proposed |
| M29a | Game request (prototype S5, conditional): a trailing optional literal group in the slot-grammar DSL (`(por favor)?`); filed only if S5 confirms the DSL cannot express it | M27a | H | proposed, conditional |
| M29b | Game request (prototype S7, conditional): evaluate with an explicit `RecognitionRequest` (the repair ladder's `RetryRequest`) instead of the prompt's request; filed only if S7 finds no `PipelineOptions` route. **Confirmed by S7 (2026-10-22):** `Analyze(PcmClip, PromptSpec)` builds the request from the prompt and answer set only; the call needed is an optional `RecognitionRequest` on `Analyze`/`Trace`/`Evaluate`. Low priority (the game shows the choices as buttons) | M28a | S | proposed |
| M29c | Game request (prototype S5, bug): number and gender agreement blames the noun when the modifier is wrong. "no, mucho gracias" gives `noun.number` expected 'gracia' (and `gender.agree` expected 'mucha'), recast «no mucha gracia»; "buenos día" recasts «Bueno día». The expected forms and the recast must agree with the noun as said when the noun is a valid word ("muchas gracias", "buenos días"), at the source in `AgreementChecker` / `RecastBuilder`, with regression tests and the fuzz oracles unchanged. Waiting: the game's S6/S7 feedback (on-screen recasts) | M28a | S | done (merged to `feature/prototype` 2026-10-22, `27808db`) |
| M29d | Fuzz finding (found during M29c at 30,000 grammar iterations, seed `6QxmQKiJ7Ip8`, oracle O3): "conejos Movo una cuestiono por favor" flags `conj.irregular` expected 'muevo' on a `[WORD]` name slot ('Movo'); a name slot must not be graded as a verb. Not blocking the game | M29c | H | proposed |
| M29e | Fuzz suite flake (found by the S2 gate, 2026-10-22): `PipelineFuzzTests` asserts at least 10 `hostturn:none` cases in 300 random-seed iterations, but the generator produces about 11 to 20 (P(host turn) 1/3 x P(unknown agent) 1/5); five runs gave 8, 11, 15, 17, 20, so roughly one gate in seven fails. Fix the generator so every covered situation's expected count is several standard deviations above its minimum; the minimums stay | M28a | H | done (merged to `feature/prototype` 2026-10-22) |
| M29f | Game request (prototype S7): the repair ladder's offered choices are the first three ordinal-sorted grammar phrases of the answer set (`UtterancePipeline.RepairFor` with `answerSet.GrammarPhrases()`), so the stall offers «agua por favor», «ayuda», «ayuda por favor» and the offer never offers «no gracias». Offer one phrase per answer (the canonical), limited to the prompt's allowed intents and the answers that serve the prompt's purpose (help/dont-know answers last or not at all), deterministic order; e.g. `AnswerSet.CanonicalPhrases()` used in `RepairFor`. Waiting: the game's choice buttons (S7) | M28a | H | done (merged to `feature/prototype` 2026-10-22, `bdc490b` and the game-test follow-up `bc85a7f`) |
| M29g | Game request (prototype S9): `provenance.json` has no kind for model-generated host files (pre-rendered Kokoro speech): `hand-authored` would be false provenance and `llm-draft` needs a human approval. Add a model-output kind with a required `model` field (optional voice and source). Waiting: the game's S9 speech files | M27a | H | done (merged to `feature/prototype` 2026-10-22) |
| M29h | Fuzz suite flake (found by the S8 gate, 2026-10-22): `ChangingOneRecordedOutput_MakesTheReplayerNameThatPath` asserts each envelope region is reached at least once in 300 random-seed iterations, but `region:effects` is reached about 4 times per run (measured 3 to 5), so about 1 to 2 runs in 100 fail. Fix the generator or the mutation-path choice so every region's expected count is well above its minimum; minimums unchanged | M29e | H | done (merged to `feature/prototype` 2026-10-22) |
| M29i | Fuzz finding (found during M29h, 2026-10-22, seed not recorded): `MutationTagFuzzTests.OneMutation_YieldsExactlyTheNamedTag_WithTheOriginalFormAsCorrection` (O4) failed once: "missing [word_order@22+23 expected 'juventudes sorprendidas' actual 'sorprendidas juventudes']"; passed on reruns. A rare analyzer word-order mismatch; reproduce with a recorded seed (raise iterations until it recurs), fix at the source. Not blocking the game | M29h | S | proposed |
| M29j | Game request (prototype S19, for S20's book): readings a learner's word record exposes. (1) `está` in «¿dónde está el río?» is read as the 2 sg imperative, because the resolver takes the first reading in lexicon order; prefer the indicative when a subject follows or the verb is not clause-initial. (2) «gracias» alone and in «muchas gracias» is read as the plural noun `gracia`; give it an interjection reading (lemma `gracias`). (3) `favor` in every «por favor» is read as a noun; read «por favor» as one fixed expression (tokens keep their spans). Agreement checks must not change: M29c's «mucho gracias» still gives `gender.agree` 'muchas'. Regression tests in the analysis module; no fuzz oracle weakened; goldens re-recorded only where a reading changes, with errors unchanged; the core demo unchanged. Numerals as digits (`dos` gives lemma `2`) stay as documented. Waiting: game S20 (the book's Nouns and Verbs pages) | M29c | S | done (merged to `feature/prototype` 2026-10-22, `7ebf9b5`) |
| M30 | LanguageTool content grammar check (D10's dev-time half, owner 2026-10-22; design in `docs/nlp/research/2026-10-22-languagetool.md`): `vocaterra-content grammar`, a plain HTTP client (no LanguageTool code linked; licence scan unchanged). (1) It extracts the bundle's Spanish strings, or reads a host's `--strings` JSONL, and renders slot templates with the core's own template engine from sampled fillings. (2) It capitalises learner-side strings before the call, so `UPPERCASE_SENTENCE_START` is not hit. (3) Output matches `lint`, with a waiver file that gives a reason per accepted match. (4) Learner-error examples act as a second oracle (flagged and tagged, tagged but silent, untagged but flagged). (5) A committed result cache keyed by sha256 of LanguageTool version, settings and text; `--offline` reads only the cache. (6) `scripts/fetch-tools.sh` and `tools/manifest.json` (Temurin JRE and LanguageTool pinned by hash; no n-grams). (7) `scripts/languagetool.sh` starts the server in a capped systemd scope (2 GB, 2 cores), not through `limited.sh`. (8) Tests use a fake HTTP server, so `dotnet test` needs no Java. (9) A cache and waivers for `content/v1`, which closes M27a2's three untagged examples. Replaces the LanguageTool half of M22. Waiting: game S23 | M29j | H | done (merged to `feature/prototype` 2026-10-22, `0d04a33`) |
| M49 | Game request (prototype S27, D-game-19): tense checks for the near future. (1) A future-time marker (`mañana`, `la próxima semana`, `luego`…) with a past form gives `conj.tense`, expected the `ir a` + infinitive or future form. (2) A conjugated verb after `ir a` (`voy a como`) gives `conj.tense`, expected the infinitive, unless the core's design finds a better existing tag. Mirror today's past-marker rule in `AgreementChecker.CheckTense`. Regression tests in the analysis module; no fuzz oracle weakened; goldens unchanged unless a reading changes; core demo unchanged. Waiting: game S27 | M29j | S | done (merged to `main` 2026-10-23, `dc53cbb`) |
| M50 | Core folder cleanup (owner, 2026-10-22: "the nlp folder needs some cleanup"). `content/v1` stays (D7: the core's sample, tests, goldens, fuzz and demo depend on it). Retire what the two-repository era left behind: `scripts/pack.sh` and `docs/nlp/PACKAGING.md` (no pack step or feed since D5 was superseded); README, TOOLS.md and CONTRIBUTING passages about packages, feeds, submodules or the separate repository; `HIGH_LEVEL_DOCS.md` moved to `docs/nlp/history/` (it is the original design note). Refresh `TOOLS.md`, or fold it into the licence-scan docs. Decide whether `samples/HostConsole` and `scripts/sample-host.sh` still earn their keep (the game is the real host), and say why. Fix stale links. No behaviour change: the build, tests, goldens, licence scan and core demo are unchanged | — | H | done (merged to `main` 2026-10-23, `3bc525b`) |
| M51 | Game finding (S27): `ir` + a bare infinitive with no `a` (`voy comer pan`) passes clean (state_action, no error). Flag it, expected `ir a` + infinitive (`voy a comer`), under `conj.tense` or a better-fitting tag; regression tests; no fuzz oracle weakened (check the generator does not produce `ir` + infinitive as a correct sentence) | M49 | S | done (merged to `feature/experiment` 2026-10-23, `e70441c`; experiment X02) |
| M52 | Merge the 11 core libraries into one language-neutral `src/Vocaterra.Nlp` (owner, 2026-10-26; D14 amendment). Each old project becomes a folder and the namespaces do not change. The 11 unit test projects become `tests/Vocaterra.Nlp.Tests`; Fuzzing and Golden stay separate. `LayeringTests` (NetArchTest.Rules) enforces today's project graph; `VocaterraCore` in `Directory.Build.props` matches the new name; `Vocaterra.Nlp.slnf` is recreated (missing today, so the licence scan is broken); ADR 0012. No behaviour change: the identity guard is green, the `content/game-v1` hash and the goldens are unchanged. Spec: `docs/nlp/research/2026-10-26-engine-and-language-packs.md` §5.1 | M31 | H | done (merged to `feature/language-packs` `1976508`) |
| M53 | Extract the Spanish pack into `src/Vocaterra.Nlp.Es` (+ `tests/Vocaterra.Nlp.Es.Tests`), namespace `Vocaterra.Nlp.Es.*`. Hosts register packs through `ContentLoadOptions.LanguagePacks` (the engine cannot reference a pack); `LayeringTests` adds the rule that the engine does not depend on Es; the toy pack runs against the engine alone. The identity guard stays byte-identical. Spec: same document, §5.2 | M35 | S | done (merged to `feature/language-packs` `65de544`, 2026-10-27) |
| M54 | Game finding (the C series, `docs/game/CONTENT_PLAN.md` C01): the Spanish identity guard pinned the bytes of `content/game-v1`. The verdicts stamp the bundle's content hash, which the `grade` and `pipeline` stages hashed, so any edit of the bundle, even one that changes no Spanish behaviour (a game file's `$schema`), failed it, against the rule that no core test pins the hash. The guard masks the hash before hashing, and the baseline is recorded again once | M31 | S | done (on `feature/content`, 2026-10-29) |
| M55 | Game finding (the C series, C03): four manifest tests used `content/game-v1`'s manifest as the example of one without language fields. game-v1 now names its support language (`supportLanguage: en`, the default locale of the game's locale bundle), so the tests strip that optional field from the example themselves, and one asserts that the support language stays out of the hash | M32 | S | done (on `feature/content`, 2026-10-29) |
| M56 | Game finding (the V series, V10 "El pueblo", 2026-10-30): three agreement checks are silent where a learner errs. (a) `ser` with a location adverb or a location question: `es lejos`, `es cerca`, `¿dónde es la herrería?` give no `ser_estar` (expected `está`); (b) a plural verb before a singular subject: `¿dónde están la herrería?` gives no `conj.person` (expected `está`); V11 "Números y pan" (2026-10-31) found the same with `costar`: `¿cuánto cuestan el pan?` and `cuánto cuesta los panes` give no `conj.person`. V13 "¿Cómo es?" (2026-10-31) found the same with `gustar`, whose subject follows it: `me gusta las manzanas` and `me gustan el pan` give no `conj.person` (`me gustan las manzanas` and `me gusta el pan` are clean). Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `gender.agree` examples meanwhile (`content/PHRASES.md`, `esta-lejos`, `donde-esta-la-herreria`) | | S | done 2026-10-31 (`d87e823`, on `feature/village`; findings in §5b) |
| M57 | Game finding (V10): (a) the fuzz suite's Spanish sentence generator knows only `mañana` as a future time marker, so a bundle grammar with `la semana que viene` or `el próximo …` makes it produce sentences it counts as correct which the core rightly flags `conj.tense`; teach the generator every marker `docs/nlp/API.md` "Tense errors" lists; (b) `pasado mañana` after a plural noun is read as an adjective of the noun (`compro panes pasado mañana` gives `noun.number`): read it as the time phrase; (c) `FuzzLanguageStreamTests`' two stream digests depend on `content/game-v1` and were re-recorded at V10, generators unchanged: pin them to a fixed fixture or document them as content-moved goldens | | S | planned (not started; after the game's V16 and before V17, which waits on (a) and (b) before `state_action` takes the later markers; (c) first, so that content edits stop moving the digests) |
| M58 | Game finding (the V series, V11 "Números y pan", 2026-10-31): two learner slips are misread. (a) `cuánto costa` (the stem change missed) is read as the noun `costa`: it gives `gender.agree` (expected `cuánta`) and no intent, where `conj.irregular` expected `cuesta` is the slip (`cuánto costan los panes` is graded `conj.irregular` correctly); (b) `hay pans` gives `vocab.unknown` and no intent, where `noun.number` expected `panes` would let the intent resolve. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `cuánto costan los panes` and `hay dos pan` as the examples meanwhile (`content/PHRASES.md`, `cuanto-cuesta`, `hay-pan`) | | S | planned (not started; before the game's V15, owner 2026-10-31) |
| M59 | Game finding (the V series, V12 "Gente del pueblo", 2026-10-31): person and tense checks the third person needs. (a) `conj.person` looks only after a subject pronoun: a name or noun subject before the verb is not checked (`Domingo trabajo en la herrería`, `Domingo voy a hacer un hacha` give nothing; `él trabajo …` gives `conj.person`); V13 found the same with `ser` and a noun subject: `las manzanas es roja` and `la manzana son roja` give nothing; (b) a reflexive clitic is not matched to its verb's person (`cómo te llama`, `cómo se llamas` give nothing); (c) `ir a` followed by a finite present is not flagged (`va a hace un hacha` gives nothing, where `va a hizo` gives `conj.tense`); (d) a literal word in a slot grammar binds its first reading, so `hace` in `qué hace` is read as the preposition of `hace dos días` and the verb is hidden from the host's person check. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet puts a pronoun before the verb in its examples and the lesson's person check catches the rest meanwhile (`content/PHRASES.md`, `trabaja-en-la-herreria`, `va-a-hacer-un-hacha`, `como-te-llamas`) | M56 | S | planned (after M56 and before the game's V14, owner 2026-10-31: session 8 retells the village's day with named subjects) |
| M61 | Game finding (the V series, V08, 2026-10-31): the core's tests that pinned the game's bundle to a clean A1 follow the bundle's ceiling, A2 (D-game-22 (b), VILLAGE_PLAN 3.9), so phase B's first A2 word does not fail the core suite: `LintTests.TheBundleLintsCleanAtBothLevels` becomes `TheBundleLintsCleanAtItsCeilingAndA1IsAReport` (A2 with fail-on-level: 0 errors, 0 warnings; A1: 0 errors and only `cefr.*` warnings), and `LintCommandTests.LevelAddsCefrFindingsAndFailOnLevelMakesThemFail` moves the bundle's line to A2; two comments say the gate is A2. Tests on their own fixtures are unchanged; no other assertion changed | M27a | S | done (merged to `feature/village` `808e0f0`, 2026-10-31) |
| M60 | Game finding (the V series, CI on PR #20, 2026-10-31): `LintTests.TheBundleLintTakesUnderFiveSeconds` took about 6 s on the GitHub runner. Phase A grew `content/game-v1` to 47 prompts and 385 examples, and the confusion check analysed each example once per rival intent (about 33 times; 12 139 analyses, 1.4 of the lint's 1.6 s). `LinguisticAnalyzer.Analyze` becomes exactly `Read` (the text alone), `BestMatch` (the only step that reads grammars) and `Complete`, all internal; `LintContext` reads each text once, matches each grammar once and completes once per winning grammar; the expectations check reuses those analyses (`TextGrader`'s internal constructor). The lint's output is byte-identical (30 outputs on the bundle before and after phase A); the lint takes 398 ms instead of 1610 (Debug). The timing test reads the bundle before its clock starts; its 5 s budget is unchanged | M27a | S | done (merged to `feature/village` `95bd7bf`, 2026-10-31) |
| M62 | Game finding (the V series, V13 "¿Cómo es?", 2026-10-31): two adjective checks are silent. (a) A predicate adjective is not agreed with the subject across `ser`: `la manzana es rojo` (expected `roja`) and `el pan es grandes` (expected `grande`) give no `gender.agree` or `noun.number`, where the attributive `quiero tres manzanas rojos` gives `gender.agree` correctly; (b) an adjective before its noun is not understood: `quiero tres rojas manzanas` and `quiero una roja manzana` resolve no intent, and with a grammar that matches them give no `word_order` (expected `manzanas rojas`); colour adjectives, which do not go before the noun in neutral Spanish, are the case to flag, and `una gran casa`, `un buen pan`, `la primera casa` stay clean. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `quiero tres manzanas rojos` (`gender.agree`) meanwhile (`content/PHRASES.md`, `la-manzana-es-roja`, `quiero-tres-manzanas-rojas`) | M56 | S | planned (not started; after M58 and before the game's V15, owner 2026-10-31: "Run m62 before v15"; V15's `es muy caro` and `más barato que` are predicate adjectives) |

### Delegation waves

- **Wave 0 (sequential):** M0 → M1. Critical path; everything else programs against M1.
- **Wave 1 (parallel, each in its own worktree):** M2, M3, M4, M5, M7, M10.
- **Wave 2 (parallel):** M6, M8, M9, M11.
- **Wave 3 (sequential):** M12 → M13.
- **Wave 4 (parallel):** M14, M15.
- **Wave 5 (sequential):** M16a → M16b (fuzzing; added 2026-10-20 at the owner's request, modelled on CodeGuard's rule-fuzzing suite).

Each parallel agent touches only its own `src/<project>` and `tests/<project>.Tests` directories
(plus `content/` or `golden/` where the milestone says so). The orchestrator merges branches to
`main` and pushes after each wave once `dotnet build` and `dotnet test` are green. (Since 2026-10-23, owner: never `main`; branches
merge into a feature branch that is pushed, and the owner merges it into `main` by pull request.)

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
  except `manifest.json` + `tests/golden/audio/`), README skeleton, `.github/workflows/ci.yml`
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
- `Slg.Bench`: `--manifest tests/golden/manifest.jsonl` → WER, learner-error preservation rate
  (does the transcript keep the annotated error?), latency p50/p95 per model and mode.
- `tests/golden/README.md`: recording protocol, consent, manifest schema; `tests/golden/manifest.jsonl` seeded
  with entries for synthetic test clips.
- Tests: end-to-end worked example with fakes reproduces the HLD verdict; replay of the same record
  is byte-identical.

#### M16 — Combinatorial fuzz testing (S)
- Full design in `docs/nlp/FUZZING_PLAN.md`: CsCheck generators driven by the project's own catalogs
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

- **R01 (2026-10-23, repository-wide; tracked in `docs/game/IMPLEMENTATION_PLAN.md` section 4.6):** the core's projects moved to the root
  `src/`, `tests/`, `tools/`; `content/v1`, `tests/golden`, `models/`, `docs/nlp/`; `nlp/Vocaterra.Nlp.sln` replaced by the filter
  `Vocaterra.Nlp.slnf` (licence scan, core-only build); one root `Directory.Build.props` keys the core's packing on project names
  `Vocaterra.Nlp.*`. Bundle hash `1c2240c7...` and all goldens unchanged. Next, R02 moves the core's tests onto `content/game-v1` and
  retires `content/v1` (supersedes D7 when done).
- **R02 (2026-10-23, done):** the core's tests, fuzz suite, demo and embedded seed lexicon run on `content/game-v1`; `content/v1` deleted; D7 superseded. Fuzz found and fixed a duplicate `conj.tense` in `AgreementChecker` (ir a + past marker). Details in `docs/game/IMPLEMENTATION_PLAN.md` section 4.6.

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
  re-recorded; `tests/golden/manifest.jsonl` is unchanged. O3 now realises every catalog grammar.
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
  `lexicon-sources/` (fetched by `scripts/fetch-lexicon-sources.sh`); `docs/nlp/LEXICON.md` has the
  regeneration command.
- **Hand-authored deference (M17a):** the importer's `--hand-authored` option drops imported readings
  that would override hand-authored ones (Wiktionary's masculine `espada`, the matador, would otherwise
  make `un espada` correct) and words spelled like hand-authored function words (`la` the note, `si`
  the noun). Curated readings win; the policy is in `docs/nlp/LEXICON.md` and ADR 0008.
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
  example was removed from `docs/nlp/CLI_EXAMPLES.md` until M18 lands and is to be restored then.
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
  check number). The `docs/nlp/CLI_EXAMPLES.md` prose for the `fuego` example (expected now empty) is
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
  `docs/nlp/API.md` (snippets compiled in a scratch host) and ADR 0009. Follow-ups taken into M19b: the
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
- **M19c results:** `james-d12/Vocaterra` has its root commit: the game design (`docs/game/SPECIFICATION.md`),
  `docs/game/ARCHITECTURE.md`, bundle `content/game-v1` (a copy of the core sample with id `game-v1`, same
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
  `docs/nlp/PACKAGING.md` documents the decided consumption route (the game includes this repository as a
  git submodule and uses the local feed from `scripts/pack.sh`; hosted feeds are not planned).
  Isolation audit: README, `HIGH_LEVEL_DOCS.md`, `TOOLS.md`, `docs/nlp/PACKAGING.md`, `docs/nlp/PACKAGE_README.md`
  and `docs/nlp/LEXICON.md` now describe the core only; ADRs 0001–0009 and the research report keep their
  bodies with a note that game matters are decided in the Vocaterra repository; text not already in
  the game's docs went to Vocaterra `docs/nlp/NOTES_FROM_CORE.md`. No code or content changed (bundle hash
  unchanged). Found by the agent's second test run: fuzz oracle O5 fails with seed `1diyRPH9lxJ1`
  (`ayer tengo sufrir`: `conj.tense` expected `tuve`, recast repeats `tengo`); reproduced by the
  orchestrator on main before the merge, so it predates M19e; scheduled as M19f. Follow-ups:
  `content/v1/ATTRIBUTION.md` still says "the game loads at run time" and "licence not chosen" (a
  content edit, needs the bundle procedure; fold into the next content milestone); the game's
  `docs/game/ARCHITECTURE.md` and `nuget.config` still name the old feed path (game side).
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
  document it in `docs/nlp/API.md`); CR-013, accent-insensitive lookup is only a fallback, so `esta`
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
  means counts before the utterance (README, `docs/nlp/PIPELINE.md`, `docs/nlp/CLI_EXAMPLES.md` examples shifted
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
  path rule; none exposed when loaded with `VerifyHashes=false`. `docs/nlp/API.md` §8 states the threading and
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
  content folder) as the main cost. Report `docs/nlp/research/2026-10-21-content-authoring.md`; outcome in §10.
  It corrected the orchestrator's first proposal: the game's copies are unchanged rather than edited, and
  pattern suggestion generalises poorly, so a lint (coverage, confusion, reachability, CEFR, examples
  with expected errors) goes first, then layering with `std-v1`, then format de-duplication. Owner chose
  lint first. Side finding: the hand grammars hold 4 real cross-intent matches, one the `gracias` →
  `cast_spell` class hidden only by the tie-break (M27a will report them).
- **M26f results (CR-010/011):** `CefrChecker` also checks prompt text, reported in its own section with
  prompt ids (`CefrPromptReport`); sample `ATTRIBUTION.md` rewritten for the sample only (hand-authored
  content Apache-2.0 per D3; third-party notices kept; model licences as in the manifest; D2 note kept;
  GitHub URLs instead of relative paths). Owner added `algo` and `acuerdo` to `cefr/a1.txt` (A1 and A2
  lists are disjoint by design and A2 inherits A1, so A2 is unchanged; the orchestrator's first
  instruction to add them to A2 broke `SampleContentTests` and was reverted). Bundle hash
  `cedb97c4…3afad1` → `1c2240c7…c9d38f2`; goldens differ only in `contentHash`. cefr-check on the sample:
  A1 prompt text 46 lemmas, 9 unlisted (game words: espada, hechizo, herrero, lanza, lanzar, moneda,
  objeto, oro, saludo). Process finding: the sandbox's worktree guard refuses `ContentBuilder ... hash`
  in an isolated agent (it reads the subcommand as the shell builtin `hash`), with no approval prompt;
  the orchestrator ran `hash` in the agent's worktree three times on the owner's explicit instruction.
  Every content milestone will hit this (M27b): either the owner runs or authorises the orchestrator to
  run `hash`, or the tool gains an alias subcommand (owner's call).
- **M26g results:** the intermittent Parakeet determinism failure was not threading (`NumThreads` is 1):
  sherpa-onnx 1.13.8's C# `Result.Durations` returns a buffer that is not float data on the Parakeet TDT
  path (pointer-like bit patterns, different on every decode), so the last word ended at ~2e37 s. The
  adapter no longer reads it: a word ends where the next starts and the last at the clip end (consumer
  change: `AsrWord.EndSeconds` includes trailing silence). Range assertions added; 30/30 isolated runs
  pass; Vosk and Kokoro checked, unaffected. Durations can return after an upstream upgrade.
- **M26 complete (2026-10-21):** all 11 game requests done; suite 2831 passed, 3 skipped.
- **Game requests, batch 2 (read 2026-10-21, late):** the game filed CR-014…CR-018 during the session
  (its G6/P4/P4b/P6b work). Accepted as M28a–c, split by module: M28a (Pipeline and Decision API,
  CR-017 "should" and CR-015), M28b (Response templates, CR-016 and CR-018), M28c (grading, CR-014). All
  three are disjoint from M27a (Content, ContentBuilder, the intent file schema), so M28a can run beside
  M27a once the owner agrees; M28a first because CR-017 costs the game a second evaluation per turn today.
- **M27a results:** `vocaterra-content lint <bundle> [--strict] [--level] [--fail-on-level] [--json]
  [--prompts a,b] [--host-pools p*,q]` over `ContentLinter.Lint(bundle, LintOptions)` in Content; `validate`
  prints lint errors/warnings and fails on them only with `--strict`. Checks: coverage (with rung;
  rule-only and uncovered examples, a grammar skeleton as a hint), confusion per prompt (`confusion.prompt`
  warning/error) and across the catalog (`confusion.catalog` info), grammar hazards (homographs as info:
  as warnings they were 7 true but unactionable findings on the sample), reachability (from all or
  chosen root prompts), CEFR (dialogue, prompt text, answer-set canonicals), example expectations.
  Additive schema: an example is a string or `{"text","errors","prompt"}`; per-intent `"lint"` waivers
  (`allowOverlap`, `allowRuleOnly`, `allowHomograph`; errors cannot be waived; waived findings still
  print). `TextGrader` grades text in Content (Content cannot reference Pipeline), with a parity test
  against `UtterancePipeline` on 99 example/prompt pairs; `ReservedPools` names the pipeline's reserved
  pools. Sample: 0 errors, 2 warnings (`thank` rule-only ×2), 8 info (the `gracias`/`cast_spell` overlap
  hidden by the tie-break, 7 homographs); 0.56 s. Game bundle (read-only): the research's 4 confusions
  reproduce; the host-voiced `guide.*` pools need `--host-pools`; from the guide's prompts the blacksmith
  material (12 pools, the agent, 7 answer sets, 8 intents) is unreachable. Reported, not fixed: `nos veo`
  (meant as a learner error) grades clean (clitic `nos` gives no subject; analyzer gap); pool-level waivers
  do not exist; the lint has no embedding rung. Content edits it proposes are M27a2 (they need `hash`).
  Suite: 2948 passed, 3 skipped.
- **M28a results (CR-017/015):** `UtterancePipeline.Analyze(clip, prompt)` returns an immutable
  `UtteranceAnalysis` (recognizer output, verdict, words, answer; no conversation state) and
  `Complete(analysis, context[, hostSlots, extraSkills])` runs decision, response, effects, repair and the
  replay record; `Evaluate`/`Trace` are now `Complete(Analyze(...))`, equal by construction and by a test
  over every example on every allowing prompt; the recognizer runs once; `Complete` refuses an analysis
  from a pipeline with another version stamp; two-phase chosen over a host callback (no host code inside
  the pipeline, testable, nothing unrecorded). Host turns: agent transitions gain optional
  `onHostEvent`, taken only by `AgentStateMachine.StepHostEvent` (utterances never take them);
  `UtterancePipeline.Advance(hostEvent, context)` returns `HostTurnResult?` (null when no transition or no
  machine: nothing changes); lines from the actions' pools with host slots only; interaction counter +1;
  learner and repeat counts untouched (nothing was observed; a host may call `StepSkills`);
  `ConversationContext.Next(HostTurnResult, now)`, `WithHost(...)`. Replay: a third line kind
  `"kind":"hostTurn"` (`HostTurnEnvelope`, `ReplayReader.ReadEntries`, `Replayer.Replay(HostTurnEnvelope,…)`,
  CLI `replay` reads both); old logs, envelopes and goldens unchanged; older core builds reject host-turn
  lines (documented). Fuzz O14: half the cases two-phase across two pipeline instances, a third take a
  host turn first; one bug found in the new code by the oracle (host turn recorded the full version stamp,
  replay differed at the phoneme model hash; seed `7CBlRsqnn4a1`; now records `ContentHash`). Side effect:
  serialized `AgentDefinition` JSON emits `onHostEvent: null` (no hash covers it). Not done: no
  two-phase example in `samples/HostConsole` (docs carry it). Suite: 2982 passed, 3 skipped.
- **Session close (2026-10-21, evening):** M26 (all 11 requests), M26g, M27a and M28a merged; the
  research report is in `docs/nlp/research/`. Next, each waiting for the owner's go: M27b layering and
  `std-v1` (with M27a2's sample fixes folded in; content edits need `hash`, which the sandbox refuses inside
  an isolated agent, so the owner runs it or tells the orchestrator to), M28b (CR-016/018 templates),
  M28c (CR-014 expected answers). Follow-ups logged: ORT native collision between
  Microsoft.ML.OnnxRuntime and sherpa-onnx in one host; Kokoro per-call engine cost (D9); `ModelStore`
  to Core; Vosk dispose race and the synthesis-cache temp file; `nos veo` analyzer gap; pool-level lint
  waivers; the `[WORD]`-absorbs-a-mis-conjugated-verb limit (seed `1RTZdjQ2tFb2`). The game's
  `CORE_REQUESTS.md` edits (all rows' statuses) are uncommitted in the game checkout for its orchestrator.
- **Two orchestrators (owner, 2026-10-21):** this repository is run by the core orchestrator only; the
  Vocaterra repository has its own orchestrator and plan. Protocol: the game writes requests for core
  changes to `docs/nlp/CORE_REQUESTS.md` in its repository; the core orchestrator reads that file at the
  start of every session, implements accepted requests as core milestones, and marks the row done with
  the core commit; the game orchestrator then bumps its submodule. The core never plans game content;
  the game never reaches into core source (packages only, API gaps go in the requests file).
- **Build output size:** each full build writes ~3 GB of native-package output per checkout;
  worktrees are cleaned after merge. Trimming runtime packs is a follow-up.
- **One repository (owner, 2026-10-21; game decision D-game-7):** the core now lives under `nlp/` of
  `james-d12/Vocaterra`, the game under `game/`; this history was rewritten under `nlp/` and merged.
  One orchestrator (`.claude/agents/orchestrator.md` at the root), one gate (`scripts/merge-gate.sh`,
  which runs this folder's licence scan too), one solution (`Vocaterra.sln` at the root; this folder's
  `Vocaterra.Nlp.sln` stays for core-only work, `scripts/pack.sh` and `scripts/license-scan.sh`). The
  game references the core's projects directly; the feed exists only for `samples/HostConsole` and a
  consumer outside the repository (D5 superseded in that sense). The game's request file is closed:
  its open rows are M28b, M28c and M28d; CR-024 (the pack picked up the game's `nuget.config`) is
  closed by the root `nuget.config` (nuget.org only). A game milestone never edits `nlp/`; a core
  change the game needs is a milestone here that names the game milestone waiting on it. Paths in this
  plan, the docs and the tests stay relative to `nlp/`.
- **M29c results (2026-10-22, Sonnet 5.5; found by the game's S5).** Root cause: `AgreementChecker.CheckDeterminer` blamed the noun for
  every number mismatch with a determiner and corrected the determiner's gender with its own number ("no, mucho gracias" gave expected
  'gracia' and 'mucha', recast «no mucha gracia»); `CheckAdjective` made "buenos" agree with "día" («Bueno día»). Fix: a table of fixed
  plural expressions (`AnalysisTables.IsFixedPluralExpression`: bueno + día/tarde/noche, mucho + gracia); when no other determiner is
  present and either side is plural, the phrase is plural and the modifier takes the noun's gender. The generic rule stays (fuzz oracle O4
  requires `noun.number` on the noun next to a singular determiner). Now: «no muchas gracias», "buenos día" → 'días'. `RecastBuilder`
  unchanged. 53 new tests; goldens unchanged; core demo unchanged. A longer fuzz run found M29d (unrelated). Recasts still lose ¿ ? (built
  from normalised text; a design question, not done).
- **M29e results (2026-10-22, Haiku 5.5; found by the game's S2 gate).** The pipeline fuzz generator now draws a host turn's agent after
  the host-turn flag (1/2 the blacksmith, 1/2 an unknown agent), so `hostturn:none` averages 54 per 300 iterations (minimum 43 in 20 runs;
  before 19, minimum 8) against the unchanged minimum of 10; every other asserted key was already more than 4 standard deviations clear.
  Coverage flake, not a pipeline bug; logged in the fuzzing README.
- **M29g results (2026-10-22, Haiku 5.5; requested by the game's S9).** `GeneratedByKinds.ModelOutput` (`"model-output"`): a provenance
  item of this kind needs a non-blank `model` and may carry `voice`, `speaker` (string) and `source`; no approval; refused for
  loader-owned language data (lexicon, intents, prompts, dialogue, ...) and the model fields refused on other kinds; the fields serialize
  only when set, so existing bundles are byte-identical. Fuzz O15 lists the four new optional fields like `approvedBy`. 23 new tests.
- **M29f results (2026-10-22, Haiku 5.5; found by the game's S7).** `AnswerSet.CanonicalPhrases()` (one canonical per answer, the set's
  declared order, no aliases); `RepairStrategy` offers the first three distinct canonicals; attempts 1 and 2 keep the full grammar, the
  attempt-3 retry grammar is the offered canonicals. The stall now offers «quiero agua por favor», «quiero pan por favor», «quiero fruta por
  favor» (was «agua por favor», «ayuda», «ayuda por favor») and the offer «sí por favor», «no gracias», «ayuda» (was «ayuda», «ayuda por
  favor», «claro»). Help answers come last by content order (no service marker in the core). The agent built only the core solution, so
  three game tests that pinned the old choices failed in the gate; a follow-up commit (the orchestrator's narrow exception) updated them.
  Lesson: a core milestone that changes behaviour visible to the game builds the whole solution.
- **M29h results (2026-10-22, Haiku 5.5; found by the S8 gate).** The replay test's effects region depends on a communicated verdict of an
  effect intent (only 37 of 300 cases had effects, and the mutated leaf was drawn uniformly over all leaves). Now the mutation picks a region
  uniformly among those the envelope has, then a leaf in it, and one case in four is aimed at an effect intent (drawn last, so earlier draws
  keep their values). Effects per run: before min 2, mean 5.4; after min 30, mean 47.4 (15 runs). Minimums unchanged. A rare O4 word-order
  failure seen once during the work is filed as M29i.
- **M29j, readings a learner's word record exposes (2026-10-22, Sonnet 5.5; found by the game's S19; merged `7ebf9b5`).**
  - **Root cause.** `result.Words` reported the first reading the resolver left, in lexicon order. The resolver rightly works in word classes:
    the imperative and indicative of one verb are both finite, and a noun beats an interjection because interjections are a last resort. So
    the reported reading was an accident of lexicon order, and the lexicon's multi-word entry `por favor` could never match a single token.
  - **Fix.** A reporting step, `Analysis/ReportedReadings`, runs after resolution and changes no error and no check (`ReadingResolver` and
    `AgreementChecker` are untouched, so M29c's «mucho gracias» still gives `gender.agree` 'muchas'):
    - an ambiguous imperative/indicative form is reported as the imperative only when it opens its clause; clause-initial `ser`, `estar` or
      `haber` before a determiner, noun or participle is the indicative;
    - `gracias` is the interjection unless an article or numeral precedes it, through a one-word table (`AnalysisTables.InterjectionFirstWords`),
      because the imported lexicon marks `sus`, `sé` and others as interjections too;
    - two or three adjacent tokens whose joined text is an exact lexicon form (`por favor`, `de nada`) report that entry's reading on each
      token. The lemma contains a space; each token keeps its `Text` and `Span`.
  - **Boundary.** No public member added; API.md documents the three cases. Goldens unchanged; 22 new tests; fuzz 57/57 at the defaults and
    at 10,000 and 8,000 iterations. Gate: 4,259 passed, 5 skipped; core demo unchanged.
  - **Known limits:** normalization drops `¿?`, so a bare «¿Está quieto?» reads as the imperative; `Está lloviendo` too (no gerunds in the
    lexicon); only exact lexicon multi-word forms are found («por qué» and «buenos días» are not in the lexicon).
- **M30, the LanguageTool content grammar check (2026-10-22, Haiku 5.5; merged `0d04a33`).**
  - **The command.** `vocaterra-content grammar <bundle> [--strings <jsonl> [--strings-only]] [--server URL] [--offline] [--cache]
    [--waivers] [--level] [--format text|json] [--strict] [--prune]`. Exit codes: 0 pass; 1 errors, or a cache miss offline; 2 usage;
    3 no server.
  - **Strings and oracle.** The strings JSONL (`id`, `text`, `kind` npc|learner|template, optional `errors`) is in API.md. Templates
    render from four sampled fillings with the core's `TemplateEngine`. Every intent example goes through the oracle.
  - **Files.** Cache `content/grammar/v1-cache.json` (sorted, keyed by sha256 of LanguageTool version, settings and text); waivers
    `v1-waivers.json` (a reason is required); learner-error fixture `v1-learner-errors.jsonl`; the tool manifest `tools/manifest.json`
    (Temurin and LanguageTool 6.6 by sha256); `scripts/fetch-tools.sh` and `scripts/languagetool.sh` (capped systemd scope, not
    `limited.sh`). Tests use an `HttpMessageHandler` fake, so no Java runs.
  - **content/v1.** No grammar error. One style waiver (`...` against `…`). Three untagged learner errors (`confirm.ex5`,
    `introduce_self.ex4`, `thank.ex4`), already M27a2's.
  - **Oracle over the 15 learner errors.** 11 flagged and tagged; 4 tagged but silent (`sí, por favo`, `yo nos vemos`, `dónde está la
    río`, `cuál es esto`). LanguageTool's suggestions are sometimes wrong (`Mucha gracia`, `cabo`).
  - **Checked by the orchestrator:** the gate (4,412 passed, 5 skipped; licence scan unchanged), and `grammar --offline` over content/v1
    with no server: exit 0, 3 warnings, 1 waived.
  - **Wired into the gate:** the orchestrator added an advisory step to `scripts/merge-gate.sh` (offline, never fails).
- **M50, core folder cleanup (2026-10-23, Haiku 5.5; merged to `main` `3bc525b`).**
  - **Removed:** `scripts/pack.sh`, `docs/nlp/PACKAGING.md`, `scripts/sample-host.sh` and `samples/HostConsole/`. The sample was in no
    solution and no test used it; its only caller was `pack.sh`, and with no feed (D5) nothing outside the repository will consume
    packages. The CLI and the game cover the host path.
  - **Moved:** `HIGH_LEVEL_DOCS.md` to `docs/nlp/history/`.
  - **Refreshed:** `TOOLS.md`, with the snapshot at `a642066`, 28 scanned packages, and the M30 JRE and LanguageTool.
  - **Rewritten:** README ("Using the core from a host"), CONTRIBUTING, API.md and scripts/README.
  - **New:** `scripts/check-links.sh`. It found 1 broken link before (to `.github/workflows-pending/README.md`) and 0 after;
    CONTRIBUTING requires it to pass.
  - **Unchanged:** `content/v1`, the goldens, fuzz, tests (4,477 passed, 5 skipped), the licence scan and the core demo.
  - **Kept as build configuration:** the packaging properties in `Directory.Build.props` and `docs/nlp/PACKAGE_README.md`.
  - **Left for the next game milestone:** stale references outside `nlp/` in `scripts/content.sh`, `scripts/README.md`, the root
    `nuget.config` comment and `docs/game/ARCHITECTURE.md`.
- **M49, near-future tense checks (2026-10-23, Sonnet 5.5; for the game's S27; merged to `main` `dc53cbb`).**
  - **The new check.** `AgreementChecker.CheckNearFuture` gives `conj.tense` in two cases:
    - a future-time marker with a preterite-only or imperfect-only finite verb. The markers are `mañana` (not after a determiner or
      `de`), `pasado mañana`, `próximo/a` + time noun, and time noun + `que viene`. Expected is the `ir a` + infinitive form for the person
      (the form S27 teaches), or the present of `ir` for `ir a`;
    - a finite `ir` + `a` + a word that is only a finite verb. Expected is the infinitive: `voy a como` gives `comer`.
  - **Past markers.** `CheckTense` also flags a simple future after `ayer`.
  - **Left clean:** the present or future after a future marker, `luego` and `más tarde` (ordinary narration; deliberately not markers),
    `esta mañana`, `por la mañana`, reported clauses, and noun phrases after `a`.
  - **Source fixes.**
    - `ReadingResolver` keeps the verb after the `a` of `ir a`. `voy a como` was read as a conjunction before.
    - `próximo/a` before a time noun is exempt from `word_order` (`AnalysisTables.IsUpcomingTimeNoun`). This removes the old false
      positive on `la próxima semana`.
  - **Caught by the gate.** The first version made `próximo` prenominal everywhere. The fuzz oracle O5 (`RecastLoopFuzzTests`, seed
    `2tirebhSKN_5`) caught `próximos salones` losing its `word_order` error, and the fix was narrowed to time nouns. The generator now
    leaves `mañana` out of its adverb pool, as it already left out `ayer`. A generated "correct" sentence with `mañana` and a preterite
    is genuinely wrong now; no oracle changed.
  - **Results.** 41 new tests; fuzz 57 of 57 at the failing seed, at the defaults and at 10,000 and 8,000 iterations (the orchestrator
    reran the seed and a fresh seed on merged `main`). Goldens, content, the core demo and the host demo are unchanged. Gate: 4,636
    passed, 5 skipped.
  - **Limits:** the checks work on the whole utterance (no clause split, and normalization drops punctuation); `mañana` has no noun
    reading.
- **M51, `ir` + bare infinitive (2026-10-23, Sonnet 5.5; found by the game's S27; experiment X02, merged to `feature/experiment` `e70441c`).**
  - **The check.** `AgreementChecker.CheckIrWithoutA` gives `conj.tense` for a present or imperfect finite `ir` directly before a bare
    infinitive: `voy comer pan` gives expected `voy a comer`, actual `voy comer`. The span covers both words.
  - **Left clean:** `voy a comer`, `quiero comer`, `ir a comer`, `vamos a la tienda`, `voy contento`, and `fui comer` on purpose (fui is also
    ser).
  - **Source fix.** `ReadingResolver` keeps `van` (also a noun) as a verb before an infinitive.
  - **Generator mirrors.** The generator skips `ir` + infinitive (`ir-bare-infinitive`), and the WordOrder mutation skips `próximo` + time
    noun. The second fixes an O4 flake left by M49 (seed `7oEL4lRaLMu2`, "los treinta próximos domingos" is correct Spanish), which is also
    present on `main`. No oracle changed.
  - **Results.** 18 new tests. Fuzz passed at the seed, twice at the defaults and twice at 10,000 and 8,000 iterations; the orchestrator
    reran the seed on the merged branch. Gate: 4,800 passed, 5 skipped. Goldens and both demos are unchanged.
  - **Limits:** adjacency only; the preterite is not checked; no gerunds in the lexicon.
  - **Note for the owner:** `main` still carries the rare O4 flake until this branch, or the one-file generator fix, reaches it.
- **M31, Spanish identity guard (2026-10-26, Haiku 5.5; merged to `feature/language-packs` `f4694ff`).**
  - **Built.** `tests/Vocaterra.Nlp.Golden.Tests/SpanishIdentityTests.cs` with a frozen corpus (`Data/identity/corpus.jsonl`, 6,349
    inputs: 103 homograph sentences, 108 regression sentences of M26b, M29c and M29j, 224 intent examples, 20 CLI examples, 894 G2P words,
    3,000 correct and 2,000 mutated fuzz sentences drawn once at seed `6VTk-wxfywU1`) and a baseline (`baseline.jsonl`, 1.5 MB, 6,524
    lines with the 175 rendered template variants). Each stage hash is the first 16 hex characters of a SHA-256 over canonical JSON. The
    test runs in about 9 s and needs no models. It re-records only with `VOCATERRA_RECORD_IDENTITY=1`, never with the golden variable.
    `scripts/identity-check.sh <base-ref>` checks the gate rules: the bundle hash is unchanged, and nothing under `tests/*/Data/`,
    `tests/golden/` or `content/` changes except pure renames.
  - **It bites.** One changed repair line (`¿Cómo?` to `¿Cómo??`) made 5,504 of 44,618 stage checks differ. A changed dialogue template
    never reaches the guard, because the loader rejects the bundle on its hash first.
  - **Base ref for Phase A.** M52 and every later Phase A branch run `scripts/identity-check.sh f4694ff`, not `main`. M31 itself adds the
    baseline, so it cannot pass against `main`.
  - **Limits.** M26b's 1,102-sentence corpus was never committed, so it is not included. The fuzz draw was a one-off and is not in the
    repository; the committed corpus is its record. `PromptGrammars` and the sample-slot rule are internal to Content, so the test keeps
    frozen copies of both.
  - **Gate.** 5,254 passed, 3 skipped. Two failures were already on `main`: the licence scan (`Vocaterra.Nlp.slnf` is missing; M52
    recreates it) and the game test `SpokenModelTests.AMistakeInASpokenLineIsHeardAsSaidAndTheCoreCorrectsIt` (with the real Vosk model the
    recognizer hears "a dónde está la árbol", one extra leading word).
- **M52, one engine assembly (2026-10-26, Haiku 5.5; merged to `feature/language-packs` `1976508`).**
  - **Built.** `src/Vocaterra.Nlp` holds the 11 former libraries as folders, moved with `git mv`; namespaces and resource names are unchanged.
    `tests/Vocaterra.Nlp.Tests` holds the 11 unit test projects as folders, with their `Data/` moved as pure renames. The 11 smoke tests are
    now one. Fuzzing and Golden stay separate and reference only `Vocaterra.Nlp` (Golden also references Bench). `Vocaterra.Nlp.slnf` is
    recreated, so the licence scan runs again. ADR 0012 is added; ADR 0003 and 0009 carry dated notes.
  - **Layering.** `Architecture/LayeringTests` (NetArchTest.Rules 1.3.2, MIT) encodes the old project graph as namespace rules. It also
    checks that every stage has types, and that a negative fixture is reported. A temporary Core to Pipeline reference made two cases fail.
  - **`VocaterraCore`** is two assignments in `Directory.Build.props`: `StartsWith('Vocaterra.Nlp.')`, then `true` for `Vocaterra.Nlp`
    itself. An MSBuild property value has no `Or`.
  - **Two ONNX Runtimes in one test process (not foreseen).** sherpa-onnx and Microsoft.ML.OnnxRuntime each ship a `libonnxruntime.so` with
    a different API version. In one output folder one overwrote the other. `NativeOnnxRuntimes.cs` (a module initializer) loads sherpa's copy
    by full path and maps the managed package's `onnxruntime` to `ort-managed/`. The csproj copies that file from
    `$(PkgMicrosoft_ML_OnnxRuntime)`, and the Linux x64 build fails if it is missing. The native and model tests share
    `NativeRuntimeCollection` (not parallel). The merged project passed three runs in a row.
  - **Licence scan.** Mono.Cecil, which NetArchTest brings in, names the MIT page by URL; `license-scan.sh` now knows that URL. NetArchTest
    names no licence and has a reviewed MIT override; the orchestrator checked its LICENSE file.
  - **Gate.** 5,258 passed (14 layering cases added, 10 smoke facts removed), 3 skipped. The licence scan passed. The only failure is the
    pre-existing game speech test. `identity-check.sh f4694ff` passed, and the CLI demo output is byte-identical. One game test
    (`ArchitectureRulesTests`) had its path regex widened to accept `Vocaterra.Nlp`.
- **M32, language id and pack registry (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `ba85fe3`).**
  - **Built.** `src/Vocaterra.Nlp/Language/Packs/` holds `ILanguagePack` (text side only: `LanguageId`, `Normalizer`, `Folding`, `Numbers`,
    `PhoneticKey`, `Punctuation`, `G2p?`, `GrammarDefaults`, `Lint`, `RepairFallback`), `SpanishLanguagePack.Instance` and the
    `LanguagePacks` registry (`Default` holds `es`; `Of`, `With`, `TryResolve` by the primary subtag, ASCII case-insensitive). The analysis
    members (morphology, analysis rules, template functions, `LexiconProfile`, `ErrorTagSet`, recast policy) are left to M34 (ADR 0011).
    The statics did not move and no call site is routed yet (M33).
  - **Manifest.** `language` and `supportLanguage` (optional, omitted when null). With `language` the bundle hash is
    `Combine("language", tag, path₁, sha₁, …)`, the tag as written; without it, the hash is today's. `supportLanguage` is not hashed: it
    changes no output, and hashing it would change every stamp and replay record. The loader resolves the pack right after reading the
    manifest (also with `VerifyHashes` off); an unknown or malformed tag is one `ContentValidationException` problem. `ContentLoadOptions.LanguagePacks`
    lets a host pass its own registry; the ContentBuilder `hash` command takes `--language` and `--support-language`.
  - **Layering.** `Language.Packs` has its own rule (Core, Language, Phonology, Response); the `Language` rule excludes it.
  - **For M33.** The pack uses `Response` for `PunctuationFixer`, and `Response` may use `Language`, so a `Response` call site that takes an
    `ILanguagePack` makes a namespace cycle the rules allow: ADR 0011 point 9 says to move the punctuation behind the interface or keep the
    cycle on purpose. The pack repeats Spanish literals (`por`, `favor`, `un`, `una`) also found in `LintCoverage`, `SlotGrammarMatcher` and
    `AnalysisSlotFinder`; M33 removes the duplicates. Two visibility-only edits: `WordClasses.SubjectPronounLemmas` (an ordered list) and
    `LintCoverage.SubjectPronouns` (internal).
  - **Not done.** The replay envelope's optional `language` field (research §4.6) is not added; it is optional and not in M32's scope.
    `TextFolding.Normalize` throws on a lone surrogate (pre-existing, unreachable from the pipeline, which replaces lone surrogates first).
  - **Gate.** 5,366 passed (109 new: pack, registry, manifest, ContentBuilder and four O15/O16 fuzz cases), 5 skipped (models not on this
    machine), 0 failed; licence scan passed. Fuzz at 10,000 grammar iterations green. `identity-check.sh f4694ff` passed, and the CLI demo
    gives `conj.person` and `noun.number` with content hash `aab525f9…`.
- **M33, text-side seams (2026-10-27, Haiku 5.5; merged to `feature/language-packs` `d677412`).**
  - **Routed through the bundle's pack.** The pipeline (normalizer, answer matching, analysis, intent resolution, pronunciation numbers and
    G2P, speech requests, recasts, the recognizer's grammar phrases), the response side (`TemplateEngine`, `SpeechRequests`,
    `RecastBuilder`), the grammar defaults (`SlotGrammarMatcher`'s optional subject and trailing polite words, `AnalysisSlotFinder`), the
    content tools (`TextGrader`, `ContentLinter`, `LintCoverage`, `CefrChecker`, `LintConfusion`, `ContentValidator`) and Bench and CLI.
    Engine signatures such as `AnswerMatcher.Match`, `LinguisticAnalyzer`, `IntentResolver` and `SlotGrammarMatcher.Match` now take an
    `ILanguagePack` (recorded in `API.md`); the game uses none of them and compiles unchanged.
  - **Repair lines.** Reserved pools `repair.ask_again` and `repair.slow_repeat` override `RepairFallback` when a bundle has them (one
    variant drawn with the interaction seed); `content/game-v1` has none, so the text is unchanged. The give-up line is always the pack's.
  - **New pack members.** `ILanguagePack.Recast` (`IRecastPolicy.RestoredSpellings`, `SpanishRecastPolicy`: the accent rules moved out of
    `RecastBuilder`) and `INumberWords.Agree`. ADR 0011 records both.
  - **Kokoro.** `FromStore` takes `lang = "es"`, folded into `ModelHash` only when it is not `es`. The CLI and Bench name each model's
    language and refuse a model whose language differs from the bundle's.
  - **Cycle removed.** `PunctuationFixer` and `NumberAgreement` moved to `Language/Packs`, so the packs use only Core, Language and
    Phonology; `LayeringTests.ThePacksAndTheResponseStageHaveNoCycle`. The duplicated `por`, `favor`, `un`, `una` literals now come from
    the pack.
  - **Guard.** `Architecture/SpanishCallSiteTests` scans `src/Vocaterra.Nlp` and `tools/Vocaterra.Nlp.*` for the Spanish statics and
    fails outside an allowlist: the packs, the statics' own files, the compatibility entry points (`RecastBuilder.Build`,
    `SpeechRequests.Create`, `TemplateEngine.CreateDefault`), and per-file M34 entries (`Lexicon`, `IntentCatalog`, `SlotGrammar`,
    `SlotGrammarMatcher`, `MorphologyGenerator`, `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`, `AnalysisTables`,
    `AgreementChecker`, `ReadingClasses`) that need `IMorphology`, `IAnalysisRules` or `LexiconProfile`. It has a negative test.
  - **Left for M34 and later.** The lexicon builds its indexes with the Spanish fold at load. `RepairStrategy.OfferLine` («¿Quieres decir
    «…»?») is still Spanish text in the engine, not a pack member. `PronunciationScorer`'s predicted-error tags are Spanish-tuned
    (research item 14).
  - **Bug caught in review.** The Spanish pack's instance initializers read word lists declared below `Instance`, before they were set; the
    lists now come first.
  - **Gate.** 5,385 passed (17 new; the 2 Parakeet tests run again now that the model is in the shared folder), 3 skipped, 0 failed;
    licence scan passed; fuzz at 10,000 grammar iterations green; no existing expected value changed. `identity-check.sh f4694ff` passed,
    and the CLI demo is unchanged.
- **M34, analysis-side seams (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `2161c6e`).**
  - **New pack members.** `LexiconProfile`, `ErrorTags` (`ErrorTagSet`: ranks, `IsKnown`, `IsPronunciation`; `V1` is today's Spanish
    set), `CreateMorphology` (`IMorphology`; `MorphologyGenerator` implements it), `CreateAnalysisRules` (`IAnalysisRules`: word-class
    predicates and `CreateAgreementChecker`) and `CreateTemplateFunctions` (`ITemplateFunctions`). `RepairLines.Offer` (`OfferLineFormat`)
    carries «¿Quieres decir «…»?», with the same text; it has no pool override, because a pool line has no placeholder for the choices.
  - **Moved into `Language/Packs`.** `SpanishAnalysisTables` (was `AnalysisTables`), `SpanishAgreementChecker` (was `AgreementChecker`),
    `SpanishAnalysisRules`, `SpanishTemplateFunctions`. `ReadingResolver`, `AccentVariantChoice`, `ReportedReadings`,
    `AccentRestoration`, `ReadingClasses` and `LinguisticAnalyzer` stay shared and take the rules and the pack's folding. `ReadingClasses`,
    `WordClass` and `RegularizedForm` are public so a pack in another assembly can implement the seams (M53).
  - **Lexicon.** Indexes are built with the pack's `ILexiconFolding`; lexicon files are checked against the pack's profile (a Spanish `n`
    gender is still refused with the same message); the lexicon cache key includes the language id. `IntentCatalog.Load` and
    `SlotGrammar.Parse` take an optional folding; without one they use `TextKey.Of` (NFC plus invariant lower case: the code of
    `TextFolding.Normalize`, moved, so the key is the same).
  - **`ContentBundle.Morphology`.** The engine uses the new `bundle.LanguageMorphology` (`IMorphology`) and `bundle.AnalysisRules`.
    `bundle.Morphology` keeps its type for the game and returns the same object for Spanish; for another pack it throws
    `NotSupportedException` on access, not at load.
  - **Schema.** `LexicalFeatures` and `EntryFeatures` gain optional `Case`, `VerbForm`, `Declension`, `Particle`, `Register` (null, left out
    of JSON). `NormalizedText.FinalMark` exists behind `NormalizationOptions.CaptureFinalMark` (default off; the pipeline does not set it).
    The analyzer and `VerdictAssembler` order errors by the pack's ranks through an optional `VerdictInputs.ErrorTags`.
  - **Guard.** The M34 allowlist went from 16 entries to 4: `MorphologyGenerator.cs` (3 patterns; the Spanish `IMorphology`, moves in
    M53) and `Lexicon.cs` (`Lexicon.Seed`, `Lexicon.Empty` and the overloads without a folding default to Spanish; the embedded seed is
    Spanish and 32 test files and the ContentBuilder use them; a bundle's lexicon never does).
  - **Breaking API, none used by the game:** the five new `ILanguagePack` members; `ReadingClasses` predicates and
    `AccentRestoration.SpellingsFor` take the rules; `IntentCatalog.Load` and `SlotGrammar.Parse` no longer convert to `Func<string, T>`;
    deconstructing `LexicalFeatures` or `EntryFeatures` yields more elements. `API.md` lists them.
  - **Left out.** `ReadingResolver`'s local-syntax rules are shared, not pluggable: verb-second order or separable prefixes need the rule
    set extended (M54 onward; ADR 0011). `PredictedErrorMatcher` still reads `ErrorTags.All` (pronunciation stays Spanish-tuned,
    research item 14).
  - **Gate.** 5,419 passed (34 new, in `AnalysisSideSeamTests`: a pack that changes one member and the engine following it), 3 skipped,
    0 failed; no assertion removed, Fuzzing and Golden untouched; fuzz at 10,000 grammar iterations green; `HomographSentencesTests` green;
    `identity-check.sh f4694ff` passed, and the CLI demo is unchanged.
- **M35, per-language test infrastructure and a toy pack (2026-10-27, Haiku 5.5; merged to `feature/language-packs` `dd8eaa7`).**
  - **Harness.** `Oracles/FuzzLanguage` (pack, bundle, free-text generator, sentence generator, mutation kinds) and
    `Oracles/LanguageOracles` (the bodies of O1, O2, O7 and a neutral O14). The Spanish classes keep their names, seeds and iteration counts
    and call those bodies; the Spanish O14 (`PipelineFuzzTests`) is unchanged. The orchestrator compared the old and new failure checks: all
    are kept, and the error-tag check now reads the pack's set (Spanish: `ErrorTagSet.V1`, built from `ErrorTags.All`).
    `FuzzLanguageStreamTests` pins the first case at 400 fixed seeds (SHA-256 over the stream), from the unchanged generators: CsCheck seeds
    only the first iteration of a sample, so later iterations are not reproducible across runs.
  - **Toy pack.** `Languages/Xx/` in the Fuzzing project, public API only: `XxLanguagePack` (every member, non-Spanish), a three-word bundle
    written to a temp directory at test time with `"language": "xx"`, loaded through a registry without Spanish (the default registry
    refuses it). O1, O2, O7 and the neutral O14 pass on it, also at 20,000 general, 10,000 grammar and 3,000 pipeline iterations. No result
    carries a non-toy tag or a Spanish mark, and `bundle.Morphology` throws `NotSupportedException`.
  - **Hidden Spanish dependencies found and fixed**, each with a regression test:
    1. `TextKey.Of` (the fold behind `TextFolding` and the no-folding parsers) threw on a lone surrogate; it now reads it as U+FFFD, as
       `Accents.Strip` does. Valid text is unchanged. `SpanishLanguagePackTests` had pinned the throw; it now pins the new contract.
    2. `SampleSlots` filled templates for the validator with Spanish words (`tener`, `espada`, `yo tengo dos espadas`, …). New
       `ILanguagePack.Samples` (`SampleWords`); the Spanish values are the same. Breaking for any other `ILanguagePack` implementer.
    3. `RecastBuilder.AddNumberEdits` spelled any digit run up to six digits; it now skips values above the pack's `Numbers.MaxValue`, the
       rule `TemplateEngine` already used. Spanish is unaffected (its bound is 999,999).
  - **Goldens.** `GoldenFiles` reads and records another language's goldens under `Data/<lang>/`; Spanish keeps `Data/<name>`. Code only;
    no file under a `Data/` folder was added.
  - **Not done.** O3, O4 and O5 do not run on xx (no grammar realiser or mutation set for it). The xx run cannot prove directly that
    `SpanishLanguagePack` is never initialised; the registry without Spanish, the result checks and `SpanishCallSiteTests` cover it
    (ADR 0011).
  - **Gate.** 5,438 passed (19 new), 3 skipped, 0 failed; fuzz at 10,000 grammar iterations green; `identity-check.sh f4694ff` passed, and
    the CLI demo is unchanged.
- **M53, the Spanish pack project (2026-10-27, Sonnet 5.5; merged to `feature/language-packs` `65de544`). Phase A is complete.**
  - **Moved** (61 renames) into `src/Vocaterra.Nlp.Es` (namespace root `Vocaterra.Nlp.Es`): `SpanishLanguagePack`; `Es.Text`
    (`SpanishRecastPolicy`, `SpanishTemplateFunctions`, `PunctuationFixer`, `NumberAgreement`, the new `SpanishRepairLines`); `Es.Analysis`
    (`SpanishAnalysisRules`, `SpanishAgreementChecker`, `SpanishAnalysisTables`, `WordClasses`); `Es.Normalization` (`SpanishNumbers`,
    `SpanishPhoneticKey`, `Accents`, `TextFolding`, and `TextNormalizer` renamed `SpanishTextNormalizer`); `Es.Morphology`
    (`MorphologyGenerator`, `SpanishOrthography`); `Es.Phonology` (`SpanishG2p` and its stages, `exceptions.json`); the seed-lexicon
    resource. The LogicalNames are unchanged.
  - **Stayed neutral:** `DialectConfig`, `G2pResult`, `PhoneInventory`, `PredictedErrorVariants`, `ErrorTags`/`ErrorTagSet.V1`,
    `LexiconProfile.V1` (the frozen v1 contract every pack may pick), and the espeak phone map (it maps the multilingual xlsr-53 vocabulary
    onto the shared inventory; ADR 0012). The engine keeps a neutral `TextTokenizer`; `SpanishTextNormalizer` adds the number words and
    `al`/`del` on top, with identical output. No `InternalsVisibleTo` from the engine to the pack: `Utf16`, `TextKey`, `Lexicon.Derive`,
    `TextTokenizer` and `InvariantLexiconFolding` became public.
  - **Host-facing (API.md, ADR 0012).** `LanguagePacks.Default` is empty; a manifest with no `language` still means `es`, resolved from
    the host's registry, so a bare `ContentLoader.Load` of a Spanish bundle now throws. `SpanishContent.Load`, `.Options`,
    `LanguagePacks.WithSpanish()` and `CreateTemplateEngine()` register it. `ContentBundle.Morphology` is removed: use
    `bundle.SpanishMorphology()` or `bundle.LanguageMorphology`. The Spanish seed is `SpanishLexicon.Seed` (and `Load`, `FromEntries`,
    `Empty`); `Lexicon.Empty` stays with the neutral folding. The pack-less Spanish overloads (`RecastBuilder.Build`,
    `SpeechRequests.Create`, `TemplateEngine.CreateDefault`, `ResponsePlanner()`, `RepairStrategy.Next`'s short forms) are gone;
    `FakePhonemeRecognizer.ForSpanish` is `ForPhoneInventory`.
  - **Game edits (allowed by the brief, no behaviour change):** a project reference to `Vocaterra.Nlp.Es` in `Game.Core` and `Host`;
    `SpanishContent.Load` at the two load sites (`GameBundle.cs`, `Host/Program.cs`); `.SpanishMorphology()` for `.Morphology` (four
    files); moved names (`SpanishTextNormalizer`, namespaces) in five files and their tests.
  - **Guards.** `LayeringTests`: no engine namespace depends on `Vocaterra.Nlp.Es`, and the engine assembly does not reference it.
    `SpanishCallSiteTests` scans `src/Vocaterra.Nlp` with an empty allowlist (asserted). `XxIndependenceTests` (source and reflection
    scans) keep the toy pack free of Es types, since the Fuzzing project now references Es. `git grep Spanish src/Vocaterra.Nlp` finds only
    comments, doc examples and the sidecar field `LexiconSourceInfo.SpanishEntries` (a JSON field name; renaming it changes the format).
  - **Test projects referencing Es:** Es.Tests; Nlp.Tests, Fuzzing and Golden (they run `content/game-v1`); the three tools. Three
    resolver tests stay in Nlp.Tests because they use engine internals, and link the Es tests' `TestTokens.cs`.
  - **Bug found and fixed.** `UtterancePipeline.RepairLinesFor` dropped the pack's `RepairLines.Offer`, so every pack got the engine's
    default offer wording (hidden while that default was Spanish). Regression test
    `PipelineTests.ThePacksOwnOfferWordingReachesTheOfferedChoices`.
  - **Not done.** `DialectConfig.LatinAmericanNeutral` is still `SpeechRequests.Create`'s default dialect (a per-pack default is a later
    decision). A pack with phones outside `PhoneInventory` needs its own espeak map (ADR 0012).
  - **Gate.** 5,445 passed (Nlp 2,452, Es 1,129, Fuzzing 76, Golden 17, Game.Core 863, Game 872, Host 36; 7 new, none lost), 3 skipped,
    0 failed; licence scan passed (30 packages); fuzz at 10,000 grammar iterations green; `identity-check.sh f4694ff` passed (the G2P golden
    and `exceptions.json` are pure renames); the CLI demo and the host demo are unchanged.
- **Test performance, the core's half (2026-10-29, Opus 5.5, `feature/test-performance`; plan `docs/TEST_PERFORMANCE_PLAN.md`, T2, T3, T4).**
  - **One bundle load per test process.** `GameBundleLocator.Bundle` (tests/Shared) loads `content/game-v1` once with every hash verified;
    Nlp.Tests' two loads and Golden.Tests' three use it. The CLI tests still load through `--content` (the CLI takes no `LexiconCache`).
  - **The model gate verifies each model once.** `ModelGate.IsAvailable` keeps its verdict per id; the pipeline tests' copy of the gate is
    gone. Measured: the gates hash Vosk (58 MB) and Parakeet (641 MB) at about 1 GB/s, so the saving is seconds.
  - **The serial collection.** Its 33 plain facts were not moved out: they take 0.2 s in all. Its cost is the model tests themselves (23.7 s with
    models, at the end of Nlp.Tests), which is why Nlp.Tests takes 59 s with models and 36 s without. T3b (owner, 2026-10-29) lets the
    collection run beside the assembly's other tests (its own tests still one at a time): Nlp.Tests alone with models went from 53.5 s
    to 34.4 s in three green runs. It reverses M52's choice that these tests never run beside another test.
  - **Fast tier:** `scripts/test.sh fast` leaves out the fuzz suite (an assembly trait), the 17 model-gated facts, `CliTests`, `ProgramTests`,
    `LintTests`, `LintCommandTests`, `SpanishIdentityTests` and three long single tests; Nlp.Tests runs 2,313 of 2,455 tests in 11 s. The
    gate, CI and `scripts/test.sh full` run the whole suite at its full depth; no oracle changed.
  - **O15 is the fuzz suite's longest test** (52 to 55 s of 63 s). It already shares the lexicon and hard-links the speech; a cheaper
    equivalent with the same oracle needs `ContentLoader` to read an in-memory file source, a core API change (T6). The owner declined a shallower local
    fuzz pass (T5): the suite runs at its one full depth.

- **M54, the identity guard masks the bundle hash (2026-10-29, Opus 5.5; on `feature/content`, for the game's C series).**
  - **Found:** C01 added a `$schema` line to each of the game's files in `content/game-v1/game/`. The identity guard then failed on 12,698 of
    44,618 stage checks, every `grade` and every `pipeline`. Their verdicts carry the bundle's content hash in their version stamp, and
    `content/game-v1` had not changed since the guard was recorded at M31, so the coupling had never shown.
  - **Proof that no behaviour changed:** with the new hash replaced by the old one in each stage's JSON before hashing, the unchanged baseline
    passed (2 of 2 tests). The guard now masks `Bundle.ContentHash` (`<bundle-hash>`), and the baseline was recorded again in its own commit.
    The masked JSON of the old and the new bundle is the same text, so the masked baseline holds for both.
  - **The rule after M54:** an edit of `content/game-v1` that changes no Spanish behaviour moves no identity hash. A hash that moves is a real
    change, and the old Phase A rule stands (re-record only for a deliberate Spanish change, in its own commit). `scripts/identity-check.sh` still
    checks that the bundle hash is unchanged, which only Phase A asked for; it is not part of the gate.

- **M55, the manifest tests stop assuming game-v1 has no support language (2026-10-29, Opus 5.5; on `feature/content`, for the game's C03).**
  - `ManifestLanguageTests` (three tests) and `ManifestTests.TheManifestSerializesOneFilePerLineAndRoundTrips` read `TestSupport.Bundle.Manifest`
    as a manifest without `language` and `supportLanguage`. The game's C03 records `supportLanguage: en` in it (the core's M32 field, recorded
    and never read by the core).
  - The tests now take the example `with { SupportLanguage = null }`, and `WithoutALanguageTheHashIsExactlyTheOldCombineOfPathsAndHashes` checks
    that the hash is still the plain combination of paths and hashes with the support language present. No behaviour of the core changed.

- **M60, the lint analyses each example once per winning grammar (2026-10-31, Opus 5.5; merged to `feature/village` `95bd7bf`, for the game's PR #20).**
  - The cause: the confusion check asks the resolver about every example in a synthetic prompt of two intents, for each rival intent, and the
    lint's cache was keyed by the prompt, so each text was read, matched and completed about 33 times. Only the grammar match depends on the
    prompt; the readings depend on the text, and the completion (names, errors, the reading resolver, the checks) on the text and the winning match.
  - The analyzer's halves (`Read`, `Match`, `BestMatch`, `Complete`, the type `TextReadings`) are internal, and the public `Analyze` is exactly the
    three steps, so the pipeline is unchanged by construction. `LintEvaluationTests` (three tests) compares the lint's analyses with fresh ones:
    one text whose winning grammar changes between prompts, a word bound as a name in one prompt only, and every example in every prompt that
    allows its intent; a cache keyed by the text alone, one handing over another grammar's match, and one without a copy of the tokens each fail them.
  - Timings (the content tool, Debug, `lint --level a2`): 680 to 240 ms on the bundle before phase A, 1610 to 398 ms now; the process 2.2 s to 1.0 s.
    What remains is grammar matching, examples times grammars, about 185 ms in all; cutting it means changing `Language/SlotGrammar` and is the
    next lever if phase B grows the bundle a lot.
  - The timing test's clock started before it read `TestSupport.Bundle` (a lazy load of the 69 MB bundle); only the type initialiser of `LintTests`
    kept the load out of it. It reads the bundle first now; its budget stays 5 s. Tests: 6033 passed, 3 skipped (6030 before).

- **M61, the core's tests follow the game bundle's A2 ceiling (2026-10-31, Opus 5.5; merged to `feature/village` `808e0f0`, with the game's V08).**
  - R02 put the core's tests on `content/game-v1`, and two of them asserted a clean A1 for it with fail-on-level. The game's gate moved to an A2
    ceiling with a per-lesson level (D-game-22 (b), V08), so phase B's A2 lessons would have failed the core suite for content that is
    correct by the owner's rule. Only the level changed: A2 with fail-on-level must be clean, and A1 may report only `cefr.*` warnings. Tests on
    the core's own fixtures are unchanged.

- **M56, the agreement checks that were silent where a learner errs (2026-10-31, Opus 5.5 xhigh; merged to `feature/village` `d87e823`,
  before the game's V14).**
  - (a) `ser` where a place takes `estar` (`ser_estar`): `ser` with `lejos` or `cerca`, past up to two adverbs (`es lejos`, `es muy cerca`,
    `eran lejos` → `estaban`); `dónde` with a third-person `ser` and a definite noun phrase, agreed with the phrase (`¿dónde es la herrería?`
    → `está`). **An event's place keeps `ser`:** the core tells an event only by its noun, so the Spanish pack holds a closed list of event
    nouns (`fiesta`, `reunión`, `clase`, `boda`, `cena`…): `la fiesta es en la plaza`, `¿dónde es la fiesta?` and `¿dónde es?` alone stay
    clean. `de dónde`, `es lejano`, `cerca de las 3` and `fue lejos` stay clean too.
  - (b) A subject after its verb (`conj.person`): a third-person verb agrees with the noun phrase after it after `estar` (`¿dónde están la
    herrería?` → `está`), after `gustar`-type verbs behind a clitic (`me gusta las manzanas` → `gustan`, `me gustan el pan` → `gusta`),
    `quedar` after `dónde`, and `costar` or `valer` after `cuánto` (`¿cuánto cuestan el pan?` → `cuesta`). `cuestan dos monedas`, `hay dos
    panes` and the others stay clean. The new rules are in `SpanishAgreementChecker.Postverbal.cs`.
  - A false positive fixed on the way: a pronoun after a preposition is no subject (`a ella le gustan las manzanas` gave `conj.person`).
  - A reading change in the engine, through a rule the pack supplies (`IsVerbSeekingQuestionWord`, no Spanish in the engine): the word after
    `dónde` or `cómo` is the verb when it can be one, so `¿dónde son las casas?` gives `están`.
  - Tests: 82 in `PostverbalSubjectTests`; on the old code the 33 error cases gave nothing and the 4 preposition cases gave a false
    `conj.person`. The fuzz generator follows the new rules (it no longer counts `dónde están la herrería` as correct) and the `conj.person`
    mutation now covers a subject after the verb: widened, nothing narrowed, no new bug. `OldSentenceDigest` moved with the generator.
  - Baselines: the end-to-end goldens did not move; the Spanish identity baseline moved in 15 entries (7 wrong-Spanish fuzz sentences now
    `conj.person`, 8 a word's reading: `queda` after `dónde` and `hace` after `cómo` are now the verbs).
  - One game test pinned the old gap (`FarmLessonTests`, "a core gap, M56"): `me gusta las manzanas` at `c2-apples` is now the core's
    person slip (the step done with the slip, the core's recast only, no double feedback), and `me gusta la manzana` keeps the lesson's own
    person retry. Changed by a follow-up on the branch, the one game file, by the orchestrator's leave.
  - Tests: 6194 passed and 3 skipped before; 6276 passed and 3 skipped after. The orchestrator's CLI spot-check matched the report; one
    note: `cuestan dos monedas` at `baker-open` gives `vocab.wrong` on `cuestan` (no `conj.person`, as specified); whether that tag is
    older than M56 was not checked.
  - The game may now restore these `content/PHRASES.md` error examples (V14, deliverable 4): `esta-lejos` (`es lejos`; its intent stays
    `unknown` unless `ask_far` also takes `ser`), `donde-esta-la-herreria`, `me-gusta-el-pan`, `me-gustan-las-manzanas`, and `cuánto
    cuestan el pan` for `cuanto-cuesta` (`cuánto costa` waits for M58).
  - Left: `son` with no pronoun or `dónde` before it is still read as the noun (`son cerca`); `ser en` with a place is not checked; the first
    and second persons and the subjunctive after the verb are not checked.

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
   `docs/nlp/adr/0008-lexicon-sources-and-frequency-filter.md`.

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
| `Slg.Core` contracts: verdict, prompt spec, error tags, replay envelope | `SPECIFICATION.md`, the game design (moved to Vocaterra `docs/game/SPECIFICATION.md` in M19c) |
| Audio, Asr, Phonology, Pronunciation, Nlp, Evaluation | Character definitions (blacksmith, innkeeper, captain) and their dialogue pools |
| Decision: learner model (BKT), scheduling (FSRS), feedback policy, and the conversation state-machine *engine* (renamed from Npc to a generic conversation agent) | Items, spells, quests, prices and rewards: the effect tables and game-event names |
| Response: templates, recast, speech synthesis, and the effect *resolver* with opaque outcome names | The game's content bundles, built with the core's ContentBuilder |
| Content: bundle format, loader, validator, provenance, lexicon import, CEFR lists | Engine integration (Unity, Godot or MonoGame), rendering, device audio capture |
| Pipeline with a game-agnostic `ConversationContext` | Game-side glue mapping outcomes to prices, spell power and quest state |
| Tools (Cli, Bench, ContentBuilder), fuzz and golden suites, a sample bundle | |

The boundary is defined on the contracts (verdict, prompt spec, content formats), which stay stable
whatever the research report in `docs/nlp/research/SCALING_APPROACHES.md` changes inside the core.

### 8.2 Milestones

- **M19a, boundary and API (Haiku, M).** ADR 0009 "core and game boundary". Rename the solution to
  `Vocaterra.Nlp.sln`, every project, namespace, assembly and future package id from `Slg.*` to
  `Vocaterra.Nlp.*` (folders too; `git mv` so history follows), the test and tool projects alike, and
  every doc reference; the `Slg.Cli` demo becomes `Vocaterra.Nlp.Cli`. Rename `GameContext` to
  `ConversationContext` (`NpcId` → `AgentId`, `NpcState` → `AgentState`, `GameStateHash` →
  `HostStateHash`); `NpcStateMachine`/`NpcDefinition`/`NpcState` to `AgentStateMachine`/`AgentDefinition`/`AgentState` (namespace `Decision.ConversationAgents`), content
  folder `npcs/` → `agents/`; `EffectTable` constants removed, outcome kinds become opaque strings
  validated for uniqueness only (the sample content defines `trade_price` and `spell_power`). Game
  words scrubbed from identifiers and doc comments (Spanish example words may stay). `docs/nlp/API.md`
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

`docs/nlp/research/SCALING_APPROACHES.md` (Opus 5.5, 120 sources, two experiments run) concludes that
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

### 9.1 Owner decisions raised by the report (with its recommendations; tracked in `docs/nlp/DECISIONS.md`)
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
`docs/nlp/research/2026-10-21-content-authoring.md` (Opus, 2026-10-21). Measured: one new intent costs 70–100
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
  `contentHash`; lint finds no new issue; a layered bundle packs, verifies and hashes; `docs/nlp/API.md` shows
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

## 11. German and a multi-language core (roadmap; owner D14, 2026-10-22; Phase A planned, D14 amended 2026-10-26)

The design and the evidence are in `docs/nlp/research/2026-10-22-multilingual.md`. **Amended 2026-10-26** (D14,
`docs/nlp/research/2026-10-26-engine-and-language-packs.md`): Phase A runs. The order is M31, then M52 (the core libraries merged into
one neutral `Vocaterra.Nlp`), then M32 to M35 on the merged layout (interfaces in `src/Vocaterra.Nlp/Language/Packs/`), then M53 (the
Spanish pack extracted into `Vocaterra.Nlp.Es`). M36 onward stays recorded only until the owner says otherwise; German, when it runs,
goes into `src/Vocaterra.Nlp.De`. `content/v1` no longer exists (R02): read "the `content/v1` hash" below as `content/game-v1`. The guard for
the whole of Phase A is `SpanishIdentityTests` (M31). It freezes an input corpus and the hash of every Spanish stage's output. Phase A
merges also need an unchanged `content/v1` hash, no change under `tests/*/Data`, `golden/` or `content/`, and green game tests.
The research lists seven places that would break silently when a second language loads: `PunctuationFixer`'s ¿ ¡,
`RepairStrategy`'s Spanish lines, accent folding of ü ä ö, lowercased recasts, slot grammars' implicit missing subject and `por favor`,
`ErrorTags.All` read by the game, and `CanonicalJson` writing nulls.

| ID | Milestone | Depends on | Tier | Status |
| --- | --- | --- | --- | --- |
| M31 | Spanish identity guard (`SpanishIdentityTests`) | M29j | H | done (merged to `feature/language-packs` `f4694ff`) |
| M32 | Language id in the manifest (absent = `es`, folded into the hash only when present), pack registry, `ContentBundle.Language`, ADR 0011 | M31, M52 | S | done (merged to `feature/language-packs` `ba85fe3`, 2026-10-27) |
| M33 | Text-side seams: normalizer, numbers, phonetic key, punctuation, repair lines, grammar defaults, lint, G2P call sites, Kokoro `lang` | M32 | H | done (merged to `feature/language-packs` `d677412`, 2026-10-27) |
| M34 | Analysis-side seams and schema: `IMorphology`, `IAnalysisRules`, template functions, optional `LexicalFeatures` fields (Case, VerbForm, Declension, Particle, Register), per-pack tag sets (`ErrorTags.All` frozen as Spanish) | M33 | S | done (merged to `feature/language-packs` `2161c6e`, 2026-10-27) |
| M35 | Per-language test harness, and a toy pack that proves no hidden Spanish dependency remains | M34 | H | done (merged to `feature/language-packs` `dd8eaa7`, 2026-10-27) |
| M36 | German lexicon import (kaikki German, `de_50k`; D4 applies as for `es_50k`) | M35 | S | proposed |
| M37 | German text layer: umlaut and ß folding (ae, oe, ue), contractions, numbers, phonetic key, noun capitals | M36 | S | proposed |
| M38 | German morphology | M36 | S | proposed |
| M39 | German A1 analyzer: chunk-based case, gender and number; V2 and verb-bracket order; separable and modal verbs. A false-positive gate runs on a corpus of correct sentences | M37, M38 | S | proposed |
| M40 | German grammar DSL options and template filters | M38 | S | proposed |
| M41 | German sample bundle `content/de-v1` (with the LanguageTool step of M30) | M39, M40, M30 | H | proposed |
| M42 | German fuzz | M41 | S | proposed |
| M43 | German speech: Vosk `vosk-model-small-de-0.15` (Apache-2.0), a Parakeet v3 test, Piper `thorsten` through sherpa-onnx (private builds, D2, D14) | M35 | H | proposed |
| M44 | Perfekt and its auxiliary, past tense, subordinate clauses, two-way prepositions | M42 | S | proposed |
| M45 | Compounds and an A2 lexicon | M44 | S | proposed |
| M46 | German G2P and pronunciation (only if D13 and the GPL-free pin allow) | M45 | S | proposed, conditional |
| M47 | German tagger spike (only if M39 or M44 fail their false-positive gates; D10) | M44 | S | proposed, conditional |
| M48 | du/Sie register check (per agent, Sie for strangers, D14) | M41 | H | proposed |

Effort, estimated: about 13,000 new or moved source lines and 15,000 test lines. Phase A (M31 to M35) is 3 to 4 days, Phase B (M36 to
M43, M48) 2 to 3 weeks, and Phase C (M44 to M47) about 2 weeks. The riskiest items:
1. M39's case checks (false positives);
2. M34 keeping Spanish byte-identical;
3. German speech output (D2);
4. lexicon size against the 300 ms load gate;
5. German content trust.
