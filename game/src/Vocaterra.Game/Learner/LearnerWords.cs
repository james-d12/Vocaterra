using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game;

/// <summary>One use of a word: the history entry it was in, the session and stored time of that entry, and the sentence (the entry's text).</summary>
/// <param name="Entry">The entry's id in the history.</param>
/// <param name="Session">The play session of the entry.</param>
/// <param name="StoredTime">The stored game time of the entry, in Unix seconds.</param>
/// <param name="Sentence">The line the word was in, as the core evaluated it.</param>
public sealed record WordUse(long Entry, int Session, long StoredTime, string Sentence);

/// <summary>
/// One form of a word as the learner used it: the text (<c>aguas</c>, <c>quiero</c>) and the reading's person, number, tense and mood where they apply,
/// with how many times it was used and how many of those were correct.
/// </summary>
/// <param name="Form">The token's text (normalized: accents kept, lower case).</param>
/// <param name="Person">The person (<c>1</c>), or <see langword="null"/>.</param>
/// <param name="Number">The number (<c>sg</c> or <c>pl</c>), or <see langword="null"/>.</param>
/// <param name="Tense">The tense (<c>pres</c>, <c>pret</c>, <c>impf</c>, <c>fut</c> or <c>cond</c>), or <see langword="null"/>.</param>
/// <param name="Mood">The mood, or <see langword="null"/>.</param>
/// <param name="Times">The uses of this form.</param>
/// <param name="TimesCorrect">Of those, the correct ones.</param>
/// <param name="Question">Whether the form was used in a question (the line ends with a question mark): <c>¿quieres?</c> is a form of its own.</param>
/// <param name="Last">The last use of this form, or <see langword="null"/> for a form read from a save that kept none (S20).</param>
public sealed record FormRow(string Form, string? Person, string? Number, string? Tense, string? Mood, int Times, int TimesCorrect, bool Question = false, WordUse? Last = null);

/// <summary>
/// One row of the word record (S19): a lemma of one part of speech that the core read with a lexicon entry, with its gender (for a noun), the
/// forms used, the counts, and its first and last use. <see cref="LastSentence"/> is the sentence of the last use.
/// </summary>
/// <param name="Lemma">The lemma: the noun (<c>agua</c>), the infinitive of a verb (<c>querer</c>).</param>
/// <param name="Pos">The part of speech the core read (<c>noun</c>, <c>verb</c>, ...).</param>
/// <param name="Gender">The lexicon's gender (<c>fem</c>), or <see langword="null"/>.</param>
/// <param name="Times">The uses of the lemma: each token of it in a line that was not held back and not sent by a button.</param>
/// <param name="TimesCorrect">Of those, the uses in a line the core understood with no error span over the token.</param>
/// <param name="Forms">The forms used, ordered by text, then person, number, tense and mood.</param>
/// <param name="First">The first use.</param>
/// <param name="Last">The last use.</param>
public sealed record WordRow(string Lemma, string Pos, string? Gender, int Times, int TimesCorrect, IReadOnlyList<FormRow> Forms, WordUse First, WordUse Last)
{
    /// <summary>The sentence of the last use.</summary>
    public string LastSentence => Last.Sentence;
}

/// <summary>
/// The word record (S19): a pure fold of the history. Each entry that is not held back and not sent by a button adds its readings: a word the lexicon
/// read (its role is <see cref="WordRole.Word"/>) counts one use of its lemma and of its form, correct when the line was understood and no error span
/// covers the token. Names and unknown tokens are not in it. Replaying the history from the start gives the same record (<see cref="Fold"/>).
/// </summary>
/// <remarks>
/// A row is kept per (lemma, part of speech), so a word read as two parts of speech has two rows. The rows and forms are sorted ordinally on
/// read, so the order never depends on insertion.
/// </remarks>
public sealed class LearnerWords
{
    private readonly Dictionary<(string Lemma, string Pos), RowState> _rows = [];

    /// <summary>The rows, sorted by lemma, then by part of speech (ordinal).</summary>
    public IReadOnlyList<WordRow> Rows => _rows.Values
        .OrderBy(static r => r.Lemma, StringComparer.Ordinal)
        .ThenBy(static r => r.Pos, StringComparer.Ordinal)
        .Select(static r => r.ToRow())
        .ToList();

    /// <summary>The record of a history, folded from its first entry.</summary>
    /// <param name="entries">The entries, in the order the game took them.</param>
    public static LearnerWords Fold(IEnumerable<InputEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var record = new LearnerWords();
        foreach (var entry in entries)
        {
            record.Apply(entry);
        }

        return record;
    }

