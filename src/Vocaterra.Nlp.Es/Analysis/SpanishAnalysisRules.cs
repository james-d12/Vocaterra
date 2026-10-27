using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Es.Analysis;

/// <summary>
/// The Spanish analysis rules (M34): the closed word classes of <see cref="WordClasses"/>, the hand-written tables of <see cref="SpanishAnalysisTables"/> and
/// the Spanish agreement checks (<see cref="SpanishAgreementChecker"/>), behind <see cref="IAnalysisRules"/>. Created by
/// <see cref="SpanishLanguagePack.CreateAnalysisRules"/>; the word-class members do not depend on the lexicon.
/// </summary>
internal sealed class SpanishAnalysisRules : IAnalysisRules
{
    private readonly IMorphology _morphology;

    public SpanishAnalysisRules(IMorphology morphology)
    {
        ArgumentNullException.ThrowIfNull(morphology);
        _morphology = morphology;
    }

    public bool IsDeterminerAdjective(string lemma) => WordClasses.IsDeterminerAdjective(lemma);

    public bool IsSubjectPronoun(string lemma) => WordClasses.IsSubjectPronoun(lemma);

    public bool IsClitic(string lemma) => WordClasses.IsClitic(lemma);

    public bool IsPreverbalPronoun(string lemma) => SpanishAnalysisTables.IsPreverbalPronoun(lemma);

    public bool IsVerbSeekingQuestionWord(string word) => SpanishAnalysisTables.IsVerbSeekingQuestionWord(word);

    public bool IsNounModifyingAdverb(string lemma) => SpanishAnalysisTables.IsNounModifyingAdverb(lemma);

    public bool IsInterrogativeQuantifier(string lemma) => SpanishAnalysisTables.IsInterrogativeQuantifier(lemma);

    public bool IsInterjectionFirst(string word) => SpanishAnalysisTables.IsInterjectionFirst(word);

    public bool IsNearFutureAuxiliary(string lemma) => string.Equals(lemma, "ir", StringComparison.Ordinal);

    public bool IsNearFutureLinker(string word) => string.Equals(word, "a", StringComparison.Ordinal);

    public bool IsCopula(string lemma) => lemma is "ser" or "estar";

    public bool IsCopulaOrAuxiliary(string lemma) => lemma is "ser" or "estar" or "haber";

    public IAgreementChecker CreateAgreementChecker(IReadOnlyList<AnalyzedToken> resolved, List<ErrorTag> errors) =>
        new SpanishAgreementChecker(resolved, _morphology, errors, this);
}
