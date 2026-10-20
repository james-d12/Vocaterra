namespace Slg.Phonology.Tests;

/// <summary>
/// Regression test for the fuzz suite finding (O8) that <c>SpanishG2p.Phonemize</c> threw <see cref="ArgumentException"/> on a lone surrogate, because
/// the G2P normalizer handed the text straight to <c>string.Normalize</c>. A lone surrogate now separates words, like any other non-letter.
/// </summary>
public class IllFormedUtf16Tests
{
    private const char High = (char)0xD800;
    private const char Low = (char)0xDC00;

    [Fact]
    public void ALoneSurrogateSeparatesWordsInsteadOfThrowing()
    {
        foreach (var text in new[] { $"hola{High}amigo", $"hola{Low}amigo", $"{High}", $"{Low}{High}hola", $"hola{High}", $"café{Low}", $"ofrecisteis 7 la luz {High} una mes" })
        {
            var result = SpanishG2p.Phonemize(text, DialectConfig.LatinAmericanNeutral);
            var separated = SpanishG2p.Phonemize(text.Replace(High, ' ').Replace(Low, ' '), DialectConfig.LatinAmericanNeutral);

            Assert.Equal(separated.Text, result.Text);
            Assert.Equal(separated.Phones, result.Phones);
            Assert.Equal(separated.Syllables, result.Syllables);
        }
    }

    [Fact]
    public void AValidSurrogatePairIsStillASeparator()
    {
        var withEmoji = SpanishG2p.Phonemize($"hola {High}{Low} amigo", DialectConfig.LatinAmericanNeutral);
        var plain = SpanishG2p.Phonemize("hola amigo", DialectConfig.LatinAmericanNeutral);

        Assert.Equal(plain.Phones, withEmoji.Phones);
    }

    [Fact]
    public void TheNormalizerKeepsTheWordsAroundALoneSurrogate()
    {
        var normalized = G2pNormalizer.Normalize($"Hola{High}AMIGO{Low}");

        Assert.Equal("hola amigo", normalized.Text);
        Assert.Equal(2, normalized.Tokens.Count);
    }
}
