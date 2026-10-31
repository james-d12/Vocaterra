using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Es.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>Which kinds of time marker a sentence holds.</summary>
/// <param name="Past">A past marker: <c>ayer</c>, <c>la semana pasada</c>.</param>
/// <param name="Future">A future marker: <c>mañana</c>, <c>pasado mañana</c>, <c>la próxima semana</c>, <c>la semana que viene</c>.</param>
public readonly record struct TimeMarkers(bool Past, bool Future);

/// <summary>
/// The generators' model of the time markers of the core's tense checks (M57): the phrases of <see cref="SpanishTimeMarkers"/>, the pack's public table that the
/// checker reads, found in a sequence of words with their lexicon readings. The words come from the table, so the generator and the checker cannot drift apart; the
/// phrase rules are written here again, independently of the checker, as the rest of the realiser models the analyzer. Before M57 the realiser knew only the single
/// words <c>mañana</c>, <c>ayer</c>, <c>anoche</c> and <c>anteayer</c>, so a grammar with <c>la semana que viene</c> or <c>el año pasado</c> gave it sentences it counted
/// as correct and the checker rightly flagged.
/// </summary>
public static class TimeMarkerModel
{
    /// <summary>
    /// The markers of a sentence. A word whose text is null is not known yet (a word the realiser has still to choose): it is no marker and no part of one, so it
    /// breaks every phrase across it. A name has its text and no readings, as the analyzer reads it.
    /// </summary>
    /// <param name="words">The words in order: the written form (lower case) and every lexicon reading of it.</param>
    public static TimeMarkers Find(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var past = false;
        var future = false;
        for (var i = 0; i < words.Count; i++)
        {
            past |= IsPastAt(words, i);
            future |= IsFutureAt(words, i);
        }

        return new TimeMarkers(past, future);
    }

    // Past: an adverb of SpanishTimeMarkers.PastAdverbs, or a time noun followed by a form of pasado as written that is not the pasado of pasado mañana.
    private static bool IsPastAt(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words, int i)
    {
        if (Has(words, i, static r => r.Pos == LexiconSchema.Adverb && SpanishTimeMarkers.PastAdverbs.Contains(r.Lemma)))
        {
            return true;
        }

        return i > 0
            && words[i].Text is { } text
            && SpanishTimeMarkers.PastAdjectiveForms.Contains(text)
            && !IsDayAfterTomorrow(words, i)
            && Has(words, i - 1, static r => r.Pos == LexiconSchema.Noun && SpanishTimeMarkers.TimeNouns.Contains(r.Lemma));
    }

    // Future: mañana the adverb unless a word before it makes it the noun; próximo before an upcoming time noun; a time noun followed by que viene as written. The
    // pasado of pasado mañana needs no rule of its own: it is the mañana after it that marks the future.
    private static bool IsFutureAt(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words, int i)
    {
        if (IsFutureAdverb(words, i) && (i == 0 || words[i - 1].Text is not { } before || !SpanishTimeMarkers.WordsBeforeTheNounMañana.Contains(before)))
        {
            return true;
        }

        if (i > 0
            && Has(words, i - 1, static r => r.Pos == LexiconSchema.Adjective && r.Lemma == SpanishTimeMarkers.UpcomingAdjective)
            && Has(words, i, static r => r.Pos == LexiconSchema.Noun && SpanishTimeMarkers.UpcomingTimeNouns.Contains(r.Lemma)))
        {
            return true;
        }

        return i > 0
            && i + 1 < words.Count
            && Has(words, i - 1, static r => r.Pos == LexiconSchema.Noun && SpanishTimeMarkers.TimeNouns.Contains(r.Lemma))
            && string.Equals(words[i].Text, SpanishTimeMarkers.UpcomingClause[0], StringComparison.Ordinal)
            && string.Equals(words[i + 1].Text, SpanishTimeMarkers.UpcomingClause[1], StringComparison.Ordinal);
    }

    private static bool IsDayAfterTomorrow(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words, int i)
    {
        return i + 1 < words.Count
            && string.Equals(words[i].Text, SpanishTimeMarkers.DayAfterTomorrow[0], StringComparison.Ordinal)
            && IsFutureAdverb(words, i + 1);
    }

    private static bool IsFutureAdverb(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words, int i)
    {
        return Has(words, i, static r => r.Pos == LexiconSchema.Adverb && r.Lemma == SpanishTimeMarkers.FutureAdverb);
    }

    private static bool Has(IReadOnlyList<(string? Text, IReadOnlyList<LexicalFeatures> Readings)> words, int i, Func<LexicalFeatures, bool> predicate)
    {
        return words[i].Readings.Any(predicate);
    }
}
