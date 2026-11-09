namespace Vocaterra.Nlp.De.Tests.Normalization;

/// <summary>Tests for <see cref="GermanNumbers"/>: spelling, parsing in all its spellings, prefix parsing, digit replacement and article agreement with case.</summary>
public class GermanNumbersTests
{
    public static TheoryData<int, string> Cardinals => new()
    {
        { 0, "null" },
        { 1, "eins" },
        { 2, "zwei" },
        { 3, "drei" },
        { 7, "sieben" },
        { 10, "zehn" },
        { 11, "elf" },
        { 12, "zwölf" },
        { 13, "dreizehn" },
        { 16, "sechzehn" },
        { 17, "siebzehn" },
        { 19, "neunzehn" },
        { 20, "zwanzig" },
        { 21, "einundzwanzig" },
        { 22, "zweiundzwanzig" },
        { 30, "dreißig" },
        { 31, "einunddreißig" },
        { 45, "fünfundvierzig" },
        { 60, "sechzig" },
        { 70, "siebzig" },
        { 99, "neunundneunzig" },
        { 100, "hundert" },
        { 101, "hunderteins" },
        { 110, "hundertzehn" },
        { 140, "hundertvierzig" },
        { 200, "zweihundert" },
        { 201, "zweihunderteins" },
        { 321, "dreihunderteinundzwanzig" },
        { 999, "neunhundertneunundneunzig" },
        { 1000, "tausend" },
        { 1001, "tausendeins" },
        { 1100, "tausendeinhundert" },
        { 2000, "zweitausend" },
        { 2300, "zweitausenddreihundert" },
        { 10000, "zehntausend" },
        { 21000, "einundzwanzigtausend" },
        { 100000, "hunderttausend" },
        { 101000, "hunderteintausend" },
        { 999999, "neunhundertneunundneunzigtausendneunhundertneunundneunzig" },
    };

    [Theory]
    [MemberData(nameof(Cardinals))]
    public void ToWordsWritesCardinals(int value, string expected)
    {
        Assert.Equal(expected, GermanNumbers.ToWords(value));
    }

