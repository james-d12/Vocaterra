namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Es.Morphology;
using Vocaterra.Nlp.Language.Analysis;

/// <summary>
/// M70 (QA-language-9): the first person of the preterite of a verb in <c>-car</c>, <c>-gar</c> or <c>-zar</c> changes its spelling before the <c>é</c>
/// (<c>busqué</c>, <c>llegué</c>, <c>empecé</c>), and the learner's slip that keeps the stem (<c>buscé</c>, <c>llegé</c>, <c>empezé</c>) is the verb with
/// <c>conj.irregular</c> and the right form as expected, as M58's missed stem change is. Before M70 each slip was an unknown word (<c>vocab.unknown</c>).
/// </summary>
public class PreteriteSpellingSlipTests
{
    private static readonly LinguisticAnalyzer Analyzer = new(TestSupport.Bundle.Lexicon, SpanishLanguagePack.Instance);

    private static readonly MorphologyGenerator Morphology = new(TestSupport.Bundle.Lexicon);

    private static string Described(string sentence)
    {
        return string.Join(';', Analyzer.Analyze(TestTokens.From(sentence)).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    [Theory]
    [InlineData("ayer buscé madera", "conj.irregular|buscé|busqué")]
    [InlineData("buscé madera", "conj.irregular|buscé|busqué")]
    [InlineData("ayer pescé en el río", "conj.irregular|pescé|pesqué")]
    [InlineData("ayer llegé al río", "conj.irregular|llegé|llegué")]
    [InlineData("ayer empezé a trabajar", "conj.irregular|empezé|empecé")]
    [InlineData("ayer pagé dos monedas", "conj.irregular|pagé|pagué")]
    [InlineData("ayer jugé en el río", "conj.irregular|jugé|jugué")]
    [InlineData("ayer tocé la puerta", "conj.irregular|tocé|toqué")]
    [InlineData("ayer cruzé el río", "conj.irregular|cruzé|crucé")]
    [InlineData("yo saqué pan y lo colocé aquí", "conj.irregular|colocé|coloqué")]
    public void TheStemKeptBeforeThePreteriteEndingIsTheVerbsSlip(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("ayer busqué madera")]
    [InlineData("ayer pesqué en el río")]
    [InlineData("ayer llegué al río")]
    [InlineData("ayer empecé a trabajar")]
    [InlineData("ayer pagué dos monedas")]
    [InlineData("ayer jugué en el río")]
    [InlineData("ayer toqué la puerta")]
    [InlineData("ayer crucé el río")]
    [InlineData("ayer recé")]
    [InlineData("ayer almorcé pan")]
    [InlineData("ayer comencé a trabajar")]
    [InlineData("ayer busqué y pagué")]
    public void TheRightFormsAndTheWordsInCeAreClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("buscé", "buscar", "busqué")]
    [InlineData("llegé", "llegar", "llegué")]
    [InlineData("empezé", "empezar", "empecé")]
    public void TheMorphologyNamesTheVerbAndItsForm(string form, string lemma, string expected)
    {
        Assert.True(Morphology.TryGetRegularization(form, out var found));
        Assert.Equal(lemma, found.Lemma);
        Assert.Equal(expected, found.Expected);
        Assert.Equal("pret", found.Tense);
    }

    [Theory]
    [InlineData("busqué")]
    [InlineData("empecé")]
    [InlineData("busce")]
    [InlineData("recé")]
    public void AWordOrAFormWithoutTheAccentIsNoSlip(string form)
    {
        Assert.False(Morphology.TryGetRegularization(form, out _));
    }
}
