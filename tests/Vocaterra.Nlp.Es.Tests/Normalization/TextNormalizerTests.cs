using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Es.Tests.Normalization;

/// <summary>Tests for <see cref="TextNormalizer"/>.</summary>
public class TextNormalizerTests
{
    private static readonly NormalizationOptions WordsKept = new(NumbersToDigits: false);

    [Fact]
    public void StripsPunctuationAndLowercasesWithSpansIntoNormalizedText()
    {
        var result = SpanishTextNormalizer.Normalize("¿Quieres dos espadas?", WordsKept);

        Assert.Equal("quieres dos espadas", result.Text);
        Assert.Equal(3, result.Tokens.Count);
        Assert.Equal(new Token("quieres", new TextSpan(0, 7), "Quieres"), result.Tokens[0]);
        Assert.Equal(new Token("dos", new TextSpan(8, 3), "dos"), result.Tokens[1]);
        Assert.Equal(new Token("espadas", new TextSpan(12, 7), "espadas"), result.Tokens[2]);
        foreach (var token in result.Tokens)
        {
            Assert.Equal(token.Text, result.Text.Substring(token.Span.Start, token.Span.Length));
        }
    }

    [Fact]
    public void DefaultOptionsConvertSingleNumberWordsToDigits()
    {
        var result = SpanishTextNormalizer.Normalize("¿Quieres dos espadas?");

        Assert.Equal("quieres 2 espadas", result.Text);
        Assert.Equal(new Token("2", new TextSpan(8, 1), "dos"), result.Tokens[1]);
        Assert.Equal(new TextSpan(10, 7), result.Tokens[2].Span);
    }

    [Fact]
    public void ConvertsCompoundNumberWordToDigits()
    {
        var result = SpanishTextNormalizer.Normalize("veintidós espadas");

        Assert.Equal("22 espadas", result.Text);
        Assert.Equal(new Token("22", new TextSpan(0, 2), "veintidós"), result.Tokens[0]);
        Assert.Equal(new Token("espadas", new TextSpan(3, 7), "espadas"), result.Tokens[1]);
    }

    [Fact]
    public void MergesMultiWordNumberIntoOneTokenWithSourceOriginal()
    {
        var result = SpanishTextNormalizer.Normalize("ciento veinticinco");

        Assert.Equal("125", result.Text);
        var token = Assert.Single(result.Tokens);
        Assert.Equal(new Token("125", new TextSpan(0, 3), "ciento veinticinco"), token);
    }

    [Theory]
    [InlineData("cero", "0")]
    [InlineData("uno", "1")]
    [InlineData("diez", "10")]
    [InlineData("veintiuno", "21")]
    [InlineData("veintiún", "21")]
    [InlineData("treinta y uno", "31")]
    [InlineData("cuarenta y cinco", "45")]
    [InlineData("noventa y nueve", "99")]
    [InlineData("cien", "100")]
    [InlineData("ciento uno", "101")]
    [InlineData("doscientos cincuenta", "250")]
    [InlineData("quinientas", "500")]
    [InlineData("mil", "1000")]
    [InlineData("mil quinientos", "1500")]
    [InlineData("dos mil veintidós", "2022")]
    [InlineData("veintiún mil", "21000")]
    [InlineData("treinta y un mil", "31000")]
    [InlineData("cien mil", "100000")]
    [InlineData("novecientos noventa y nueve mil novecientos noventa y nueve", "999999")]
    [InlineData("VEINTIDOS", "22")]
    public void ConvertsNumberPhrases(string input, string expected)
    {
        Assert.Equal(expected, SpanishTextNormalizer.Normalize(input).Text);
    }

    [Fact]
    public void KeepsSeparateNumbersAndTheConjunctionApart()
    {
        Assert.Equal("2 3", SpanishTextNormalizer.Normalize("dos tres").Text);
        Assert.Equal("2 y 3", SpanishTextNormalizer.Normalize("dos y tres").Text);
        Assert.Equal("100 20", SpanishTextNormalizer.Normalize("cien veinte").Text);
        Assert.Equal("1000 1000", SpanishTextNormalizer.Normalize("mil mil").Text);
    }

    [Fact]
    public void KeepsDigitTokensAsTheyAre()
    {
        var result = SpanishTextNormalizer.Normalize("Quiero 3 espadas y 22 escudos");

        Assert.Equal("quiero 3 espadas y 22 escudos", result.Text);
    }

