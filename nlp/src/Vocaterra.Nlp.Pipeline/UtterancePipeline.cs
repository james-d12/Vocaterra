using System.Runtime.ExceptionServices;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Replay;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pronunciation;
using Vocaterra.Nlp.Response.Effects;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// Runs one recorded utterance through every stage in a fixed order: recognition, normalization, answer matching, linguistic analysis,
/// intent resolution, pronunciation scoring, verdict assembly, the decision layer and response realization.
/// </summary>
/// <remarks>
/// Two utterances with the same clip, prompt, context and services give <c>CanonicalJson</c>-identical results. The word and phoneme recognizers may
/// run on separate tasks, but their results are consumed in a fixed order and the stages share no mutable state. Indefinite articles are kept
/// during normalization (<c>una espada</c> stays <c>una espada</c>) because agreement checks and <c>[ART]</c> grammar tokens need them.
/// The pipeline keeps no mutable state of its own between utterances, so one instance may serve many utterances; whether it may be shared
/// across threads then depends only on the injected recognizers.
/// </remarks>
public sealed partial class UtterancePipeline
{
    private static readonly NormalizationOptions Normalization = new(ConvertIndefiniteArticles: false);

    // What the intent stage reports for a prompt that does not ask for an intent.
    private static readonly IntentResolution NoIntent = new(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0);

    private readonly PipelineServices _services;
    private readonly PipelineOptions _options;
    private readonly LinguisticAnalyzer _analyzer;
    private readonly IntentResolver _resolver;
    private readonly ResponsePlanner _planner;
    private readonly PronunciationScorer _scorer;
    private readonly DialogueCatalog _catalogWithSilence;

    /// <summary>Creates a pipeline over the services; the options default to <see cref="PipelineOptions.Default"/>.</summary>
    public UtterancePipeline(PipelineServices services, PipelineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _options = options ?? PipelineOptions.Default;
        var content = services.Content;
        _analyzer = new LinguisticAnalyzer(content.Lexicon, content.Morphology);
        _resolver = new IntentResolver(content.Intents, services.Embeddings);
        _planner = new ResponsePlanner(content.TemplateEngine);
        _scorer = new PronunciationScorer();
        _catalogWithSilence = WithSilentPool(content.Dialogue);
    }

    /// <summary>The services this pipeline runs on.</summary>
    public PipelineServices Services => _services;

    /// <summary>The options this pipeline runs with.</summary>
    public PipelineOptions Options => _options;

    /// <summary>Evaluates one utterance; see <see cref="Trace"/> for the variant that also returns the recognizer outputs.</summary>
    public PipelineResult Evaluate(PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        return Trace(clip, prompt, context).Result;
    }

    /// <summary>Evaluates one utterance and also returns the recognizer outputs, which is what a replay log needs.</summary>
    /// <exception cref="ArgumentException">The prompt names an answer set the bundle does not have, or the agent state belongs to another agent.</exception>
    public PipelineTrace Trace(PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);

        var answerSet = AnswerSetFor(prompt);
        var request = BuildRequest(prompt, answerSet);
        var (asr, posteriors) = Recognize(clip, request);

        var normalized = TextNormalizer.Normalize(CleanTranscript(asr.Transcript), Normalization);
        var match = AnswerMatcher.Match(normalized, answerSet, prompt.Difficulty, Normalization);
        var analysis = AnswerCorrections.WithCanonicalWords(
            _analyzer.Analyze(normalized, GrammarsFor(prompt)),
            normalized,
            match,
            answerSet,
            Normalization);
        var intent = IsAnswerOnly(prompt) ? NoIntent : _resolver.Resolve(normalized, analysis, prompt);
        var pronunciation = ScorePronunciation(normalized, posteriors, prompt);

        var content = _services.Content;
        var verdict = VerdictAssembler.Assemble(new VerdictInputs(
            prompt,
            asr,
            match,
            analysis,
            intent,
            pronunciation,
            content.Intents.RequiredSlots(intent.Intent),
            KnownWords(analysis),
            content.EvaluationConfig,
            _services.Versions,
            CanonicalAnswersOf(answerSet)));

