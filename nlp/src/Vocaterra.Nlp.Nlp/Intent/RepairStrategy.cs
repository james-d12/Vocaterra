using System.Text;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Nlp.Intent;

/// <summary>What the NPC does after an utterance could not be resolved.</summary>
public enum RepairKind
{
    /// <summary>The NPC asks the player to say it again ("¿Cómo?").</summary>
    AskAgain,

    /// <summary>The NPC repeats its line more slowly and the player tries again.</summary>
    SlowRepeat,

    /// <summary>The NPC offers a few spoken choices and the retry is limited to them.</summary>
    OfferChoices,

    /// <summary>The NPC stops asking; the conversation moves on.</summary>
    GiveUp,
}

/// <summary>One repair step: what to do, the NPC line to speak and the recognition request for the retry (null when giving up).</summary>
/// <param name="Kind">The kind of repair step.</param>
/// <param name="NpcLine">The Spanish line for the NPC; for <see cref="RepairKind.SlowRepeat"/> it introduces the slow repetition of the original line.</param>
/// <param name="RetryRequest">The request to recognize the player's next attempt with, or null when giving up.</param>
public sealed record RepairStep(RepairKind Kind, string NpcLine, RecognitionRequest? RetryRequest);

/// <summary>
/// The deterministic repair ladder for an unresolved utterance: attempt 1 asks again, attempt 2 repeats slowly, attempt 3 offers up to
/// three choices and switches the retry to constrained recognition, later attempts give up.
/// </summary>
public static class RepairStrategy
{
    /// <summary>The most choices offered at once.</summary>
    public const int MaxChoices = 3;

    /// <summary>The NPC line for <see cref="RepairKind.AskAgain"/>.</summary>
    public const string AskAgainLine = "¿Cómo?";

    /// <summary>The NPC line for <see cref="RepairKind.SlowRepeat"/>.</summary>
    public const string SlowRepeatLine = "Perdón, te lo repito más despacio.";

    /// <summary>The NPC line for <see cref="RepairKind.GiveUp"/>.</summary>
    public const string GiveUpLine = "Bueno, no importa.";

    /// <summary>
    /// Returns the repair step for the given failed attempt (1 for the first failure). Attempts 1 and 2 keep the prompt's recognition
    /// mode (constrained retries use all <paramref name="choicePhrases"/>); attempt 3 offers the first three distinct non-blank phrases
    /// as a constrained request, or gives up when there are none; attempts of 4 and more give up.
    /// </summary>
    public static RepairStep Next(int attempt, PromptSpec prompt, IReadOnlyList<string> choicePhrases)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(choicePhrases);
        switch (attempt)
        {
            case 1:
                return new RepairStep(RepairKind.AskAgain, AskAgainLine, SameMode(prompt, choicePhrases));
            case 2:
                return new RepairStep(RepairKind.SlowRepeat, SlowRepeatLine, SameMode(prompt, choicePhrases));
            case 3:
                var choices = FirstChoices(choicePhrases);
                return choices.Count == 0
                    ? GiveUp()
                    : new RepairStep(RepairKind.OfferChoices, OfferLine(choices), RecognitionRequest.Constrained(choices));
            default:
                return GiveUp();
        }
    }

    private static RepairStep GiveUp()
    {
        return new RepairStep(RepairKind.GiveUp, GiveUpLine, null);
    }

    private static RecognitionRequest SameMode(PromptSpec prompt, IReadOnlyList<string> choicePhrases)
    {
        return prompt.Mode == RecognitionMode.Constrained
            ? RecognitionRequest.Constrained(choicePhrases.ToArray())
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

    private static string OfferLine(List<string> choices)
    {
        var builder = new StringBuilder("¿Quieres decir ");
        for (var i = 0; i < choices.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == choices.Count - 1 ? " o " : ", ");
            }

            builder.Append('«').Append(choices[i]).Append('»');
        }

        return builder.Append('?').ToString();
    }
}
