using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// The listener's name said as a vocative (VILLAGE_PLAN V14): a learner who knows the villagers' names opens or closes a line with the name of the one spoken to
/// (<c>Luz, quiero pan</c>, <c>quiero pan, Luz</c>, <c>hola Luz, quiero pan</c>). The name is no part of the sentence, and no grammar of the bundle has room for it, so the
/// line is read without it: the core resolved <c>Luz quiero pan</c> at the baker to no intent and read <c>Mar dónde está la panadería</c> at the fisher as a question about
/// the sea. The core leaves a lone name before a verb unchecked since M59 for this reason (it may be the person spoken to, and a transcript has no comma); the game, which
/// knows who the listener is, takes the name out before the core reads the line.
/// </summary>
/// <remarks>
/// Only the listener's own name (the NPC file's <c>name</c>: Luz at the baker, Mar at the fisher, Luna at the guide) is taken out, and only where it cannot be a word of
/// the sentence: as the first word, or the word after an opening greeting (any of <see cref="Greeting"/>'s: <c>hola</c>, <c>buenos días</c>, <c>buenas tardes</c>,
/// <c>buenas noches</c>, <c>buen día</c>; V19), when more follows and the next word does not join it to another (<c>Luz y Domingo
/// comieron pan</c> keeps it); and as the last word, when the word before it does not make it a noun of the sentence (an article, a possessive, a preposition or a
/// conjunction: <c>el pan de Luz</c>, <c>la luna</c>). A line that is only the name is left as it is. The punctuation the name carried is kept on its neighbour, so
/// <c>¿Mar, dónde está la panadería?</c> is read as <c>¿dónde está la panadería?</c>. The recognizer's unknown tokens (<c>[unk]</c>) before the line are looked past (V15:
/// <c>[unk] hola luz quiero dos panes</c> kept the name).
/// </remarks>
public static class Vocative
{
    // Words after a leading name that join it to another noun: then the name is a subject (Luz y Domingo comieron pan), not the person spoken to.
    private static readonly string[] Joiners = ["y", "e", "o", "u", "ni"];

    // Words before a trailing name that make it a noun of the sentence (el pan de Luz, veo el mar, la luna): articles, possessives, prepositions, conjunctions.
    private static readonly string[] Binders =
    [
        "a", "al", "con", "de", "del", "en", "para", "por", "sin",
        "el", "la", "los", "las", "un", "una", "unos", "unas", "lo",
        "mi", "mis", "tu", "tus", "su", "sus",
        "y", "e", "o", "u", "ni", "que",
    ];

    private const string OpeningMarks = "¿¡";
    private const string ClosingMarks = "?!.…";

    /// <summary>The line without the listener's name where it stands as a vocative (see the type's remarks), or the line as it is.</summary>
    /// <param name="line">What the learner typed or the recognizer heard.</param>
    /// <param name="name">The listener's name, or <see langword="null"/> for an NPC that has none (the villager, the shopkeeper): then the line is unchanged.</param>
    public static string Without(string line, string? name)
    {
        ArgumentNullException.ThrowIfNull(line);
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var (leading, trailing) = Positions(tokens, name);
        if (leading is null && trailing is null)
        {
            return line;
        }

        if (trailing is { } last)
        {
            // The closing mark the name carried (quiero pan, Luz.) moves to the word before it, whose comma goes.
            var closing = new string(tokens[last].Reverse().TakeWhile(c => ClosingMarks.Contains(c, StringComparison.Ordinal)).Reverse().ToArray());
            tokens[last - 1] = tokens[last - 1].TrimEnd(',', ';', ':') + closing;
            tokens.RemoveAt(last);
        }

        if (leading is { } first)
        {
            // The opening mark the name carried (¿Mar, dónde está…?) moves to the word after it.
            var opening = new string(tokens[first].TakeWhile(c => OpeningMarks.Contains(c, StringComparison.Ordinal)).ToArray());
            tokens[first + 1] = opening + tokens[first + 1];
            tokens.RemoveAt(first);
            if (first > 0)
            {
                tokens[first - 1] = tokens[first - 1].TrimEnd(',', ';', ':');
            }
        }

        return string.Join(' ', tokens);
    }

    /// <summary>
    /// A recognition without the listener's name where it stands as a vocative: the transcript, its one hypothesis and its words without the name's word, the confidence
    /// and the model as they were. <see langword="null"/> when the recognizer heard no such name (the recognition is then used as it is).
    /// </summary>
    /// <param name="heard">What the recognizer heard.</param>
    /// <param name="name">The listener's name, or <see langword="null"/>.</param>
    public static AsrResult? Without(AsrResult heard, string? name)
    {
        ArgumentNullException.ThrowIfNull(heard);
        var tokens = heard.Transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var (leading, trailing) = Positions(tokens, name);
        if (leading is null && trailing is null)
        {
            return null;
        }

        var transcript = Without(heard.Transcript, name);
        var words = heard.Words.ToList();
        if (words.Count == tokens.Count)
        {
            // The recognizer's words are the transcript's tokens, one for one: the name's word goes with it.
            if (trailing is { } last)
            {
                words.RemoveAt(last);
            }

            if (leading is { } first)
            {
                words.RemoveAt(first);
            }
        }

        return heard with { Transcript = transcript, NBest = [new AsrHypothesis(transcript, heard.NBest.Count > 0 ? heard.NBest[0].Score : heard.Confidence)], Words = words };
    }

    // Where the listener's name stands as a vocative: its index at the start (or after an opening greeting: hola Luz, buenos días Luz) and at the end, each null when it
    // does not.
    private static (int? Leading, int? Trailing) Positions(List<string> tokens, string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || tokens.Count < 2)
        {
            return (null, null);
        }

        var plainName = PhraseSheet.Plain(name);
        var words = tokens.Select(PhraseSheet.Plain).ToList();
        int? trailing = null;
        var end = words.Count - 1;
        if (words[end] == plainName && !Binders.Contains(words[end - 1], StringComparer.Ordinal))
        {
            trailing = end;
        }

        int? leading = null;
        var lead = tokens.TakeWhile(static t => string.Equals(t, GrammarBuilder.UnknownToken, StringComparison.Ordinal)).Count(); // the recognizer's [unk] before the line (V15)
        if (lead >= words.Count - 1)
        {
            return (null, trailing);
        }

        var opening = Greeting.OpeningLength(tokens); // the greeting a vocative may follow: any of Greeting's (V19, the review's R4: only hola before)
        var start = opening > 0 ? opening : lead;
        var rest = (trailing ?? words.Count) - start - 1; // the words after the name that stay
        if (words[start] == plainName && rest > 0 && !Joiners.Contains(words[start + 1], StringComparer.Ordinal))
        {
            leading = start;
        }

        return (leading, trailing);
    }
}
