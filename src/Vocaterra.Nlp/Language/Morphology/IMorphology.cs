namespace Vocaterra.Nlp.Language.Morphology;

/// <summary>A regularized irregular verb form (such as <c>sabo</c>) with the lexicon form the learner should have used.</summary>
/// <param name="Lemma">The lemma of the irregular verb.</param>
/// <param name="Expected">The lexicon form of the same paradigm cell.</param>
/// <param name="Person">The person of the cell.</param>
/// <param name="Number">The number of the cell.</param>
/// <param name="Tense">The tense of the cell.</param>
public sealed record RegularizedForm(string Lemma, string Expected, string Person, string Number, string Tense);

/// <summary>
/// A learner's plural of a lexicon noun made with the wrong ending (such as <c>pans</c>), with the plural the learner should have used (M58); since M66 also of a lexicon
/// adjective (<c>azuls</c>).
/// </summary>
/// <param name="Lemma">The lemma of the noun or adjective.</param>
/// <param name="Expected">The plural of the noun or adjective.</param>
public sealed record PluralSlip(string Lemma, string Expected);

/// <summary>
/// Generates and recognizes the word forms of one language: nouns, articles, adjectives, verbs and regularized irregular forms, lexicon first and rules
/// second. A language pack creates one over its lexicon (<see cref="Packs.ILanguagePack.CreateMorphology"/>); the engine and the template functions use it
/// through this interface (M34, ADR 0011). The Spanish implementation is the Spanish pack's <c>MorphologyGenerator</c>. Pure and deterministic.
/// </summary>
public interface IMorphology
{
    /// <summary>Returns the plural of a noun.</summary>
    /// <param name="noun">The noun, singular.</param>
    string Pluralize(string noun);

    /// <summary>Returns the singular of a noun.</summary>
    /// <param name="noun">The noun, plural or singular.</param>
    string Singularize(string noun);

    /// <summary>Returns the noun form for a lemma in the given number (<c>sg</c> or <c>pl</c>), lexicon first.</summary>
    /// <param name="lemma">The noun lemma.</param>
    /// <param name="number">The number, <c>sg</c> or <c>pl</c>.</param>
    string InflectNoun(string lemma, string number);

    /// <summary>Returns the article for the gender and number.</summary>
    /// <param name="definiteness">Definite or indefinite.</param>
    /// <param name="gender">The gender value of the pack's profile.</param>
    /// <param name="number">The number, <c>sg</c> or <c>pl</c>.</param>
    string Article(Definiteness definiteness, string gender, string number);

    /// <summary>Returns the article for the gender and number of the noun with the given lemma, which may change the form (Spanish: <c>el agua</c>).</summary>
    /// <param name="definiteness">Definite or indefinite.</param>
    /// <param name="gender">The gender.</param>
    /// <param name="number">The number.</param>
    /// <param name="nounLemma">The noun lemma.</param>
    string Article(Definiteness definiteness, string gender, string number, string nounLemma);

    /// <summary>Returns the adjective form agreeing in gender and number; the lemma is the base form.</summary>
    /// <param name="lemma">The adjective lemma.</param>
    /// <param name="gender">The gender.</param>
    /// <param name="number">The number.</param>
    string AgreeAdjective(string lemma, string gender, string number);

    /// <summary>Returns the finite form for a lemma, person, number and tense, or null when it cannot be generated.</summary>
    /// <param name="lemma">The verb lemma.</param>
    /// <param name="person">The person, <c>1</c> to <c>3</c>.</param>
    /// <param name="number">The number.</param>
    /// <param name="tense">The tense.</param>
    string? Conjugate(string lemma, string person, string number, string tense);

    /// <summary>
    /// The persons and tenses of the conjugation table of the language (M87): what a host shows as the rows and the columns of a verb's table. The default is
    /// <see cref="ParadigmLayout.Default"/>; a pack overrides it with its own persons and tenses.
    /// </summary>
    ParadigmLayout Paradigm => ParadigmLayout.Default;

    /// <summary>
    /// The cells of a verb's table (M87): for each tense of <see cref="Paradigm"/> in order, one cell for each person in order, each with the form
    /// <see cref="Conjugate"/> generates (null where it generates none).
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    IReadOnlyList<ParadigmCell> ParadigmOf(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var layout = Paradigm;
        var cells = new List<ParadigmCell>(layout.Tenses.Count * layout.Persons.Count);
        foreach (var tense in layout.Tenses)
        {
            foreach (var person in layout.Persons)
            {
                cells.Add(new ParadigmCell(person, tense, Conjugate(lemma, person.Person, person.Number, tense)));
            }
        }

        return cells;
    }

    /// <summary>Detects a form that is the regular paradigm cell of an irregular lexicon verb (Spanish: <c>sabo</c>, <c>tení</c>).</summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="lemma">The irregular verb when the method returns true.</param>
    /// <param name="expected">The correct form when the method returns true.</param>
    bool IsRegularizedIrregular(string form, out string lemma, out string expected);

    /// <summary>Like <see cref="IsRegularizedIrregular"/> and also returns the person, number and tense of the cell. Known words are never reported.</summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The regularized form when the method returns true.</param>
    bool TryGetRegularization(string form, out RegularizedForm result);

    /// <summary>
    /// Like <see cref="TryGetRegularization"/> for a form that is also a lexicon word (M58; Spanish: <c>costa</c>, the noun, is <c>costar</c>'s cell for
    /// <c>cuesta</c> with the stem change missed). The analyzer gives the word that reading besides its own and keeps it only where the sentence calls for the
    /// verb. A form that differs from the expected one only in its accents is not reported (<c>esta</c> for <c>está</c> is an accent variant). The default
    /// finds none.
    /// </summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The regularized form when the method returns true.</param>
    bool TryGetRegularizationOfWord(string form, out RegularizedForm result)
    {
        result = null!;
        return false;
    }

    /// <summary>
    /// Detects a form the lexicon lacks that is the plural of a lexicon noun made with the ending of another class of nouns (M58; Spanish: <c>pans</c>,
    /// <c>flors</c>, <c>relojs</c> for <c>panes</c>, <c>flores</c>, <c>relojes</c>), or of a lexicon adjective made the same way (M66; Spanish: <c>azuls</c> for
    /// <c>azules</c>). The default finds none.
    /// </summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The noun or adjective and the plural the learner should have used, when the method returns true.</param>
    bool TryGetPluralSlip(string form, out PluralSlip result)
    {
        result = null!;
        return false;
    }
}
