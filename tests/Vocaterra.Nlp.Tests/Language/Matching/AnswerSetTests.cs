using System.Text.Json;
using Vocaterra.Nlp.Language.Matching;

namespace Vocaterra.Nlp.Language.Tests.Matching;

/// <summary>Tests for <see cref="AnswerSet"/> and <see cref="Answer"/>.</summary>
public class AnswerSetTests
{
    private static AnswerSet Sample()
    {
        return new AnswerSet(
            "car-set",
            [
                new Answer("car", "coche", ["carro", "auto"], ["es-ES", "es-MX", "es-AR"]),
                new Answer("sword", "espada", [], []),
            ]);
    }

    [Fact]
    public void SerializesToCanonicalJsonAndBack()
    {
        var set = Sample();

        var json = set.ToJson();

        Assert.Equal(
            "{\"answerSetId\":\"car-set\",\"answers\":["
            + "{\"answerId\":\"car\",\"canonical\":\"coche\",\"aliases\":[\"carro\",\"auto\"],\"dialects\":[\"es-ES\",\"es-MX\",\"es-AR\"]},"
            + "{\"answerId\":\"sword\",\"canonical\":\"espada\",\"aliases\":[],\"dialects\":[]}]}",
            json);
        Assert.Equal(set, AnswerSet.FromJson(json));
        Assert.Equal(json, AnswerSet.FromJson(json).ToJson());
    }

    [Fact]
    public void AliasesAndDialectsAreOptionalInJson()
    {
        var set = AnswerSet.FromJson("{\"answerSetId\":\"s\",\"answers\":[{\"answerId\":\"a\",\"canonical\":\"hola\"}]}");

        var answer = Assert.Single(set.Answers);
        Assert.Empty(answer.Aliases);
        Assert.Empty(answer.Dialects);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"answerSetId\":\"s\"}")]
    [InlineData("{\"answerSetId\":\"\",\"answers\":[]}")]
    [InlineData("{\"answerSetId\":\"s\",\"answers\":[{\"answerId\":\"\",\"canonical\":\"x\"}]}")]
    [InlineData("{\"answerSetId\":\"s\",\"answers\":[{\"answerId\":\"a\",\"canonical\":\" \"}]}")]
    [InlineData("{\"answerSetId\":\"s\",\"answers\":[{\"answerId\":\"a\",\"canonical\":\"x\",\"aliases\":[\"\"]}]}")]
    [InlineData("{\"answerSetId\":\"s\",\"answers\":[{\"answerId\":\"a\",\"canonical\":\"x\"},{\"answerId\":\"a\",\"canonical\":\"y\"}]}")]
    [InlineData("not json")]
    public void RejectsMalformedOrInconsistentJson(string json)
    {
        Assert.ThrowsAny<JsonException>(() => AnswerSet.FromJson(json));
    }

    [Fact]
    public void SavesAndLoadsUtf8FileWithoutByteOrderMark()
    {
        var set = new AnswerSet("accents", [new Answer("a", "adiós", ["¿qué tal?"], [])]);
        var path = Path.Combine(Path.GetTempPath(), $"vocaterra-answerset-{Guid.NewGuid():N}.json");
        try
        {
            set.Save(path);

            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Contains("adiós", File.ReadAllText(path, System.Text.Encoding.UTF8), StringComparison.Ordinal);
            Assert.Equal(set, AnswerSet.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GrammarPhrasesAreNormalizedDistinctAndOrdinalSorted()
    {
        var set = new AnswerSet(
            "grammar",
            [
                new Answer("z", "Zeta", ["¿Ñu?"], []),
                new Answer("a", "¡Adiós!", ["zeta", "  Dos   Espadas "], []),
                new Answer("n", "2 espadas", ["veintidós espadas", "22 espadas"], []),
            ]);

        var phrases = set.GrammarPhrases(SpanishLanguagePack.Instance);

        Assert.Equal(["adiós", "dos espadas", "veintidós espadas", "zeta", "ñu"], phrases);
    }

    [Fact]
    public void GrammarPhrasesAreEmptyForAnEmptySet()
    {
        Assert.Empty(new AnswerSet("none", []).GrammarPhrases(SpanishLanguagePack.Instance));
    }

    [Fact]
    public void CanonicalPhrasesAreOnePerAnswerInDeclaredOrderWithoutAliases()
    {
        // Declared out of ordinal order on purpose: the offered choices follow the authoring order, not the sort the grammar uses.
        var set = new AnswerSet(
            "declared",
            [
                new Answer("water", "quiero agua por favor", ["agua por favor", "necesito agua"], []),
                new Answer("help", "ayuda", ["ayuda por favor"], []),
                new Answer("dont-know", "no sé", ["no lo sé"], []),
            ]);

        Assert.Equal(["quiero agua por favor", "ayuda", "no sé"], set.CanonicalPhrases(SpanishLanguagePack.Instance));
        Assert.Equal(set.CanonicalPhrases(SpanishLanguagePack.Instance), set.CanonicalPhrases(SpanishLanguagePack.Instance));
    }

    [Fact]
    public void CanonicalPhrasesAreNormalizedLikeTheGrammar()
    {
        var set = new AnswerSet("normalized", [new Answer("two", "¡2 Espadas!", [], []), new Answer("zeta", "Zeta", [], [])]);

        Assert.Equal(["dos espadas", "zeta"], set.CanonicalPhrases(SpanishLanguagePack.Instance));
        Assert.Contains("dos espadas", set.GrammarPhrases(SpanishLanguagePack.Instance));
    }

    [Fact]
    public void CanonicalPhrasesSkipDuplicatesAndBlanksAndKeepFewerThanThree()
    {
        var set = new AnswerSet(
            "short",
            [
                new Answer("a", "sí", [], []),
                new Answer("b", " ", [], []),
                new Answer("c", "Sí", ["si"], []),
                new Answer("d", "no", [], []),
            ]);

        Assert.Equal(["sí", "no"], set.CanonicalPhrases(SpanishLanguagePack.Instance));
        Assert.Equal(["sí"], new AnswerSet("one", [new Answer("a", "sí", [], [])]).CanonicalPhrases(SpanishLanguagePack.Instance));
        Assert.Empty(new AnswerSet("none", []).CanonicalPhrases(SpanishLanguagePack.Instance));
    }

    [Fact]
    public void ValueEqualityComparesListContents()
    {
        Assert.Equal(Sample(), Sample());
        Assert.Equal(Sample().GetHashCode(), Sample().GetHashCode());
        Assert.NotEqual(Sample(), new AnswerSet("car-set", [new Answer("car", "coche", ["carro"], [])]));
        Assert.NotEqual(Sample().Answers[0], Sample().Answers[1]);
    }
}
