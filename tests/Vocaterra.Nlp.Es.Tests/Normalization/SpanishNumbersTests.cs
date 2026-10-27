using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Es.Tests.Normalization;

/// <summary>Tests for <see cref="SpanishNumbers"/>.</summary>
public class SpanishNumbersTests
{
    [Theory]
    [InlineData(0, "cero")]
    [InlineData(1, "uno")]
    [InlineData(2, "dos")]
    [InlineData(16, "dieciséis")]
    [InlineData(21, "veintiuno")]
    [InlineData(22, "veintidós")]
    [InlineData(30, "treinta")]
    [InlineData(31, "treinta y uno")]
    [InlineData(100, "cien")]
    [InlineData(101, "ciento uno")]
    [InlineData(115, "ciento quince")]
    [InlineData(200, "doscientos")]
    [InlineData(500, "quinientos")]
    [InlineData(999, "novecientos noventa y nueve")]
    [InlineData(1000, "mil")]
    [InlineData(1001, "mil uno")]
    [InlineData(2000, "dos mil")]
    [InlineData(21000, "veintiún mil")]
    [InlineData(31000, "treinta y un mil")]
    [InlineData(100000, "cien mil")]
    [InlineData(101000, "ciento un mil")]
    [InlineData(125342, "ciento veinticinco mil trescientos cuarenta y dos")]
    [InlineData(999999, "novecientos noventa y nueve mil novecientos noventa y nueve")]
    public void ToWordsWritesCardinals(int value, string expected)
    {
        Assert.Equal(expected, SpanishNumbers.ToWords(value));
    }

    [Fact]
    public void ToWordsRoundTripsThroughTryParseForEveryValue()
    {
        for (var value = 0; value <= SpanishNumbers.MaxValue; value++)
        {
            var words = SpanishNumbers.ToWords(value);
            Assert.True(SpanishNumbers.TryParse(words, out var parsed), words);
            Assert.Equal(value, parsed);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void ToWordsRejectsOutOfRangeValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpanishNumbers.ToWords(value));
    }

    [Fact]
    public void TryParseAcceptsTokenListsAndUnaccentedWords()
    {
        Assert.True(SpanishNumbers.TryParse(new[] { "veintidos" }, out var a));
        Assert.Equal(22, a);
        Assert.True(SpanishNumbers.TryParse("Dieciseis", out var b));
        Assert.Equal(16, b);
        Assert.True(SpanishNumbers.TryParse("  mil   veintidós ", out var c));
        Assert.Equal(1022, c);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hola")]
    [InlineData("dos hola")]
    [InlineData("cien veinte")]
    [InlineData("dos tres")]
    [InlineData("y")]
    [InlineData("treinta y")]
    public void TryParseRejectsAnythingThatIsNotExactlyOneNumber(string text)
    {
        Assert.False(SpanishNumbers.TryParse(text, out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryParsePrefixReportsHowManyTokensWereUsed()
    {
        var tokens = new[] { "quiero", "treinta", "y", "cinco", "espadas" };

        Assert.Equal(0, SpanishNumbers.TryParsePrefix(tokens, 0, out _));
        Assert.Equal(3, SpanishNumbers.TryParsePrefix(tokens, 1, out var value));
        Assert.Equal(35, value);
        Assert.Equal(0, SpanishNumbers.TryParsePrefix(tokens, 5, out _));
        Assert.Equal(1, SpanishNumbers.TryParsePrefix(new[] { "treinta", "y", "espadas" }, 0, out var thirty));
        Assert.Equal(30, thirty);
    }

    [Theory]
    [InlineData("2 espadas", "dos espadas")]
    [InlineData("tengo 25 y 100", "tengo veinticinco y cien")]
    [InlineData("0", "cero")]
    [InlineData("007", "007")]
    [InlineData("2da", "2da")]
    [InlineData("a2", "a2")]
    [InlineData("1000000", "1000000")]
    [InlineData("sin numeros", "sin numeros")]
    [InlineData("", "")]
    public void DigitsToWordsReplacesOnlyStandaloneDigitRuns(string text, string expected)
    {
        Assert.Equal(expected, SpanishNumbers.DigitsToWords(text));
    }
}
