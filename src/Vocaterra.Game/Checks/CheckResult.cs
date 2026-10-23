using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game;

/// <summary>How a check item was answered.</summary>
public enum CheckInput
{
    /// <summary>A comprehension item: one of the three options was picked (a recognition measure).</summary>
    Pick,

    /// <summary>A production item: the answer was typed.</summary>
    Typed,

    /// <summary>A production item: the answer was spoken.</summary>
    Spoken,

    /// <summary>A production item: the "No sé" button (graded as <c>dont_know</c>, never a failure).</summary>
    Button,

    /// <summary>The item was left unanswered (the learner pressed Esc to end the battery early).</summary>
    Skipped,
}

/// <summary>The wire names of the check inputs (<c>pick</c>, <c>typed</c>, <c>spoken</c>, <c>button</c>, <c>skipped</c>).</summary>
public static class CheckInputNames
{
    /// <summary>The wire name of a check input.</summary>
    public static string Of(CheckInput input) => input switch
    {
        CheckInput.Pick => "pick",
        CheckInput.Typed => "typed",
        CheckInput.Spoken => "spoken",
        CheckInput.Button => "button",
        CheckInput.Skipped => "skipped",
        _ => throw new ArgumentOutOfRangeException(nameof(input), input, "Unknown check input."),
    };
}

/// <summary>
/// The answer to one check item, as the session log records it (S11). It is measurement only: nothing here is written to the learner model,
/// the phrase progress or the world.
/// </summary>
/// <param name="ItemId">The item (<c>say.pedir-agua</c>).</param>
/// <param name="Kind">Comprehension or production.</param>
/// <param name="Phrase">The phrase sheet row.</param>
/// <param name="Input">How it was answered.</param>
/// <param name="Option">For a pick, the position shown (from 0); otherwise <see langword="null"/>.</param>
/// <param name="Correct">Whether the answer is right (a pick: the right option; a production: communicated with the row's intent).</param>
/// <param name="Transcript">What was typed or heard, or <see langword="null"/> for a pick or a skip.</param>
/// <param name="Intent">The intent the core resolved, or <see langword="null"/>.</param>
/// <param name="Communicated">Whether the core understood the line (false for a pick).</param>
/// <param name="DontKnow">Whether the answer was "no sé" (a production item).</param>
/// <param name="ErrorTags">The core's error tags for a production answer.</param>
/// <param name="Confidence">The recognizer's mean word confidence for a spoken answer, or <see langword="null"/>.</param>
/// <param name="LatencyMs">From the item being shown (a pick) or from its submission (production) to the answer, on the front-end's clock.</param>
/// <param name="Replay">The replay record of a production evaluation, or <see langword="null"/>.</param>
public sealed record CheckResult(
    string ItemId,
    CheckItemKind Kind,
    string Phrase,
    CheckInput Input,
    int? Option,
    bool Correct,
    string? Transcript,
    string? Intent,
    bool Communicated,
    bool DontKnow,
    IReadOnlyList<string> ErrorTags,
    double? Confidence,
    double LatencyMs,
    ReplayEntry? Replay);
