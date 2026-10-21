# The host API

This is what a host calls, in order, to evaluate a learner's utterance and act on the result: load a content bundle, build the services,
build a pipeline, build a conversation context, evaluate an utterance, plan the response, and record and replay a log. The snippets compile
against the renamed projects (`Vocaterra.Nlp.*`); they are the shape of the calls, not a complete program. The boundary they respect is in
[ADR 0009](adr/0009-core-and-game-boundary.md). The sample host (`samples/HostConsole`, run by `scripts/sample-host.sh`) makes the calls of
sections 1 to 5 against the packages and checks their results; [PACKAGING.md](PACKAGING.md) describes it.

Every step is tagged with its determinism tier. **D0** is plain code with fixed-order math: rules, matching, verdict assembly, the agent
state machine, the feedback policy and the response templates. **D1** is a frozen model behind a core interface (speech recognition,
phoneme recognition, embeddings, speech synthesis): pinned by SHA-256, recorded in every verdict's `versions`, and loaded only when the host
gives it a models directory. With the scripted recognizer and the fakes a host runs entirely at D0, with no model file at all.

The host owns what the core cannot know: the world seed, the stored time, the learner model it persists between utterances, the repeat counts
per error tag, and a digest of the rest of its state (`HostStateHash`). The core copies every one of them into the replay record.

## 1. Load a content bundle (D0)

```csharp
using Vocaterra.Nlp.Content;

ContentBundle bundle = ContentLoader.Load("content/v1");   // verifies every file's hash and the cross-references
Console.WriteLine(bundle.ContentHash);                      // the bundle hash, recorded in every verdict
```

`ContentLoader.Load` throws `ContentValidationException` for a bundle that fails validation. The bundle carries the prompts
(`bundle.Prompts`), the agents (`bundle.Agents`), the effect table (`bundle.Effects`) and the lexicon; nothing is read again after loading.

### Host-owned files

A bundle may hold files that the loader does not parse, such as the host's own data (`game/nouns.json`). Such a file is hashed in
`manifest.json` like every other file, has an item in `provenance.json` like every other file, and is ignored by the loader: validation
accepts an unknown file that is listed in both, and it does not check the file's contents. The host reads it back with
`TryReadHostFile`:

```csharp
if (bundle.TryReadHostFile("game/nouns.json", out ReadOnlyMemory<byte> nouns))
{
    string json = System.Text.Encoding.UTF8.GetString(nouns.Span);   // the bytes the load hashed against the manifest
}

IReadOnlyList<string> hostFiles = bundle.HostFilePaths;               // the host-owned paths, in ordinal order
```

The bytes are the ones the load read and checked: the bundle keeps them from the load, so a read never touches the disk and cannot return
bytes that changed after the load (a changed file fails the next load, not this read). A path is accepted only when it is relative, uses `/`
separators, has no empty, `.` or `..` segment, and names a host-owned file of this bundle. `TryReadHostFile` returns false for an unlisted
path, an absolute path, a path with `..`, a backslash or a drive letter, and for a file the loader owns (`provenance.json`,
`ATTRIBUTION.md`, `lexicon/`, `answer-sets/`, `agents/`, `cefr/`, `intents/`, `prompts/`, `dialogue/`, `feedback/`, `evaluation/`,
`effects/`). A bundle loaded with `VerifyHashes = false` has no host files.

## 2. Build the services (D0, with optional D1)

```csharp
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;

// D0: a scripted recognizer returns a fixed transcript, so no model and no audio are needed.
IWordRecognizer words = new ScriptedWordRecognizer(
    new AsrResult("yo tiene dos espada", [new AsrHypothesis("yo tiene dos espada", 1.0)], [], 1.0, "scripted-transcript", RecognitionMode.Open));

IPhonemeRecognizer? phonemes = null;                        // optional: without it pronunciation is not scored
IEmbeddingModel? embeddings = new FakeEmbeddingModel();     // D0 stand-in for the embedding rung
ISpeechSynthesizer? synthesizer = new SilentSynthesizer();  // D0 stand-in: no audio is produced

var services = new PipelineServices(words, phonemes, embeddings, synthesizer, bundle);
```

