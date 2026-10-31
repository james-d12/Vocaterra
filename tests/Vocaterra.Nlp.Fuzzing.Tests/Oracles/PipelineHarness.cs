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
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Speech;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// The repository's one content bundle, <c>content/game-v1</c>, and the stage objects wired once for every oracle, with the D1 models replaced by fakes
/// (scripted recognizers, trigram embedder, silent synthesizer) so no model file is needed. Stage 2 oracles build on the same members:
/// <see cref="Bundle"/>, <see cref="NewServices"/>, <see cref="NewPipeline"/>, <see cref="NewContext"/> and <see cref="Prompt"/>.
/// </summary>
public sealed class PipelineHarness : IGeneratorInputs
{
    /// <summary>The fixed instant every generated conversation context uses; nothing in the suite reads a clock.</summary>
    public const long FixedNow = 1_700_000_000;

    private PipelineHarness()
    {
        BundleDirectory = GameBundleLocator.Directory;
        Bundle = SpanishContent.Load(BundleDirectory);
        Words = new LexiconCatalog(Bundle.Lexicon, Bundle.SpanishMorphology());
        Analyzer = new LinguisticAnalyzer(Bundle.Lexicon, Bundle.SpanishMorphology(), SpanishLanguagePack.Instance);
        Grammars = PatternCatalog.FromBundle(Bundle.Intents, FixtureGrammars);
    }

    /// <summary>
    /// Grammar patterns the game's bundle does not contain, kept as in-memory fixtures so the oracles that need them keep running: names bound by <c>[WORD]</c>
    /// (the bundle binds no free name), an adjective after a noun, and a verb whose lemma is bound to a slot.
    /// </summary>
    public static IReadOnlyList<string> FixtureGrammars { get; } =
    [
        "me [VERB:llamar] [WORD:name]",
        "mi nombre es [WORD:name]",
        "[VERB:ser] [WORD:name]",
        "[VERB:querer]? [ART]? [NOUN:thing] [ADJ]?",
        "[PRON]? [VERB:tener:verb] [NUM:qty] [NOUN:item]",
    ];

    /// <summary>The shared harness, built once on first use.</summary>
    public static PipelineHarness Shared { get; } = new();

    /// <summary>The indefinite articles stay articles in the pipeline, so every oracle normalizes with this option.</summary>
    public static NormalizationOptions Normalization { get; } = new(ConvertIndefiniteArticles: false);

    /// <summary>The directory the bundle was loaded from (found by walking up from the test assembly; the bundle is not copied to the output).</summary>
    public string BundleDirectory { get; }

    /// <summary>The loaded, hash-verified content bundle.</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The bundle lexicon (the embedded seed plus the bundle's own words).</summary>
    public NlpLexicon Lexicon => Bundle.Lexicon;

    /// <summary>The bundle's morphology generator.</summary>
    public MorphologyGenerator Morphology => Bundle.SpanishMorphology();

    /// <summary>
    /// A morphology generator over an empty lexicon: it can only apply the regular -ar/-er/-ir paradigm, which is how the suite obtains the
    /// regularized form of an irregular verb cell without reaching into internals.
    /// </summary>
    public MorphologyGenerator RegularParadigm { get; } = new(SpanishLexicon.Empty);

    /// <inheritdoc />
    public LexiconCatalog Words { get; }

    /// <summary>A shared analyzer over the bundle lexicon; stateless apart from a lazily built, thread-safe index.</summary>
    public LinguisticAnalyzer Analyzer { get; }

    /// <summary>Every slot grammar of the intent catalog as a parseable pattern plus its compiled <see cref="SlotGrammar"/>, then the <see cref="FixtureGrammars"/>.</summary>
    public PatternCatalog Grammars { get; }

    /// <summary>Normalizes text exactly as the pipeline does.</summary>
    public static NormalizedText Normalize(string text)
    {
        return SpanishTextNormalizer.Normalize(text, Normalization);
    }

    /// <summary>Creates a fresh analyzer with its own morphology generator, for oracles that compare two independent runs.</summary>
    public LinguisticAnalyzer NewAnalyzer()
    {
        return new LinguisticAnalyzer(Lexicon, new MorphologyGenerator(Lexicon), SpanishLanguagePack.Instance);
    }

    /// <summary>Creates a fresh intent resolver over the bundle catalog, with or without the trigram embedder.</summary>
    public IntentResolver NewResolver(bool embeddings)
    {
        return new IntentResolver(Bundle.Intents, SpanishLanguagePack.Instance, embeddings ? new FakeEmbeddingModel() : null);
    }

    /// <summary>Returns the bundle prompt with the id (throws when the bundle has none).</summary>
    public PromptSpec Prompt(string promptId)
    {
        return Bundle.Prompts[promptId];
    }

