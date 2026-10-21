using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

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

    /// <summary>The host event every state of the blacksmith in <see cref="HostTurnBundle"/> handles.</summary>
    public const string HostTurnEvent = "fuzz.ask";

    private static readonly Lazy<ContentBundle> HostTurnBundleLazy = new(BuildHostTurnBundle);

    /// <summary>
    /// A copy of <see cref="Bundle"/> (same bundle hash; hash verification is off because the copy is edited) whose blacksmith has, in every state, a
    /// host-event transition on <see cref="HostTurnEvent"/> to the next state in declaration order, voicing two plain dialogue pools. Utterances behave
    /// exactly as in <see cref="Bundle"/>, because a verdict never takes a host-event transition.
    /// </summary>
    public static ContentBundle HostTurnBundle => HostTurnBundleLazy.Value;

    /// <summary>Creates a scripted pipeline service set: the recognizers return the transcript, the other models are the fakes.</summary>
    public PipelineServices NewServices(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, bool embeddings = true, ContentBundle? bundle = null)
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
            bundle ?? Bundle);
    }

    /// <summary>Creates a pipeline over <see cref="NewServices"/>.</summary>
    public UtterancePipeline NewPipeline(string transcript, RecognitionMode mode = RecognitionMode.Open, bool phonemes = false, PipelineOptions? options = null)
    {
        return new UtterancePipeline(NewServices(transcript, mode, phonemes), options);
    }

    /// <summary>Creates a conversation context for an agent of the bundle in one of its states.</summary>
    public ConversationContext NewContext(string agentId, string stateId, long counter = 0, ulong worldSeed = 42, IReadOnlyDictionary<string, int>? repeats = null, SlotMap? hostSlots = null, IReadOnlyList<string>? extraSkills = null)
    {
        return ConversationContext.Create(
            worldSeed,
            agentId,
            new AgentState(agentId, stateId, counter),
            new StoredTimestamp(FixedNow),
            LearnerModel.New(),
            repeats,
            hostSlots,
            extraSkills);
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

    private static ContentBundle BuildHostTurnBundle()
    {
        var directory = Directory.CreateTempSubdirectory("vocaterra-fuzz-hostturn-");
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                directory.Delete(recursive: true);
            }
            catch (IOException)
            {
            }
        };
        var source = Path.Combine(AppContext.BaseDirectory, "content", "v1");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(directory.FullName, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        var path = Path.Combine(directory.FullName, "agents", "blacksmith.json");
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var states = root["states"]!.AsArray();
        var ids = states.Select(static state => state!["id"]!.GetValue<string>()).ToArray();
        for (var i = 0; i < states.Count; i++)
        {
            var transition = System.Text.Json.Nodes.JsonNode.Parse(
                "{\"onHostEvent\":\"" + HostTurnEvent + "\",\"to\":\"" + ids[(i + 1) % ids.Length] + "\",\"actions\":["
                + "{\"dialogueIntent\":\"greet_reply\",\"hostEvent\":\"fuzz.said\",\"variantPoolId\":\"greet_reply\"},"
                + "{\"dialogueIntent\":\"thank_reply\"}]}");
            states[i]!["transitions"]!.AsArray().Insert(0, transition);
        }

        File.WriteAllText(path, root.ToJsonString());
        return ContentLoader.Load(directory.FullName, new ContentLoadOptions { VerifyHashes = false });
    }
}