    [Fact]
    public void LeavesNumberWordsAloneWhenConversionIsOff()
    {
        Assert.Equal("veintidós espadas", SpanishTextNormalizer.Normalize("Veintidós espadas", WordsKept).Text);
    }

    [Fact]
    public void ConvertsStandaloneUnAndUnaByDefaultButCanKeepThemAsArticles()
    {
        var keep = new NormalizationOptions(ConvertIndefiniteArticles: false);

        Assert.Equal("1 espada", SpanishTextNormalizer.Normalize("una espada").Text);
        Assert.Equal("una espada", SpanishTextNormalizer.Normalize("una espada", keep).Text);
        Assert.Equal("31 espadas", SpanishTextNormalizer.Normalize("treinta y una espadas", keep).Text);
        Assert.Equal("1", SpanishTextNormalizer.Normalize("uno", keep).Text);
    }

    [Fact]
    public void ExpandsContractionsOnlyWhenEnabled()
    {
        var expand = new NormalizationOptions(ExpandContractions: true);

        var plain = SpanishTextNormalizer.Normalize("Voy al mercado del pueblo");
        Assert.Equal("voy al mercado del pueblo", plain.Text);

        var expanded = SpanishTextNormalizer.Normalize("Voy al mercado del pueblo", expand);
        Assert.Equal("voy a el mercado de el pueblo", expanded.Text);
        Assert.Equal(new Token("a", new TextSpan(4, 1), "al"), expanded.Tokens[1]);
        Assert.Equal(new Token("el", new TextSpan(6, 2), "al"), expanded.Tokens[2]);
        Assert.Equal(new Token("de", new TextSpan(17, 2), "del"), expanded.Tokens[4]);
        Assert.Equal(new Token("el", new TextSpan(20, 2), "del"), expanded.Tokens[5]);
        foreach (var token in expanded.Tokens)
        {
            Assert.Equal(token.Text, expanded.Text.Substring(token.Span.Start, token.Span.Length));
        }
    }

    [Fact]
    public void CanonicallyEquivalentInputsNormalizeToEqualResults()
    {
        var composed = SpanishTextNormalizer.Normalize("¿Qué café, señor?");
        var decomposed = SpanishTextNormalizer.Normalize("¿Qué café, señor?");

        Assert.Equal("qué café señor", composed.Text);
        Assert.Equal(composed, decomposed);
        Assert.True(composed.Text.IsNormalized(System.Text.NormalizationForm.FormC));
    }

    [Fact]
    public void KeepsNTildeUmlautAndAccentedVowels()
    {
        var result = SpanishTextNormalizer.Normalize("PINGÜINO Ñandú CANCIÓN");

        Assert.Equal("pingüino ñandú canción", result.Text);
    }

    [Fact]
    public void KeepsInternalHyphensOnly()
    {
        Assert.Equal("bien-estar", SpanishTextNormalizer.Normalize("bien-estar").Text);
        Assert.Equal("hola mundo", SpanishTextNormalizer.Normalize("- hola - mundo -").Text);
        Assert.Equal("hola mundo", SpanishTextNormalizer.Normalize("hola--mundo").Text);
    }

    [Fact]
    public void TreatsApostrophesAndQuotesAsSeparators()
    {
        Assert.Equal("hola qué", SpanishTextNormalizer.Normalize("\"Hola\"... (¡qué!)").Text);
        Assert.Equal("d oro", SpanishTextNormalizer.Normalize("d'oro").Text);
    }

    [Fact]
    public void CollapsesWhitespace()
    {
        var result = SpanishTextNormalizer.Normalize("  hola \t\n  mundo  ");

        Assert.Equal("hola mundo", result.Text);
        Assert.Equal(new TextSpan(5, 5), result.Tokens[1].Span);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("¿?¡!...")]
    public void EmptyOrPunctuationOnlyInputGivesNoTokens(string input)
    {
        var result = SpanishTextNormalizer.Normalize(input);

        Assert.Equal(string.Empty, result.Text);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void ThrowsOnNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => SpanishTextNormalizer.Normalize(null!));
    }

    [Fact]
    public void IsDeterministic()
    {
        const string input = "¿Cuánto cuestan ciento veinticinco espadas del herrero?";
        var options = new NormalizationOptions(ExpandContractions: true);

        Assert.Equal(SpanishTextNormalizer.Normalize(input, options), SpanishTextNormalizer.Normalize(input, options));
    }

    [Fact]
    public void DigitsToWordsReplacesStandaloneDigitRuns()
    {
        Assert.Equal("dos espadas", SpanishTextNormalizer.DigitsToWords("2 espadas"));
    }
}
