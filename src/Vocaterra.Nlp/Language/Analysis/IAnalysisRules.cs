using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Language.Analysis;

/// <summary>
/// The language-specific rules of the analysis: the closed word classes and word lists that the shared reading resolver, the accent-variant choice,
/// the reported readings and the typed slot-grammar matching consult, and the agreement checks themselves. A language pack supplies one
/// (<see cref="Packs.ILanguagePack.CreateAnalysisRules"/>); the resolver and the analyzer stay shared (M34, ADR 0011). Every member is pure and
/// deterministic: ordinal comparison, no clock, no randomness.
/// </summary>
/// <remarks>
/// The lemma arguments are lexicon lemmas in the pack's <see cref="Packs.ILexiconFolding.Key"/> form. A language with no such class returns false
/// for it (a language without clitics returns false from <see cref="IsClitic"/>).
/// </remarks>
public interface IAnalysisRules
{
    /// <summary>True for adjectives that behave as determiners: quantifiers, possessives and demonstratives (Spanish: <c>mucho</c>, <c>mi</c>, <c>este</c>).</summary>
    /// <param name="lemma">The adjective lemma.</param>
    bool IsDeterminerAdjective(string lemma);

    /// <summary>True for personal pronouns that can be the explicit subject of a verb.</summary>
    /// <param name="lemma">The pronoun lemma.</param>
    bool IsSubjectPronoun(string lemma);

    /// <summary>True for object pronouns that may sit between a subject pronoun and its verb (Spanish: <c>me</c>, <c>lo</c>).</summary>
    /// <param name="lemma">The pronoun lemma.</param>
    bool IsClitic(string lemma);

    /// <summary>True for the pronouns that stand before a verb or before another such pronoun (Spanish: <c>me</c>, <c>te</c>, <c>se</c>, <c>lo</c>, <c>la</c>).</summary>
    /// <param name="lemma">The pronoun lemma.</param>
    bool IsPreverbalPronoun(string lemma);

    /// <summary>True for the question words that take a verb right after them, spelled with or without their accent (Spanish: <c>dónde</c>, <c>donde</c>, <c>cómo</c>).</summary>
    /// <param name="word">The folded word as typed.</param>
    bool IsVerbSeekingQuestionWord(string word);

    /// <summary>True for adverbs that may stand directly before a noun (Spanish: <c>solo agua</c>, <c>más agua</c>).</summary>
    /// <param name="lemma">The adverb lemma.</param>
    bool IsNounModifyingAdverb(string lemma);

    /// <summary>True for the interrogative quantifier that is an adverb before a verb and a determiner before a noun (Spanish: <c>cuánto</c>).</summary>
    /// <param name="lemma">The lemma.</param>
    bool IsInterrogativeQuantifier(string lemma);

    /// <summary>True for a word that is reported as an interjection unless an article or a numeral precedes it (Spanish: <c>gracias</c>).</summary>
    /// <param name="word">The word as typed, folded.</param>
    bool IsInterjectionFirst(string word);

    /// <summary>True for the lemma of the verb of the near future, <c>ir</c> in <c>ir a</c> (Spanish: <c>ir</c>).</summary>
    /// <param name="lemma">The verb lemma.</param>
    bool IsNearFutureAuxiliary(string lemma);

    /// <summary>True for the word that links that verb to the infinitive (Spanish: <c>a</c>).</summary>
    /// <param name="word">The word as typed.</param>
    bool IsNearFutureLinker(string word);

    /// <summary>True for a copula whose complement is not a finite verb (Spanish: <c>ser</c>, <c>estar</c>).</summary>
    /// <param name="lemma">The verb lemma.</param>
    bool IsCopula(string lemma);

    /// <summary>True for a copula or auxiliary whose imperative is rare enough that a clause-initial form followed by a noun phrase is the indicative (Spanish: <c>ser</c>, <c>estar</c>, <c>haber</c>).</summary>
    /// <param name="lemma">The verb lemma.</param>
    bool IsCopulaOrAuxiliary(string lemma);

    /// <summary>Creates the agreement checker (subject-verb, tense, copula, noun phrase agreement and word order) for one resolved sentence.</summary>
    /// <param name="resolved">The tokens with the readings narrowed by the resolver.</param>
    /// <param name="errors">The list the checker adds its errors to.</param>
    IAgreementChecker CreateAgreementChecker(IReadOnlyList<AnalyzedToken> resolved, List<ErrorTag> errors);
}

/// <summary>The grammar checks of a language over one resolved sentence; created by <see cref="IAnalysisRules.CreateAgreementChecker"/>.</summary>
public interface IAgreementChecker
{
    /// <summary>Runs every check once and adds the errors it finds to the list given at creation.</summary>
    void Run();

    /// <summary>True when the sentence has a marker that puts it in the past (Spanish: <c>ayer</c>); the analyzer charges a sentence with such a marker and no finite verb.</summary>
    bool HasPastTimeMarker();
}