    /// <summary>The host event every state of every agent in <see cref="HostTurnBundle"/> handles.</summary>
    public const string HostTurnEvent = "fuzz.ask";

    private static readonly Lazy<ContentBundle> HostTurnBundleLazy = new(BuildHostTurnBundle);

    /// <summary>
    /// A copy of <see cref="EffectsBundle"/> (same bundle hash; hash verification is off because the copy is edited) whose agents have, in every state, a
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
            var fake = FakePhonemeRecognizer.ForPhoneInventory();

            // The fake hears exactly the phones of the transcript; a transcript with no letters still gets some audio-independent phones.
            var spoken = SpanishNumbers.DigitsToWords(Normalize(transcript).Text);
            phoneRecognizer = fake.Returns(fake.FromPhones(SpanishG2p.Phonemize(spoken.Length == 0 ? "hola" : spoken, DialectConfig.LatinAmericanNeutral).Phones));
        }

        return new PipelineServices(
            new ScriptedWordRecognizer(asr),
            phoneRecognizer,
            embeddings ? new FakeEmbeddingModel() : null,
            new SilentSynthesizer(),
            bundle ?? EffectsBundle);
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

    /// <summary>
    /// The effect table the pipeline oracles run with, because the game's own table is empty (it derives its effects from its host files). The kinds are
    /// made up; the core treats them as opaque names. Without it no pipeline run would ever resolve an effect, and the replay oracle's effects region
    /// (<c>ChangingOneRecordedOutput_MakesTheReplayerNameThatPath</c>) would have nothing to mutate.
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

    private static readonly Lazy<ContentBundle> EffectsBundleLazy = new(() => BuildCopy(copy => copy.Write("effects/effects.json", EffectsJson)));

    /// <summary>
    /// A copy of <see cref="Bundle"/> (same bundle hash; hash verification is off because the copy is edited) that carries <see cref="EffectsJson"/>: the
    /// effects fixture. <see cref="NewServices"/> uses it unless a bundle is given, so the pipeline oracles see the game's content plus an effect table.
    /// </summary>
    public static ContentBundle EffectsBundle => EffectsBundleLazy.Value;

    // The full copy, with the imported lexicon: the generators draw their words from Bundle, so the pipeline must know every one of them. On the
    // light copy most correct sentences of the game's grammars (a noun such as "cuchillo" or "carretera") resolved to no intent, and the replay
    // test's effects region got 0 to 6 mutations in 300 iterations instead of about 25 (it failed on CI with 0).
    private static ContentBundle BuildCopy(Action<TempBundle> edit)
    {
        using var copy = new TempBundle(full: true, rehash: false);
        edit(copy);
        return copy.Load();
    }

    private static ContentBundle BuildHostTurnBundle()
    {
        return BuildCopy(copy =>
        {
            copy.Write("effects/effects.json", EffectsJson);

            // Two plain dialogue pools for the host turn to voice (the game's own pools are the agents' replies).
            copy.EditJson("dialogue/dialogue.json", root =>
            {
                var pools = root["pools"]!.AsObject();
                pools["fuzz_said"] = System.Text.Json.Nodes.JsonNode.Parse("{\"poolId\":\"fuzz_said\",\"variants\":[{\"lineId\":\"fuzz_said.1\",\"text\":\"Mira.\"},{\"lineId\":\"fuzz_said.2\",\"text\":\"Oye.\"}]}");
                pools["fuzz_plain"] = System.Text.Json.Nodes.JsonNode.Parse("{\"poolId\":\"fuzz_plain\",\"variants\":[{\"lineId\":\"fuzz_plain.1\",\"text\":\"Hola.\"}]}");
            });

            // Every agent of the bundle gets, in every state, a host-event transition on the event to the next state in declaration order.
            foreach (var file in Directory.EnumerateFiles(copy.PathOf("agents"), "*.json").Select(path => "agents/" + Path.GetFileName(path)))
            {
                copy.EditJson(file, node =>
                {
                    var root = node.AsObject();
                    root["variantPools"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"fuzz_said\",\"count\":2}"));
                    var states = root["states"]!.AsArray();
                    var ids = states.Select(static state => state!["id"]!.GetValue<string>()).ToArray();
                    for (var i = 0; i < states.Count; i++)
                    {
                        var transition = System.Text.Json.Nodes.JsonNode.Parse(
                            "{\"onHostEvent\":\"" + HostTurnEvent + "\",\"to\":\"" + ids[(i + 1) % ids.Length] + "\",\"actions\":["
                            + "{\"dialogueIntent\":\"fuzz_said\",\"hostEvent\":\"fuzz.said\",\"variantPoolId\":\"fuzz_said\"},"
                            + "{\"dialogueIntent\":\"fuzz_plain\"}]}");
                        states[i]!["transitions"]!.AsArray().Insert(0, transition);
                    }
                });
            }
        });
    }
}
