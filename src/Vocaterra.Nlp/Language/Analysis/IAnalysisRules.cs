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

    /// <summary>
    /// True for a preposition of elapsed time that is also a finite verb form, and that the reading resolver reads as the preposition only before a phrase of
    /// time (Spanish: <c>hace</c>, the preposition of <c>hace dos días</c> and a form of <c>hacer</c>; M59).
    /// </summary>
    /// <param name="lemma">The preposition lemma.</param>
    bool IsElapsedTimePreposition(string lemma);

    /// <summary>True for a noun of a stretch of time that a preposition of elapsed time takes (Spanish: <c>día</c>, <c>semana</c>, <c>año</c>, <c>rato</c>; M59).</summary>
    /// <param name="lemma">The noun lemma.</param>
    bool IsElapsedTimeNoun(string lemma);

    /// <summary>True for a copula whose complement is not a finite verb (Spanish: <c>ser</c>, <c>estar</c>).</summary>
    /// <param name="lemma">The verb lemma.</param>
    bool IsCopula(string lemma);

    /// <summary>True for a copula or auxiliary whose imperative is rare enough that a clause-initial form followed by a noun phrase is the indicative (Spanish: <c>ser</c>, <c>estar</c>, <c>haber</c>).</summary>
    /// <param name="lemma">The verb lemma.</param>
    bool IsCopulaOrAuxiliary(string lemma);

    /// <summary>
    /// True for an adjective that goes after its noun in neutral speech, so that before a noun it is a slip of order and not a noun of its own: a word that is such an
    /// adjective and also a noun is read as the adjective right before a word that can only be a noun (Spanish: the colours, <c>rojo</c>, <c>blanco</c>, <c>gris</c>:
    /// <c>tres rojas manzanas</c>; M62).
    /// </summary>
    /// <param name="lemma">The adjective lemma.</param>
    bool IsPostnominalAdjective(string lemma);

    /// <summary>
    /// True for an adverb of degree that modifies an adjective or an adverb and never a noun, so that a word after it that can be an adjective is one (Spanish:
    /// <c>muy</c>, <c>tan</c>: <c>es muy cara</c> is the adjective, not the noun <c>cara</c>; M62).
    /// </summary>
    /// <param name="lemma">The adverb lemma.</param>
    bool IsDegreeAdverb(string lemma);

    /// <summary>
    /// True for a verb that takes an infinitive right after it, or after the word <paramref name="linker"/> when that is not null (Spanish: <c>poder</c>,
    /// <c>querer</c>, <c>deber</c> and <c>necesitar</c>; <c>tener</c> with <c>que</c>), so that the reading resolver reads a word there that can be a finite verb of the
    /// indicative as that verb, the learner's slip for the infinitive, where its other readings cannot stand there (<c>puedo pago</c>, <c>tengo que voy</c>; M66). The
    /// default is none.
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    /// <param name="linker">The lemma of the word right after the verb, or null for the verb alone.</param>
    bool IsModalVerb(string lemma, string? linker) => false;

    /// <summary>
    /// True for a verb of <see cref="IsModalVerb"/> that may also take a noun as its object (Spanish: <c>querer</c>, <c>deber</c>, <c>necesitar</c>: <c>quiero
    /// vino</c>, <c>necesito pago</c>), so that a word after it that is a noun and a verb stays the noun unless the word after it can only be a noun (<c>quiero vendo
    /// madera</c>; M66). The default is none.
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    bool ModalTakesNounObject(string lemma) => false;

    /// <summary>
    /// True for an adverb of where a thing is, which a copula takes and a noun does not (Spanish: <c>lejos</c>, <c>cerca</c>), so that a finite form of a copula that
    /// is also a noun, with no determiner before it, right before such an adverb (past adverbs of degree) is the copula (<c>son cerca</c>, <c>son muy lejos</c>; M69).
    /// The default is none.
    /// </summary>
    /// <param name="lemma">The adverb lemma.</param>
    bool IsLocationAdverb(string lemma) => false;

    /// <summary>
    /// True for a common descriptive adjective whose noun of the same spelling is no predicate of a person or a thing without a determiner (Spanish: <c>alto</c>,
    /// <c>caro</c>, <c>bonito</c>, whose <c>alta</c>, <c>cara</c> and <c>bonito</c> are also the discharge, the face and the tuna), so that a word of it right after a
    /// copula, with no determiner, is read as the adjective where none of its noun readings agrees with the subject before the copula and the copula's number (<c>él es
    /// alta</c>, <c>la casa es bonito</c>, <c>luz son alta</c>; M69). The default is none.
    /// </summary>
    /// <param name="lemma">The adjective lemma.</param>
    bool IsPredicateAdjective(string lemma) => false;

    /// <summary>
    /// True when the words around a lexicon word of another class call for the learner's slip the word may stand for (M58: <c>podo</c>, a form of <c>podar</c>, for
    /// <c>puedo</c>; <c>esto</c> for <c>estoy</c>), so that the analysis reads the word as that slip, and reports its error, without a slot grammar that names the verb
    /// (M69): only where no reading of the word as written fits the words around it (<c>yo podo ir</c>: <c>podar</c> takes no infinitive; <c>yo esto cansado</c>).
    /// The default is never, as before M69.
    /// </summary>
    /// <param name="tokens">The tokens with every lexicon reading (the readings of the slip are not among them).</param>
    /// <param name="index">The index of the word.</param>
    /// <param name="slip">The readings of the slip: the verb form the learner meant.</param>
    bool CallsForSlip(IReadOnlyList<AnalyzedToken> tokens, int index, IReadOnlyList<LexicalFeatures> slip) => false;

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
