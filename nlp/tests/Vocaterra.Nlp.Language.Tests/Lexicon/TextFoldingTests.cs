namespace Vocaterra.Nlp.Language.Tests.Lexicon;

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
}
