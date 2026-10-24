using Vocaterra.Game;

namespace Vocaterra.MonoGame.UI.Book;

/// <summary>
/// The plain-English label of a Spanish verb form (S20, the book's Verbs page): <c>quiero — I want</c>, <c>¿quieres? — do you want?</c>,
/// <c>está — he/she/it is</c>. The English is a small table over the reading's person, number, tense and mood (the core's codes: person 1 to 3,
/// number <c>sg</c> or <c>pl</c>, tense <c>pres</c>, <c>pret</c>, <c>impf</c>, <c>fut</c>, <c>cond</c>, mood <c>ind</c>, <c>subj</c>, <c>imp</c>), built from
/// the verb's English gloss (<c>to want</c>) and its third person and past. <c>to be</c> is the one verb with its own forms (am, is, are, was, were).
/// Plain values; no clock.
/// </summary>
public static class BookVerbLabels
{
    /// <summary>The separator between the Spanish form and its English.</summary>
    public const string Separator = " — ";

    /// <summary>The English subject of a person and number: <c>I</c>, <c>you</c>, <c>he/she/it</c>, <c>we</c>, <c>you all</c>, <c>they</c>.</summary>
    /// <param name="person">The person, <c>1</c> to <c>3</c>.</param>
    /// <param name="number"><c>sg</c> or <c>pl</c>.</param>
    /// <returns>The subject, or <see langword="null"/> for a reading with no person or an unknown one.</returns>
    public static string? Subject(string? person, string? number) => (person, number) switch
    {
        ("1", "sg") => "I",
        ("2", "sg") => "you",
        ("3", "sg") => "he/she/it",
        ("1", "pl") => "we",
        ("2", "pl") => "you all",
        ("3", "pl") => "they",
        _ => null,
    };

    /// <summary>
    /// The English of a finite form, or <see langword="null"/> when the reading is not one the table covers (an infinitive, a participle or an
    /// unknown tense): the caller shows the Spanish alone.
    /// </summary>
    /// <param name="english">The gloss (<c>to want</c>), or <see langword="null"/> when the verb has none.</param>
    /// <param name="third">The third person singular (<c>wants</c>), or <see langword="null"/> for the regular rule.</param>
    /// <param name="past">The past (<c>wanted</c>), or <see langword="null"/> for the regular rule.</param>
    /// <param name="person">The person.</param>
    /// <param name="number">The number.</param>
    /// <param name="tense">The tense.</param>
    /// <param name="mood">The mood.</param>
    /// <param name="question">Whether the form was used in a question.</param>
    public static string? English(string? english, string? third, string? past, string? person, string? number, string? tense, string? mood, bool question)
    {
        if (english is null || Subject(person, number) is not { } subject)
        {
            return null;
        }

        var verb = new Conjugation(english, third, past);
        var singularThird = person == "3" && number == "sg";
        return (mood, tense) switch
        {
            ("imp", _) => Command(verb, person, number, subject),
            ("subj", "pres") => $"that {subject} {(verb.IsBe ? "be" : verb.Base)}",
            ("ind" or null, "pres") => question
                ? $"{PresentAux(verb, person, number, singularThird)} {subject} {(verb.IsBe ? string.Empty : verb.Base + " ")}".TrimEnd() + "?"
                : $"{subject} {verb.Present(person, number)}",
            ("ind" or null, "pret") => question
                ? $"{(verb.IsBe ? verb.Past(person, number) : "did")} {subject}{(verb.IsBe ? string.Empty : " " + verb.Base)}?"
                : $"{subject} {verb.Past(person, number)}",
            ("ind" or null, "impf") => question
                ? $"did {subject} use to {verb.Base}?"
                : $"{subject} used to {verb.Base}",
            ("ind" or null, "fut") => question ? $"will {subject} {verb.Base}?" : $"{subject} will {verb.Base}",
            ("ind" or null, "cond") => question ? $"would {subject} {verb.Base}?" : $"{subject} would {verb.Base}",
            _ => null,
        };
    }

