using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Locations and builders shared by the pipeline tests.</summary>
internal static class TestSupport
{
    /// <summary>The open-mode prompt of the villager in <c>content/game-v1</c>; the worked example below answers it.</summary>
    public const string OpenPrompt = "villager-open";

    /// <summary>
    /// The design document's worked example in the words of the bundle: a wrong verb form and a noun left singular after a number, said to an open
    /// prompt that allows <c>request_item</c> (item and quantity slots). The document's own sentence, <see cref="HldWorkedExample"/>, runs on a fixture.
    /// </summary>
    public const string WorkedExample = "yo quiere dos espada";

    /// <summary>The worked example from the high-level design. No prompt of the game answers <c>tener</c> openly, so it runs with <see cref="HldPrompt"/>.</summary>
    public const string HldWorkedExample = "yo tiene dos espada";

    /// <summary>The open prompt for the design document's sentence: <c>state_have</c> (the verb <c>tener</c> with an item and a quantity), the villager, any place word as answer set.</summary>
    public static PromptSpec HldPrompt { get; } = new(
        "hld-offer-open", RecognitionMode.Open, Difficulty.Beginner, "places-basic", ["state_have"], "villager", [], "¿Qué tienes?");

    /// <summary>
    /// An open prompt with no agent that allows <c>request_item</c> and <c>state_have</c>: the player talks to no one, so the pipeline voices only feedback
    /// and the answer-accepted acknowledgement. The game's prompts all name an agent, so this one is built here. A prompt is data passed to the pipeline, so
    /// nothing is added to the bundle.
    /// </summary>
    public static PromptSpec AgentlessOpenPrompt { get; } = new(
        "fx-agentless-open", RecognitionMode.Open, Difficulty.Beginner, "supplies", ["request_item", "state_have"], null, [], "¿Qué necesitas?");

    /// <summary>A quiz prompt: constrained to the place names, no intents allowed, no agent. The answer alone is graded.</summary>
    public static PromptSpec QuizPrompt { get; } = new(
        "fx-quiz", RecognitionMode.Constrained, Difficulty.Beginner, "places-basic", [], null, [], "¿Qué es esto?");

    /// <summary>
    /// The effect table the fixture bundle carries, because the game's own table is empty (it derives its effects from its host files). The kinds are made up;
    /// the core treats them as opaque names. <c>state_have</c> has a three-point curve, <c>request_item</c> a two-point one.
    /// </summary>
    public const string EffectsJson =
        """
        {
          "effects": [
            {
              "effectId": "fx_item_quality",
              "intent": "request_item",
              "kind": "item_quality",
              "curve": [ { "correctness": 0, "value": 0.5 }, { "correctness": 1, "value": 1 } ],
              "requiresCommunicated": true
            },
            {
              "effectId": "fx_have_power",
              "intent": "state_have",
              "kind": "have_power",
              "curve": [ { "correctness": 0, "value": 0 }, { "correctness": 0.5, "value": 0.5 }, { "correctness": 1, "value": 1 } ],
              "requiresCommunicated": true
            }
          ]
        }
        """;

    /// <summary>The repository's one content bundle, <c>content/game-v1</c>, found by walking up from the test assembly (not copied to the output).</summary>
    public static string BundleDir { get; } = GameBundleLocator.Directory;

    /// <summary>The model manifest, copied next to the test assembly by the project file.</summary>
    public static string ModelManifest { get; } = Path.Combine(AppContext.BaseDirectory, "models", "manifest.json");

    /// <summary>The bundle, loaded once with hash verification.</summary>
    public static ContentBundle Bundle { get; } = SpanishContent.Load(BundleDir);

    /// <summary>A one-second 440 Hz tone; the scripted recognizers ignore its content, only its hash matters.</summary>
    public static PcmClip Clip(double frequency = 440.0)
    {
        var samples = new short[PcmClip.DefaultSampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(8000.0 * Math.Sin(2.0 * Math.PI * frequency * i / PcmClip.DefaultSampleRate));
        }

        return PcmClip.FromSamples(samples);
    }

