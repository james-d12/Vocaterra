namespace Vocaterra.Nlp.Es.Tests.Lexicon;

using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;

public class LexiconTests
{
    private const string SmallJson = """
        {
          "schemaVersion": 1,
          "generated_by": "unit-test",
          "entries": [
            {"form":"fue","lemma":"ser","pos":"verb","features":{"person":"3","number":"sg","tense":"pret","mood":"ind"}},
            {"form":"fue","lemma":"ir","pos":"verb","features":{"person":"3","number":"sg","tense":"pret","mood":"ind"}},
            {"form":"está","lemma":"estar","pos":"verb","features":{"person":"3","number":"sg","tense":"pres","mood":"ind"}},
            {"form":"Casa","lemma":"casa","pos":"noun","features":{"number":"sg","gender":"f"}},
            {"form":"hola","lemma":"hola","pos":"interj","features":{}}
          ]
        }
        """;

    private static LexiconEntry Entry(string form, string lemma, string pos, string? number = null, string? gender = null)
    {
        return new LexiconEntry(form, lemma, pos, new EntryFeatures(null, number, null, null, gender));
    }

    [Fact]
    public void LoadsEntriesAndExposesProvenance()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        Assert.Equal("unit-test", lexicon.GeneratedBy);
        Assert.Equal(5, lexicon.Entries.Count);
        Assert.Equal(["casa", "estar", "hola", "ir", "ser"], lexicon.Lemmas);
    }

    [Fact]
    public void LookupReturnsAllReadingsOfARepeatedForm()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        var readings = lexicon.Lookup("fue");

        Assert.False(readings.IsAccentInsensitive);
        Assert.Equal(2, readings.Count);
        Assert.Equal(["ir", "ser"], readings.Select(r => r.Lemma).ToArray());
        Assert.All(readings, r => Assert.Equal("pret", r.Tense));
    }

    [Fact]
    public void LookupNormalizesCaseAndUnicodeForm()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        Assert.Single(lexicon.Lookup("CASA"));
        Assert.Single(lexicon.Lookup("casa"));
        Assert.Single(lexicon.Lookup("está"));
        Assert.False(lexicon.Lookup("está").IsAccentInsensitive);
    }

    [Fact]
    public void AccentInsensitiveFallbackIsFlagged()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        var folded = lexicon.Lookup("esta");

        Assert.True(folded.IsAccentInsensitive);
        Assert.Equal("estar", Assert.Single(folded).Lemma);
        Assert.Equal("esta", folded.Form);
    }

    [Fact]
    public void ExactMatchWinsOverAccentInsensitiveFallback()
    {
        var lexicon = SpanishLexicon.FromEntries([Entry("esta", "este", "adj"), Entry("está", "estar", "verb")]);

        var exact = lexicon.Lookup("esta");

        Assert.False(exact.IsAccentInsensitive);
        Assert.Equal("este", Assert.Single(exact).Lemma);
    }

    [Fact]
    public void UnknownWordHasNoReadings()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        var result = lexicon.Lookup("xyzzy");

        Assert.Empty(result);
        Assert.False(result.IsAccentInsensitive);
    }

    [Fact]
    public void EntriesAreOrdinalSortedAndInputOrderDoesNotMatter()
    {
        var entries = new[]
        {
            Entry("zorro", "zorro", "noun", "sg", "m"),
            Entry("árbol", "árbol", "noun", "sg", "m"),
            Entry("casa", "casa", "noun", "sg", "f"),
            Entry("casa", "casar", "verb"),
            Entry("abrir", "abrir", "verb"),
        };
        var reversed = entries.Reverse().ToArray();

        var first = SpanishLexicon.FromEntries(entries);
        var second = SpanishLexicon.FromEntries(reversed);

        Assert.Equal(["abrir", "casa", "casa", "zorro", "árbol"], first.Entries.Select(e => e.Form).ToArray());
        Assert.Equal(first.Entries, second.Entries);
        Assert.Equal(first.ToJson(), second.ToJson());
    }

    [Fact]
    public void ExactDuplicatesAreDropped()
    {
        var lexicon = SpanishLexicon.FromEntries([Entry("casa", "casa", "noun", "sg", "f"), Entry("Casa", "casa", "noun", "sg", "f")]);

        Assert.Single(lexicon.Entries);
    }

    [Fact]
    public void JsonRoundTripPreservesEntries()
    {
        var original = SpanishLexicon.Load(SmallJson);

        var reloaded = SpanishLexicon.Load(original.ToJson());

        Assert.Equal(original.Entries, reloaded.Entries);
        Assert.Equal(original.GeneratedBy, reloaded.GeneratedBy);
        Assert.Equal(original.ToJson(), reloaded.ToJson());
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"entries":[]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"bogus","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"verb","features":{"person":"4"}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun","features":{"gender":"n"}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"","lemma":"x","pos":"noun","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"verb","features":{"tense":"past"}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","pos":"noun"}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"lemma":"x","pos":"noun","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","pos":"noun","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1,"entries":[{"form":"x","lemma":"x","features":{}}]}""")]
    [InlineData("""{"schemaVersion":1}""")]
    public void InvalidDocumentsAreRejected(string json)
    {
        Assert.Throws<FormatException>(() => SpanishLexicon.Load(json));
    }

    [Fact]
    public void MalformedJsonIsRejected()
    {
        Assert.ThrowsAny<JsonException>(() => SpanishLexicon.Load("{ not json"));
    }

    [Fact]
    public void EntriesForLemmaAndContainsLemma()
    {
        var lexicon = SpanishLexicon.Load(SmallJson);

        Assert.True(lexicon.ContainsLemma("SER"));
        Assert.False(lexicon.ContainsLemma("tener"));
        Assert.Single(lexicon.EntriesForLemma("ir"));
        Assert.Empty(lexicon.EntriesForLemma("tener"));
    }

    [Fact]
    public void EmptyLexiconHasNothing()
    {
        Assert.Empty(SpanishLexicon.Empty.Entries);
        Assert.Empty(SpanishLexicon.Empty.Lemmas);
        Assert.Empty(SpanishLexicon.Empty.Lookup("casa"));
    }

    [Fact]
    public void ToFeaturesMapsEveryField()
    {
        var entry = new LexiconEntry("tengo", "tener", "verb", new EntryFeatures("1", "sg", "pres", "ind", null));

        Assert.Equal(new LexicalFeatures("tener", "verb", "1", "sg", "pres", "ind", null), entry.ToFeatures());
    }

    [Fact]
    public void LoadingTwiceIsDeterministic()
    {
        var first = SpanishLexicon.LoadSeed();
        var second = SpanishLexicon.LoadSeed();

        Assert.Equal(CanonicalJson.Hash(first.Entries), CanonicalJson.Hash(second.Entries));
        Assert.Equal(first.ToJson(), second.ToJson());
    }
}
