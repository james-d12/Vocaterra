using System.Globalization;
using Vocaterra.Game;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>How a cell of a conjugation table is shown (S28, D-game-19).</summary>
public enum CellState
{
    /// <summary>The learner has used this form: highlighted, with its count.</summary>
    Used,

    /// <summary>A lesson that is completed or under way has taught this form and the learner has not used it yet: shown plainly.</summary>
    Taught,

    /// <summary>Neither taught nor used: the cell is empty (a dash), so nothing overwhelms.</summary>
    Locked,
}

/// <summary>One cell of a conjugation table.</summary>
/// <param name="Text">The form, from the core's morphology (<c>quiero</c>, <c>voy a comer</c>), or <see langword="null"/> for a locked cell.</param>
/// <param name="State">How the cell is shown.</param>
/// <param name="Uses">How many times the learner has used the form (0 unless <see cref="CellState.Used"/>).</param>
public sealed record ConjugationCell(string? Text, CellState State, int Uses);

/// <summary>One row of a conjugation table: a person (yo, tú, él/ella/usted, nosotros, ustedes/ellos).</summary>
/// <param name="Label">The Spanish pronoun.</param>
/// <param name="Person">The grammatical person, <c>1</c> to <c>3</c>.</param>
/// <param name="Number"><c>sg</c> or <c>pl</c>.</param>
/// <param name="Cells">One cell per visible column, in column order.</param>
public sealed record ConjugationRow(string Label, string Person, string Number, IReadOnlyList<ConjugationCell> Cells);

/// <summary>One visible column of a conjugation table: a tense.</summary>
/// <param name="Tense">The tense code: <c>pres</c>, <c>pret</c> or <c>ir-a</c> (the near future, <c>ir</c> + <c>a</c> + the infinitive).</param>
/// <param name="Title">The English heading: present, past or future (ir a).</param>
public sealed record ConjugationColumn(string Tense, string Title);

/// <summary>
/// The conjugation table of one verb the learner has met (S28, D-game-19): five rows (yo, tú, él/ella/usted, nosotros, ustedes/ellos; there is no
/// vosotros row) and the columns the verb has earned: a tense once a lesson that is completed or under way has taught
/// one of the verb's forms in that tense, or the learner has used one (what the learner says is never hidden from them); the present alone when there is none yet.
/// </summary>
/// <param name="Lemma">The infinitive.</param>
/// <param name="Gloss">The English of the infinitive (<c>to want</c>), or <see langword="null"/> when the verb has none.</param>
/// <param name="Columns">The visible columns.</param>
/// <param name="Rows">The five rows.</param>
public sealed record ConjugationTable(string Lemma, string? Gloss, IReadOnlyList<ConjugationColumn> Columns, IReadOnlyList<ConjugationRow> Rows)
{
    /// <summary>The persons of the rows, in order: label, person, number.</summary>
    public static IReadOnlyList<(string Label, string Person, string Number)> Persons { get; } =
    [
        ("yo", "1", "sg"),
        ("tú", "2", "sg"),
        ("él/ella/usted", "3", "sg"),
        ("nosotros", "1", "pl"),
        ("ustedes/ellos", "3", "pl"),
    ];

    /// <summary>The columns a table can have, in order: present, past, future (ir a).</summary>
    public static IReadOnlyList<ConjugationColumn> AllColumns { get; } =
    [
        new("pres", "present"),
        new("pret", "past"),
        new(TaughtVerbForm.NearFuture, "future (ir a)"),
    ];

