using Vocaterra.Game.Language.Speech;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Language.Checks;

/// <summary>
/// What the core made of one production answer of a check (S11). <see cref="Correct"/> is the check's rule: the line was communicated and its
/// intent is the row's intent. The errors are recorded, not scored; <see cref="DontKnow"/> is the "no sé" answer, recorded as such.
/// </summary>
/// <param name="Transcript">What the learner typed, or what the recognizer heard.</param>
/// <param name="Input">How the answer came: typed, spoken, or a button (<c>No sé</c>).</param>
/// <param name="Communicated">Whether the core understood the line.</param>
/// <param name="Intent">The intent the core resolved (<c>dont_know</c> for "no sé").</param>
/// <param name="ErrorTags">The core's error tags (<c>conj.person</c>), in the verdict's order.</param>
/// <param name="Confidence">For a spoken answer, the mean per-word confidence of the recognizer; <see langword="null"/> otherwise.</param>
/// <param name="DontKnow">Whether the learner said they do not know.</param>
/// <param name="Correct">Whether the line was communicated with the row's intent.</param>
/// <param name="Entry">The replay record of the evaluation (utterance envelope), for the session's replay log.</param>
/// <param name="Words">The answer's words with the core's readings (S19: the learner's record of what was said).</param>
/// <param name="Errors">The core's errors with their spans and forms (S19), in the verdict's order.</param>
public sealed record CheckGrade(
    string Transcript,
    InputKind Input,
    bool Communicated,
    string Intent,
    IReadOnlyList<string> ErrorTags,
    double? Confidence,
    bool DontKnow,
    bool Correct,
    ReplayEntry Entry,
    IReadOnlyList<GuideWord>? Words = null,
    IReadOnlyList<GuideError>? Errors = null);

/// <summary>
/// Grades the production answers of a check through the core, at the item's prompt, in a throwaway conversation context: a fresh learner model,
/// no repeat counts and no host facts, so nothing the check does reaches the learner's progress, the NPCs or the world (S11: a check is
/// measurement, not practice). It runs on the turn worker's thread for the spoken answers (the recognizer belongs to that thread).
/// </summary>
public sealed class CheckGrader
{
    private readonly ContentBundle _bundle;
    private readonly ulong _worldSeed;
    private readonly TypedUtteranceEvaluator _typed;

    /// <summary>Creates a grader over a loaded bundle; the world seed only seeds the throwaway contexts' random draws.</summary>
    /// <param name="bundle">The content bundle.</param>
    /// <param name="worldSeed">The world seed of the session the check belongs to.</param>
    public CheckGrader(ContentBundle bundle, ulong worldSeed)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        _bundle = bundle;
        _worldSeed = worldSeed;
        _typed = new TypedUtteranceEvaluator(bundle);
    }

    /// <summary>Grades a typed (or "no sé" button) answer of a production item.</summary>
    /// <param name="item">A production item.</param>
    /// <param name="line">What the learner typed.</param>
    /// <param name="input">Typed, or <see cref="InputKind.Button"/> for the "no sé" button.</param>
    /// <param name="now">The stored time of the check (a fixed value per battery).</param>
    /// <param name="hostStateHash">The state digest the replay record carries.</param>
    public CheckGrade GradeTyped(CheckItem item, string line, InputKind input, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(line);
        RequireProduction(item);
        var prompt = PromptOf(item);
        var context = ContextFor(prompt, now, hostStateHash);
        var analysis = _typed.Analyze(line, prompt);
        var trace = _typed.CompleteTrace(analysis, context);
        return Build(item, trace, context, input, confidence: null);
    }

    /// <summary>Grades a spoken answer of a production item: the clip is recognized once, on the calling thread (the worker's).</summary>
    /// <param name="spoken">The spoken-line evaluator of the worker's recognizer.</param>
    /// <param name="item">A production item.</param>
    /// <param name="clip">The answer, 16 kHz mono.</param>
    /// <param name="now">The stored time of the check.</param>
    /// <param name="hostStateHash">The state digest the replay record carries.</param>
    public CheckGrade GradeSpoken(SpokenUtteranceEvaluator spoken, CheckItem item, PcmClip clip, StoredTimestamp now, string hostStateHash)
    {
        ArgumentNullException.ThrowIfNull(spoken);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(clip);
        RequireProduction(item);
        var prompt = PromptOf(item);
        var context = ContextFor(prompt, now, hostStateHash);
        var trace = spoken.Trace(clip, prompt, context);
        return Build(item, trace, context, InputKind.Spoken, UncertaintyRule.MeanWordConfidence(trace.Asr));
    }

    /// <summary>The check's rule for a production answer: communicated, and the intent the row expects.</summary>
    /// <param name="communicated">Whether the core understood the line.</param>
    /// <param name="intent">The intent the core resolved.</param>
    /// <param name="expected">The row's intent.</param>
    public static bool IsCorrect(bool communicated, string intent, string expected) =>
        communicated && string.Equals(intent, expected, StringComparison.Ordinal) && !string.Equals(intent, "dont_know", StringComparison.Ordinal);

    // What the learner said, without the recognizer's unknown-word marks (the same cleaning the NPCs' turns use).
    private static string Clean(string transcript) =>
        string.Join(' ', transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static void RequireProduction(CheckItem item)
    {
        if (item.Kind != CheckItemKind.Production)
        {
            throw new ArgumentException($"The item '{item.Id}' is a comprehension item: only production items are graded by the core.", nameof(item));
        }
    }

    private PromptSpec PromptOf(CheckItem item) => _bundle.Prompts[item.Prompt];

    // A throwaway context: the agent's initial state, a learner model with no evidence, no repeat counts, no host slots and no extra skills.
    private ConversationContext ContextFor(PromptSpec prompt, StoredTimestamp now, string hostStateHash)
    {
        var agentId = prompt.AgentId ?? NpcService.GuideAgent;
        var machine = _bundle.Agents[agentId];
        return ConversationContext.Create(_worldSeed, agentId, machine.InitialState(), now, LearnerModel.New()) with { HostStateHash = hostStateHash };
    }

    private static CheckGrade Build(CheckItem item, PipelineTrace trace, ConversationContext context, InputKind input, double? confidence)
    {
        var verdict = trace.Result.Verdict;
        var intent = verdict.Intent.Value;
        return new CheckGrade(
            Clean(trace.Asr.Transcript),
            input,
            verdict.Communicated,
            intent,
            verdict.Errors.Select(static e => e.Tag).ToList(),
            confidence,
            string.Equals(intent, "dont_know", StringComparison.Ordinal),
            IsCorrect(verdict.Communicated, intent, item.Intent),
            new ReplayEntry(ReplayEnvelope.From(trace, context), null),
            GuideWord.From(trace.Result),
            verdict.Errors.Select(static e => new GuideError(e.Tag, e.Expected, e.Actual, e.Span)).ToList());
    }
}
