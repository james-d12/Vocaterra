using Slg.Nlp.Normalization;

namespace Slg.Nlp.Tests.Normalization;

/// <summary>Tests for <see cref="Accents"/>.</summary>
public class AccentsTests
{
    [Theory]
    [InlineData("canción", "cancion")]
    [InlineData("ÁÉÍÓÚ", "AEIOU")]
    [InlineData("pingüino", "pinguino")]
    [InlineData("niño", "niño")]
    [InlineData("MAÑANA", "MAÑANA")]
    [InlineData("camión grande", "camion grande")]
    [InlineData("", "")]
    [InlineData("plain ascii", "plain ascii")]
    public void StripRemovesAccentsButKeepsNTilde(string input, string expected)
    {
        Assert.Equal(expected, Accents.Strip(input));
    }

    [Fact]
    public void StripHandlesDecomposedInputAndReturnsNfc()
    {
        Assert.Equal("cafe", Accents.Strip("café"));
        Assert.Equal("niño", Accents.Strip("niño"));
        Assert.True(Accents.Strip("niñó").IsNormalized(System.Text.NormalizationForm.FormC));
    }

    [Fact]
    public void StripReturnsTheSameInstanceForAscii()
    {
        const string text = "hola mundo";

        Assert.Same(text, Accents.Strip(text));
    }

    [Fact]
    public void StripThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => Accents.Strip(null!));
    }
}
