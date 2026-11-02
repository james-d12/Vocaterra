namespace Vocaterra.Nlp.Es.Text;

/// <summary>
/// The Spanish wording of an <see cref="AccentNote"/> (M70, D-game-22 item 3): a short line that names the accented form and what its accent marks, for a host to
/// show beside its reply (<c>«compré», con tilde: es pasado</c>). A host may word the note in its own words, or in the learner's language, from the note's members.
/// </summary>
public static class SpanishAccentNotes
{
    /// <summary>
    /// The line for the note: <c>«compré», con tilde: es pasado</c> for an accent that marks the tense (pasado, presente, futuro, condicional, subjuntivo or
    /// imperativo, from the accented form's tense and mood), <c>«habló», con tilde: es de él o ella</c> for one that marks only the person.
    /// </summary>
    public static string Describe(AccentNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        return $"«{note.Accented}», con tilde: es {(note.Marks == AccentMark.Tense ? TenseName(note.Reading) : "de " + PersonName(note.Reading))}";
    }

    private static string TenseName(LexicalFeatures reading)
    {
        if (reading.Mood == LexiconSchema.Subjunctive)
        {
            return "subjuntivo";
        }

        if (reading.Mood == LexiconSchema.Imperative)
        {
            return "imperativo";
        }

        return reading.Tense switch
        {
            LexiconSchema.Preterite or LexiconSchema.Imperfect => "pasado",
            LexiconSchema.Future => "futuro",
            LexiconSchema.Conditional => "condicional",
            _ => "presente",
        };
    }

    private static string PersonName(LexicalFeatures reading)
    {
        var plural = reading.Number == LexiconSchema.Plural;
        return reading.Person switch
        {
            "1" => plural ? "nosotros" : "yo",
            "2" => plural ? "vosotros" : "tú",
            _ => plural ? "ellos o ellas" : "él o ella",
        };
    }
}
