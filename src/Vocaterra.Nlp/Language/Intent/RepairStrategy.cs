using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Language.Intent;

/// <summary>What the agent does after an utterance could not be resolved.</summary>
public enum RepairKind
{
    /// <summary>The agent asks the player to say it again (for example "¿Cómo?" in Spanish).</summary>
    AskAgain,

    /// <summary>The agent repeats its line more slowly and the player tries again.</summary>
    SlowRepeat,

    /// <summary>The agent offers a few spoken choices and the retry is limited to them.</summary>
    OfferChoices,

    /// <summary>The agent stops asking; the conversation moves on.</summary>
    GiveUp,
}

/// <summary>One repair step: what to do, the agent line to speak and the recognition request for the retry (null when giving up).</summary>
/// <param name="Kind">The kind of repair step.</param>
/// <param name="AgentLine">The line for the agent; for <see cref="RepairKind.SlowRepeat"/> it introduces the slow repetition of the original line.</param>
/// <param name="RetryRequest">The request to recognize the player's next attempt with, or null when giving up.</param>
public sealed record RepairStep(RepairKind Kind, string AgentLine, RecognitionRequest? RetryRequest);

/// <summary>
/// The deterministic repair ladder for an unresolved utterance: attempt 1 asks again, attempt 2 repeats slowly, attempt 3 offers up to
/// three choices and switches the retry to constrained recognition, later attempts give up.
/// </summary>
public static class RepairStrategy
{
    /// <summary>The most choices offered at once.</summary>
    public const int MaxChoices = 3;

    /// <summary>
    /// The wording of the offer line when a pack gives none (<see cref="RepairLines.Offer"/> is null): the choices in plain quotes, separated by commas, with no
    /// question around them. The Spanish pack gives its own (<c>¿Quieres decir «a», «b» o «c»?</c>).
    /// </summary>
    public static OfferLineFormat OfferFormat { get; } = new(string.Empty, "\"", "\"", ", ", ", ", string.Empty);

    /// <summary>
    /// The same ladder with the two lists apart: <paramref name="retryPhrases"/> is the grammar of a constrained retry at attempts 1 and 2 (the
    /// pipeline passes every phrase the answer set accepts, aliases included), and <paramref name="choicePhrases"/> are the phrases offered at
    /// attempt 3, in their order (the pipeline passes one canonical phrase per answer, in the set's declared order). The first three distinct
    /// non-blank choices are offered, and the constrained retry at attempt 3 is limited to exactly those.
    /// </summary>
    /// <param name="attempt">The failed attempt, 1 for the first failure.</param>
    /// <param name="prompt">The prompt the player answered.</param>
    /// <param name="retryPhrases">The grammar of a constrained retry at attempts 1 and 2.</param>
    /// <param name="choicePhrases">The phrases offered at attempt 3, in order.</param>
    /// <param name="lines">
    /// The agent lines of the step: the bundle's reserved pool lines where it has them, else the language pack's fallback (see
    /// <see cref="RepairLines"/>).
    /// </param>
    public static RepairStep Next(int attempt, PromptSpec prompt, IReadOnlyList<string> retryPhrases, IReadOnlyList<string> choicePhrases, RepairLines lines)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(retryPhrases);
        ArgumentNullException.ThrowIfNull(choicePhrases);
        ArgumentNullException.ThrowIfNull(lines);
        switch (attempt)
        {
            case 1:
                return new RepairStep(RepairKind.AskAgain, lines.AskAgain, SameMode(prompt, retryPhrases));
            case 2:
                return new RepairStep(RepairKind.SlowRepeat, lines.SlowRepeat, SameMode(prompt, retryPhrases));
            case 3:
                var choices = FirstChoices(choicePhrases);
                return choices.Count == 0
                    ? GiveUp(lines)
                    : new RepairStep(RepairKind.OfferChoices, (lines.Offer ?? OfferFormat).Format(choices), RecognitionRequest.Constrained(choices));
            default:
                return GiveUp(lines);
        }
    }

    private static RepairStep GiveUp(RepairLines lines)
    {
        return new RepairStep(RepairKind.GiveUp, lines.GiveUp, null);
    }

    private static RecognitionRequest SameMode(PromptSpec prompt, IReadOnlyList<string> retryPhrases)
    {
        return prompt.Mode == RecognitionMode.Constrained
            ? RecognitionRequest.Constrained(retryPhrases.ToArray())
            : RecognitionRequest.Open();
    }

    private static List<string> FirstChoices(IReadOnlyList<string> phrases)
    {
        var choices = new List<string>(MaxChoices);
        foreach (var phrase in phrases)
        {
            if (string.IsNullOrWhiteSpace(phrase) || choices.Contains(phrase, StringComparer.Ordinal))
            {
                continue;
            }

            choices.Add(phrase);
            if (choices.Count == MaxChoices)
            {
                break;
            }
        }

        return choices;
    }
}