    /// <summary>A recognition result for a transcript, with confidence 1.</summary>
    public static AsrResult Asr(string transcript, RecognitionMode mode = RecognitionMode.Open, string modelHash = "scripted-test")
    {
        return new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, modelHash, mode);
    }

    /// <summary>A fixed conversation context for the villager; its start state <c>idle</c> answers a request.</summary>
    public static ConversationContext Context(
        string state = "idle",
        long counter = 0,
        ulong seed = 42,
        string agentId = "villager",
        IReadOnlyDictionary<string, int>? repeats = null)
    {
        return ConversationContext.Create(seed, agentId, new AgentState(agentId, state, counter), new StoredTimestamp(1_700_000_000), LearnerModel.New(), repeats);
    }

    /// <summary>A fake phoneme recognizer that hears exactly the phones of the text (every pronunciation correct).</summary>
    public static FakePhonemeRecognizer HearsCorrectly(string text, IReadOnlyDictionary<string, string>? confusions = null)
    {
        var fake = FakePhonemeRecognizer.ForPhoneInventory();
        var phones = SpanishG2p.Phonemize(text, DialectConfig.LatinAmericanNeutral).Phones;
        return fake.Returns(fake.FromPhones(phones, confusions: confusions));
    }

    /// <summary>Services around a scripted word recognizer; the optional models are included on request.</summary>
    public static PipelineServices Services(
        string transcript,
        RecognitionMode mode = RecognitionMode.Open,
        bool phonemes = false,
        bool embeddings = false,
        bool synthesizer = false,
        IPhonemeRecognizer? phonemeRecognizer = null,
        ContentBundle? bundle = null)
    {
        return new PipelineServices(
            new ScriptedWordRecognizer(Asr(transcript, mode)),
            phonemeRecognizer ?? (phonemes ? HearsCorrectly(transcript) : null),
            embeddings ? new FakeEmbeddingModel() : null,
            synthesizer ? new SilentSynthesizer() : null,
            bundle ?? Bundle);
    }

    /// <summary>A pipeline over scripted services.</summary>
    public static UtterancePipeline Pipeline(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, PipelineOptions? options = null, ContentBundle? bundle = null)
    {
        return new UtterancePipeline(Services(transcript, mode, phonemes, bundle: bundle), options);
    }

    /// <summary>
    /// The bundle plus <see cref="EffectsJson"/>: a copy of <c>content/game-v1</c> built for the process, in a temporary folder that is deleted at once (the
    /// copy has no speech clips), with the effect table written into it. Hashes are not verified because the copy was edited. No second bundle is kept on disk.
    /// </summary>
    public static ContentBundle EffectsBundle { get; } = BuildEffectsBundle();

    /// <summary>
    /// The bundle plus one intent, <c>introduce_self</c> (<c>me llamo [WORD:name]</c>), because no intent of the game binds a free name, and the core's
    /// <c>[WORD]</c> element keeps its coverage here. Built like <see cref="EffectsBundle"/>; the open prompt <see cref="IntroductionPrompt"/> allows it.
    /// </summary>
    public static ContentBundle NameBundle { get; } = BuildNameBundle();

    /// <summary>The open prompt for <c>introduce_self</c> of <see cref="NameBundle"/>.</summary>
    public static PromptSpec IntroductionPrompt { get; } = new(
        "fx-introduce-open", RecognitionMode.Open, Difficulty.Beginner, "places-basic", ["introduce_self"], null, [], "¿Cómo te llamas?");

    /// <summary>
    /// A writable copy of the bundle for the command-line tests, which take a bundle folder: <c>effects/effects.json</c> is <see cref="EffectsJson"/> and
    /// <see cref="QuizPrompt"/> is one of the prompts. Rehashed, so the command line (which verifies hashes) loads it; the caller disposes it.
    /// </summary>
    public static TempBundle CliBundle()
    {
        var copy = new TempBundle();
        copy.Write("effects/effects.json", EffectsJson);
        AddPrompt(copy, QuizPrompt);
        copy.Rehash();
        return copy;
    }

    /// <summary>Appends a prompt to the copy's <c>prompts/prompts.json</c> in the file's own format.</summary>
    public static void AddPrompt(TempBundle copy, PromptSpec prompt)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        copy.EditJson("prompts/prompts.json", root => root.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(prompt, options))));
    }

    private static ContentBundle BuildNameBundle()
    {
        using var copy = new TempBundle(rehash: false);
        copy.EditJson(
            "intents/intents.json",
            root => root["intents"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(
                "{\"intentId\":\"introduce_self\",\"requiredSlots\":[\"name\"],\"slotGrammars\":[\"me [VERB:llamar] [WORD:name]\"],\"examples\":[\"me llamo Rosa\"]}")));
        return copy.Load();
    }

    private static ContentBundle BuildEffectsBundle()
    {
        using var copy = new TempBundle(rehash: false);
        copy.Write("effects/effects.json", EffectsJson);
        return copy.Load();
    }

    /// <summary>The canonical JSON of any value.</summary>
    public static string Json<T>(T value)
    {
        return CanonicalJson.Serialize(value);
    }
}