With real models (D1), the same constructor takes the adapters. `ModelStore` reads the directory named by `VOCATERRA_MODELS_DIR` and checks
each file against `models/manifest.json`; an adapter that is not pinned is refused by its factory.

```csharp
using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;

ModelStore store = ModelStore.FromEnvironment() ?? throw new InvalidOperationException("Set VOCATERRA_MODELS_DIR to a models directory.");
IWordRecognizer words = VoskWordRecognizer.FromStore(store, "vosk-model-small-es-0.42");           // D1, constrained prompts
IPhonemeRecognizer phonemes = new Wav2Vec2PhonemeRecognizer(store.ModelDirectory("wav2vec2-xlsr-53-espeak-cv-ft"));   // D1, optional
IEmbeddingModel embeddings = new OnnxEmbeddingModel(store.ModelDirectory("multilingual-e5-small"));  // D1, open prompts
ISpeechSynthesizer synthesizer = KokoroSynthesizer.FromStore(store, "kokoro-multi-lang-v1_0", speakerId: 28); // D1, optional (28 = ef_dora)
```

The recognizers are chosen by the host: Vosk for constrained prompts, sherpa-onnx (`SherpaOnnxOfflineRecognizer.FromStore`) for open prompts;
the CLI shows the choice. Every adapter exposes its `ModelHash`, and the pipeline puts those hashes into the verdict's `versions`.

## 3. Build a pipeline (D0)

```csharp
var pipeline = new UtterancePipeline(services);             // PipelineOptions is optional
```

`UtterancePipeline` runs the stages in a fixed order; the same input gives the same bytes of output. One pipeline serves many
utterances.

## 4. Build a conversation context (D0)

```csharp
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

var agentState = bundle.Agents["blacksmith"].InitialState();   // the host persists the state between utterances
ConversationContext context = ConversationContext.Create(
    worldSeed: 42,                                              // with the agent id and counter, seeds every random draw
    agentId: "blacksmith",
    agentState: agentState,
    now: new StoredTimestamp(1_760_000_000),                    // the host's clock, passed in; nothing in the core reads one
    learner: LearnerModel.New(),                                // the host persists this and passes it back next time
    repeatCountsByTag: null);                                   // the counts before this utterance: none at the start, then the previous Next's (see below)
```

`ConversationContext.Create` computes `HostStateHash` over the values it was given. A host with its own state digest uses the constructor
instead and passes that digest.

### Host slots and host-named skills (CR-003, CR-005)

Two more inputs belong to one utterance. They are optional trailing parameters of `Create` and init properties of the context, so existing
`Create` calls and positional constructions compile unchanged.

```csharp
using Vocaterra.Nlp.Core.Verdicts;

ConversationContext context = ConversationContext.Create(
    worldSeed: 42, agentId: "blacksmith", agentState: agentState, now: new StoredTimestamp(1_760_000_000), learner: learner,
    hostSlots: SlotMap.From([new("target", "árbol"), new("direction", "a la derecha"), new("distance", "12")]),
    extraSkills: ["word.árbol"]);
```

- **`HostSlots`** (`SlotMap?`, strings only like verdict slots): facts only the host knows. Reply and feedback templates read them with the same
  formatters verdict slots get, so `Mira {target:article(def)}, {direction}, a {distance:words(m)} metros.` renders as
  `Mira el árbol, a la derecha, a doce metros.` A noun lemma works with `article(def)` and `plural-if(...)` exactly as a verdict slot does; a
  number slot works with `words`. Keys are held in ordinal order, so insertion order never matters.
  - *Precedence.* The name space is shared and **the verdict wins**: where a host slot and a verdict slot have the same name the verdict's
    value is used, and the reserved feedback names `tag`, `expected`, `actual` and `recast` win over both. Reason: the verdict is what the
    player said and the pipeline derived, a host fact must not silently rewrite it; and a bundle that already works keeps rendering the same
    when the host adds a slot. Give host slots names the intents do not use (`target`, `direction`, `distance`).
  - *Missing slot.* A template that reads a host slot the host did not supply fails exactly like one that reads a missing verdict slot: a
    feedback template falls back to the generic pool of its kind (then to no feedback), while a reply template throws `TemplateException`
    from `Evaluate`. The loader's template check does not know the host's names; it proves a template renders with sample values (a slot read
    by `words`, `plural-if` or `conj` gets a number or a verb whatever its name, `TemplateEngine.SlotKinds`).
  - *Empty map.* An empty `SlotMap` is stored as `null`.
