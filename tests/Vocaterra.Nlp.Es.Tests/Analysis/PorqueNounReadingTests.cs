namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;

/// <summary>
/// M96 (fuzz seeds <c>dTc3W4dw5Cg2</c>, O4, and <c>7PcP-w7xauk1</c>, O5): <c>porque</c> with no word after it fell back to its accented sibling, the noun <c>porqué</c>,
/// even after a plural determiner, where the number check then asked for <c>porqués</c> (and a recast wrote it). A singular noun cannot follow <c>unos</c>, so the word
/// stays the conjunction there. The singular neighbours keep their readings.
/// </summary>
public class PorqueNounReadingTests
{
    private static readonly LinguisticAnalyzer Analyzer = new(TestSupport.Bundle.Lexicon, SpanishLanguagePack.Instance);

    private static string Errors(string sentence)
    {
        return string.Join(';', Analyzer.Analyze(TestTokens.From(sentence)).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    [Theory]
    [InlineData("túneles unos porque")]
    [InlineData("vebegoz unos porque", "vocab.unknown|vebegoz|")]
    [InlineData("los porque")]
    public void PorqueAfterAPluralDeterminerIsNotMadeThePluralNoun(string sentence, string expected = "")
    {
        Assert.Equal(expected, Errors(sentence));
    }

    [Theory]
    [InlineData("quiero pan porque tengo hambre")]
    [InlineData("no sé el porqué")]
    [InlineData("no sé el porque")]
    [InlineData("no sé los porqués")]
    [InlineData("por qué porque sí")]
    public void TheNeighboursAreGradedAsBefore(string sentence)
    {
        Assert.Equal(string.Empty, Errors(sentence));
    }
}
