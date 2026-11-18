# Implementation Plan — Voice Recognition & Feedback System

> **Layout note (R01, 2026-10-23):** the repository now has one root layout. `nlp/src`, `nlp/tests`, `nlp/tools` and `game/src`, `game/tests`, `game/tools` are `src/`, `tests/` and `tools/`; `nlp/docs` is `docs/nlp/`, `game/docs` is `docs/game/`; `nlp/content/v1` is `content/v1`, `game/content/*` is `content/`, `nlp/golden` is `tests/golden`, `nlp/models` is `models/`, `game/assets` is `assets/`, `nlp/scripts` is `scripts/`, `game/playtest` is `scripts/playtest`; `LICENSE` and `NOTICE` are at the root and cover the whole repository (Apache-2.0). The core is the `Vocaterra.Nlp.*` projects (since M52 and M53 one engine, `src/Vocaterra.Nlp`, and the Spanish pack `src/Vocaterra.Nlp.Es`), the game is `Vocaterra.{Game.Core,Game,Host,SpeechRender}` (`Simulation` and `MonoGame` until R16); `Vocaterra.Nlp.slnf` builds the core alone. Paths and links below were updated to match; the narrative is otherwise unchanged. Since then: `content/v1` was deleted (R02; the one bundle is `content/game-v1`) and CI runs from `.github/workflows/ci.yml` (2026-10-31).

Derived from `high-level-docs.md` (Oct 18, 2026) and `specification.md`. This file is the
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