- **`ExtraSkills`** (`IReadOnlyList<string>?`): skill ids the utterance also exercised, such as `word.árbol`. The pipeline merges them with its
  own `intent.<id>` skill before its single `LearnerModel.Update` call, so each skill and each error family is stepped once: naming
  `intent.offer_item` again, or an error family such as `conj`, changes nothing; a new id is added as one correct (or, for a family the
  verdict has errors in, incorrect) observation. The list is sorted ordinally and de-duplicated. An id must be non-empty and contain no
  whitespace (`LearnerModel.ValidateSkillId`); the context throws `ArgumentException` otherwise, at `Create` or when the property is set.
  A host that has no utterance (the learner met a word in the world) calls `learner.StepSkills(["word.árbol"], correct: true)`, which steps the
  named skills only and involves no error families.
- **Carry-over.** Both are per utterance: `Next` does **not** carry them over, the next context has none, and the host sets them again with
  `Create` when the next utterance needs them. (`HostStateHash` is recomputed from the carried values, as before.)
- **Hash.** `Create` folds the two inputs into `HostStateHash` only when at least one is present; a context without them hashes exactly as
  it did before this feature, so earlier logs and goldens still match. A host that built its own digest with the constructor must include
  them in it. Both are written into the replay envelope's `context` (omitted when absent), so `Replayer` reproduces the response and the
  learner model (see [REPLAY.md](REPLAY.md)).

## 5. Evaluate an utterance and read the verdict (D0 over the recognizer's output)

```csharp
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Verdicts;

PromptSpec prompt = bundle.Prompts["offer-items-open"];
string? agentLine = prompt.Text;                                // the line the agent says before the player answers (null when there is none)
PcmClip clip = PcmClip.FromSamples([]);                         // a real host passes the captured clip (16 kHz mono)
PipelineResult result = pipeline.Evaluate(clip, prompt, context);

Verdict verdict = result.Verdict;
Console.WriteLine($"{verdict.Intent.Value} communicated={verdict.Communicated} correctness={verdict.Correctness}");
foreach (ErrorTag error in verdict.Errors)
{
    Console.WriteLine($"{error.Tag}: expected '{error.Expected}', heard '{error.Actual}'");
}
```

`communicated` says whether the learner was understood; `correctness` says how well they spoke, from 0 to 1. The errors carry the span in the
normalized text, the expected form and what was heard. A host gates its own reactions on `communicated` and uses `correctness` for scoring.

### The words and the answer match (CR-002, CR-004)

`result.Words` has one entry per normalized token, in order. Each entry has the token (`Text`, the normalized text; `Span`, its offsets in
that text; `Original`, the surface as typed), a `Role` and the `Reading` the resolver chose. The role is `Word` (the lexicon knows it),
`Name` (the best slot grammar binds it to a `[WORD]` element; it has no reading) or `Unknown` (no reading, no grammar). A reading is the
`LexicalFeatures` record (`Lemma`, `Pos`, `Person`, `Number`, `Tense`, `Mood`, `Gender`; null where a feature does not apply). It is the
reading the agreement checks used, so its lemma agrees with the error spans; a host need not run `TextNormalizer` or `LinguisticAnalyzer`
again. Where the resolver left several readings of one word, the first in lexicon order is reported. `Text` keeps accents and writes number
words as digits (`dos` is `2`); `Original` is the typed text. A word typed without its accent is reported with the reading the resolver
chose, so `esta` is `estar` after `donde`, and its `Original` stays `esta`.

