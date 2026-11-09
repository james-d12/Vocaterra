using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>The toy language's morphology: a plural adds <c>s</c>, the article is <c>the</c> or <c>a</c>, nothing is irregular.</summary>
public sealed class XxMorphology : IMorphology
{
    /// <inheritdoc />
    public string Pluralize(string noun) => noun + "s";

    /// <inheritdoc />
    public string Singularize(string noun) => noun;

    /// <inheritdoc />
    public string InflectNoun(string lemma, string number) => number == "pl" ? lemma + "s" : lemma;

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number) => definiteness == Definiteness.Definite ? "the" : "a";

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number, string nounLemma) => Article(definiteness, gender, number);

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number, string nounLemma, string? grammaticalCase) =>
        Article(definiteness, gender, number) + (grammaticalCase is null ? string.Empty : "." + grammaticalCase);

    /// <inheritdoc />
    public string AgreeAdjective(string lemma, string gender, string number) => lemma;

    /// <inheritdoc />
    public string? Conjugate(string lemma, string person, string number, string tense) => null;

    /// <inheritdoc />
    public bool IsRegularizedIrregular(string form, out string lemma, out string expected)
    {
        lemma = string.Empty;
        expected = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetRegularization(string form, out RegularizedForm result)
    {
        result = null!;
        return false;
    }
}

/// <summary>The toy language's analysis rules: no word class is closed, so every predicate is false and there is no agreement to check.</summary>
public sealed class XxAnalysisRules : IAnalysisRules
{
    /// <inheritdoc />
    public bool IsDeterminerAdjective(string lemma) => false;

    /// <inheritdoc />
    public bool IsSubjectPronoun(string lemma) => false;

    /// <inheritdoc />
    public bool IsClitic(string lemma) => false;

    /// <inheritdoc />
    public bool IsPreverbalPronoun(string lemma) => false;

    /// <inheritdoc />
    public bool IsVerbSeekingQuestionWord(string word) => false;

    /// <inheritdoc />
    public bool IsNounModifyingAdverb(string lemma) => false;

    /// <inheritdoc />
    public bool IsInterrogativeQuantifier(string lemma) => false;

    /// <inheritdoc />
    public bool IsInterjectionFirst(string word) => false;

    /// <inheritdoc />
    public bool IsNearFutureAuxiliary(string lemma) => false;

    /// <inheritdoc />
    public bool IsNearFutureLinker(string word) => false;

    /// <inheritdoc />
    public bool IsCopula(string lemma) => false;

    /// <inheritdoc />
    public bool IsCopulaOrAuxiliary(string lemma) => false;

    /// <inheritdoc />
    public bool IsElapsedTimePreposition(string lemma) => false;

    /// <inheritdoc />
    public bool IsElapsedTimeNoun(string lemma) => false;

    /// <inheritdoc />
    public bool IsPostnominalAdjective(string lemma) => false;

    /// <inheritdoc />
    public bool IsDegreeAdverb(string lemma) => false;

    /// <inheritdoc />
    public IAgreementChecker CreateAgreementChecker(IReadOnlyList<AnalyzedToken> resolved, List<ErrorTag> errors) => new XxAgreementChecker();
}

/// <summary>The toy language's agreement checker: it checks nothing and finds no past-time marker.</summary>
public sealed class XxAgreementChecker : IAgreementChecker
{
    /// <inheritdoc />
    public void Run()
    {
    }

    /// <inheritdoc />
    public bool HasPastTimeMarker() => false;
}

/// <summary>The toy language's template functions: no verb is conjugated, nouns take <c>s</c> in the plural, every noun is singular unless it ends in <c>s</c>.</summary>
public sealed class XxTemplateFunctions : ITemplateFunctions
{
    /// <inheritdoc />
    public string ResolveVerbLemma(string value) => value;

    /// <inheritdoc />
    public string? Conjugate(string lemma, string person, string number, string tense) => null;

    /// <inheritdoc />
    public string InflectNoun(string word, string number) => number == "pl" ? word + "s" : word;

    /// <inheritdoc />
    public string InferNumber(string word) => word.EndsWith('s') ? "pl" : "sg";

    /// <inheritdoc />
    public string GenderOf(string singular) => string.Empty;

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number, string singular) =>
        definiteness == Definiteness.Definite ? "the" : "a";

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number, string singular, string? grammaticalCase) =>
        Article(definiteness, gender, number, singular) + (grammaticalCase is null ? string.Empty : "." + grammaticalCase);

    /// <inheritdoc />
    public bool SupportsAdjectiveDeclension => true;

    /// <inheritdoc />
    public string DeclineAdjective(string adjective, string gender, string number, string grammaticalCase, Definiteness? articleKind) =>
        $"{adjective}-{number}.{grammaticalCase}{(articleKind is { } kind ? "." + (kind == Definiteness.Definite ? "def" : "indef") : string.Empty)}";

    /// <inheritdoc />
    public bool SupportsSeparableParticle => true;

    /// <summary>A lemma written <c>particle+stem</c> has that particle; any other lemma has none.</summary>
    public string? SeparableParticle(string verbLemma) => verbLemma.IndexOf('+', StringComparison.Ordinal) is var plus and > 0 ? verbLemma[..plus] : null;
}
