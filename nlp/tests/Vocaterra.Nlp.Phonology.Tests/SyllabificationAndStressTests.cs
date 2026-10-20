namespace Vocaterra.Nlp.Phonology.Tests;

/// <summary>Tests for syllabification and stress assignment.</summary>
public class SyllabificationAndStressTests
{
    [Theory]
    [InlineData("espada", "es-pa-da")]
    [InlineData("teléfono", "te-lé-fo-no")]
    [InlineData("ciudad", "ciu-dad")]
    [InlineData("país", "pa-ís")]
    [InlineData("construir", "cons-truir")]
    [InlineData("estudiáis", "es-tu-diáis")]
    [InlineData("buey", "buey")]
    [InlineData("hoy", "hoy")]
    [InlineData("muy", "muy")]
    [InlineData("rey", "rey")]
    [InlineData("Uruguay", "u-ru-guay")]
    [InlineData("ahí", "a-hí")]
    [InlineData("guion", "guion")]
    [InlineData("quiero", "quie-ro")]
    [InlineData("pingüino", "pin-güi-no")]
    [InlineData("instrumento", "ins-tru-men-to")]
    [InlineData("obstáculo", "obs-tá-cu-lo")]
    [InlineData("abstracto", "abs-trac-to")]
    [InlineData("monstruo", "mons-truo")]
    [InlineData("siglo", "si-glo")]
    [InlineData("atlas", "at-las")]
    [InlineData("hombre", "hom-bre")]
    [InlineData("perro", "pe-rro")]
    [InlineData("calle", "ca-lle")]
    [InlineData("mucho", "mu-cho")]
    [InlineData("examen", "e-xa-men")]
    [InlineData("extranjero", "ex-tran-je-ro")]
    [InlineData("leer", "le-er")]
    [InlineData("poeta", "po-e-ta")]
    [InlineData("día", "dí-a")]
    [InlineData("baúl", "ba-úl")]
    [InlineData("cuídate", "cuí-da-te")]
    [InlineData("ruido", "rui-do")]
    [InlineData("veintiuno", "vein-tiu-no")]
    [InlineData("ayuda", "a-yu-da")]
    [InlineData("y", "y")]
    public void SyllabifierFollowsSpanishOnsetAndNucleusRules(string word, string expected)
    {
        Assert.Equal(expected, TestHelpers.Hyphenate(word));
    }

    [Fact]
    public void SyllableSpansPartitionTheWord()
    {
        var result = TestHelpers.La("construcción");

        var spans = result.Syllables.Select(s => s.GraphemeSpan).ToList();
        Assert.Equal(0, spans[0].Start);
        for (var i = 1; i < spans.Count; i++)
        {
            Assert.Equal(spans[i - 1].End, spans[i].Start);
        }

        Assert.Equal(result.Text.Length, spans[^1].End);
    }

    [Theory]
    [InlineData("teléfono", 3)]
    [InlineData("música", 3)]
    [InlineData("rápido", 3)]
    public void WrittenAccentOnTheAntepenultimateSyllableIsThirdFromEnd(string word, int fromEnd)
    {
        var result = TestHelpers.La(word);

        Assert.Equal(result.Syllables.Count - fromEnd, result.StressedSyllableIndex);
    }

    [Fact]
    public void WordsEndingInAConsonantOtherThanNOrSAreStressedOnTheLastSyllable()
    {
        foreach (var word in new[] { "comer", "ciudad", "reloj", "hotel", "azul", "papel", "Uruguay", "virrey" })
        {
            var result = TestHelpers.La(word);
            Assert.Equal(result.Syllables.Count - 1, result.StressedSyllableIndex);
        }
    }

    [Fact]
    public void WordsEndingInAVowelNOrSAreStressedOnThePenultimateSyllable()
    {
        foreach (var word in new[] { "casa", "mesas", "joven", "lunes", "examen", "zapato", "cumpleaños" })
        {
            var result = TestHelpers.La(word);
            Assert.Equal(result.Syllables.Count - 2, result.StressedSyllableIndex);
        }
    }

    [Theory]
    [InlineData("café", 1)]
    [InlineData("canción", 1)]
    [InlineData("también", 1)]
    [InlineData("país", 1)]
    [InlineData("maíz", 1)]
    [InlineData("estás", 1)]
    [InlineData("árbol", 0)]
    [InlineData("lápiz", 0)]
    [InlineData("médico", 0)]
    public void WrittenAccentAlwaysWins(string word, int expectedIndex)
    {
        Assert.Equal(expectedIndex, TestHelpers.La(word).StressedSyllableIndex);
    }

    [Theory]
    [InlineData("sol")]
    [InlineData("pan")]
    [InlineData("de")]
    [InlineData("y")]
    [InlineData("qué")]
    public void MonosyllablesAreStressed(string word)
    {
        var result = TestHelpers.La(word);

        Assert.Single(result.Syllables);
        Assert.True(result.Syllables[0].Stressed);
        Assert.Equal(0, result.StressedSyllableIndex);
    }

    [Fact]
    public void EveryWordOfAPhraseCarriesItsOwnStress()
    {
        var result = TestHelpers.La("buenos días señor");

        Assert.Equal(new[] { true, false, true, false, false, true }, result.Syllables.Select(s => s.Stressed).ToArray());
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2 }, result.Syllables.Select(s => s.WordIndex).ToArray());
        Assert.Equal(0, result.StressedSyllableIndex);
    }

    [Fact]
    public void TextWithoutLettersHasNoSyllablesAndNoStress()
    {
        var result = TestHelpers.La("¿?");

        Assert.Empty(result.Phones);
        Assert.Empty(result.Syllables);
        Assert.Equal(-1, result.StressedSyllableIndex);
        Assert.Equal(string.Empty, result.ToIpaString());
    }
}