> **As planned in October 2026.** The names below (`SpanishLearningGame`, `Slg.*`, `content/v1`, `golden/`) are the original plan's.
> Today the engine is one project, `src/Vocaterra.Nlp`, with these stages as folders (M52), plus the Spanish pack `src/Vocaterra.Nlp.Es`
> (M53); the current layout and dependencies are in [architecture.md](architecture.md) and ADR 0012. Section 4's `Slg.Core` contracts are
> likewise the original design; the public types today are in [api.md](api.md).

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
| M16a | Combinatorial fuzz testing, stage 1: project, CsCheck, generators, harness, language oracles (see `docs/nlp/fuzzing-plan.md`) | M13b | S | done |
| M16b | Combinatorial fuzz testing, stage 2: signal, decision, content and pipeline oracles | M16a | S | done |
| M17a | Vocabulary expansion: Wiktionary (kaikki.org) lexicon import filtered by a frequency list, provenance, coverage gates (see §7) | M16b | M | done |
| M17b | Intent expansion, game part: planned and built in the Vocaterra repository (not tracked here since 2026-10-21) | M17b-core | — | moved (Vocaterra) |
| M17b-core | The core part of M17b: `[WORD:slot]` grammar token, generic sample intents (confirm, deny, farewell, thank, introduce_self), blacksmith confirming state, fuzz support | M19a | M | done |
| M18 | Part-of-speech disambiguation in the analyzer (triggered: `ayer compré un libro` yields a false `conj.tense` on main after M17a) | M17a | M | done |
| M19a | Standalone core, step 1: boundary ADR, rename `Slg.*` → `Vocaterra.Nlp.*` (solution, projects, namespaces, packages), game-agnostic context and agent naming, opaque outcome kinds, `docs/nlp/api.md`, README reframed as a library (see §8) | M18 | M | done |
| M19b | Standalone core, step 2: NuGet packaging of `Slg.*`, content bundle packaging, `samples/HostConsole` consuming the packages (see §8) | M19a | S | done |
| M19c | Standalone core, step 3: new game repository scaffold with the game spec, game content bundle and a host project on the packages (see §8) | M19b | S | done |
| M17c | Evaluator polish from the CLI-examples review: near-miss recast, `vocab.wrong` expected value, plural/accent corrections, leading-adverb grammars (see §5b) | M16b | S | done |
| M19d | Post-split core follow-ups from the game host author: repeat-count helper and next-turn context, prompt `text` in `PromptSpec`, ContentBuilder as a dotnet tool `vocaterra-content` | M19c, M17b-core | H | done |
| M19e | Apache-2.0 licence (D3), submodule consumption route (D5), core/game documentation isolation (D12) | M19d | H | done |
| M19f | Recast bug from the fuzz suite (O5, seed `1diyRPH9lxJ1`): a `conj.tense` error after a leading `ayer` on an irregular verb (`ayer tengo sufrir`) is detected but the recast repeats the wrong form instead of `tuve` | M19e | H | done |
| M26 | Game requests, batch 1 (CR-002…CR-013, triaged 2026-10-21 into M26a–f below; all rows `done` in the game's `docs/nlp/CORE_REQUESTS.md`) | M19f | — | done |
| M26a | CR-012: repeat counts mean "before this utterance" on `ConversationContext`; the pipeline applies `RepeatCounts.Next` itself before `FeedbackPolicy.Plan` and `RepairFor`; docs and sample host follow | M19f | H | done |
| M26b | CR-013: accent-insensitive lookup also offers the accented readings of a form the lexicon has (`esta`/`está`, `si`/`sí`, `mas`/`más`, `el`/`él`, `tu`/`tú`…) and the reading resolver chooses | M19f | S | done |
| M26c | CR-002 analysis on the result, CR-004 answer match on the result, CR-007 host-owned bundle files (`ContentBundle.TryReadHostFile`), CR-008 threading and load-cost contract in `docs/nlp/api.md` | M26a | H | done |
| M26d | CR-003 host slot values (`SlotMap` on the context, template access, replay record and `HostStateHash`), CR-005 host-named skills in the learner update | M26c | S | done (results below, "M26d results"; `ConversationContext.HostSlots` and `ExtraSkills`) |
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
| M30 | LanguageTool content grammar check (D10's dev-time half, owner 2026-10-22; design in `docs/research/2026-10-22-languagetool.md`): `vocaterra-content grammar`, a plain HTTP client (no LanguageTool code linked; licence scan unchanged). (1) It extracts the bundle's Spanish strings, or reads a host's `--strings` JSONL, and renders slot templates with the core's own template engine from sampled fillings. (2) It capitalises learner-side strings before the call, so `UPPERCASE_SENTENCE_START` is not hit. (3) Output matches `lint`, with a waiver file that gives a reason per accepted match. (4) Learner-error examples act as a second oracle (flagged and tagged, tagged but silent, untagged but flagged). (5) A committed result cache keyed by sha256 of LanguageTool version, settings and text; `--offline` reads only the cache. (6) `scripts/fetch-tools.sh` and `tools/manifest.json` (Temurin JRE and LanguageTool pinned by hash; no n-grams). (7) `scripts/languagetool.sh` starts the server in a capped systemd scope (2 GB, 2 cores), not through `limited.sh`. (8) Tests use a fake HTTP server, so `dotnet test` needs no Java. (9) A cache and waivers for `content/v1`, which closes M27a2's three untagged examples. Replaces the LanguageTool half of M22. Waiting: game S23 | M29j | H | done (merged to `feature/prototype` 2026-10-22, `0d04a33`) |
| M49 | Game request (prototype S27, D-game-19): tense checks for the near future. (1) A future-time marker (`mañana`, `la próxima semana`, `luego`…) with a past form gives `conj.tense`, expected the `ir a` + infinitive or future form. (2) A conjugated verb after `ir a` (`voy a como`) gives `conj.tense`, expected the infinitive, unless the core's design finds a better existing tag. Mirror today's past-marker rule in `AgreementChecker.CheckTense`. Regression tests in the analysis module; no fuzz oracle weakened; goldens unchanged unless a reading changes; core demo unchanged. Waiting: game S27 | M29j | S | done (merged to `main` 2026-10-23, `dc53cbb`) |
| M50 | Core folder cleanup (owner, 2026-10-22: "the nlp folder needs some cleanup"). `content/v1` stays (D7: the core's sample, tests, goldens, fuzz and demo depend on it). Retire what the two-repository era left behind: `scripts/pack.sh` and `docs/nlp/PACKAGING.md` (no pack step or feed since D5 was superseded); README, tools.md and CONTRIBUTING passages about packages, feeds, submodules or the separate repository; `high-level-docs.md` moved to `docs/archive/nlp/history/` (it is the original design note). Refresh `tools.md`, or fold it into the licence-scan docs. Decide whether `samples/HostConsole` and `scripts/sample-host.sh` still earn their keep (the game is the real host), and say why. Fix stale links. No behaviour change: the build, tests, goldens, licence scan and core demo are unchanged | — | H | done (merged to `main` 2026-10-23, `3bc525b`) |
| M51 | Game finding (S27): `ir` + a bare infinitive with no `a` (`voy comer pan`) passes clean (state_action, no error). Flag it, expected `ir a` + infinitive (`voy a comer`), under `conj.tense` or a better-fitting tag; regression tests; no fuzz oracle weakened (check the generator does not produce `ir` + infinitive as a correct sentence) | M49 | S | done (merged to `feature/experiment` 2026-10-23, `e70441c`; experiment X02) |
| M52 | Merge the 11 core libraries into one language-neutral `src/Vocaterra.Nlp` (owner, 2026-10-26; D14 amendment). Each old project becomes a folder and the namespaces do not change. The 11 unit test projects become `tests/Vocaterra.Nlp.Tests`; Fuzzing and Golden stay separate. `LayeringTests` (NetArchTest.Rules) enforces today's project graph; `VocaterraCore` in `Directory.Build.props` matches the new name; `Vocaterra.Nlp.slnf` is recreated (missing today, so the licence scan is broken); ADR 0012. No behaviour change: the identity guard is green, the `content/game-v1` hash and the goldens are unchanged. Spec: `docs/research/2026-10-26-engine-and-language-packs.md` §5.1 | M31 | H | done (merged to `feature/language-packs` `1976508`) |
| M53 | Extract the Spanish pack into `src/Vocaterra.Nlp.Es` (+ `tests/Vocaterra.Nlp.Es.Tests`), namespace `Vocaterra.Nlp.Es.*`. Hosts register packs through `ContentLoadOptions.LanguagePacks` (the engine cannot reference a pack); `LayeringTests` adds the rule that the engine does not depend on Es; the toy pack runs against the engine alone. The identity guard stays byte-identical. Spec: same document, §5.2 | M35 | S | done (merged to `feature/language-packs` `65de544`, 2026-10-27) |
| M54 | Game finding (the C series, `docs/game/content-plan.md` C01): the Spanish identity guard pinned the bytes of `content/game-v1`. The verdicts stamp the bundle's content hash, which the `grade` and `pipeline` stages hashed, so any edit of the bundle, even one that changes no Spanish behaviour (a game file's `$schema`), failed it, against the rule that no core test pins the hash. The guard masks the hash before hashing, and the baseline is recorded again once | M31 | S | done (on `feature/content`, 2026-10-29) |
| M55 | Game finding (the C series, C03): four manifest tests used `content/game-v1`'s manifest as the example of one without language fields. game-v1 now names its support language (`supportLanguage: en`, the default locale of the game's locale bundle), so the tests strip that optional field from the example themselves, and one asserts that the support language stays out of the hash | M32 | S | done (on `feature/content`, 2026-10-29) |
| M56 | Game finding (the V series, V10 "El pueblo", 2026-10-30): three agreement checks are silent where a learner errs. (a) `ser` with a location adverb or a location question: `es lejos`, `es cerca`, `¿dónde es la herrería?` give no `ser_estar` (expected `está`); (b) a plural verb before a singular subject: `¿dónde están la herrería?` gives no `conj.person` (expected `está`); V11 "Números y pan" (2026-10-31) found the same with `costar`: `¿cuánto cuestan el pan?` and `cuánto cuesta los panes` give no `conj.person`. V13 "¿Cómo es?" (2026-10-31) found the same with `gustar`, whose subject follows it: `me gusta las manzanas` and `me gustan el pan` give no `conj.person` (`me gustan las manzanas` and `me gusta el pan` are clean). Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `gender.agree` examples meanwhile (`content/PHRASES.md`, `esta-lejos`, `donde-esta-la-herreria`) | | S | done 2026-10-31 (`d87e823`, on `feature/village`; findings in §5b) |
| M57 | Game finding (V10): (a) the fuzz suite's Spanish sentence generator knows only `mañana` as a future time marker, so a bundle grammar with `la semana que viene` or `el próximo …` makes it produce sentences it counts as correct which the core rightly flags `conj.tense`; teach the generator every marker `docs/nlp/api.md` "Tense errors" lists; (b) `pasado mañana` after a plural noun is read as an adjective of the noun (`compro panes pasado mañana` gives `noun.number`): read it as the time phrase; (c) `FuzzLanguageStreamTests`' two stream digests depend on `content/game-v1` and were re-recorded at V10, generators unchanged: pin them to a fixed fixture or document them as content-moved goldens | | S | done 2026-10-31 (`888f914`, on `feature/village`; findings in §5b) |
| M58 | Game finding (the V series, V11 "Números y pan", 2026-10-31): two learner slips are misread. (a) `cuánto costa` (the stem change missed) is read as the noun `costa`: it gives `gender.agree` (expected `cuánta`) and no intent, where `conj.irregular` expected `cuesta` is the slip (`cuánto costan los panes` is graded `conj.irregular` correctly); (b) `hay pans` gives `vocab.unknown` and no intent, where `noun.number` expected `panes` would let the intent resolve. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `cuánto costan los panes` and `hay dos pan` as the examples meanwhile (`content/PHRASES.md`, `cuanto-cuesta`, `hay-pan`). (c) Added by the owner's decision (2026-10-31, "go with option 1"): the fuzz suite's `word_order` mutation guard reads the pattern's roles, not the words' readings, so it moves an adjective after a word that can itself be an adjective and expects a `word_order` the core rightly does not give (M57's gate: CsCheck seed `ff2aNwHuJBk8`, about one full fuzz run in eight). The guard reads the words' readings; this narrows O4 and O5 for exactly that case, recorded in the fuzz README's narrowed oracles | | S | done 2026-11-01 (`78ce805`, on `feature/village`; findings in §5b; one older `conj.tense` fuzz finding left for the owner) |
| M59 | Game finding (the V series, V12 "Gente del pueblo", 2026-10-31): person and tense checks the third person needs. (a) `conj.person` looks only after a subject pronoun: a name or noun subject before the verb is not checked (`Domingo trabajo en la herrería`, `Domingo voy a hacer un hacha` give nothing; `él trabajo …` gives `conj.person`); V13 found the same with `ser` and a noun subject: `las manzanas es roja` and `la manzana son roja` give nothing; (b) a reflexive clitic is not matched to its verb's person (`cómo te llama`, `cómo se llamas` give nothing); (c) `ir a` followed by a finite present is not flagged (`va a hace un hacha` gives nothing, where `va a hizo` gives `conj.tense`); (d) a literal word in a slot grammar binds its first reading, so `hace` in `qué hace` is read as the preposition of `hace dos días` and the verb is hidden from the host's person check. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet puts a pronoun before the verb in its examples and the lesson's person check catches the rest meanwhile (`content/PHRASES.md`, `trabaja-en-la-herreria`, `va-a-hacer-un-hacha`, `como-te-llamas`) | M56 | S | done 2026-10-31 (`12ea9d7`, on `feature/village`; findings in §5b; a lone name is never checked) |
| M61 | Game finding (the V series, V08, 2026-10-31): the core's tests that pinned the game's bundle to a clean A1 follow the bundle's ceiling, A2 (D-game-22 (b), village-plan 3.9), so phase B's first A2 word does not fail the core suite: `LintTests.TheBundleLintsCleanAtBothLevels` becomes `TheBundleLintsCleanAtItsCeilingAndA1IsAReport` (A2 with fail-on-level: 0 errors, 0 warnings; A1: 0 errors and only `cefr.*` warnings), and `LintCommandTests.LevelAddsCefrFindingsAndFailOnLevelMakesThemFail` moves the bundle's line to A2; two comments say the gate is A2. Tests on their own fixtures are unchanged; no other assertion changed | M27a | S | done (merged to `feature/village` `808e0f0`, 2026-10-31) |
| M60 | Game finding (the V series, CI on PR #20, 2026-10-31): `LintTests.TheBundleLintTakesUnderFiveSeconds` took about 6 s on the GitHub runner. Phase A grew `content/game-v1` to 47 prompts and 385 examples, and the confusion check analysed each example once per rival intent (about 33 times; 12 139 analyses, 1.4 of the lint's 1.6 s). `LinguisticAnalyzer.Analyze` becomes exactly `Read` (the text alone), `BestMatch` (the only step that reads grammars) and `Complete`, all internal; `LintContext` reads each text once, matches each grammar once and completes once per winning grammar; the expectations check reuses those analyses (`TextGrader`'s internal constructor). The lint's output is byte-identical (30 outputs on the bundle before and after phase A); the lint takes 398 ms instead of 1610 (Debug). The timing test reads the bundle before its clock starts; its 5 s budget is unchanged | M27a | S | done (merged to `feature/village` `95bd7bf`, 2026-10-31) |
| M62 | Game finding (the V series, V13 "¿Cómo es?", 2026-10-31): two adjective checks are silent. (a) A predicate adjective is not agreed with the subject across `ser`: `la manzana es rojo` (expected `roja`) and `el pan es grandes` (expected `grande`) give no `gender.agree` or `noun.number`, where the attributive `quiero tres manzanas rojos` gives `gender.agree` correctly; (b) an adjective before its noun is not understood: `quiero tres rojas manzanas` and `quiero una roja manzana` resolve no intent, and with a grammar that matches them give no `word_order` (expected `manzanas rojas`); colour adjectives, which do not go before the noun in neutral Spanish, are the case to flag, and `una gran casa`, `un buen pan`, `la primera casa` stay clean. Regression tests in the owning module; the fuzz oracles unchanged or widened, never narrowed. The game's sheet uses `quiero tres manzanas rojos` (`gender.agree`) meanwhile (`content/PHRASES.md`, `la-manzana-es-roja`, `quiero-tres-manzanas-rojas`) | M56 | S | done 2026-11-01 (`65b04e6`, on `feature/village`, and the follow-up M62b `5f3450b`; findings in §5b; three older fuzz findings at five times the depth left for the owner and the orchestrator) |
| M63 | Fuzz finding (M62's runs at five times the default depth, 2026-11-01; older than M62, analysed the same at `76f52f8`): a subject pronoun before a question word is lost to M56's check. `ellas adónde están un detenido lento` (CsCheck seeds `8zKtIhOY3951`, O5, `MutationCase { Kind = WordOrder, Seed = 19287181529 }`, and `c5SlpISRmTZ3`, O3, `ellas dónde están teme`): the check of `estar` against the phrase after it starts its clause after `adónde` and gives `conj.person` `está`, where `están` agrees with `ellas`. Keep a subject pronoun before the question word as the subject (`ellos dónde están`, `ella qué quiere`), with regression tests in the owning module; the fuzz oracles unchanged or widened (the realiser may model the clause opener instead), never narrowed. Not in phase B's queue; filed for after it | M56 | S | folded into M66 (2026-11-01, the follow-ups) |
| M64 | Review finding (the review of `feature/village` before PR #20's merge, `docs/qa-reports/2026-11-01-review-feature-village.md` R1, R2, R5 and R6; the owner, 2026-11-01: "fix them all"): (a) a subject pronoun joined by `y` is read alone, so correct Spanish is flagged and recast into wrong Spanish (`mar y yo fuimos al río` at session 8's `lesson-we-went` gave `conj.person` `fui` and the recast «Mar y yo fui al río»; `tú y yo somos amigos` gave `soy`; M62 added `noun.number` on a predicate adjective); (b) an amount before a singular `ser` (`diez monedas es mucho dinero` gave `son`); (c) `es cerca del mediodía` gave `ser_estar`; (d) ADR 0009 had no note for M56 to M62's public surface; (e) the lists of `SpanishTimeMarkers` could be written through a cast. Regression tests in the owning module; the fuzz oracles widened, with one narrowing for (b) at the owner's request | M59, M62 | S | done 2026-11-01 (`23af5cd`, on `feature/village`; findings in §5b) |
| M65 | Review follow-up (`docs/handovers/done/2026-11-01-village-phase-b.md` 9.3 A; the owner, 2026-11-01): fuzz stability. The open fuzz failures, with the owner's yes to three narrowings, each guard with a test that what it leaves out is correct Spanish (as `AmountMutationGuardTests`): (a) the `conj.tense` mutation takes M26b's guard for a name inside a grammar alternative (`egVgLELrxyL4`, O4, about one run in six); (b) the `conj.person` mutation takes M59's exclusion of `haber` after a noun subject (`b3HSKSLCsL_1`, `4YuZ2yubxdC1`, O5; at the default depth since M64); (c) the `conj.tense` mutation keeps a mutated case only when it matches the same grammar as the original (`2ldq2rIKLrG2`, O4). (d) `2tpLGUOSqj41` stays recorded (owner). A skip budget: a fixed-seed test that fails when one realiser skip kind exceeds a share of the draws. `scripts/try-lines.sh`, the review's two wrappers in one script, with the review's correct lines and M64's as line files | M64 | S | done 2026-11-01 (`479eae4`, on `feature/village-followups`; findings in §5b) |
| M66 | Review follow-up (handover 9.3 B): the grading gaps a learner of sessions 7 to 10 meets, with M63 folded in. False alarms first: `¿puede usted vender manzanas?` gives `conj.person` (an inverted subject pronoun before an infinitive); `ni tú ni yo fuimos` and `tú o yo vamos` (M64's coordination extended). Missed errors: a finite verb after a modal (`puedo pago`, `quiero vendo`; the tag `ir a` + a finite verb already gives); `¿cuánto cuestan todo?` and `¿cuánto son todo?` (`todo`, `eso`, `esto` singular); `los panes costa`; `azuls`; `ti` in the lexicon; M63; and the QA pass's QA-language-3 (`las manzanas cuesta una moneda` praised: a plural subject before a singular `costar`) | M65 | M | done 2026-11-01 (`53f63e6`, on `feature/village-followups`; findings in §5b) |
| M67 | Review follow-up (handover 9.3 C): `vocab.wrong` is derived only on a line that resolved no intent or is a near miss of one answer, never on a line a grammar understood (`VerdictAssembler.DeriveVocabWrong`; `hola luz quiero dos panes`, `usted comprará pan`, `buscaré madera para luz` were scored 0.4). With the QA pass's QA-language-5 and 10 (`docs/qa-reports/2026-11-01-language.md`): a line at a constrained prompt that differs from its nearest answer only in a verb's form gets `conj.tense` or `conj.person` with the answer's form as expected (`Luz compra pescado` against `luz compró pescado`, `sí fue al río` against `fui`), where today the near answer is taken with no error. The goldens and the identity baseline re-recorded, each change listed | M65 | M | done 2026-11-01 for (a) and the tooling (`6b922d7`, on `feature/village-followups`; findings in §5b); the answer-set verb forms of QA-5 and 10 parked as `M67b-answer-verb-forms` (the game's lessons already give a tense retry) |
| M68 | Review follow-up (handover 9.3 H): the timed tests' budgets on CI, measured after a warm-up and scaled by `VOCATERRA_TIMING_SCALE` set in `.github/workflows/ci.yml` (the brief names the file and lets this one milestone edit both halves' tests) | | S | not needed now (the owner, 2026-11-01: `main`'s CI green after PR #20's merge); runs only if a timed test fails on CI (Sonnet 5.5) |
| M69 | Review follow-up (handover 9.3 B, last part): the left points of M56, M58 and M62, all missed errors: M56's `son cerca`, `ser en` with a place, the first and second persons after the verb; M58's `podo` and `esto` read as the slip without a grammar; M62's `él es alta` with no grammar and no `muy`, `son` after a lone name, the gender of a subject with `de …`; and M71's finding: the realiser never draws `buenas tardes` (re-rolled as `literal-verb-subject`, `tardes` being a form of `tardar`); and two analyzer findings of M67b's widened O4 (not merged), if they reproduce: `vaya sentir` read as `ir` plus a bare infinitive (O4 `3isZGy3IeUc8`), a recast keeping a gender error (O5 `deXmFwx4GzL4`); and V20's: `cuanto son todo?` (the question word without its accent) is understood but `son` is not corrected; QA-language-10's `cuántos cuestan todo` (the quantity word not checked) | M66 | M | done (2026-11-02, Opus 5.5 xhigh; gated at `76d3658`) |
| M70 | QA follow-up (the QA pass on the village, `docs/qa-reports/2026-11-01-language.md`, 2026-11-01): (a) `me llamo ana`, a learner's own name, is not understood (`vocab.unknown` on the name; `me llamo domingo` gets a recast that drops the name), QA-2; (b) `gustar`: `yo gusto el pan` and `me gusto el pan` get correctness 1 (expected `me gusta`), QA-4; (c) an unknown word is blamed on its neighbour, so the recast repeats the wrong word (`compré three manzanas` gives `vocab.unknown` expected `compré`, recast «compré compré manzanas»; `manana iré al río` gives expected `al`), QA-6; (d) the preterite's spelling changes: `buscé`, `pescé`, `llegé`, `empezé` read as unknown words, to be graded as the verb's slip (`busqué`), QA-9; (e) `dos panes es caro` reports `heard '2'` for the learner's `dos`; (f) the owner's answers of 2026-11-01 (D-game-22): typed `n` for `ñ` accepted as a dropped accent when the spelling without `ñ` is not itself a word (`manana`, QA-7; `ano`/`año` stay distinct), and an accent that marks a verb's tense or person (`compre` for `compré`) accepted with no deduction but with a note naming the accented form (a public addition: ADR 0009 and `docs/nlp/api.md`). Each with regression tests; the fuzz oracles widened where the realiser can model them | M67 | M | done 2026-11-02 (`f1a02d3`, on `feature/village-followups`; findings in §5b) |
| M71 | Fuzz finding (V23a's gate, 2026-11-01, `feature/village-followups` at `e02b5df`; no core change since the last pass): O3's check that every catalog grammar is realised fails at random on the family of V15's and V17's grammars with an optional name and two time slots, `(sí)? (time)? (domingo|luz|mar|león)? [VERB:vender] [ART]? [NUM:qty]? [NOUN:item] (time)?` and its `comprar` sibling (twice in a row, a different grammar each time). The realiser rejects most of their draws (`literal-disagreement`, `conflicting-time-markers`), so one of them is sometimes drawn zero times in 2 000; it predates M65 (whose change only counts rejections). Fix by widening: the realiser honours a name before the verb and a first time marker when it draws, and the coverage check gives each grammar not yet realised a targeted, fixed-seed pass before it asserts | M65 | S | done 2026-11-01 (`35ece1c`, on `feature/village-followups`; findings in §5b) |
| M72 | The off-prompt lines (the owner, D-game-22, 2026-11-02, option 2, after a design study; V27 waits on it): at an NPC's constrained open prompt a correct line whose intent the prompt does not take got `vocab.wrong` (0.4, an empty expected word: M67's derived tag) — (a) the derived tag only where it names the word (a one-answer set, a quiz) or explains a near miss; (b) a second resolver pass over the whole catalog (grammar and keyword rungs) when the allowed pass gives `unknown`: `IntentResolution.OffPromptIntent` and `Verdict.OffPromptIntent`, optional and null-omitted, `Intent` and `Communicated` unchanged, ADR 0009's note; (c) `hace frío` is not `ask_activity` (a keyword rule on `hacer` alone). Fuzz: O10's model follows the rule; O7 and O3 widened (an off-topic correct sentence gets no error at a prompt) | | M | Opus 5.5 at xhigh | done (gated at `4af6bd7`, 8199 passed) |
| M73 | The small core points after the follow-ups (the owner, 2026-11-02; after V27): (a) `cuántas cuesta todo` is read as a noun phrase (M69's left point): graded as M69 grades `cuántos cuestan todo`; (b) the recorded fuzz families `2tpLGUOSqj41` with `fSMdah3kOSd7` (O5, a `word_order` recast where a `[WORD]` slot binds the moved adjective) and `8yc522XLB_W2` (O4, `word_order`: the mutation breaks the grammar match, `rica` read as an adjective, a false `conj.person`): fixed at the source where the analysis or the recast is wrong, never narrowed (the owner's (d)); a cause whose only fix is a narrowing is reported and the seed stays recorded; (c) found by M72's fuzz runs and tries: O2 `7HIoK0InC7l7` (`conj.person` reported twice on one word), O3 slots `92moRWz9NQ54` (no `item=suyo`), O5 `3-VvCuMLFEA2` (a new seed of the word-order family), `ustedes trabajan mucho` read as `ask_activity` (the `trabajar` rule), `hoy hace buen tiempo` read as `ask_craft`, `cómo está usted` and `cómo estás` not understood (`greet` takes neither), `dos panes es caro` with no error (a numeral subject is not checked); (d) found by V27's run without models: O5 `26dVDqElv1l2`, the realiser draws the noun `son` in `estómago son vencido` and the analyzer reads it as `ser`: each fixed at the source, or reported with its cause | M72 | M | Opus 5.5 at xhigh | done (gated at `59d4044`, 8265 passed) |
| M76 | Checks as C# tests (the owner, 2026-11-02, before PR #28 merges: "I would prefer things like this to be a csharp unit test, not some throwaway script or junk text file"; D-game-22): (a) the 540 rows of `scripts/try-lines/` (9 line files with their CLI and host baselines, M64 to M73) become xUnit tests grouped by topic, the core path in the core's test projects and the game's path (a greeting and the listener's name taken out) in `tests/Vocaterra.Host.Tests`, deduplicated against each other and the existing tests; then `scripts/try-lines.sh` and `scripts/try-lines/` are deleted and `docs/nlp/contributing.md` follows; (b) an audit of the other check and one-off scripts in `scripts/` (test, keep or delete each; the straightforward ones done, anything large or called by the gate or CI proposed). The brief lets it edit both halves' tests and `scripts/`. (M74 and M75 are reserved by `docs/handovers/done/2026-11-02-after-followups.md`) | M73, V28 | M | Sonnet 5.5 | done (gated at `523f818`, 8797 passed) |
| M77 | Unused usings as a build warning (the owner, 2026-11-02, "do the IDE0005 warning too", after M76's audit): IDE0005 at warning with `EnforceCodeStyleInBuild` for every project of `Vocaterra.sln` (with the documentation file it needs, its missing-comment warnings suppressed), every unused using removed, no other rule changed, `scripts/audit-usings.py` deleted; the brief lets it edit the root `Directory.Build.props` and `.editorconfig` and the using lines of both halves | M76 | S | Sonnet 5.5 | done (gated at `7bcaefd`, 8797 passed) |
| M74 | The weather intents and `ask_wellbeing` (the owner, D-game-22, 2026-11-02, items 1, 3 and 5; `docs/handovers/done/2026-11-02-after-followups.md` sections 2 and 4; V29 waits on it): a statement intent `state_weather` (`hace frío`, `hace calor`, `hace sol`, `hace buen tiempo`, `hace mal tiempo`, `hoy hace …`, `llueve`, `está nublado`) with a slot for the weather word, a question intent `ask_weather` (`¿qué tiempo hace?`, `¿hace frío?`), and `ask_wellbeing` (`¿cómo estás?`, `¿cómo está usted?` and the plurals) taken out of `greet`; `hacer` with a weather noun kept out of `state_action`, `ask_activity` and `ask_craft` by the resolver's order or the grammars; `hace dos días compré pan` neither weather nor an action; O7 and O3 widened, the grading tests and the identity baseline moved; every word within A2 | M73 | M | Opus 5.5 (xhigh, D-game-22) | done (2026-11-04, gated at `7ce037c4`, 8957 passed) |
| M75 | The near-miss rule (the owner, D-game-22, 2026-11-02, item 4; the after-followups handover section 5): a near miss must name its word: the line and the nearest answer phrase have the same number of words and differ in exactly one, a slip pair (within that word's fuzzy budget, or both of one closed class), the answer's word reported as `Expected`; the PR #28 review's item 8 (an empty answer set is never a near miss); O10's model and generator follow the rule, O3's verdict leg strict again; every changed grading case listed by kind | M74 | M | Opus 5.5 (xhigh, D-game-22) | done (2026-11-04, gated at `4d7b2c7e`, 9019 passed) |
| M78 | The small core points left after PR #28 (the after-followups handover section 8): say which intent `la tienda está en el pueblo`, `usted hace hachas muy buenas` and `tú pescas mucho` should be, or that they stay unknown, and fix the catalog; an unknown name (`pedro`) not `vocab.unknown`; `necesito ayuda domingo` (a name said to the listener) in the CLI, or accepted; `docs/nlp/tools.md`'s parked workflow; the recast after a near miss names the answer's word (M75's left point) | M75 | S | Sonnet 5.5 | done (2026-11-05, gated with V29 at `3b4a2214`, 9131 passed) |
| M79 | The typo slip (the owner, 2026-11-04, from a playtest: `queiro comprar el pan` and `querio` typed for `quiero` in free talk with the shopkeeper): an unknown word has no reading, so its `vocab.unknown` drops the analysis confidence to 0.5 and the verdict's minimum confidence makes the line not communicated, although the keyword rung found `state_action` with `item: pan`; outside an answer-set near miss (M17c, M70's `AnswerCorrections`) nothing names the word the learner meant. (a) A new `WordSlipKind`-style reading in stage 4, tried last (after the accent and `ñ` fallbacks of M70): a word the lexicon lacks that is exactly one Damerau-Levenshtein edit (a transposition counts as one) from **exactly one** lexicon form, or from several of which the sentence can use exactly one (a grammar or keyword rule needs it, as the reading resolver already keeps a `BesideNoun` slip only where the sentence calls for it: `el pnn` gives `pan`), takes that form's readings as a slip, and its error names it as the expected text (`queiro` gives `quiero`), so the line resolves as a line with a small mistake; (b) the guards, each with tests: a minimum word length (proposed 5 letters; short words have many neighbours), no reading when the sentence leaves two or more candidates (`pedro` is one edit from both `perro` and `pero`), an optional frequency cut on the candidate (the Wiktionary import's rank, ADR 0008), never a word a slot grammar binds as a name (`[WORD]`, M70's names and M78's), never a word the learner's line spells as a lexicon form; (b2) suggestions (the owner, 2026-11-04): when no single candidate is read, the line stays not understood and its `vocab.unknown` error lists up to 3 candidates, ranked deterministically: one the sentence can use, then a word of the prompt (its answer set, its scene's words), then one at or under the prompt's level, then the frequency rank, then ordinal order; a word above the bundle's ceiling (A2) is never suggested, nor anything for a word a grammar binds as a name. The list is a public addition (an optional, null-omitted field on `ErrorTag`: ADR 0009, `docs/nlp/api.md`); (c) the tag of a read slip: `vocab.unknown` with the expected text filled, as `AnswerCorrections` does (no public change), or a new `vocab.spelling` tag with its own weight (a public addition: ADR 0009, `docs/nlp/api.md`); the brief proposes one, the owner decides; (d) the confidence a slip gives (below 1, above the communicated margin) and whether a typo slip lowers correctness as much as an unknown word (0.4); (e) the fuzz oracles widened where the realiser can make a one-edit typo, and lint's unknown-word coverage (M27a) unchanged. The game's side (a feedback line that names the spelling or the suggestions, «did you mean *pero* or *perro*?», in `content/locale`) is a game milestone after this one | M75, M78 | M | Sonnet 5.5 | proposed |
| M80 | The open-recognition fallback for free talk (the owner, D-game-22, 2026-11-06, the spoken-lines study's option 1; V31 waits on it): `PipelineOptions.OpenRecognitionFallback`, a second open decode only when the constrained one is not taken, the taken pass wins, replay-exact; measured on the seven free-talk prompts with Kokoro voices only (no owner recordings, answer 5), zero false takes the bar (`docs/research/2026-11-04-spoken-off-prompt-lines.md` section 5) | M72 | M | Sonnet 5.5 high | done (2026-11-07, gated at `d939ceb7` on `feature/bread-pilot`, 9893 passed, 3 skipped; with the owner's 0.5 weakest-word floor, §5b) |
| M81 | The loader takes an intent with its `slotGrammars` removed (O15, ContentSchemaFuzzTests, seed `41s2qgWEC-M9`: `describe`, intent 27, has no `keywordRules`, and the mutation is accepted; found by N01's agent on 2026-11-05 and reproduced on `feature/adaptive_design` at 558ed095): fix the cause (an intent no grammar and no rule can produce is a content error) or make the tolerance exact, with a regression test and a fuzz README finding | none | S | Haiku 5.5 | done (2026-11-05, gated on `feature/adaptive_design`, 9160 passed) |
| M82 | The loader refuses an intent with no `slotGrammars`, no `keywordRules` and no `examples` (the owner, 2026-11-05, on M81's finding: no bundle has one, so enforce it): a `ContentValidationException` naming the file and the intent; `OptionalMembersMayBeOmitted` gives its intent one member; the fuzz README's M81 entry moves to fixed | M81 | S | Haiku 5.5 | done (2026-11-05, gated at `80492cf9`, 9363 passed) |
| M83 | The O7 fuzz seed `8eu5g1sTe_A8` (found by W02, §5b; the owner, 2026-11-07, D-game-23 item 5): «ayuda por favor por favor» at a prompt without `ask_help` resolves as Grammar `request_item` although `ask_help`'s grammar outranks the match; fix the resolver or the analyzer at the cause, with the seed's case as a regression test; the oracle stands | | S | Sonnet 5.5 medium | done (2026-11-08, merged at `45499631`, gated with the water merge at `bc881c58` on `feature/bread-pilot`, 10328 passed, 3 skipped; §5b) |
| M84 | Free talk's forced rows (found by V31; the owner, 2026-11-07, D-game-23 item 5): with `OpenRecognitionFallback` on, a constrained reading that was taken but looks forced (most of the clip's speech unused, or a clip much longer than its row, or weak words) also gets the open decode, and a confident, different free reading wins; measured on M80's Kokoro corpus plus V31's forced lines («el río es grande» heard «es gracias»), zero false takes the bar, the cost per turn reported | M80 | M | Sonnet 5.5 high | done (2026-11-08, gated with T10 at `386815a6` on `feature/bread-pilot`, 10363 passed, 3 skipped; §5b) |
| M85 | The lint's speed again (CI on PR #40, 2026-11-08; the owner: "fix the failing tests"): `LintTests.TheBundleLintTakesUnderFiveSeconds` took 5.54 s on the GitHub runner (about four times slower than the desktop). M60 brought the lint to 398 ms (Debug); find what made it slower since (the bundle's growth, M74/M75's grammar rung, M80 to M84) and fix it at the source as M60 did, output byte-identical; the 5 s budget stays | M60 | S/M | Sonnet 5.5 medium | done (2026-11-08, gated at `a1066151` on `feature/bread-pilot`, 10394 passed, 3 skipped; §5b) |
| M86 | O9's double silent h (CI on PR #40, 2026-11-08, CsCheck seed `8HYgz_I9Ysn1`; the owner: "CI is still broken: fuzz"): `PronunciationFuzzTests.SpokenPhrases_ScorePerfectly_OrYieldExactlyTheInjectedPredictedError` injected `pron.silent_h` at «hieran» in «ligaba eh hieran» (heard [... e h j e ...]) and the scorer reported it twice, at «eh»'s h and at «hieran»'s: one inserted [h] between two silent h letters is claimed by both. Fix the scorer so one heard phone yields one error; the oracle stays as it is | M16b | S/M | Sonnet 5.5 medium | open: launched 2026-11-08, no fix reached `main` (PR #40 merged without it; no branch or report is left, the brief is `.claude/briefs/m86-o9-silent-h.md`); CI on `main` has passed since, the seed is random |
| M91 | O4's `podar` before an infinitive (the merge gate of U08 on `feature/ui-issues`, 2026-11-08, CsCheck seed `3m67O5Rpuya2`; reproduced on the code before U08, so not U08's): `MutationTagFuzzTests.OneMutation_YieldsExactlyTheNamedTag_WithTheOriginalFormAsCorrection` built «vosotras podáis rastrear opero» (`podar`, then an infinitive), mutated `conj.person` to «podo», and the analyzer read «podo rastrear» as `poder` (M58's `CallsForSlip`: `podar` takes no infinitive), reporting `conj.person` → `podéis` and `conj.irregular` → `puedo` instead of `conj.person` → `podáis`. Find whether the generator or the analyzer is wrong; fix that one; the oracle's assertion stays as it is. Numbered M91 because `feature/german` holds M87 to M90 | M58, M73 | S | Sonnet 5.5 medium | done (2026-11-08, gated at `6275653f` on `feature/ui-issues`, 10431 passed) |
| M96 | «porque» read as a plural noun (the owner's yes, 2026-11-15, on `feature/biomes`): «túneles unos porque» gets the noun reading of «porqué»; O4 fails with CsCheck seed `dTc3W4dw5Cg2` (Y11, 2026-11-12) and O5 with `7PcP-w7xauk1` (B08b's gate, 2026-11-15; the recast writes «porqués»). Find the source (lookup, tagger, agreement or realiser), fix it there with regression facts per seed and the correct neighbours («el porqué», «los porqués») unchanged; no oracle narrowed. M88, M89, M92, M93 and M95 are taken on `feature/german`; M94 is skipped to avoid a clash | M65 | S | Sonnet 5.5 medium | done (2026-11-16, gated at `fbe763f9` on `feature/biomes`, 11,988 passed): `porque` with no word after it, after a determiner of the other number, stays the conjunction (`AccentVariantChoice.DisagreesWithDeterminerBefore`); both seeds fixed; the M65 (c) guard test now expects tense errors only (stricter) |
| M97 | Two fuzz seeds and a load-sensitive oracle (the owner's yes, 2026-11-16, on `feature/biomes`; core findings of 2026-11-16): O5 `2LxdNIrNHnQ6`, the recast drops a plural («cuanto almuerzos veintiocho zeponefu transportan» recast as «cuanto almuerzo 28 …»); O3 `9kz6LGNo8tf2`, a correct «que me» graded at `smith-receive` gets `vocab.wrong` expected 'tal'; `PipelineFuzzTests.OpenFallback_LeavesATrustedFirstReadingAlone` fails under load and passes alone. Fix each at its source with regression facts per seed; make the coverage oracle independent of load without narrowing it | M96 | S/M | Sonnet 5.5 medium | done (2026-11-16, gated at `63acefae` on `feature/biomes`, 12,319 passed): two fuzz seeds fixed (a realiser re-roll for a noun that reads as the next noun's determiner, `skip:noun-reads-as-determiner`; a pronoun slip pair in `AnswerNearMisses` needs a person on each side, api.md and pipeline.md updated) and the open fallback's coverage floor asserted on a fixed seed (`OpenFallback_TheGeneratorReachesEveryPath_OnAFixedSeed`); report `.claude/briefs/m97-report.md` |
| M98 | A realisation-rate gate for the fuzz generator (the codebase review's T3, the owner's yes, 2026-11-16, D-game-34 item 37): `SentenceRealizer` holds 54 re-roll rules; pin the realisation rate per pattern family and print counts per skip reason, so the suite cannot lose power silently; no oracle narrowed; and M97's `OpenFallback_TheGeneratorReachesEveryPath_OnAFixedSeed` failed once inside a full run (B18's, 2026-11-16) while passing alone: find what makes a fixed-seed coverage count vary (CsCheck's parallel sampling, shared state, the iteration count) and make it truly deterministic without lowering its floor; and the seed `7q7l8dSNG6n6` (O3, «alrededor aclara construir»: the realiser gives `alrededor` as an item noun and the analyser finds no item; B07e's gate, 2026-11-17): triaged; fixed when the fault is the generator's or the oracle's, and an engine or pack change proposed for the owner's yes like the silent-h seed's | M97 | S | Sonnet 5.5 medium | done 2026-11-17 (`155e27c5`, 12,489 passed, no failures): a realisation-rate gate (floors per pattern family), the fixed-seed coverage test truly fixed (one case per seed), and seed `7q7l8dSNG6n6` fixed in the realiser (a noun that is also an adverb is re-rolled beside an optional adverb) |
| M99 | Two silent h's in a row (the owner's yes, 2026-11-17, D-game-34 item 46; core findings, seed `a2RKk5N6Ibt4`): «ah hambrientos» with one [h] injected at «ah» is scored `pron.silent_h` twice; one inserted [h] yields one error at the word it belongs to; regression facts for the phrase and its neighbours | M97 | S | Sonnet 5.5, medium | done 2026-11-17 (`7dc4054a`, 12,569 passed; the one failure the game's wall-clock scene-build test, not the core's): one heard extra phone is one tag (a silent h ending a word and one starting the next no longer double-tag; the earlier word gets it), seed `a2RKk5N6Ibt4` fixed |
| M100 | `GeneratedLexiconTests.LoadingTheBundleIsFastEnough` on CPU time (B24's finding; the codebase review's T2): the median of three wall-clock loads against 3 s fails gates under load; measure the load's CPU time, the fastest of three, in a non-parallel collection, budget twice the measured cost | M99 | S | Haiku 5.5 | done 2026-11-17 (`12ae33c8`, gated with B28: 12,715 passed, no failures): the lexicon load measured on CPU time, the fastest of three (541 ms), budget 1,082 ms, in a non-parallel timing collection |
| M101 | A fuzz seed on an adjective's gender (found 2026-11-17 by the B11c gate; core findings): O3 seed `3gcdMpDLcrQ4`, «Laná doloroso les» realised as correct, the analyser reports `gender.agree` (expected «dolorosa»); triaged like M98: fixed in the realiser or the oracle when the fault is theirs, an engine or pack change proposed for the owner's yes | M98 | S | Sonnet 5.5, medium | done 2026-11-18 (`4c7b87a1`, 12,768 passed; the one failure a game wall-clock test, passing alone): the fault the realiser's: a drawn name («Laná») that is a lexicon noun with accents ignored («lana») is re-rolled before an unheaded adjective (`name-read-as-noun`); the oracle unchanged; seed `3gcdMpDLcrQ4` fixed |

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
- Full design in `docs/nlp/fuzzing-plan.md`: CsCheck generators driven by the project's own catalogs
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

The findings log is in [findings.md](findings.md) (moved out on 2026-11-09; "§5b" in other documents means that file). Record a
milestone's findings there and its status in section 5's table.

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
6. Measurements recorded in the fuzz README and tools.md: lexicon file size, bundle load time, full
   test-suite and fuzz-suite wall time, homograph skip counts of the fuzz generators before and after.
   If all gates below hold at 3 000 lemmas, regenerate at 5 000 and keep whichever is the largest
   that still passes.
7. Docs: README vocabulary section and regeneration recipe; tools.md rows for kaikki.org,
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
| `Slg.Core` contracts: verdict, prompt spec, error tags, replay envelope | `specification.md`, the game design (moved to Vocaterra `docs/game/specification.md` in M19c) |
| Audio, Asr, Phonology, Pronunciation, Nlp, Evaluation | Character definitions (blacksmith, innkeeper, captain) and their dialogue pools |
| Decision: learner model (BKT), scheduling (FSRS), feedback policy, and the conversation state-machine *engine* (renamed from Npc to a generic conversation agent) | Items, spells, quests, prices and rewards: the effect tables and game-event names |
| Response: templates, recast, speech synthesis, and the effect *resolver* with opaque outcome names | The game's content bundles, built with the core's ContentBuilder |
| Content: bundle format, loader, validator, provenance, lexicon import, CEFR lists | Engine integration (Unity, Godot or MonoGame), rendering, device audio capture |
| Pipeline with a game-agnostic `ConversationContext` | Game-side glue mapping outcomes to prices, spell power and quest state |
| Tools (Cli, Bench, ContentBuilder), fuzz and golden suites, a sample bundle | |

The boundary is defined on the contracts (verdict, prompt spec, content formats), which stay stable
whatever the research report in `docs/research/2026-10-20-scaling-approaches.md` changes inside the core.

### 8.2 Milestones

- **M19a, boundary and API (Haiku, M).** ADR 0009 "core and game boundary". Rename the solution to
  `Vocaterra.Nlp.sln`, every project, namespace, assembly and future package id from `Slg.*` to
  `Vocaterra.Nlp.*` (folders too; `git mv` so history follows), the test and tool projects alike, and
  every doc reference; the `Slg.Cli` demo becomes `Vocaterra.Nlp.Cli`. Rename `GameContext` to
  `ConversationContext` (`NpcId` → `AgentId`, `NpcState` → `AgentState`, `GameStateHash` →
  `HostStateHash`); `NpcStateMachine`/`NpcDefinition`/`NpcState` to `AgentStateMachine`/`AgentDefinition`/`AgentState` (namespace `Decision.ConversationAgents`), content
  folder `npcs/` → `agents/`; `EffectTable` constants removed, outcome kinds become opaque strings
  validated for uniqueness only (the sample content defines `trade_price` and `spell_power`). Game
  words scrubbed from identifiers and doc comments (Spanish example words may stay). `docs/nlp/api.md`
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
  `specification.md`, the game content bundle (blacksmith scenes, spells, items, effects; later the
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

`docs/research/2026-10-20-scaling-approaches.md` (Opus 5.5, 120 sources, two experiments run) concludes that
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

### 9.1 Owner decisions raised by the report (with its recommendations; tracked in `docs/nlp/decisions.md`)
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
  `contentHash`; lint finds no new issue; a layered bundle packs, verifies and hashes; `docs/nlp/api.md` shows
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

The design and the evidence are in `docs/research/2026-10-22-multilingual.md`. **Amended 2026-10-26** (D14,
`docs/research/2026-10-26-engine-and-language-packs.md`): Phase A runs. The order is M31, then M52 (the core libraries merged into
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
