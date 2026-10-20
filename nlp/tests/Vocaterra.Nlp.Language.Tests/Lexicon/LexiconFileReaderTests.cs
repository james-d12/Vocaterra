namespace Vocaterra.Nlp.Language.Tests.Lexicon;

using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The hand-written lexicon reader must agree with the canonical serializer and report damage the way the serializer did.</summary>
public class LexiconFileReaderTests
{
    private static LexiconEntry Entry(string form, string lemma, string pos, string? number = null, string? gender = null, string? person = null, string? tense = null, string? mood = null)
    {
        return new LexiconEntry(form, lemma, pos, new EntryFeatures(person, number, tense, mood, gender));
    }

    [Fact]
    public void ReadsTheSeedLexiconLikeTheCanonicalDeserializer()
    {
        var json = Lexicon.ReadSeedJson();

        var read = Lexicon.ReadFile(json);
        var expected = CanonicalJson.Deserialize<LexiconFile>(json);

        Assert.Equal(expected.SchemaVersion, read.SchemaVersion);
        Assert.Equal(expected.GeneratedBy, read.GeneratedBy);
        Assert.Equal(expected.Entries, read.Entries);
    }

    [Fact]
    public void TheByteOverloadReadsTheSameFileAndSkipsAByteOrderMark()
    {
        var json = Lexicon.ReadSeedJson();
        var bytes = Encoding.UTF8.GetBytes(json);
        byte[] withBom = [0xEF, 0xBB, 0xBF, .. bytes];

        Assert.Equal(Lexicon.ReadFile(json).Entries, Lexicon.ReadFile(bytes).Entries);
        Assert.Equal(Lexicon.ReadFile(json).Entries, Lexicon.ReadFile(withBom).Entries);
    }

    [Fact]
    public void RoundTripsEveryFeatureAndAMarkerThroughTheSerializer()
    {
        var lexicon = Lexicon.FromEntries(
        [
            Entry("hablábamos", "hablar", "verb", "pl", null, "1", "impf", "ind"),
            Entry("casa", "casa", "noun", "sg", "f"),
            Entry("hola", "hola", "interj"),
        ]);

        var reloaded = Lexicon.ReadFile(lexicon.ToJson());

        Assert.Equal(lexicon.Entries, reloaded.Entries);
    }

    [Fact]
    public void UnknownPropertiesAreIgnoredAndTheLastDuplicateWins()
    {
        const string json = """
            {"extra":{"a":[1,2,{"b":null}]},"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun","note":["q"],"features":{"gender":"m","other":3,"gender":"f"}}],"generated_by":"a","generated_by":"b"}
            """;

        var file = Lexicon.ReadFile(json);

        Assert.Equal("b", file.GeneratedBy);
        Assert.Equal("f", Assert.Single(file.Entries).Features.Gender);
    }

    [Fact]
    public void TheGeneratedByMarkerMayBeNullOrMissing()
    {
        Assert.Null(Lexicon.ReadFile("""{"schemaVersion":1,"generated_by":null,"entries":[]}""").GeneratedBy);
        Assert.Null(Lexicon.ReadFile("""{"schemaVersion":1,"entries":[]}""").GeneratedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("""{"schemaVersion":"1","entries":[]}""")]
    [InlineData("""{"schemaVersion":1.5,"entries":[]}""")]
    [InlineData("""{"schemaVersion":1,"entries":{}}""")]
    [InlineData("""{"schemaVersion":1,"entries":[1]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[[]]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":3,"lemma":"x","pos":"noun","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":["noun"],"features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun","features":[]}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun","features":{"gender":5}}]}""")]
    [InlineData("""{"schemaVersion":1,"generated_by":3,"entries":[]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[]} trailing""")]
    [InlineData("""{"schemaVersion":1,"entries":[]}{}""")]
    [InlineData("""{"schemaVersion":1,"entries":[""")]
    [InlineData("""{"schemaVersion":1,"entries":[],}""")]
    public void DamagedDocumentsAreJsonExceptions(string json)
    {
        Assert.ThrowsAny<JsonException>(() => Lexicon.ReadFile(json));
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"entries":[null]}""")]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"entries":[]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":null,"lemma":"x","pos":"noun","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun","features":null}]}""")]
    public void MissingPiecesAreFormatExceptions(string json)
    {
        Assert.Throws<FormatException>(() => Lexicon.ReadFile(json));
    }

    [Fact]
    public void InvalidUtf8IsAJsonException()
    {
        byte[] bytes = [.. "{\"schemaVersion\":1,\"entries\":[{\"form\":\"x"u8, 0xFF, .. "\",\"lemma\":\"x\",\"pos\":\"noun\",\"features\":{}}]}"u8];

        Assert.ThrowsAny<JsonException>(() => Lexicon.ReadFile(bytes));
    }

    [Fact]
    public void EscapedStringsAndPropertyNamesAreDecoded()
    {
        const string json = """
            {"schemaVersion":1,"entries":[{"form":"día","lemma":"día","pos":"noun","features":{"gender":"m"}}]}
            """;

        var entry = Assert.Single(Lexicon.ReadFile(json).Entries);

        Assert.Equal("día", entry.Form);
        Assert.Equal("m", entry.Features.Gender);
    }
}
