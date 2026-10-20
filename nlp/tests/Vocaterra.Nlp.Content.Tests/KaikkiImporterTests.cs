using Vocaterra.Nlp.ContentBuilder;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for <see cref="KaikkiImporter"/> on the hand-written wiktextract fixture.</summary>
public class KaikkiImporterTests
{
    private static readonly KaikkiImportResult Result = KaikkiImporter.ImportFile(TestSupport.KaikkiFixture);
    private static readonly NlpLexicon Imported = NlpLexicon.FromEntries(Result.Entries);

    private static LexicalFeatures Only(string form)
    {
        return Assert.Single(Imported.Lookup(form));
    }

    [Fact]
    public void TheFixtureHasTwentyLines()
    {
        Assert.Equal(20, File.ReadAllLines(TestSupport.KaikkiFixture).Length);
    }

    [Fact]
    public void CountsLinesSpanishEntriesImportsAndSkips()
    {
        Assert.Equal(20, Result.LinesRead);
        Assert.Equal(19, Result.SpanishEntries);
        Assert.Equal(13, Result.ImportedEntries);
        Assert.Equal(6, Result.SkippedEntries);
        Assert.Equal(41, Result.Entries.Count);
        Assert.Equal(
            [
                KaikkiImporter.ArticleReason,
                "multi-word or affix head word",
                "numeral (lemma is a digit string in the lexicon schema)",
                "unsupported part of speech 'contraction'",
                "unsupported part of speech 'name'",
                "unsupported part of speech 'phrase'",
            ],
            Result.SkippedByReason.Keys);
        Assert.All(Result.SkippedByReason.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void OnlySpanishEntriesAreRead()
    {
        Assert.Empty(Imported.Lookup("sword"));
        Assert.Empty(Imported.Lookup("swords"));
        Assert.False(Imported.ContainsLemma("sword"));
    }

    [Fact]
    public void TheHeadWordOfANounGetsItsGenderAndThePluralFormIsAdded()
    {
        Assert.Equal(new LexicalFeatures("espada", "noun", null, "sg", null, null, "f"), Only("espada"));
        Assert.Equal(new LexicalFeatures("espada", "noun", null, "pl", null, null, "f"), Only("espadas"));
    }

    [Fact]
    public void GenderFromTheTemplateAndFeminineFormsAreMapped()
    {
        Assert.Equal(new LexicalFeatures("gato", "noun", null, "sg", null, null, "m"), Only("gato"));
        Assert.Equal(new LexicalFeatures("gato", "noun", null, "sg", null, null, "f"), Only("gata"));
        Assert.Equal(new LexicalFeatures("gato", "noun", null, "pl", null, null, "m"), Only("gatos"));
        Assert.Equal(new LexicalFeatures("gato", "noun", null, "pl", null, null, "f"), Only("gatas"));
    }

    [Fact]
    public void ANounOfBothGendersGetsBothReadings()
    {
        var genders = Imported.Lookup("estudiante").Select(reading => reading.Gender).Order(StringComparer.Ordinal);
        var pluralGenders = Imported.Lookup("estudiantes").Select(reading => reading.Gender).Order(StringComparer.Ordinal);

        Assert.Equal(["f", "m"], genders);
        Assert.Equal(["f", "m"], pluralGenders);
    }

    [Theory]
    [InlineData("tener", null, null, null, null)]
    [InlineData("tengo", "1", "sg", "pres", "ind")]
    [InlineData("tienes", "2", "sg", "pres", "ind")]
    [InlineData("tiene", "3", "sg", "pres", "ind")]
    [InlineData("tenemos", "1", "pl", "pres", "ind")]
    [InlineData("tuve", "1", "sg", "pret", "ind")]
    [InlineData("tuvieron", "3", "pl", "pret", "ind")]
    [InlineData("tengas", "2", "sg", "pres", "subj")]
    [InlineData("ten", "2", "sg", null, "imp")]
    public void VerbFormsGetPersonNumberTenseAndMood(string form, string? person, string? number, string? tense, string? mood)
    {
        Assert.Equal(new LexicalFeatures("tener", "verb", person, number, tense, mood, null), Only(form));
    }

    [Theory]
    [InlineData("tenés")]
    [InlineData("tú tienes")]
    [InlineData("tenido")]
    [InlineData("teniendo")]
    public void VoseoMultiWordAndNonFiniteFormsAreSkipped(string form)
    {
        Assert.Empty(Imported.Lookup(form));
    }

    [Fact]
    public void AnotherVerbIsImportedWithItsForms()
    {
        Assert.Equal(new LexicalFeatures("comprar", "verb", "1", "sg", "pret", "ind", null), Only("compré"));
        Assert.Equal(
            ["compra", "comprar", "compras", "compro", "compré"],
            Imported.EntriesForLemma("comprar").Select(entry => entry.Form).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AdjectivesGetFeminineAndPluralForms()
    {
        Assert.Equal(new LexicalFeatures("rojo", "adj", null, "sg", null, null, "m"), Only("rojo"));
        Assert.Equal(new LexicalFeatures("rojo", "adj", null, "sg", null, null, "f"), Only("roja"));
        Assert.Equal(new LexicalFeatures("rojo", "adj", null, "pl", null, null, "m"), Only("rojos"));
        Assert.Equal(new LexicalFeatures("rojo", "adj", null, "pl", null, null, "f"), Only("rojas"));
    }

    [Fact]
    public void AnAdjectiveWithoutAFeminineFormServesBothGenders()
    {
        Assert.Equal(["f", "m"], Imported.Lookup("feliz").Select(reading => reading.Gender).Order(StringComparer.Ordinal));
        Assert.Equal(["f", "m"], Imported.Lookup("felices").Select(reading => reading.Gender).Order(StringComparer.Ordinal));
        Assert.All(Imported.Lookup("felices"), reading => Assert.Equal("pl", reading.Number));
    }

    [Fact]
    public void ArticlesStayHandAuthoredAndDeterminersBecomeAdjectives()
    {
        // The analyzer reads the lemmas el and un and the gender and number of each article form; Wiktionary also has entries such as la and unx
        // under lemmas of their own, so no article is imported.
        Assert.Empty(Imported.Lookup("el"));
        Assert.Empty(Imported.Lookup("la"));
        Assert.Empty(Imported.Lookup("los"));
        Assert.Equal(new LexicalFeatures("este", "adj", null, "sg", null, null, "f"), Only("esta"));
    }

    [Theory]
    [InlineData("con", "prep")]
    [InlineData("y", "conj")]
    [InlineData("yo", "pron")]
    [InlineData("muy", "adv")]
    [InlineData("hola", "interj")]
    public void FunctionWordsAreImportedWithoutFeatures(string word, string pos)
    {
        Assert.Equal(new LexicalFeatures(word, pos, null, null, null, null, null), Only(word));
    }

    [Theory]
    [InlineData("dos")]
    [InlineData("al")]
    [InlineData("por favor")]
    [InlineData("madrid")]
    [InlineData("a pesar de")]
    public void NumeralsContractionsPhrasesNamesAndMultiWordHeadsAreSkipped(string form)
    {
        Assert.Empty(Imported.Lookup(form));
    }

    [Fact]
    public void ImportedFormsDriveTheMorphologyGenerator()
    {
        var morphology = new MorphologyGenerator(Imported);

        Assert.Equal("tengo", morphology.Conjugate("tener", "1", "sg", "pres"));
        Assert.Equal("tuvieron", morphology.Conjugate("tener", "3", "pl", "pret"));
        Assert.Equal("rojas", morphology.AgreeAdjective("rojo", "f", "pl"));
        Assert.Equal("gatas", morphology.InflectNoun("gata", "pl"));
    }

    [Fact]
    public void TheOutputFileIsALexiconMarkedAsImported()
    {
        var json = KaikkiImporter.ToJson(Result.Entries);

        var reloaded = NlpLexicon.Load(json);

        Assert.Equal("wiktionary-import", reloaded.GeneratedBy);
        Assert.Equal(Result.Entries, reloaded.Entries);
        Assert.Contains("\"generated_by\": \"wiktionary-import\"", json, StringComparison.Ordinal);
        Assert.Contains("{\"form\":\"tengo\",\"lemma\":\"tener\",\"pos\":\"verb\",\"features\":{\"person\":\"1\",\"number\":\"sg\",\"tense\":\"pres\",\"mood\":\"ind\"}}", json, StringComparison.Ordinal);
        Assert.Contains("{\"form\":\"hola\",\"lemma\":\"hola\",\"pos\":\"interj\",\"features\":{}}", json, StringComparison.Ordinal);
        Assert.EndsWith("]\n}\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportingTwiceGivesIdenticalOutput()
    {
        var again = KaikkiImporter.ImportFile(TestSupport.KaikkiFixture);

        Assert.Equal(KaikkiImporter.ToJson(Result.Entries), KaikkiImporter.ToJson(again.Entries));
        Assert.Equal(Result.Summary(), again.Summary());
    }

    [Fact]
    public void MaxStopsAfterThatManyImportedEntries()
    {
        var limited = KaikkiImporter.ImportFile(TestSupport.KaikkiFixture, 3);

        Assert.Equal(3, limited.ImportedEntries);
        Assert.Equal(3, limited.SpanishEntries);
        Assert.Equal(4, limited.LinesRead);
        Assert.True(limited.Entries.Count < Result.Entries.Count);
        Assert.Contains(limited.Entries, entry => entry.Form == "roja");
        Assert.DoesNotContain(limited.Entries, entry => entry.Form == "compro");
    }

    [Fact]
    public void ZeroMaxImportsNothing()
    {
        var none = KaikkiImporter.ImportFile(TestSupport.KaikkiFixture, 0);

        Assert.Empty(none.Entries);
        Assert.Equal(0, none.LinesRead);
    }

    [Fact]
    public void ABadLineIsReportedWithItsNumber()
    {
        var text = "{\"word\":\"hola\",\"lang_code\":\"es\",\"pos\":\"intj\"}\n\n{not json}\n";

        var exception = Assert.Throws<FormatException>(() => KaikkiImporter.Import(new StringReader(text)));

        Assert.StartsWith("Line 3: invalid JSON", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALineThatIsNotAnObjectIsRejected()
    {
        Assert.Throws<FormatException>(() => KaikkiImporter.Import(new StringReader("[1,2]")));
    }

    [Fact]
    public void BlankLinesAndEntriesWithoutALanguageCodeAreIgnored()
    {
        var text = "\n  \n{\"word\":\"x\",\"pos\":\"noun\"}\n{\"word\":\"hola\",\"lang_code\":\"es\",\"pos\":\"intj\"}\n";

        var result = KaikkiImporter.Import(new StringReader(text));

        Assert.Equal(2, result.LinesRead);
        Assert.Equal(1, result.SpanishEntries);
        Assert.Equal("hola", Assert.Single(result.Entries).Form);
    }

    [Fact]
    public void AMissingWordOrPartOfSpeechIsSkippedWithAReason()
    {
        var text = "{\"lang_code\":\"es\",\"pos\":\"noun\"}\n{\"word\":\"x\",\"lang_code\":\"es\"}\n";

        var result = KaikkiImporter.Import(new StringReader(text));

        Assert.Empty(result.Entries);
        Assert.Equal(["no head word", "no part of speech"], result.SkippedByReason.Keys);
    }

    [Fact]
    public void ANounWithoutAnyGenderInformationGetsNoGender()
    {
        var text = "{\"word\":\"cosa\",\"lang_code\":\"es\",\"pos\":\"noun\",\"forms\":[{\"form\":\"cosas\",\"tags\":[\"plural\"]}]}\n";

        var result = KaikkiImporter.Import(new StringReader(text));

        Assert.Equal(
            [
                new LexiconEntry("cosa", "cosa", "noun", new EntryFeatures(null, "sg", null, null, null)),
                new LexiconEntry("cosas", "cosa", "noun", new EntryFeatures(null, "pl", null, null, null)),
            ],
            result.Entries);
    }

    [Fact]
    public void FormsTaggedAsObsoleteOrDiminutiveAreSkipped()
    {
        var text = "{\"word\":\"casa\",\"lang_code\":\"es\",\"pos\":\"noun\",\"head_templates\":[{\"args\":{\"1\":\"f\"}}],"
            + "\"forms\":[{\"form\":\"casas\",\"tags\":[\"plural\"]},{\"form\":\"casita\",\"tags\":[\"diminutive\"]},{\"form\":\"casaz\",\"tags\":[\"obsolete\",\"plural\"]}]}\n";

        var result = KaikkiImporter.Import(new StringReader(text));

        Assert.Equal(["casa", "casas"], result.Entries.Select(entry => entry.Form));
    }

    [Fact]
    public void TheSummaryNamesTheCounts()
    {
        var summary = Result.Summary();

        Assert.StartsWith("read 20 lines, 19 Spanish entries: 13 imported into 41 lexicon entries, 6 skipped\n", summary, StringComparison.Ordinal);
        Assert.Contains("  skipped 1: unsupported part of speech 'phrase'\n", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ANegativeMaxIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KaikkiImporter.Import(new StringReader(string.Empty), -1));
    }
}