`result.Answer` is the answer match of the prompt's answer set: `AnswerId` (null when nothing matched), `Type` (`Exact`, `Alias`, `Fuzzy` or
`None`), `Distance` (the edit distance, or the phonetic distance when that is smaller, so `ezpada` for `espada` is 0), `LikelyPronunciation`
(the spelling differs but the phonetic key matches) and `Confidence`.

```csharp
foreach (ResolvedWord word in result.Words!)
{
    Console.WriteLine($"{word.Token.Original} {word.Role} lemma={word.Reading?.Lemma} pos={word.Reading?.Pos} gender={word.Reading?.Gender}");
}

if (result.Answer is { AnswerId: not null } answer)
{
    Console.WriteLine($"{answer.AnswerId} {answer.Type} distance={answer.Distance} likelyPronunciation={answer.LikelyPronunciation}");
}
```

The pipeline always sets both members; they are nullable only so that a result built by hand (in a test) needs no analysis. Both are part of
the result's `CanonicalJson`, but not of the replay record (see [REPLAY.md](REPLAY.md)).

### Carry the context to the next utterance

The host passes the repeat counts it carried from the previous `Next`; the first utterance of a conversation starts with none (`Create` with
`repeatCountsByTag: null`). `RepeatCountsByTag` holds how many times in a row each error tag, and `not_communicated`, was seen before this
utterance. The pipeline counts this utterance itself: after the verdict it applies `RepeatCounts.Next` and gives the result to the feedback
policy and the repair ladder, so the host never adds the current tags and never sends counts that already include them.

`ConversationContext.Next` builds the context of the next utterance from this one and the result: the agent the utterance spoke to in the
state the decision moved it to, the learner model after the update, the repeat counts before the next utterance (`RepeatCounts.Next` of
this context's counts and the verdict, which counts each error tag and `not_communicated` one more in a row and drops every other tag), and
the world seed.

```csharp
using Vocaterra.Nlp.Pipeline;

IReadOnlyDictionary<string, int> counts = RepeatCounts.Next(context.RepeatCountsByTag, result.Verdict);   // the counts before the next utterance, ordinal keys
context = context.Next(result, new StoredTimestamp(now));          // the context of the next utterance
```

The host still supplies the stored time, and it keeps the states of the other agents it talks to: `Next` carries only the agent of this
utterance. `Next` recomputes `HostStateHash` from the values it carries, as `Create` does, so a host that gave its own digest to the constructor
computes that digest again after `Next`.

## 6. Plan the response (D0, with D1 for speech)

`Evaluate` also plans the response: the agent's lines, the effects the verdict triggers and an optional repair step.

```csharp
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Response.Effects;

foreach (var line in result.Response.Lines)
{
    Console.WriteLine($"agent: {line.Text}");
}

foreach (ResolvedEffect effect in result.Effects)              // the kind is an opaque string; the host decides what it means
{
    Console.WriteLine($"{effect.EffectId} {effect.Kind} {effect.Value}");
}

if (result.Repair is { } repair)
{
    Console.WriteLine($"repair: {repair.Kind} {repair.AgentLine}");
}
```

Host slots (section 4) are available to the reply and feedback templates of this response; the planner is called with them as
`ResponsePlanner.Plan(..., hostSlots: context.HostSlots)`, and a host that renders a template of its own can use the same engine:
`bundle.TemplateEngine.Render(template, result.Verdict.Slots.WithFallback(hostSlots))` (the verdict wins, as in the pipeline).

The effect values come from the bundle's curves at the verdict's correctness; the core does not know what a value is worth. The host maps
`trade_price` or `spell_power` (or any other kind) to its own rules.

Speech is a separate call, so a host that shows text does not pay for audio. When the services have a synthesizer (D1 with Kokoro, D0 with
the silent stand-in), `Synthesize` renders every line of the plan in order:

```csharp
IReadOnlyList<AudioBuffer> speech = pipeline.Synthesize(result.Response, "agent-voice");
```

**Kokoro voices (CR-006).** `KokoroSynthesizer.FromStore(store, "kokoro-multi-lang-v1_0", speakerId, speed)` builds the adapter from the
manifest entry, with the manifest's model hash; the manifest must pin `model.onnx`, `voices.bin`, `tokens.txt` and the `espeak-ng-data/`
files, or the call throws. The adapter takes a numeric speaker id only (it does not look up names). The model has 54 speakers (ids 0 to 53),
the audio is 24000 Hz. The Spanish voices, read from the `speaker2id` metadata of the model's `model.onnx` (the release's `README.md` agrees
on `em_santa`):

