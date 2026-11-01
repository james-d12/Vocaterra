using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// A greeting that opens a line with something else to say (VILLAGE_PLAN V15): <c>hola, quiero dos panes</c>, <c>buenos días, ¿cuánto cuesta el pan?</c>. The core reads one
/// intent per line, and it read such a line as the greeting alone, so the bread was never bought (found at V14: <c>hola Luz quiero dos panes</c> at the bakery). A market
/// makes the line likelier, and the greeting is no part of what is asked, so the game answers the rest: when the whole line is read as a greeting (or as nothing) and the
/// words after the greeting are understood as another intent the prompt takes, the turn is that intent's. The greeting itself credits nothing (no greeting phrase, no
/// greeting reply): the NPC answers what was asked, as a shopkeeper does when a customer says hello and asks for bread in one breath. The listener's name is taken out
/// first (<see cref="Vocative"/>), so <c>hola Luz, quiero dos panes</c> is read as <c>hola, quiero dos panes</c> and answered as <c>quiero dos panes</c>.
/// </summary>
/// <remarks>
/// A line that is only a greeting (<c>hola</c>, <c>buenos días</c>) is left as it is, and so is a line whose rest is a greeting too (<c>hola, buenas tardes</c>), whose rest is
/// not understood (<c>hola amigo</c>, which the core reads as a greeting with a noun) or whose rest is an intent the prompt does not take. The core is never asked to read
/// two intents: the game reads the line, and then its rest, each as one line. The recognizer may put its unknown token (<c>[unk]</c>, a sound outside the prompt's words)
/// before the greeting; the rest leaves it out with the greeting.
/// </remarks>
public static class Greeting
{
    /// <summary>The intent of a greeting in the bundle (<c>intents/intents.json</c>).</summary>
    public const string Intent = "greet";

    // The greetings a line may open with, as plain words (the greet intent's grammars: hola; buenos días, buenas tardes, buenas noches; buen día).
    private static readonly string[][] Openings =
    [
        ["hola"],
        ["buenos", "dias"],
        ["buenas", "tardes"],
        ["buenas", "noches"],
        ["buenas", "dias"],
        ["buen", "dia"],
    ];

    // Every word of the openings (hola, buenos, buenas, dias, tardes, noches, buen, dia), as plain words.
    private static readonly HashSet<string> GreetingWords = Openings.SelectMany(static o => o).ToHashSet(StringComparer.Ordinal);

    /// <summary>The words of the line after its opening greeting, with their own punctuation, or <see langword="null"/> when the line does not open with a greeting or has nothing after it.</summary>
    /// <param name="line">What the learner typed or the recognizer heard (the listener's name already taken out, <see cref="Vocative.Without(string, string?)"/>).</param>
    public static string? Rest(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var length = OpeningLength(tokens);
        return length == 0 ? null : string.Join(' ', tokens.Skip(length));
    }

    /// <summary>
    /// A recognition without its opening greeting: the transcript, its one hypothesis and its words after the greeting, the confidence and the model as they were.
    /// <see langword="null"/> when the recognizer heard no greeting with something after it.
    /// </summary>
    /// <param name="heard">What the recognizer heard (the listener's name already taken out).</param>
    public static AsrResult? Rest(AsrResult heard)
    {
        ArgumentNullException.ThrowIfNull(heard);
        var tokens = heard.Transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var length = OpeningLength(tokens);
        if (length == 0)
        {
            return null;
        }

        var transcript = string.Join(' ', tokens.Skip(length));
        var words = heard.Words.Count == tokens.Length ? heard.Words.Skip(length).ToList() : heard.Words.ToList(); // the recognizer's words are the tokens, one for one
        return heard with { Transcript = transcript, NBest = [new AsrHypothesis(transcript, heard.NBest.Count > 0 ? heard.NBest[0].Score : heard.Confidence)], Words = words };
    }

    /// <summary>
    /// Whether the turn is answered by its rest (see the type's summary): the rest is understood, as another intent than a greeting, which the prompt takes; and the whole
    /// line was read as a greeting or not understood, or (V15b) as the rest's own intent with the rest's errors and some on the greeting's words besides. A greeting's
    /// words are never flagged when the rest is understood: <c>buenas tardes, ¿cuánto cuesta el pan?</c> was the price with <c>vocab.wrong</c> on <c>buenas</c> (the
    /// keyword <c>costar</c> read the whole line, and a constrained prompt's answers have no greeting); a real error after the greeting stays, since the rest has it too
    /// (<c>buenos días, ¿cuánto cuestan el pan?</c> is <c>conj.person</c> alone).
    /// </summary>
    /// <param name="line">The verdict of the whole line.</param>
    /// <param name="rest">The verdict of the words after the greeting.</param>
    /// <param name="prompt">The prompt the line was said at.</param>
    public static bool AnswersRest(Verdict line, Verdict rest, PromptSpec prompt)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(rest);
        ArgumentNullException.ThrowIfNull(prompt);
        var lineGreets = !line.Communicated || string.Equals(line.Intent.Value, Intent, StringComparison.Ordinal);
        var sameButTheGreeting = line.Communicated && string.Equals(line.Intent.Value, rest.Intent.Value, StringComparison.Ordinal)
            && Errors(line, exceptOnTheGreeting: true).SequenceEqual(Errors(rest, exceptOnTheGreeting: false), StringComparer.Ordinal);
        return (lineGreets || sameButTheGreeting)
            && rest.Communicated
            && !string.Equals(rest.Intent.Value, Intent, StringComparison.Ordinal)
            && prompt.AllowedIntentIds.Contains(rest.Intent.Value, StringComparer.Ordinal);
    }

    // A verdict's errors as tag, expected and heard word, in order; without those on a greeting's words when asked (the word heard is one of the openings').
    private static IEnumerable<string> Errors(Verdict verdict, bool exceptOnTheGreeting) =>
        verdict.Errors
            .Where(e => !exceptOnTheGreeting || !GreetingWords.Contains(PhraseSheet.Plain(e.Actual)))
            .Select(static e => $"{e.Tag}|{e.Expected}|{PhraseSheet.Plain(e.Actual)}")
            .Order(StringComparer.Ordinal);

    // How many tokens the opening greeting takes, with the recognizer's unknown tokens before it (0 when the line does not open with one, or has nothing after it).
    private static int OpeningLength(string[] tokens)
    {
        var lead = tokens.TakeWhile(static t => string.Equals(t, GrammarBuilder.UnknownToken, StringComparison.Ordinal)).Count();
        var words = tokens.Skip(lead).Select(PhraseSheet.Plain).ToArray();
        foreach (var opening in Openings)
        {
            if (words.Length > opening.Length && opening.Select((word, i) => string.Equals(words[i], word, StringComparison.Ordinal)).All(static same => same))
            {
                return lead + opening.Length;
            }
        }

        return 0;
    }
}
