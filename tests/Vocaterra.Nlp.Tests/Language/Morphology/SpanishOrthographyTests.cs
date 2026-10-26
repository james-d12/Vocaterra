namespace Vocaterra.Nlp.Language.Tests.Morphology;

using Vocaterra.Nlp.Language.Morphology;

public class SpanishOrthographyTests
{
    [Theory]
    [InlineData("casa", 2)]
    [InlineData("país", 2)]
    [InlineData("bueno", 2)]
    [InlineData("queso", 2)]
    [InlineData("guerra", 2)]
    [InlineData("pingüino", 3)]
    [InlineData("leer", 2)]
    [InlineData("ciudad", 2)]
    [InlineData("tren", 1)]
    [InlineData("hay", 1)]
    [InlineData("dios", 1)]
    [InlineData("poción", 2)]
    [InlineData("camión", 2)]
    [InlineData("herrería", 4)]
    [InlineData("murciélago", 4)]
    [InlineData("paraguay", 3)]
    [InlineData("cuidado", 3)]
    [InlineData("viernes", 2)]
    public void NucleiCountSyllables(string word, int syllables)
    {
        Assert.Equal(syllables, SpanishOrthography.Nuclei(word).Count);
    }

    [Fact]
    public void AccentHelpersAddAndRemoveAccents()
    {
        Assert.Equal("jardín", SpanishOrthography.AddAccentAt("jardin", 4));
        Assert.Equal("jardin", SpanishOrthography.RemoveAccentAt("jardín", 4));
        Assert.Equal("países", SpanishOrthography.RemoveAccentAt("países", 2));
        Assert.Equal('a', SpanishOrthography.RemoveAccent('á'));
        Assert.Equal('ñ', SpanishOrthography.RemoveAccent('ñ'));
        Assert.Equal('ó', SpanishOrthography.AddAccent('o'));
    }

    [Fact]
    public void AccentPositionPrefersTheStrongVowelOfADiphthong()
    {
        var nuclei = SpanishOrthography.Nuclei("pocion");

        Assert.Equal(4, SpanishOrthography.AccentPosition("pocion", nuclei[^1]));
        Assert.Equal(-1, SpanishOrthography.WrittenAccentIndex("pocion", nuclei[^1]));
        Assert.Equal(4, SpanishOrthography.WrittenAccentIndex("poción", SpanishOrthography.Nuclei("poción")[^1]));
    }
}
