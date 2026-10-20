namespace Slg.Asr.Tests;

/// <summary>Tests for <see cref="GrammarBuilder"/>.</summary>
public class GrammarBuilderTests
{
    [Fact]
    public void BuildProducesSortedDeduplicatedNormalizedJsonGolden()
    {
        string[] phrases = ["Hola", "adiós", "  ¿Cómo   estás? ", "hola", "[unk]", string.Empty, "Dos espadas", "   "];

        var json = GrammarBuilder.Build(phrases);

        Assert.Equal("[\"adiós\",\"cómo estás\",\"dos espadas\",\"hola\",\"[unk]\"]", json);
    }

    [Fact]
    public void BuildAcceptsDecomposedAccentsAndMergesThemWithPrecomposedForms()
    {
        string[] phrases = ["adiós", "adiós", "NIÑO"];

        Assert.Equal("[\"adiós\",\"niño\",\"[unk]\"]", GrammarBuilder.Build(phrases));
    }

    [Fact]
    public void BuildIsIndependentOfInputOrder()
    {
        string[] forward = ["uno", "dos", "tres"];
        string[] backward = ["tres", "dos", "uno"];

        Assert.Equal(GrammarBuilder.Build(forward), GrammarBuilder.Build(backward));
    }

    [Fact]
    public void BuildSortsWithOrdinalComparison()
    {
        // Ordinal: digits < lower-case ASCII < letters above U+007F; "ñu" therefore sorts after "zu".
        string[] phrases = ["ñu", "zu", "2 casas", "abc"];

        Assert.Equal(["2 casas", "abc", "zu", "ñu", "[unk]"], GrammarBuilder.BuildList(phrases));
    }

    [Fact]
    public void BuildOfNothingIsJustTheUnknownToken()
    {
        Assert.Equal("[\"[unk]\"]", GrammarBuilder.Build([]));
    }

    [Fact]
    public void BuildKeepsApostrophesAndDropsPunctuation()
    {
        string[] phrases = ["¡Hola, amigo!", "it's"];

        Assert.Equal(["hola amigo", "it's", "[unk]"], GrammarBuilder.BuildList(phrases));
    }

    [Fact]
    public void BuildRejectsNullPhrases()
    {
        Assert.Throws<ArgumentNullException>(() => GrammarBuilder.Build(null!));
        Assert.Throws<ArgumentNullException>(() => GrammarBuilder.Build(["hola", null!]));
    }
}
