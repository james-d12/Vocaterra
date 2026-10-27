using System.Text;

namespace Vocaterra.Nlp.Es.Tests.Phonology;

/// <summary>Tests for the normalization and digraph grouping stages.</summary>
public class NormalizerAndGrouperTests
{
    [Fact]
    public void NormalizeComposesLowercasesAndKeepsSpanishLetters()
    {
        var decomposed = "NIÑO  CóMO   PINGÜINO";

        var normalized = G2pNormalizer.Normalize(decomposed);

        Assert.Equal("niño cómo pingüino", normalized.Text);
        Assert.Equal(3, normalized.Tokens.Count);
        Assert.True(normalized.Text.IsNormalized(NormalizationForm.FormC));
    }

    [Fact]
    public void NormalizeStripsPunctuationAndDigitsButKeepsWordBoundaries()
    {
        var normalized = G2pNormalizer.Normalize("¿Cómo estás? ¡Bien! 123 e-mail, \"hola\"...");

        Assert.Equal("cómo estás bien e mail hola", normalized.Text);
    }

    [Fact]
    public void NormalizeTokensCarrySpansIntoTheNormalizedTextAndTheOriginalSpelling()
    {
        var normalized = G2pNormalizer.Normalize("  ¡Hola,   MUNDO!");

        Assert.Equal("hola mundo", normalized.Text);
        Assert.Equal("hola", normalized.Tokens[0].Text);
        Assert.Equal(new Vocaterra.Nlp.Core.Verdicts.TextSpan(0, 4), normalized.Tokens[0].Span);
        Assert.Equal("MUNDO", normalized.Tokens[1].Original);
        Assert.Equal("mundo", normalized.Text.Substring(normalized.Tokens[1].Span.Start, normalized.Tokens[1].Span.Length));
    }

    [Fact]
    public void NormalizeFoldsForeignLettersToTheirBaseLetter()
    {
        Assert.Equal("francais naive", G2pNormalizer.Normalize("français naïve").Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("¿?!")]
    [InlineData("123")]
    public void NormalizeOfTextWithoutLettersIsEmpty(string text)
    {
        var normalized = G2pNormalizer.Normalize(text);

        Assert.Equal(string.Empty, normalized.Text);
        Assert.Empty(normalized.Tokens);
    }

    [Theory]
    [InlineData("chico", "ch|i|c|o")]
    [InlineData("llave", "ll|a|v|e")]
    [InlineData("perro", "p|e|rr|o")]
    [InlineData("queso", "qu|e|s|o")]
    [InlineData("guerra", "gu|e|rr|a")]
    [InlineData("pingüino", "p|i|n|gü|i|n|o")]
    [InlineData("guapo", "g|u|a|p|o")]
    [InlineData("agua", "a|g|u|a")]
    [InlineData("quásar", "q|u|á|s|a|r")]
    [InlineData("cocheras", "c|o|ch|e|r|a|s")]
    public void GrouperFormsDigraphsOnlyWhereSpellingRulesSaySo(string word, string expected)
    {
        var graphemes = DigraphGrouper.Group(word, 0);

        Assert.Equal(expected, string.Join('|', graphemes.Select(g => g.Text)));
    }

    [Fact]
    public void GrouperOffsetsGraphemesIntoTheNormalizedText()
    {
        var graphemes = DigraphGrouper.Group("llave", 5);

        Assert.Equal(5, graphemes[0].Start);
        Assert.Equal(7, graphemes[1].Start);
        Assert.Equal(2, graphemes[0].Length);
        Assert.Equal(10, graphemes[^1].End);
    }
}
