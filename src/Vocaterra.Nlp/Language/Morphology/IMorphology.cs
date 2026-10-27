namespace Vocaterra.Nlp.Language.Morphology;

/// <summary>A regularized irregular verb form (such as <c>sabo</c>) with the lexicon form the learner should have used.</summary>
/// <param name="Lemma">The lemma of the irregular verb.</param>
/// <param name="Expected">The lexicon form of the same paradigm cell.</param>
/// <param name="Person">The person of the cell.</param>
/// <param name="Number">The number of the cell.</param>
/// <param name="Tense">The tense of the cell.</param>
public sealed record RegularizedForm(string Lemma, string Expected, string Person, string Number, string Tense);

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

    /// <summary>Detects a form that is the regular paradigm cell of an irregular lexicon verb (Spanish: <c>sabo</c>, <c>tení</c>).</summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="lemma">The irregular verb when the method returns true.</param>
    /// <param name="expected">The correct form when the method returns true.</param>
    bool IsRegularizedIrregular(string form, out string lemma, out string expected);

    /// <summary>Like <see cref="IsRegularizedIrregular"/> and also returns the person, number and tense of the cell. Known words are never reported.</summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The regularized form when the method returns true.</param>
    bool TryGetRegularization(string form, out RegularizedForm result);
}