    /// <summary>Adds one entry's words to the record.</summary>
    /// <param name="entry">The entry.</param>
    public void Apply(InputEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Held || entry.Source == InputSource.Button)
        {
            return;
        }

        foreach (var word in entry.Words)
        {
            if (word.Role != WordRole.Word || word.Lemma is null || word.Pos is null)
            {
                continue;
            }

            var correct = entry.Communicated && !entry.Errors.Any(error => Covers(word.Span, error.Span));
            var use = new WordUse(entry.Id, entry.Session, entry.StoredTime, entry.Text);
            if (!_rows.TryGetValue((word.Lemma, word.Pos), out var row))
            {
                row = new RowState(word.Lemma, word.Pos);
                _rows[(word.Lemma, word.Pos)] = row;
            }

            row.Times++;
            if (correct)
            {
                row.TimesCorrect++;
            }

            if (word.Gender is not null)
            {
                row.Gender = word.Gender;
            }

            var key = new FormKey(word.Text, word.Person, word.Number, word.Tense, word.Mood, IsQuestion(entry.Text));
            row.Forms.TryGetValue(key, out var form);
            form.Times++;
            if (correct)
            {
                form.TimesCorrect++;
            }

            form.Last = use;
            row.Forms[key] = form;
            row.First ??= use;
            row.Last = use;
        }
    }

    /// <summary>Puts a saved record back (S10's load). A row given twice is refused.</summary>
    /// <param name="rows">The rows of a save.</param>
    internal void Restore(IEnumerable<WordRow> rows)
    {
        _rows.Clear();
        foreach (var row in rows)
        {
            if (_rows.ContainsKey((row.Lemma, row.Pos)))
            {
                throw new ArgumentException($"The word record has the lemma '{row.Lemma}' ({row.Pos}) twice.", nameof(rows));
            }

            _rows[(row.Lemma, row.Pos)] = RowState.From(row);
        }
    }

    // Whether an error's span covers a token: a zero-length error (a missing word) covers the token it starts in.
    private static bool Covers(TextSpan word, TextSpan error) => error.Length == 0
        ? error.Start >= word.Start && error.Start < word.End
        : error.Start < word.End && word.Start < error.End;

    // A line that asks something: the form was used in a question (S20).
    private static bool IsQuestion(string text) => text.Contains('?', StringComparison.Ordinal) || text.Contains('¿', StringComparison.Ordinal);

    private readonly record struct FormKey(string Form, string? Person, string? Number, string? Tense, string? Mood, bool Question);

    private struct FormCount
    {
        public int Times;
        public int TimesCorrect;
        public WordUse? Last;
    }

    private sealed class RowState(string lemma, string pos)
    {
        public string Lemma { get; } = lemma;

        public string Pos { get; } = pos;

        public string? Gender { get; set; }

        public int Times { get; set; }

        public int TimesCorrect { get; set; }

        public Dictionary<FormKey, FormCount> Forms { get; } = [];

        public WordUse? First { get; set; }

        public WordUse? Last { get; set; }

        public static RowState From(WordRow row)
        {
            var state = new RowState(row.Lemma, row.Pos)
            {
                Gender = row.Gender,
                Times = row.Times,
                TimesCorrect = row.TimesCorrect,
                First = row.First,
                Last = row.Last,
            };
            foreach (var form in row.Forms)
            {
                state.Forms[new FormKey(form.Form, form.Person, form.Number, form.Tense, form.Mood, form.Question)] = new FormCount { Times = form.Times, TimesCorrect = form.TimesCorrect, Last = form.Last };
            }

            return state;
        }

        public WordRow ToRow()
        {
            var forms = Forms
                .OrderBy(static f => f.Key.Form, StringComparer.Ordinal)
                .ThenBy(static f => f.Key.Person ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static f => f.Key.Number ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static f => f.Key.Tense ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static f => f.Key.Mood ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(static f => f.Key.Question)
                .Select(static f => new FormRow(f.Key.Form, f.Key.Person, f.Key.Number, f.Key.Tense, f.Key.Mood, f.Value.Times, f.Value.TimesCorrect, f.Key.Question, f.Value.Last))
                .ToList();
            return new WordRow(Lemma, Pos, Gender, Times, TimesCorrect, forms, First ?? throw new InvalidOperationException("A word row has no use."), Last ?? First);
        }
    }
}
