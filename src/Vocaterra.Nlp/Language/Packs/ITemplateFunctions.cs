using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// The language-specific operations behind the filters of a dialogue template (<c>article()</c>, <c>words()</c>, <c>conj()</c>, <c>plural-if()</c>): the
/// template engine parses the placeholders, validates the arguments and renders; this interface inflects. A language pack creates one over its lexicon and
/// morphology (<see cref="ILanguagePack.CreateTemplateFunctions"/>). M34, ADR 0011. Pure and deterministic.
/// </summary>
public interface ITemplateFunctions
{
    /// <summary>Resolves the text of a <c>conj</c> slot to a verb lemma: the lemma itself when the lexicon has it, else the lemma of the first verb reading of the word, else the text.</summary>
    /// <param name="value">The slot value, trimmed and not empty.</param>
    string ResolveVerbLemma(string value);

    /// <summary>Conjugates a verb lemma, or returns null when the form cannot be generated.</summary>
    /// <param name="lemma">The verb lemma.</param>
    /// <param name="person">The person, <c>1</c> to <c>3</c>.</param>
    /// <param name="number">The number, <c>sg</c> or <c>pl</c>.</param>
    /// <param name="tense">The tense, a value of the pack's lexicon profile.</param>
    string? Conjugate(string lemma, string person, string number, string tense);

    /// <summary>Returns the form of a noun in the given number.</summary>
    /// <param name="word">The noun, one word.</param>
    /// <param name="number">The number, <c>sg</c> or <c>pl</c>.</param>
    string InflectNoun(string word, string number);

    /// <summary>The number a noun has as written when no quantity decides it: the lexicon reading, else singular.</summary>
    /// <param name="word">The noun as written.</param>
    string InferNumber(string word);

    /// <summary>The gender of a noun given in the singular: the lexicon first, else the language's spelling heuristic.</summary>
    /// <param name="singular">The singular form.</param>
    string GenderOf(string singular);

    /// <summary>The article that goes with a noun, agreeing in gender and number.</summary>
    /// <param name="definiteness">Definite or indefinite.</param>
    /// <param name="gender">The gender from <see cref="GenderOf"/>.</param>
    /// <param name="number">The number.</param>
    /// <param name="singular">The singular form of the noun.</param>
    string Article(Definiteness definiteness, string gender, string number, string singular);

    /// <summary>
    /// The article that goes with a noun, agreeing in gender, number and grammatical case (M88, additive). A null case is the pack's default and gives the article
    /// of the overload without a case; the default implementation throws <see cref="NotSupportedException"/> for any other case.
    /// </summary>
    /// <param name="definiteness">Definite or indefinite.</param>
    /// <param name="gender">The gender from <see cref="GenderOf"/>.</param>
    /// <param name="number">The number.</param>
    /// <param name="singular">The singular form of the noun.</param>
    /// <param name="grammaticalCase">The case id, a value of <see cref="ILanguagePack.GrammaticalCases"/>, or null for the default.</param>
    string Article(Definiteness definiteness, string gender, string number, string singular, string? grammaticalCase) =>
        grammaticalCase is null
            ? Article(definiteness, gender, number, singular)
            : throw new NotSupportedException($"This language has no grammatical case; article() cannot take '{grammaticalCase}'.");

    /// <summary>True when <see cref="DeclineAdjective"/> works: the template filter <c>decl(gender,number,case[,def|indef])</c> is available (M88, gap 5). The default is false.</summary>
    bool SupportsAdjectiveDeclension => false;

    /// <summary>
    /// The adjective with the ending that agrees in gender, number and case, and with the kind of article before it (German <c>gut</c> to <c>guten</c>; M88, gap 5). The
    /// default implementation throws <see cref="NotSupportedException"/>; the template engine reports "not supported by this language" before it calls this when
    /// <see cref="SupportsAdjectiveDeclension"/> is false.
    /// </summary>
    /// <param name="adjective">The adjective lemma, as the slot holds it.</param>
    /// <param name="gender">The gender.</param>
    /// <param name="number">The number.</param>
    /// <param name="grammaticalCase">The case id.</param>
    /// <param name="articleKind">The article before the adjective (definite, indefinite), or null when there is none.</param>
    string DeclineAdjective(string adjective, string gender, string number, string grammaticalCase, Definiteness? articleKind) =>
        throw new NotSupportedException("decl() is not supported by this language.");

    /// <summary>True when <see cref="SeparableParticle"/> works: the template filter <c>particle</c> is available (M88, gap 5). The default is false.</summary>
    bool SupportsSeparableParticle => false;

    /// <summary>
    /// The separable particle of a verb lemma (German <c>anrufen</c> gives <c>an</c>), or null when the verb has none (M88, gap 5). The default implementation throws
    /// <see cref="NotSupportedException"/>; the template engine reports "not supported by this language" first when <see cref="SupportsSeparableParticle"/> is false.
    /// </summary>
    /// <param name="verbLemma">The verb lemma.</param>
    string? SeparableParticle(string verbLemma) => throw new NotSupportedException("particle() is not supported by this language.");
}
