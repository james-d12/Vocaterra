namespace Vocaterra.Nlp.Es.Analysis;

/// <summary>
/// The Spanish time markers of the tense checks (<c>conj.tense</c>; M49, M57; docs/nlp/api.md, "Tense errors"), in one table: the agreement checker reads it, and the
/// fuzz suite's sentence generator models the same phrases from it, so the two cannot drift apart. A marker is a phrase, not a word:
/// <list type="bullet">
/// <item>past: an adverb of <see cref="PastAdverbs"/> (<c>ayer</c>), or a noun of <see cref="TimeNouns"/> followed by a form of <see cref="PastAdjectiveForms"/>
/// (<c>la semana pasada</c>, <c>el año pasado</c>), matched by its form;</item>
/// <item>future: <see cref="FutureAdverb"/> unless a word of <see cref="WordsBeforeTheNounMañana"/> stands before it (<c>esta mañana</c> is the noun); <see cref="UpcomingAdjective"/>
/// before a noun of <see cref="UpcomingTimeNouns"/> (<c>la próxima semana</c>); a noun of <see cref="TimeNouns"/> followed by <see cref="UpcomingClause"/>
/// (<c>la semana que viene</c>).</item>
/// <item><see cref="DayAfterTomorrow"/> (<c>pasado mañana</c>) is the future marker wherever it stands: its <c>pasado</c> is neither an adjective of the noun before it
/// (<c>compro panes pasado mañana</c>) nor a past marker after a time noun (<c>voy a ir el domingo pasado mañana</c>).</item>
/// </list>
/// The adverbs, the adjectives and the nouns are lemmas, matched on a reading of the word, so <c>proxima</c> without its accent is still <c>próximo</c>; the forms of
/// <c>pasado</c> and the words of <see cref="UpcomingClause"/> and <see cref="WordsBeforeTheNounMañana"/> are matched as written. <c>luego</c>, <c>después</c> and
/// <c>más tarde</c> are no markers (<c>luego comí</c> is narration). Each list is read-only (M64): a caller cannot cast it to an array or a list and change it.
/// </summary>
public static class SpanishTimeMarkers
{
    /// <summary>The adverbs that put a sentence in the past (lemmas).</summary>
    public static IReadOnlyList<string> PastAdverbs { get; } = Array.AsReadOnly<string>(["ayer", "anoche", "anteayer"]);

    /// <summary>
    /// The forms of <c>pasado</c> that put a time noun before them in the past (<c>la semana pasada</c>). They are matched as written: after a time noun the noun
    /// <c>pasada</c> is never meant, and a reading would let it hide the slip (M57).
    /// </summary>
    public static IReadOnlyList<string> PastAdjectiveForms { get; } = Array.AsReadOnly<string>(["pasado", "pasada", "pasados", "pasadas"]);

    /// <summary>The adverb that puts a sentence in the future (lemma).</summary>
    public const string FutureAdverb = "mañana";

    /// <summary>The words after which <see cref="FutureAdverb"/> is the noun, the morning, and no marker (<c>esta mañana</c>, <c>por la mañana</c>, <c>de mañana</c>).</summary>
    public static IReadOnlyList<string> WordsBeforeTheNounMañana { get; } = Array.AsReadOnly<string>(["la", "esta", "una", "cada", "toda", "esa", "aquella", "de"]);

    /// <summary>The day after tomorrow, one future marker of two words: <c>pasado</c> as written, then the adverb <see cref="FutureAdverb"/>.</summary>
    public static IReadOnlyList<string> DayAfterTomorrow { get; } = Array.AsReadOnly<string>(["pasado", FutureAdverb]);

    /// <summary>The adjective that, before a noun of <see cref="UpcomingTimeNouns"/>, puts it in the future (lemma; <c>la próxima semana</c>, <c>el próximo día</c>).</summary>
    public const string UpcomingAdjective = "próximo";

    /// <summary>The words that, after a noun of <see cref="TimeNouns"/>, put it in the future (written forms; <c>la semana que viene</c>, <c>el año que viene</c>).</summary>
    public static IReadOnlyList<string> UpcomingClause { get; } = Array.AsReadOnly<string>(["que", "viene"]);

    /// <summary>The nouns of time that <see cref="PastAdjectiveForms"/> and <see cref="UpcomingClause"/> follow (lemmas).</summary>
    public static IReadOnlyList<string> TimeNouns { get; } =
        Array.AsReadOnly<string>(["semana", "mes", "año", "día", "noche", "vez", "hora", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"]);

    /// <summary>The nouns that <see cref="UpcomingAdjective"/> precedes as a time word (lemmas): <see cref="TimeNouns"/> and the seasons, the century and the weekend.</summary>
    public static IReadOnlyList<string> UpcomingTimeNouns { get; } = Array.AsReadOnly<string>([.. TimeNouns, "siglo", "verano", "invierno", "otoño", "primavera", "fin", "finde"]);
}
