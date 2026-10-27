namespace Vocaterra.Nlp.Es.Tests.Lexicon;

using Vocaterra.Nlp.Language.Lexicon;

public class TextFoldingTests
{
    [Theory]
    [InlineData("Está", "está")]
    [InlineData("ESPAÑA", "españa")]
    [InlineData("está", "está")]
    [InlineData("ñ", "ñ")]
    public void NormalizeComposesAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, TextFolding.Normalize(input));
    }

    [Theory]
    [InlineData("está", "esta")]
    [InlineData("canción", "cancion")]
    [InlineData("pingüino", "pinguino")]
    [InlineData("ÁÉÍÓÚ", "aeiou")]
    [InlineData("niño", "niño")]
    [InlineData("casa", "casa")]
    [InlineData("ñó", "ño")]
    public void StripAccentsKeepsEnye(string input, string expected)
    {
        Assert.Equal(expected, TextFolding.StripAccents(input));
    }

    // M35 (O1 on the Spanish pack's keys): a lone surrogate reads as U+FFFD, as the normalization entry points do, instead of throwing. The inputs are built
    // in code: a lone surrogate does not survive the xUnit data serialization of an InlineData argument.
    [Fact]
    public void NormalizeAndStripAccentsReadALoneSurrogateAsReplacement()
    {
        var cases = new[] { ("\uD800", "�"), ("Es" + '\uDC00' + "tá", "es�tá"), ("a" + '\uD83D' + "b", "a�b") };
        foreach (var (input, expected) in cases)
        {
            Assert.Equal(expected, TextFolding.Normalize(input));
            Assert.Equal(TextFolding.StripAccents(expected), TextFolding.StripAccents(input));
        }
    }

    [Fact]
    public void ANonLoneSurrogatePairIsKeptAsItIs()
    {
        const string emoji = "😀";
        Assert.Equal(emoji, TextFolding.Normalize(emoji));
    }
}