    /// <summary>
    /// The label of a verb's reading: the Spanish (with <c>¿</c> and <c>?</c> when it was asked) and the English after <see cref="Separator"/>. An
    /// infinitive (no person) is labelled with the gloss; a form the table does not cover is the Spanish alone.
    /// </summary>
    /// <param name="form">The form's text (<c>quieres</c>).</param>
    /// <param name="infinitive">The lemma (<c>querer</c>).</param>
    /// <param name="gloss">The verb's gloss and forms, or <see langword="null"/>.</param>
    /// <param name="person">The person.</param>
    /// <param name="number">The number.</param>
    /// <param name="tense">The tense.</param>
    /// <param name="mood">The mood.</param>
    /// <param name="question">Whether the form was used in a question.</param>
    public static string Label(string form, string infinitive, VerbGloss? gloss, string? person, string? number, string? tense, string? mood, bool question)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(infinitive);
        var spanish = question ? "¿" + form + "?" : form;
        if (person is null && mood is null && tense is null)
        {
            return string.Equals(form, infinitive, StringComparison.Ordinal) && gloss is not null
                ? spanish + Separator + gloss.English
                : spanish;
        }

        var english = English(gloss?.English, gloss?.Third, gloss?.Past, person, number, tense, mood, question);
        return english is null ? spanish : spanish + Separator + english;
    }

    private static string PresentAux(Conjugation verb, string? person, string? number, bool singularThird) =>
        verb.IsBe ? verb.Present(person, number) : singularThird ? "does" : "do";

    private static string Command(Conjugation verb, string? person, string? number, string subject) => (person, number) switch
    {
        ("2", "pl") => $"{verb.Base}! (you all)",
        ("2", _) => $"{verb.Base}!",
        ("1", "pl") => $"let's {verb.Base}",
        _ => $"let {subject} {verb.Base}",
    };

    // The English forms of one verb: the base (the gloss without its "to"), the present and past, and the verb "to be", which has its own.
    private sealed class Conjugation
    {
        public Conjugation(string english, string? third, string? past)
        {
            // "to be" and its glosses that say what the verb is for (ser: "to be: what", estar: "to be: where or how") all conjugate as be.
            IsBe = english is "to be" || english.StartsWith("to be:", StringComparison.Ordinal) || english.StartsWith("to be (", StringComparison.Ordinal);
            Base = IsBe ? "be" : english.StartsWith("to ", StringComparison.Ordinal) ? english[3..] : english;
            Third = third ?? (IsBe ? "is" : ThirdOf(Base));
            Pst = past ?? (IsBe ? "was" : PastOf(Base));
        }

        public bool IsBe { get; }

        public string Base { get; }

        private string Third { get; }

        private string Pst { get; }

        // The present for a person and number: am, are, is for to be; the base, or the third person's form.
        public string Present(string? person, string? number) => IsBe
            ? (person, number) switch
            {
                ("1", "sg") => "am",
                ("3", "sg") => "is",
                _ => "are",
            }
            : person == "3" && number == "sg" ? Third : Base;

        // The past for a person and number: was or were for to be; the past form otherwise.
        public string Past(string? person, string? number) => IsBe
            ? (person, number) is ("1" or "3", "sg") ? "was" : "were"
            : Pst;

        // The regular third person of the first word: wants, goes to, asks for; a verb ending in s, x, z, ch, sh or o takes es.
        private static string ThirdOf(string verb)
        {
            var (first, rest) = Split(verb);
            var inflected = first.EndsWith('y') && first.Length > 1 && !"aeiou".Contains(first[^2])
                ? first[..^1] + "ies"
                : first.EndsWith('s') || first.EndsWith('x') || first.EndsWith('z') || first.EndsWith("ch", StringComparison.Ordinal) || first.EndsWith("sh", StringComparison.Ordinal) || first.EndsWith('o')
                    ? first + "es"
                    : first + "s";
            return inflected + rest;
        }

        // The regular past of the first word: wanted, stayed; a verb ending in e takes d, a consonant and y becomes ied.
        private static string PastOf(string verb)
        {
            var (first, rest) = Split(verb);
            var inflected = first.EndsWith('e')
                ? first + "d"
                : first.EndsWith('y') && first.Length > 1 && !"aeiou".Contains(first[^2])
                    ? first[..^1] + "ied"
                    : first + "ed";
            return inflected + rest;
        }

        private static (string Head, string Tail) Split(string verb)
        {
            var space = verb.IndexOf(' ', StringComparison.Ordinal);
            return space < 0 ? (verb, string.Empty) : (verb[..space], verb[space..]);
        }
    }
}
