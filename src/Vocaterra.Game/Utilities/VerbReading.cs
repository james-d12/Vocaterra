using System.Globalization;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Game.Utilities;

/// <summary>
/// The game's reading of a used verb form (X03), shared by the book's labels and its conjugation tables so they agree. The core reads some forms as an
/// imperative with no tense (S27: <c>vamos</c>, <c>está</c>, <c>come</c>). When such a form's text is also the indicative present of the same verb, the
/// learner's answer is almost always a statement, so the form is read as that indicative present (<c>está</c> is estar's <c>he/she/it is</c>). A form
/// with no indicative twin (<c>ven</c>, a true command) keeps the core's reading. Engine free.
/// </summary>
public static class VerbReading
{
    // The persons of the indicative present, in order: a twin is the first person whose present is the text.
    private static readonly (string Person, string Number)[] Persons =
    [
        ("1", "sg"),
        ("2", "sg"),
        ("3", "sg"),
        ("1", "pl"),
        ("2", "pl"),
        ("3", "pl"),
    ];

    /// <summary>
    /// The person, number, tense and mood to label or count a form by. An imperative (or a reading with no tense and no mood) whose text is the indicative
    /// present of the same verb in some person and number is that present; every other form is returned as the core gave it.
    /// </summary>
    /// <param name="lemma">The infinitive.</param>
    /// <param name="text">The form's text.</param>
    /// <param name="person">The core's person, or <see langword="null"/>.</param>
    /// <param name="number">The core's number, or <see langword="null"/>.</param>
    /// <param name="tense">The core's tense, or <see langword="null"/>.</param>
    /// <param name="mood">The core's mood, or <see langword="null"/>.</param>
    /// <param name="morphology">The bundle's morphology.</param>
    public static (string? Person, string? Number, string? Tense, string? Mood) Normalize(
        string lemma, string text, string? person, string? number, string? tense, string? mood, MorphologyGenerator morphology)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(morphology);
        var core = (person, number, tense, mood);
        var eligible = mood == "imp" || (mood is null && tense is null);
        if (!eligible || Plain(text) == Plain(lemma))
        {
            return core; // a finite indicative, a subjunctive, a participle, or the infinitive: as the core read it
        }

        foreach (var (candidate, candidateNumber) in Persons)
        {
            if (morphology.Conjugate(lemma, candidate, candidateNumber, "pres") is { } present && Plain(present) == Plain(text))
            {
                return (candidate, candidateNumber, "pres", "ind");
            }
        }

        return core;
    }

    private static string Plain(string text) => text.Trim().ToLower(CultureInfo.InvariantCulture);
}