    [Theory]
    [MemberData(nameof(Cardinals))]
    public void TryParseReadsWhatToWordsWrites(int value, string words)
    {
        Assert.True(GermanNumbers.TryParse(words, out var parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("Einundzwanzig", 21)]
    [InlineData("EINUNDZWANZIG", 21)]
    [InlineData("fuenf", 5)]
    [InlineData("funf", 5)]
    [InlineData("zwoelf", 12)]
    [InlineData("zwolf", 12)]
    [InlineData("dreissig", 30)]
    [InlineData("dreißig", 30)]
    [InlineData("fuenfundvierzig", 45)]
    [InlineData("zwo", 2)]
    [InlineData("ein", 1)]
    [InlineData("eintausend", 1000)]
    [InlineData("einhundert", 100)]
    [InlineData("einhundertdreiundzwanzig", 123)]
    [InlineData("eintausendzweihundert", 1200)]
    [InlineData("zweitausenddreihundertvierundfünfzig", 2354)]
    [InlineData("zweihundertzweiundachtzigtausend", 282000)]
    public void TryParseReadsEverySpelling(string text, int expected)
    {
        Assert.True(GermanNumbers.TryParse(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("zwei hundert vierzig", 240)]
    [InlineData("zweitausend dreihundert", 2300)]
    [InlineData("ein hundert", 100)]
    [InlineData("hundert eins", 101)]
    [InlineData("drei tausend", 3000)]
    [InlineData("zwanzig tausend", 20000)]
    public void TryParseReadsATokenListBrokenAtAScaleWord(string text, int expected)
    {
        Assert.True(GermanNumbers.TryParse(text.Split(' '), out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hallo")]
    [InlineData("zwanzigeins")]
    [InlineData("zehnzwei")]
    [InlineData("hundertundeins")]
    [InlineData("millionen")]
    [InlineData("zwei zwei")]
    [InlineData("drei zehn")]
    public void TryParseRefusesWhatIsNotANumber(string text)
    {
        Assert.False(GermanNumbers.TryParse(text, out _));
    }

    [Theory]
    [InlineData("einen", false)]
    [InlineData("eine", false)]
    [InlineData("eins", true)]
    public void AnArticleFormAloneIsNotConvertedByTheNormalizer(string word, bool converted)
    {
        var result = GermanNormalizer.Instance.Normalize(word);

        Assert.Equal(converted ? "1" : word, result.Text);
    }

    [Fact]
    public void TryParsePrefixReturnsTheTokensUsed()
    {
        string[] tokens = ["ich", "habe", "zwei", "hundert", "vierzig", "euro"];

        var used = GermanNumbers.TryParsePrefix(tokens, 2, out var value);

        Assert.Equal(3, used);
        Assert.Equal(240, value);
    }

    [Fact]
    public void TryParsePrefixTakesTheLongestRun()
    {
        string[] tokens = ["zweitausend", "dreihundert", "euro"];

        var used = GermanNumbers.TryParsePrefix(tokens, 0, out var value);

        Assert.Equal(2, used);
        Assert.Equal(2300, value);
    }

    [Fact]
    public void TryParsePrefixReturnsZeroForNoNumber()
    {
        var used = GermanNumbers.TryParsePrefix(["hallo", "welt"], 0, out var value);

        Assert.Equal(0, used);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryParsePrefixStopsAtTheEnd()
    {
        Assert.Equal(0, GermanNumbers.TryParsePrefix(["zwei"], 1, out _));
    }

    [Theory]
    [InlineData("3", "drei")]
    [InlineData("Ich habe 21 Euro", "Ich habe einundzwanzig Euro")]
    [InlineData("1000", "tausend")]
    [InlineData("2300 Meter", "zweitausenddreihundert Meter")]
    [InlineData("kein Wort", "kein Wort")]
    [InlineData("0", "null")]
    [InlineData("1234567", "1234567")]
    public void DigitsToWordsReplacesDigitRuns(string text, string expected)
    {
        Assert.Equal(expected, GermanNumbers.DigitsToWords(text));
    }

    [Theory]
    [InlineData("eins", "m", null, "ein")]
    [InlineData("eins", "m", "nom", "ein")]
    [InlineData("eins", "m", "acc", "einen")]
    [InlineData("eins", "m", "dat", "einem")]
    [InlineData("eins", "m", "gen", "eines")]
    [InlineData("eins", "f", "nom", "eine")]
    [InlineData("eins", "f", "acc", "eine")]
    [InlineData("eins", "f", "dat", "einer")]
    [InlineData("eins", "f", "gen", "einer")]
    [InlineData("eins", "n", "nom", "ein")]
    [InlineData("eins", "n", "acc", "ein")]
    [InlineData("eins", "n", "dat", "einem")]
    [InlineData("eins", "n", "gen", "eines")]
    [InlineData("hunderteins", "f", "acc", "hunderteine")]
    [InlineData("hunderteins", "m", "acc", "hunderteinen")]
    [InlineData("zwei", "f", "dat", "zwei")]
    [InlineData("einundzwanzig", "m", "acc", "einundzwanzig")]
    [InlineData("hundert", "n", "gen", "hundert")]
    public void AgreeGivesTheArticleEndingOfTheCase(string words, string gender, string? grammaticalCase, string expected)
    {
        Assert.Equal(expected, GermanNumbers.Agree(words, gender, grammaticalCase));
    }

    [Fact]
    public void AgreeRefusesAnUnknownCase()
    {
        Assert.Throws<NotSupportedException>(() => GermanNumbers.Agree("eins", "m", "abl"));
    }

    [Fact]
    public void ToWordsRefusesWhatItCannotSpell()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GermanNumbers.ToWords(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GermanNumbers.ToWords(1_000_000));
    }

    [Fact]
    public void EveryValueRoundTripsThroughItsWords()
    {
        for (var value = 0; value <= 20_000; value++)
        {
            Assert.True(GermanNumbers.TryParse(GermanNumbers.ToWords(value), out var parsed), $"{value}");
            Assert.Equal(value, parsed);
        }

        for (var value = 20_000; value <= GermanNumbers.MaxValue; value += 997)
        {
            Assert.True(GermanNumbers.TryParse(GermanNumbers.ToWords(value), out var parsed), $"{value}");
            Assert.Equal(value, parsed);
        }
    }

    [Fact]
    public void ThePackExposesTheSameNumbers()
    {
        var numbers = GermanLanguagePack.Instance.Numbers;

        Assert.Equal(999_999, numbers.MaxValue);
        Assert.Equal("einundzwanzig", numbers.ToWords(21));
        Assert.True(numbers.TryParse("zweihundert", out var value));
        Assert.Equal(200, value);
        Assert.Equal("einen", numbers.Agree("eins", "m", "acc"));
        Assert.Equal("ein", numbers.Agree("eins", "m"));
    }
}
