using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>One line of the journal: a catalogue noun, what the learner has done with it, and how far it is known.</summary>
/// <param name="Lemma">The noun (<c>árbol</c>), or the lemma of an unseen noun.</param>
/// <param name="Label">The noun with its article (<c>el árbol</c>) once it has been seen, or <c>¿?</c> before.</param>
/// <param name="Seen">Whether the learner has heard, read or said it.</param>
/// <param name="Record">The counts.</param>
/// <param name="Pips">How many of the three uses are in (0 to <see cref="WordProgress.UsesToKnow"/>), for the three progress pips.</param>
/// <param name="Saturation">The colour of the noun's objects, 0 to 1.</param>
/// <param name="DueText">When the next credited use is possible (<c>otra vez en 8 min</c>, <c>lista para repasar</c>), or <see langword="null"/> for a word with no credited use or a known one.</param>
/// <param name="IsDue">Whether the word is due now (a correct use earns colour).</param>
public sealed record JournalRow(string Lemma, string Label, bool Seen, WordRecord Record, int Pips, double Saturation, string? DueText = null, bool IsDue = false)
{
    /// <summary>Whether the noun is known (three correct uses in three turns).</summary>
    public bool Known => Record.Known;
}

/// <summary>
/// The journal's rows, built from the learner's counts: every catalogue noun, the known ones first, then the ones learned in part, the ones met
/// and the ones not met yet, each group in catalogue (ordinal) order. Plain values; no clock.
/// </summary>
public static class JournalRows
{
    /// <summary>The label of a noun the learner has not met.</summary>
    public const string UnseenLabel = "¿?";

    /// <summary>The text of a word that can earn colour now.</summary>
    public const string DueNow = "lista para repasar";

    /// <summary>
    /// The text for the wait before a word can earn colour again: <c>otra vez en 8 min</c> (minutes rounded up), <c>otra vez en 40 s</c> under a
    /// minute, <c>otra vez en 1 h</c> from an hour; <see cref="DueNow"/> for zero.
    /// </summary>
    public static string DueText(long seconds)
    {
        if (seconds <= 0)
        {
            return DueNow;
        }

        if (seconds < 60)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"otra vez en {seconds} s");
        }

        if (seconds < 3600)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"otra vez en {(seconds + 59) / 60} min");
        }

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"otra vez en {(seconds + 3599) / 3600} h");
    }

    /// <summary>Builds the rows for the words' catalogue, without the time of the next review.</summary>
    public static IReadOnlyList<JournalRow> Build(WordProgress words) => Build(words, null);

    /// <summary>
    /// Builds the rows for the words' catalogue. With a stored time (Unix seconds), a word that has a credited use and is not known shows when
    /// its next credited use is possible.
    /// </summary>
    public static IReadOnlyList<JournalRow> Build(WordProgress words, long now) => Build(words, (long?)now);

    private static IReadOnlyList<JournalRow> Build(WordProgress words, long? now)
    {
        ArgumentNullException.ThrowIfNull(words);
        var rows = new List<JournalRow>();
        foreach (var noun in words.Catalogue.Nouns)
        {
            var record = words.RecordOf(noun.Lemma);
            var wait = now is { } at ? words.SecondsUntilNextCredit(noun.Lemma, at, CreditRules.Default) : null;
            rows.Add(new JournalRow(
                noun.Lemma,
                record.Seen ? noun.WithDefinite : UnseenLabel,
                record.Seen,
                record,
                record.Pips,
                record.Saturation,
                wait is { } seconds ? DueText(seconds) : null,
                wait == 0));
        }

        return rows
            .OrderBy(static r => StageRank(r.Record.Stage))
            .ThenBy(static r => r.Lemma, StringComparer.Ordinal)
            .ToList();
    }

    private static int StageRank(WordStage stage) => stage switch
    {
        WordStage.Known => 0,
        WordStage.Learning => 1,
        WordStage.Encountered => 2,
        _ => 3,
    };
}

/// <summary>The journal's two pages (S8): the words (the nouns, D-game-6) and the phrases (the phrase sheet, D-game-10).</summary>
public enum JournalPage
{
    /// <summary>The nouns with their counts and pips.</summary>
    Words,

    /// <summary>The phrases with their support level and counts.</summary>
    Phrases,
}

/// <summary>
/// Whether the journal screen is open, and on which page. The journal takes the keyboard while it is open (the player does not walk); it is opened and
/// closed by the game (J, Esc) and by the scripted run's <c>--journal</c> and <c>--journal-phrases</c>. Tab, Left and Right turn the page.
/// </summary>
public sealed class JournalModel
{
    private WordProgress? _words;
    private bool _read;

    /// <summary>Whether the journal is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The page on screen (the words page opens first).</summary>
    public JournalPage Page { get; private set; } = JournalPage.Words;

    /// <summary>
    /// Opens the journal on its words page. Each seen noun's label counts as read once per opening, when the words page is shown (the research's
    /// "shown for a second" rule, with the opening as the unit of time, so no clock is needed).
    /// </summary>
    public void Open(WordProgress words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _words = words;
        _read = false;
        Page = JournalPage.Words;
        MarkRead();
    }

    /// <summary>Shows a page (the words page counts the seen labels as read, once per opening).</summary>
    /// <param name="page">The page.</param>
    public void Show(JournalPage page)
    {
        if (!IsOpen)
        {
            return;
        }

        Page = page;
        MarkRead();
    }

    /// <summary>Turns to the other page (Tab, Left or Right).</summary>
    public void TogglePage() => Show(Page == JournalPage.Words ? JournalPage.Phrases : JournalPage.Words);

    /// <summary>Closes the journal.</summary>
    public void Close() => IsOpen = false;

    private void MarkRead()
    {
        if (Page != JournalPage.Words || _read || _words is null)
        {
            return;
        }

        _read = true;
        foreach (var row in JournalRows.Build(_words))
        {
            if (row.Seen)
            {
                _words.MarkRead(row.Lemma);
            }
        }
    }
}
