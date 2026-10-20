using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Nlp.Analysis;
using Vocaterra.Nlp.Nlp.Embeddings;
using Vocaterra.Nlp.Nlp.Intent;
using Vocaterra.Nlp.Nlp.Matching;
using Vocaterra.Nlp.Nlp.Morphology;
using Vocaterra.Nlp.Nlp.Normalization;
using Vocaterra.Nlp.Nlp.SlotGrammar;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Nlp.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// The committed <c>content/v1</c> bundle and the stage objects wired once for every oracle, with the D1 models replaced by fakes
/// (scripted recognizers, trigram embedder, silent synthesizer) so no model file is needed. Stage 2 oracles build on the same members:
/// <see cref="Bundle"/>, <see cref="NewServices"/>, <see cref="NewPipeline"/>, <see cref="NewContext"/> and <see cref="Prompt"/>.
/// </summary>
public sealed class PipelineHarness
{
    /// <summary>The fixed instant every generated conversation context uses; nothing in the suite reads a clock.</summary>
    public const long FixedNow = 1_700_000_000;

    private PipelineHarness()
    {
        BundleDirectory = Path.Combine(AppContext.BaseDirectory, "content", "v1");
        Bundle = ContentLoader.Load(BundleDirectory);
        Words = new LexiconCatalog(Bundle.Lexicon, Bundle.Morphology);
        Analyzer = new LinguisticAnalyzer(Bundle.Lexicon, Bundle.Morphology);
        Grammars = PatternCatalog.FromBundle(Bundle.Intents);
    }

    /// <summary>The shared harness, built once on first use.</summary>
    public static PipelineHarness Shared { get; } = new();

    /// <summary>The indefinite articles stay articles in the pipeline, so every oracle normalizes with this option.</summary>
    public static NormalizationOptions Normalization { get; } = new(ConvertIndefiniteArticles: false);

    /// <summary>The directory the bundle was loaded from.</summary>
    public string BundleDirectory { get; }

    /// <summary>The loaded, hash-verified content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The bundle lexicon (the embedded seed plus the sample words).</summary>
    public NlpLexicon Lexicon => Bundle.Lexicon;

    /// <summary>The bundle's morphology generator.</summary>
    public MorphologyGenerator Morphology => Bundle.Morphology;

    /// <summary>
    /// A morphology generator over an empty lexicon: it can only apply the regular -ar/-er/-ir paradigm, which is how the suite obtains the
    /// regularized form of an irregular verb cell without reaching into internals.
    /// </summary>
    public MorphologyGenerator RegularParadigm { get; } = new(NlpLexicon.Empty);

    /// <summary>The lexicon seen through the generators' eyes.</summary>
    public LexiconCatalog Words { get; }

    /// <summary>A shared analyzer over the bundle lexicon; stateless apart from a lazily built, thread-safe index.</summary>
    public LinguisticAnalyzer Analyzer { get; }

    /// <summary>Every slot grammar of the intent catalog as a parseable pattern plus its compiled <see cref="SlotGrammar"/>.</summary>
    public PatternCatalog Grammars { get; }

    /// <summary>Normalizes text exactly as the pipeline does.</summary>
    public static NormalizedText Normalize(string text)
    {
        return TextNormalizer.Normalize(text, Normalization);
    }

    /// <summary>Creates a fresh analyzer with its own morphology generator, for oracles that compare two independent runs.</summary>
    public LinguisticAnalyzer NewAnalyzer()
    {
        return new LinguisticAnalyzer(Lexicon, new MorphologyGenerator(Lexicon));
    }

    /// <summary>Creates a fresh intent resolver over the bundle catalog, with or without the trigram embedder.</summary>
    public IntentResolver NewResolver(bool embeddings)
    {
        return new IntentResolver(Bundle.Intents, embeddings ? new FakeEmbeddingModel() : null);
    }

    /// <summary>Returns the bundle prompt with the id (throws when the bundle has none).</summary>
    public PromptSpec Prompt(string promptId)
    {
        return Bundle.Prompts[promptId];
    }

    /// <summary>Creates a scripted pipeline service set: the recognizers return the transcript, the other models are the fakes.</summary>
    public PipelineServices NewServices(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, bool embeddings = true)
    {
        var asr = new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, "scripted-fuzz", mode);
        IPhonemeRecognizer? phoneRecognizer = null;
        if (phonemes)
        {
            var fake = FakePhonemeRecognizer.ForSpanish();

            // The fake hears exactly the phones of the transcript; a transcript with no letters still gets some audio-independent phones.
            var spoken = SpanishNumbers.DigitsToWords(Normalize(transcript).Text);
            phoneRecognizer = fake.Returns(fake.FromPhones(SpanishG2p.Phonemize(spoken.Length == 0 ? "hola" : spoken, DialectConfig.LatinAmericanNeutral).Phones));
        }

        return new PipelineServices(
            new ScriptedWordRecognizer(asr),
            phoneRecognizer,
            embeddings ? new FakeEmbeddingModel() : null,
            new SilentSynthesizer(),
            Bundle);
    }

    /// <summary>Creates a pipeline over <see cref="NewServices"/>.</summary>
    public UtterancePipeline NewPipeline(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, PipelineOptions? options = null)
    {
        return new UtterancePipeline(NewServices(transcript, mode, phonemes), options);
    }

    /// <summary>Creates a conversation context for an agent of the bundle in one of its states.</summary>
    public ConversationContext NewContext(string agentId, string stateId, long counter = 0, ulong worldSeed = 42, IReadOnlyDictionary<string, int>? repeats = null)
    {
        return ConversationContext.Create(
            worldSeed,
            agentId,
            new AgentState(agentId, stateId, counter),
            new StoredTimestamp(FixedNow),
            LearnerModel.New(),
            repeats);
    }

    /// <summary>A one-second tone; the scripted recognizers ignore the audio, only its hash matters.</summary>
    public static PcmClip Clip(double frequency = 440.0)
    {
        var samples = new short[PcmClip.DefaultSampleRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(8000.0 * Math.Sin(2.0 * Math.PI * frequency * i / PcmClip.DefaultSampleRate));
        }

        return PcmClip.FromSamples(samples);
    }

    /// <summary>The answer sets of the bundle in ordinal id order.</summary>
    public IReadOnlyList<AnswerSet> AnswerSets => [.. Bundle.AnswerSets.Values];
}