| Speaker id | Voice | Use |
| --- | --- | --- |
| `28` | `ef_dora` | Spanish, female |
| `29` | `em_alex` | Spanish, male |
| `53` | `em_santa` | Spanish, male |

Speed defaults to `1.0`. The engine is loaded for every `Synthesize` call and disposed at its end (see section 8), so wrap the
synthesizer in `CachedSynthesizer` when the same lines repeat.

## 7. Record and replay a log (D0, no models)

```csharp
using System.Text;
using Vocaterra.Nlp.Asr;

PipelineTrace trace = pipeline.Trace(clip, prompt, context);   // Trace is Evaluate plus the recognizer output
using (var writer = new StreamWriter("session.jsonl", append: true, new UTF8Encoding(false)))
{
    new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));
}

// Later: every entry is re-evaluated from its recorded recognizer output, so no audio is needed. The replay pipeline keeps the
// recording's services (the replayer swaps in only the recognizers), so the model hashes in `versions` match the recording.
var envelopes = ReplayReader.ReadEnvelopes(new StreamReader("session.jsonl", Encoding.UTF8));
var recorded = new ReplayWordRecognizer(new Dictionary<string, AsrResult>(StringComparer.Ordinal));
var replayPipeline = new UtterancePipeline(new PipelineServices(recorded, phonemes, embeddings, synthesizer, bundle));
foreach (ReplayEnvelope envelope in envelopes)
{
    ReplayDiff? diff = Replayer.Replay(envelope, replayPipeline, static hash => new PcmClip([], PcmClip.DefaultSampleRate, hash));
    Console.WriteLine(diff is null ? "OK" : $"DIFF {diff}");
}
```

A replay that prints `DIFF` means the stages, the content or the versions changed since the entry was recorded. A replay needs the same
embedder and synthesizer that produced the log, because those stages run again; with the fakes and the silent synthesizer that needs no model
file. [REPLAY.md](REPLAY.md) lists the envelope fields and what a diff points at.

## 8. Threads and load cost (CR-008)

What the code guarantees, as of this section. Where it gives no guarantee, the safe rule is given.

**Threads.** One `UtterancePipeline` may be called from several threads at once, including one thread in `Evaluate` or `Trace` while
another runs `Synthesize` on the same pipeline. The pipeline has no mutable state: its fields are read-only, the bundle and the lexicon are
immutable after the load, and the parts built on first use (the lexicon's derived indexes, the intent resolver's example vectors) are
published through `Lazy` or `ConcurrentDictionary`, so two first calls build them once. `Evaluate` also runs the word and phoneme
recognizers on thread-pool tasks when `RunRecognizersConcurrently` is set, so a recognizer sees calls on a pool thread, not on the caller's.
The guarantee stops at the injected adapters, which the pipeline does not make thread-safe:

