using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Matching;

namespace Vocaterra.Nlp.Pipeline;

/// <summary>
/// The result of phase 1 of a two-phase evaluation (<see cref="UtterancePipeline.Analyze"/>): everything the pipeline learned from the clip and
/// the prompt before the decision layer runs. The host reads <see cref="Words"/>, <see cref="Verdict"/> and <see cref="Answer"/> to compute
/// host slots and extra skills, then passes the object to <see cref="UtterancePipeline.Complete(UtteranceAnalysis, ConversationContext)"/>.
/// </summary>
/// <remarks>
/// It holds the recognizer outputs (<see cref="Asr"/>, <see cref="Posteriors"/>), so completing it never runs a recognizer again, and the
/// intermediate stage values phase 2 needs. It is immutable: no member can be changed after construction and the pipeline never mutates what it
/// holds, so one instance may be read, and completed, from any thread (and more than once, in different contexts). The posterior matrix inside
/// <see cref="Posteriors"/> is a shared array: callers must treat it as read-only. An instance can only be completed by a pipeline over the same
/// content and models (the version stamp must match), and is not serialized; a replay log stores the recognizer outputs instead.
/// </remarks>
public sealed class UtteranceAnalysis
{
    internal UtteranceAnalysis(
        string clipHash,
        PromptSpec prompt,
        AsrResult asr,
        PhonePosteriors? posteriors,
        NormalizedText normalized,
        AnalysisResult analysis,
        MatchResult match,
        AnswerSet answerSet,
        Verdict verdict)
    {
        ClipHash = clipHash;
        Prompt = prompt;
        Asr = asr;
        Posteriors = posteriors;
        Normalized = normalized;
        Analysis = analysis;
        Match = match;
        AnswerSet = answerSet;
        Verdict = verdict;
    }

    /// <summary>The hash of the clip that was recognized (copied into the replay record).</summary>
    public string ClipHash { get; }

    /// <summary>The prompt the utterance answered.</summary>
    public PromptSpec Prompt { get; }

    /// <summary>The word recognizer's output; phase 2 and the replay log reuse it, so recognition runs once.</summary>
    public AsrResult Asr { get; }

    /// <summary>The phoneme recognizer's output, or null when pronunciation scoring did not run.</summary>
    public PhonePosteriors? Posteriors { get; }

    /// <summary>The graded verdict: the same value <see cref="PipelineResult.Verdict"/> will carry.</summary>
    public Verdict Verdict { get; }

    /// <summary>The normalized tokens in order with their roles and chosen readings: the same value <see cref="PipelineResult.Words"/> will carry (the pipeline always sets it).</summary>
    public IReadOnlyList<ResolvedWord>? Words => Analysis.Words;

    /// <summary>The answer match for the prompt's answer set: the same value <see cref="PipelineResult.Answer"/> will carry.</summary>
    public MatchResult Answer => Match;

    internal NormalizedText Normalized { get; }

    internal AnalysisResult Analysis { get; }

    internal MatchResult Match { get; }

    internal AnswerSet AnswerSet { get; }
}