    /// <summary>
    /// Builds the table of a verb. The counts come from the learner's word record (<paramref name="verb"/>, the forms it holds) for the present and the past, and
    /// from the history for the future (ir a), which is a phrase and not a word: <c>ir</c> in the present, <c>a</c>, then the infinitive. Two readings of the core
    /// are handled here, in the game: an imperative reading whose text is the indicative present of its verb is that present (<see cref="VerbReading"/>, the
    /// rule the labels use too). So <c>vamos</c> (the word record shows <c>vamos (1 pl imp)</c>) counts in <c>ir</c>'s present, 1 pl, and <c>está</c> in
    /// <c>estar</c>'s present, 3 sg. <c>vamos a comer</c> also counts as the future (ir a) of <c>comer</c>.
    /// </summary>
    /// <param name="verb">The verb's row of the learner's record.</param>
    /// <param name="history">The learner's history, oldest first (for the future (ir a) counts).</param>
    /// <param name="taught">The verb forms taught by the lessons that are completed or under way.</param>
    /// <param name="morphology">The bundle's morphology (every form comes from here).</param>
    /// <param name="gloss">The verb's English, or <see langword="null"/>.</param>
    public static ConjugationTable Build(WordRow verb, IReadOnlyList<InputEntry> history, IReadOnlyList<TaughtVerbForm> taught, MorphologyGenerator morphology, VerbGloss? gloss)
    {
        ArgumentNullException.ThrowIfNull(verb);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(taught);
        ArgumentNullException.ThrowIfNull(morphology);
        var lemma = verb.Lemma;
        var nearFutureUses = NearFutureUses(history, lemma, morphology);

        var cellsByTense = new Dictionary<string, List<ConjugationCell>>(StringComparer.Ordinal);
        foreach (var column in AllColumns)
        {
            var cells = new List<ConjugationCell>();
            foreach (var (_, person, number) in Persons)
            {
                var form = new TaughtVerbForm(lemma, person, number, column.Tense);
                var text = form.Text(morphology);
                var uses = column.Tense == TaughtVerbForm.NearFuture
                    ? nearFutureUses.GetValueOrDefault((person, number))
                    : WordRecordUses(verb, person, number, column.Tense, morphology);
                var isTaught = taught.Any(t => Same(t, form));
                var state = uses > 0 ? CellState.Used : isTaught ? CellState.Taught : CellState.Locked;
                cells.Add(new ConjugationCell(state == CellState.Locked ? null : text, state, uses));
            }

            cellsByTense[column.Tense] = cells;
        }

        // A column is there once a lesson has taught a form of its tense, or the learner has used one. A verb with neither (only its infinitive was said) still gets the present.
        var visible = AllColumns.Where(column => cellsByTense[column.Tense].Any(static c => c.State != CellState.Locked)).ToList();
        if (visible.Count == 0)
        {
            visible.Add(AllColumns[0]);
        }

        var rows = new List<ConjugationRow>();
        for (var i = 0; i < Persons.Count; i++)
        {
            var (label, person, number) = Persons[i];
            rows.Add(new ConjugationRow(label, person, number, visible.Select(column => cellsByTense[column.Tense][i]).ToList()));
        }

        return new ConjugationTable(lemma, gloss?.English, visible, rows);
    }

    private static bool Same(TaughtVerbForm a, TaughtVerbForm b) =>
        string.Equals(a.Lemma, b.Lemma, StringComparison.Ordinal) && string.Equals(a.Person, b.Person, StringComparison.Ordinal)
        && string.Equals(a.Number, b.Number, StringComparison.Ordinal) && string.Equals(a.Tense, b.Tense, StringComparison.Ordinal);

    private static int WordRecordUses(WordRow verb, string person, string number, string tense, MorphologyGenerator morphology) =>
        verb.Forms.Where(form => Matches(verb.Lemma, form, person, number, tense, morphology)).Sum(static form => form.Times);

    // A form is in a cell when its reading (VerbReading: an imperative whose text is the indicative present of the verb counts as that present) is that person,
    // number and tense in the indicative. So "está" is estar's 3 sg present, and "vamos" is ir's 1 pl present, as the labels read them.
    private static bool Matches(string lemma, FormRow form, string person, string number, string tense, MorphologyGenerator morphology)
    {
        var (readPerson, readNumber, readTense, readMood) = VerbReading.Normalize(lemma, form.Form, form.Person, form.Number, form.Tense, form.Mood, morphology);
        return readPerson == person && readNumber == number && readTense == tense && (readMood is null or "ind");
    }

    /// <summary>
    /// The uses of the future (ir a) of a verb, by person and number: <c>ir</c> in the present (the text of its form for that person), then <c>a</c>, then the verb. It
    /// is read from the history's words in order, over the lines the word record counts (not held, not a button, not a check answer). <c>vamos a comer</c> counts
    /// for <c>comer</c>, 1 pl, whatever the core made of <c>vamos</c>.
    /// </summary>
    private static Dictionary<(string Person, string Number), int> NearFutureUses(IReadOnlyList<InputEntry> history, string lemma, MorphologyGenerator morphology)
    {
        var uses = new Dictionary<(string, string), int>();
        var irForms = new Dictionary<string, (string Person, string Number)>(StringComparer.Ordinal);
        foreach (var (_, person, number) in Persons)
        {
            if (morphology.Conjugate("ir", person, number, "pres") is { } ir)
            {
                irForms[Plain(ir)] = (person, number);
            }
        }

        foreach (var entry in history.Where(static e => !e.Held && e.Source != InputSource.Button && e.Place != InputPlace.Check))
        {
            var words = entry.Words;
            for (var i = 0; i + 2 < words.Count; i++)
            {
                if (irForms.TryGetValue(Plain(words[i].Text), out var key)
                    && string.Equals(Plain(words[i + 1].Text), "a", StringComparison.Ordinal)
                    && string.Equals(words[i + 2].Lemma, lemma, StringComparison.Ordinal))
                {
                    uses[key] = uses.GetValueOrDefault(key) + 1;
                }
            }
        }

        return uses;
    }

    private static string Plain(string text) => text.Trim().ToLower(CultureInfo.InvariantCulture);
}
