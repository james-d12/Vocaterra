# The host API

This is what a host calls, in order, to evaluate a learner's utterance and act on the result: load a content bundle, build the services,
build a pipeline, build a conversation context, evaluate an utterance, plan the response, and record and replay a log. The snippets compile
against the renamed projects (`Vocaterra.Nlp.*`); they are the shape of the calls, not a complete program. The boundary they respect is in
[ADR 0009](adr/0009-core-and-game-boundary.md).

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

## 2. Build the services (D0, with optional D1)

```csharp
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Nlp.Embeddings;
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
using Vocaterra.Nlp.Nlp.Embeddings;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;

ModelStore store = ModelStore.FromEnvironment() ?? throw new InvalidOperationException("Set VOCATERRA_MODELS_DIR to a models directory.");
IWordRecognizer words = VoskWordRecognizer.FromStore(store, "vosk-model-small-es-0.42");           // D1, constrained prompts
IPhonemeRecognizer phonemes = new Wav2Vec2PhonemeRecognizer(store.ModelDirectory("wav2vec2-xlsr-53-espeak-cv-ft"));   // D1, optional
IEmbeddingModel embeddings = new OnnxEmbeddingModel(store.ModelDirectory("multilingual-e5-small"));  // D1, open prompts
ISpeechSynthesizer synthesizer = new KokoroSynthesizer(store.ModelDirectory("kokoro-multi-lang-v1_0"), speakerId: 0); // D1, optional
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
    repeatCountsByTag: null);                                   // how often each error tag was repeated in a row
```

`ConversationContext.Create` computes `HostStateHash` over the values it was given. A host with its own state digest uses the constructor
instead and passes that digest.

## 5. Evaluate an utterance and read the verdict (D0 over the recognizer's output)

```csharp
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Verdicts;

PromptSpec prompt = bundle.Prompts["offer-items-open"];
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

## 6. Plan the response (D0, with D1 for speech)

`Evaluate` also plans the response: the agent's lines, the effects the verdict triggers and an optional repair step.

```csharp
using Vocaterra.Nlp.Nlp.Intent;
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

The effect values come from the bundle's curves at the verdict's correctness; the core does not know what a value is worth. The host maps
`trade_price` or `spell_power` (or any other kind) to its own rules.

Speech is a separate call, so a host that shows text does not pay for audio. When the services have a synthesizer (D1 with Kokoro, D0 with
the silent stand-in), `Synthesize` renders every line of the plan in order:

```csharp
IReadOnlyList<AudioBuffer> speech = pipeline.Synthesize(result.Response, "agent-voice");
```

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

## What is deterministic

| Step | Tier | Notes |
| --- | --- | --- |
| Load a bundle | D0 | Hashes and cross-references are checked; a changed file changes the bundle hash |
| Services with scripted or fake recognizers | D0 | No model file; the verdict is a pure function of the transcript and the context |
| Services with real recognizers, phonemes, embeddings, synthesis | D1 | Frozen models, one CPU thread, greedy decoding; their hashes are in `versions` |
| Pipeline, context | D0 | No clock: the stored timestamp, the seed and the counter are inputs |
| Evaluate, verdict, decision, response plan | D0 over the recognizer output | Same input, byte-identical `CanonicalJson` output |
| Record and replay | D0 | Replays need the recorded recognizer output and the recording's embedder and synthesizer (fakes need no model file); they compare the recorded verdict field by field |
