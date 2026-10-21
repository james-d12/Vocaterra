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
    /// <summary>The open-mode offer prompt of the blacksmith, which the HLD worked example answers.</summary>
    public const string OfferPrompt = "offer-items-open";

    /// <summary>The worked example from the high-level design.</summary>
    public const string WorkedExample = "yo tiene dos espada";

    /// <summary>The committed bundle, copied next to the test assembly by the project file.</summary>
    public static string BundleDir { get; } = Path.Combine(AppContext.BaseDirectory, "content", "v1");

    /// <summary>The model manifest, copied next to the test assembly by the project file.</summary>
    public static string ModelManifest { get; } = Path.Combine(AppContext.BaseDirectory, "models", "manifest.json");

    /// <summary>The sample bundle, loaded once with hash verification.</summary>
    public static ContentBundle Bundle { get; } = ContentLoader.Load(BundleDir);

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

    /// <summary>A fixed conversation context for the blacksmith; the default state <c>trading</c> is the one that handles offers.</summary>
    public static ConversationContext Context(
        string state = "trading",
        long counter = 0,
        ulong seed = 42,
        string agentId = "blacksmith",
        IReadOnlyDictionary<string, int>? repeats = null)
    {
        return ConversationContext.Create(seed, agentId, new AgentState(agentId, state, counter), new StoredTimestamp(1_700_000_000), LearnerModel.New(), repeats);
    }

    /// <summary>A fake phoneme recognizer that hears exactly the phones of the text (every pronunciation correct).</summary>
    public static FakePhonemeRecognizer HearsCorrectly(string text, IReadOnlyDictionary<string, string>? confusions = null)
    {
        var fake = FakePhonemeRecognizer.ForSpanish();
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
        IPhonemeRecognizer? phonemeRecognizer = null)
    {
        return new PipelineServices(
            new ScriptedWordRecognizer(Asr(transcript, mode)),
            phonemeRecognizer ?? (phonemes ? HearsCorrectly(transcript) : null),
            embeddings ? new FakeEmbeddingModel() : null,
            synthesizer ? new SilentSynthesizer() : null,
            Bundle);
    }

    /// <summary>A pipeline over scripted services.</summary>
    public static UtterancePipeline Pipeline(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, PipelineOptions? options = null)
    {
        return new UtterancePipeline(Services(transcript, mode, phonemes), options);
    }

    /// <summary>The canonical JSON of any value.</summary>
    public static string Json<T>(T value)
    {
        return CanonicalJson.Serialize(value);
    }
}