| Adapter | Constructed | Concurrent calls on one instance | Shared across pipelines or threads |
| --- | --- | --- | --- |
| `ScriptedWordRecognizer` (test double) | in code | serialized by a lock | yes |
| `VoskWordRecognizer` | loads the model in the constructor (and a runtime copy on disk) | no lock: each `Recognize` makes its own native recognizer on a shared model, which the code does not guarantee; `Dispose` is not synchronized with `Recognize` | one instance per thread; do not dispose while another thread may recognize |
| `SherpaOnnxOfflineRecognizer` | loads the model in the constructor | `Recognize` and `Dispose` take one lock: calls queue, `Dispose` is safe | yes, with calls serialized |
| `Wav2Vec2PhonemeRecognizer`, `OnnxEmbeddingModel` | ONNX session in the constructor | no lock; the code gives no guarantee (ONNX Runtime documents concurrent `Run` as supported, which this code does not test) | one instance per thread, or serialize the calls |
| `KokoroSynthesizer` | checks the files in the constructor; each `Synthesize` loads its own engine and disposes it (about 1.5 s per short line on the development machine) | `Synthesize` takes one lock: calls queue | yes, with calls serialized |
| `CachedSynthesizer` over `DirectorySynthesisCache` | in code | documented as not safe for concurrent requests of the same key: a miss on both threads can make one `Put` throw (both write the same temporary file) | one thread per key |
| `SilentSynthesizer` | in code | its counter is interlocked | yes |

The rule for a host that runs evaluation and speech on different threads: one pipeline (or one set of services) per process is fine; give
each Vosk, ONNX recognizer and each cached synthesis key one thread of its own, or serialize their calls, and do not dispose a recognizer
while another thread can call it.

**Load cost.** `ContentLoader.Load` reads every file once into memory, checks every hash, parses the catalogs and validates the
cross-references; the imported Wiktionary lexicon (tens of thousands of entries) dominates. Load once per process and share the bundle;
`ContentLoadOptions.LexiconCache` avoids re-parsing the same lexicon files when a host loads several bundles. Building the pipeline is cheap:
the constructor creates the analyzer, the resolver and the response planner, and copies the dialogue pools. Models load where the adapter's
constructor says so: a recognizer or an ONNX model when it is constructed (`FromStore` uses the manifest hash, so it hashes nothing), the
Kokoro engine on every `Synthesize` (no engine is kept between calls: sherpa-onnx's Kokoro keeps state, so a reused engine changes its
output for the same text), and the intent resolver's example vectors on the first open-prompt call that embeds. `PipelineServices`
reads each adapter's `ModelHash` when it is constructed, so Kokoro hashes its files there. Load the models before the first utterance a
player waits for.

**Parakeet word timing (M26g).** `SherpaOnnxOfflineRecognizer` reads the transcript, the tokens and their start times from the native
result, and never the per-token durations: sherpa-onnx 1.13.8 returns a buffer for them on the Parakeet TDT greedy path that is not
float data (it changes on every decode), which made the last word's end time differ between identical decodes. Each word therefore ends
where the next one starts, and the last word ends at the clip end. Recognition results are deterministic for the same clip and model
(the whole `AsrResult` is compared by `ParakeetFromStoreTranscribesTheSpanishSampleClipDeterministically`). The thread count stays 1.

**Immutable after construction.** The `ContentBundle` and all it exposes (catalogs, lexicon, prompts, host file bytes), `UtterancePipeline`
and `PipelineOptions` (init-only), `PipelineServices` (read-only properties; the recognizers it holds are not immutable), `ConversationContext`,
`AgentState` and `LearnerModel` (updates return new values). The adapters are not immutable: they own native handles and counters, and
`Dispose` ends them.

## What is deterministic

| Step | Tier | Notes |
| --- | --- | --- |
| Load a bundle | D0 | Hashes and cross-references are checked; a changed file changes the bundle hash |
| Services with scripted or fake recognizers | D0 | No model file; the verdict is a pure function of the transcript and the context |
| Services with real recognizers, phonemes, embeddings, synthesis | D1 | Frozen models, one CPU thread, greedy decoding; their hashes are in `versions` |
| Pipeline, context | D0 | No clock: the stored timestamp, the seed and the counter are inputs |
| Evaluate, verdict, decision, response plan | D0 over the recognizer output | Same input, byte-identical `CanonicalJson` output |
| Record and replay | D0 | Replays need the recorded recognizer output and the recording's embedder and synthesizer (fakes need no model file); they compare the recorded verdict field by field |