        // The context carries the counts before this utterance; the counts after it are what the feedback policy and the repair ladder read.
        var counts = RepeatCounts.Next(context.RepeatCountsByTag, verdict);
        var decision = Decide(verdict, prompt, context, counts);
        var response = Respond(verdict, prompt, decision, normalized, analysis, match, context);
        var effects = EffectResolver.Resolve(verdict, content.Effects);
        var repair = RepairFor(verdict, prompt, answerSet, counts);
        var replay = new ReplayRecord(clip.ClipHash, prompt.PromptId, context.HostStateHash, _services.Versions, verdict);
        return new PipelineTrace(new PipelineResult(verdict, decision, response, effects, repair, replay), asr, posteriors);
    }

    /// <summary>Synthesizes every line of a response with the pipeline's speech synthesizer, in line order.</summary>
    /// <exception cref="InvalidOperationException">The pipeline has no speech synthesizer.</exception>
    public IReadOnlyList<AudioBuffer> Synthesize(ResponsePlan plan, string voiceId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(voiceId);
        var synthesizer = _services.Synthesizer ?? throw new InvalidOperationException("The pipeline has no speech synthesizer.");
        var buffers = new List<AudioBuffer>(plan.Lines.Count);
        foreach (var line in plan.Lines)
        {
            buffers.Add(synthesizer.Synthesize(SpeechRequests.Create(line.Text, voiceId)));
        }

        return buffers;
    }

    private static string CleanTranscript(string transcript)
    {
        return transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal);
    }

    private static List<string> KnownWords(AnalysisResult analysis)
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in analysis.Tokens)
        {
            if (token.Readings.Count > 0)
            {
                words.Add(token.Token.Text);
            }
        }

        return [.. words];
    }

    // The answer set's canonical answers; a derived vocab.wrong names its word only when there is exactly one.
    private static IReadOnlyList<string> CanonicalAnswersOf(AnswerSet answerSet)
    {
        return [.. answerSet.Answers.Select(static answer => answer.Canonical)];
    }

    private AnswerSet AnswerSetFor(PromptSpec prompt)
    {
        return _services.Content.AnswerSets.TryGetValue(prompt.AnswerSetId, out var set)
            ? set
            : throw new ArgumentException($"Prompt '{prompt.PromptId}' names unknown answer set '{prompt.AnswerSetId}'.", nameof(prompt));
    }

    private RecognitionRequest BuildRequest(PromptSpec prompt, AnswerSet answerSet)
    {
        return prompt.Mode == RecognitionMode.Constrained
            ? RecognitionRequest.Constrained(GrammarBuilder.BuildList(answerSet.GrammarPhrases()), _options.MaxAlternatives)
            : RecognitionRequest.Open(_options.MaxAlternatives);
    }

    // A prompt that allows no intent (a quiz) only asks for an answer: the answer match is its resolution, so no intent grammar may claim the
    // utterance (a bare noun would otherwise fit a verb-optional grammar such as cast_spell) and the intent stage is skipped.
    private static bool IsAnswerOnly(PromptSpec prompt)
    {
        return prompt.AllowedIntentIds.Count == 0;
    }

    // The prompt's own grammars come first (they win ties), followed by every other grammar of the allowed intents.
    private List<SlotGrammar> GrammarsFor(PromptSpec prompt)
    {
        var catalog = _services.Content.Intents;
        var grammars = new List<SlotGrammar>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (prompt.SlotGrammarIds.Count > 0)
        {
            var wanted = new HashSet<string>(prompt.SlotGrammarIds, StringComparer.Ordinal);
            foreach (var grammar in catalog.Grammars)
            {
                if (wanted.Contains(grammar.Id) && seen.Add(grammar.Id))
                {
                    grammars.Add(grammar);
                }
            }
        }

        var allowed = new HashSet<string>(prompt.AllowedIntentIds, StringComparer.Ordinal);
        foreach (var intent in catalog.Intents)
        {
            if (!allowed.Contains(intent.Id.Value))
            {
                continue;
            }

            foreach (var grammar in intent.Grammars)
            {
                if (seen.Add(grammar.Id))
                {
                    grammars.Add(grammar);
                }
            }
        }

        return grammars;
    }

    private (AsrResult Asr, PhonePosteriors? Posteriors) Recognize(PcmClip clip, RecognitionRequest request)
    {
        var words = _services.WordRecognizer;
        var phonemes = _services.PhonemeRecognizer;
        if (phonemes is null)
        {
            return (words.Recognize(clip, request), null);
        }

        if (!_options.RunRecognizersConcurrently)
        {
            var asr = words.Recognize(clip, request);
            return (asr, phonemes.Recognize(clip));
        }

        var asrTask = Task.Run(() => words.Recognize(clip, request));
        var phonemeTask = Task.Run(() => phonemes.Recognize(clip));
        try
        {
            Task.WaitAll(asrTask, phonemeTask);
        }
        catch (AggregateException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
        }

        return (asrTask.Result, phonemeTask.Result);
    }

    private PronunciationResult? ScorePronunciation(NormalizedText normalized, PhonePosteriors? posteriors, PromptSpec prompt)
    {
        var recognizer = _services.PhonemeRecognizer;
        if (recognizer is null || posteriors is null || posteriors.FrameCount == 0)
        {
            return null;
        }

        // The G2P drops digits, so numerals are spelled out before phonemizing.
        var spoken = SpanishNumbers.DigitsToWords(normalized.Text);
        if (spoken.Length == 0)
        {
            return null;
        }

        var variants = SpanishG2p.PhonemizeAll(spoken);
        var predicted = PronunciationScorer.PredictedErrorsFor(variants);
        return _scorer.Score(posteriors, variants, predicted, prompt.Difficulty, _options.PronunciationMode, recognizer.ModelHash);
    }
}
