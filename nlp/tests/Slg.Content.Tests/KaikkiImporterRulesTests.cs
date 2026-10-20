using Slg.ContentBuilder;
using Slg.Nlp.Lexicon;
using NlpLexicon = Slg.Nlp.Lexicon.Lexicon;

namespace Slg.Content.Tests;

/// <summary>
/// The rules the importer applies to the real kaikki.org extract, tested on small synthetic lines in the same shape: inflected-form pages, alternative
/// spellings, unsuitable senses, letter names, plural-only nouns and plural look-alikes, the form tags that are not imported, and the deference to
/// hand-authored lexicons.
/// </summary>
public class KaikkiImporterRulesTests
{
    private const string OneSense = """[{"glosses":["x"]}]""";

    private static string Line(string word, string pos, string senses = OneSense, string forms = "[]", string? headKind = null)
    {
        var head = headKind is null ? string.Empty : ",\"head_templates\":[{\"name\":\"head\",\"args\":{\"1\":\"es\",\"2\":\"" + headKind + "\"}}]";
        return "{\"word\":\"" + word + "\",\"lang_code\":\"es\",\"pos\":\"" + pos + "\",\"senses\":" + senses + ",\"forms\":" + forms.ReplaceLineEndings(" ") + head + "}";
    }

    private static KaikkiImportResult Import(KaikkiImportOptions? options, params string[] lines)
    {
        return KaikkiImporter.Import(new StringReader(string.Join('\n', lines)), options ?? new KaikkiImportOptions());
    }

    private static KaikkiImportResult Import(params string[] lines)
    {
        return Import(null, lines);
    }

    private static string[] Forms(KaikkiImportResult result)
    {
        return [.. result.Entries.Select(entry => entry.Form).Distinct(StringComparer.Ordinal)];
    }

    // ---- Entries that are not lemmas ------------------------------------------------------------------------------------------------

    [Fact]
    public void APageThatOnlyInflectsAnotherWordIsSkippedBecauseTheLemmaListsTheForm()
    {
        var result = Import(Line("tengo", "verb", """[{"form_of":[{"word":"tener"}],"tags":["form-of","first-person"]}]"""));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.InflectedFormReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Fact]
    public void AnInflectedFormPageWithASenseOfItsOwnIsStillAnInflectedFormPage()
    {
        // huevos is the plural of huevo and also slang for "guts": the head template says "noun form".
        var result = Import(Line("huevos", "noun", """[{"form_of":[{"word":"huevo"}],"tags":["form-of","plural"]},{"glosses":["guts"],"tags":["vulgar"]}]""", headKind: "noun form"));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.InflectedFormReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Fact]
    public void ALemmaWithAnExtraInflectionSenseIsKept()
    {
        var result = Import(Line("abierto", "adj", """[{"glosses":["open"]},{"form_of":[{"word":"abrir"}],"tags":["form-of","participle"]}]"""));

        Assert.Contains(result.Entries, entry => entry.Form == "abierto" && entry.Lemma == "abierto" && entry.Pos == "adj");
    }

    [Fact]
    public void AnAlternativeSpellingOrClippingIsSkipped()
    {
        var result = Import(Line("lanza", "noun", """[{"alt_of":[{"word":"lanzaperfume"}],"tags":["alt-of","clipping","masculine"]}]"""));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.AlternativeFormReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Theory]
    [InlineData("neologism")]
    [InlineData("gender-neutral")]
    [InlineData("obsolete")]
    [InlineData("archaic")]
    [InlineData("nonstandard")]
    public void AWordWhoseEverySenseIsUnsuitableIsSkipped(string tag)
    {
        var result = Import(Line("amigue", "noun", $$"""[{"glosses":["friend"],"tags":["{{tag}}","masculine"]}]"""));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.UnsuitableSensesReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Fact]
    public void OneCurrentSenseIsEnoughToKeepTheWord()
    {
        var result = Import(Line("cosa", "noun", """[{"glosses":["thing"],"tags":["feminine"]},{"glosses":["old"],"tags":["obsolete"]}]"""));

        Assert.Contains(result.Entries, entry => entry.Lemma == "cosa");
    }

    [Fact]
    public void TheNameOfALetterIsSkippedButANounWithAnotherSenseIsKept()
    {
        var letter = Import(Line("ese", "noun", """[{"glosses":["The name of the letter S"],"categories":[{"name":"Latin letter names"}],"tags":["feminine"]}]"""));
        var both = Import(Line("pi", "noun", """[{"glosses":["pi"],"categories":[{"name":"Greek letter names"}]},{"glosses":["3.14"],"categories":[{"name":"Mathematics"}],"tags":["masculine"]}]"""));

        Assert.Empty(letter.Entries);
        Assert.Equal(KaikkiImporter.LetterNameReason, Assert.Single(letter.SkippedByReason).Key);
        Assert.Contains(both.Entries, entry => entry.Lemma == "pi");
    }

    [Fact]
    public void ArticlesAreNeverImported()
    {
        var result = Import(Line("la", "article", forms: """[{"form":"las","tags":["plural"]}]"""), Line("unx", "article"));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.ArticleReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Fact]
    public void AVerbWhoseHeadWordIsNotAnInfinitiveIsSkippedButAReflexiveInfinitiveIsKept()
    {
        var result = Import(
            Line("persona", "verb", """[{"glosses":["only used in se persona"]}]"""),
            Line("irse", "verb"));

        Assert.Equal(["irse"], Forms(result));
        Assert.Equal(1, result.SkippedByReason[KaikkiImporter.NotAnInfinitiveReason]);
    }

    [Theory]
    [InlineData("s.")]
    [InlineData("a1")]
    [InlineData("o'clock")]
    public void AHeadWordThatIsNotMadeOfLettersIsSkipped(string word)
    {
        var result = Import(Line(word, "noun"));

        Assert.Empty(result.Entries);
        Assert.Equal(1, result.SkippedEntries);
    }

    // ---- Forms that are not imported -------------------------------------------------------------------------------------------------

    private const string HablarForms = """
        [
          {"form":"hablo","tags":["first-person","indicative","present","singular"]},
          {"form":"hablás","tags":["indicative","informal","present","second-person","singular","vos-form"]},
          {"form":"hablaba","tags":["first-person","imperfect","indicative","singular"]},
          {"form":"hablase","tags":["first-person","imperfect","imperfect-se","singular","subjunctive"]},
          {"form":"hablara","tags":["first-person","imperfect","singular","subjunctive"]},
          {"form":"hablare","tags":["first-person","future","singular","subjunctive"]},
          {"form":"hablaré","tags":["first-person","future","indicative","singular"]},
          {"form":"habla","tags":["imperative","informal","second-person","singular"]},
          {"form":"hables","tags":["imperative","negative","second-person","singular"]},
          {"form":"hablarme","tags":["combined-form","dative","infinitive"]},
          {"form":"fué","tags":["alternative"]},
          {"form":"hablemos","tags":["first-person","imperative","plural"]}
        ]
        """;

    [Fact]
    public void VosFormsMisspellingsFutureSubjunctiveAndSeSubjunctiveAreNotImported()
    {
        var result = Import(Line("hablar", "verb", forms: HablarForms));

        var forms = Forms(result);

        Assert.Contains("hablo", forms);
        Assert.Contains("hablaba", forms);
        Assert.Contains("hablara", forms);
        Assert.Contains("hablaré", forms);
        Assert.DoesNotContain("hablás", forms);
        Assert.DoesNotContain("hablase", forms);
        Assert.DoesNotContain("hablare", forms);
        Assert.DoesNotContain("hables", forms);
        Assert.DoesNotContain("hablarme", forms);
        Assert.DoesNotContain("fué", forms);
    }

    [Fact]
    public void ImperativesAreImportedWithTheirMoodAndNoTense()
    {
        var result = Import(Line("hablar", "verb", forms: HablarForms));

        Assert.Contains(result.Entries, entry => entry.Form == "habla" && entry.Features is { Person: "2", Number: "sg", Tense: null, Mood: "imp" });
        Assert.Contains(result.Entries, entry => entry.Form == "hablemos" && entry.Features is { Person: "1", Number: "pl", Tense: null, Mood: "imp" });
    }

    [Fact]
    public void ANounThatExistsOnlyInThePluralHasAPluralHeadWord()
    {
        var result = Import(Line("pantalones", "noun", """[{"glosses":["trousers"],"tags":["masculine","plural","plural-only"]}]"""));

        var entry = Assert.Single(result.Entries);
        Assert.Equal(new LexiconEntry("pantalones", "pantalones", "noun", new EntryFeatures(null, "pl", null, null, "m")), entry);
    }

    [Fact]
    public void ANounLemmaThatIsOnlyThePluralOfAnotherNounIsDropped()
    {
        // Wiktionary has "diamantes" (the suit in a deck of cards) as a singular noun of its own next to the plural of diamante.
        var result = Import(
            Line("diamante", "noun", """[{"glosses":["diamond"],"tags":["masculine"]}]""", """[{"form":"diamantes","tags":["plural"]}]"""),
            Line("diamantes", "noun", """[{"glosses":["diamonds (card suit)"],"tags":["masculine","uncountable"]}]"""));

        Assert.Equal(1, result.PluralLookalikeLemmas);
        Assert.DoesNotContain(result.Entries, entry => entry.Lemma == "diamantes");
        Assert.Contains(result.Entries, entry => entry.Form == "diamantes" && entry.Lemma == "diamante" && entry.Features.Number == "pl");
    }

    [Fact]
    public void ALookAlikeIsDroppedBeforeTheFrequencyRankingToo()
    {
        var frequency = FrequencyList.Parse(new StringReader("diamantes 1000\ndiamante 10\nperro 5\n"));
        var result = Import(
            new KaikkiImportOptions { Frequency = frequency, Top = 1 },
            Line("diamante", "noun", """[{"glosses":["diamond"],"tags":["masculine"]}]""", """[{"form":"diamantes","tags":["plural"]}]"""),
            Line("diamantes", "noun", """[{"glosses":["diamonds"],"tags":["masculine"]}]"""),
            Line("perro", "noun", """[{"glosses":["dog"],"tags":["masculine"]}]"""));

        Assert.Equal(["diamante"], result.Entries.Select(entry => entry.Lemma).Distinct(StringComparer.Ordinal));
        Assert.Equal(1, result.PluralLookalikeLemmas);
    }

    // ---- Hand-authored deference ---------------------------------------------------------------------------------------------------

    private static HandAuthoredLemmas HandAuthored()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(
            directory.PathOf("hand.json"),
            """
            {"schemaVersion":1,"generated_by":"hand-authored","entries":[
              {"form":"espada","lemma":"espada","pos":"noun","features":{"number":"sg","gender":"f"}},
              {"form":"comer","lemma":"comer","pos":"verb","features":{}},
              {"form":"la","lemma":"el","pos":"art","features":{"number":"sg","gender":"f"}},
              {"form":"una","lemma":"un","pos":"art","features":{"number":"sg","gender":"f"}},
              {"form":"uno","lemma":"1","pos":"num","features":{"number":"sg","gender":"m"}},
              {"form":"si","lemma":"si","pos":"conj","features":{}},
              {"form":"bien","lemma":"bien","pos":"adv","features":{}}]}
            """);
        return HandAuthoredLemmas.Read(directory.PathOf("hand.json"));
    }

    [Fact]
    public void ALemmaThatAHandAuthoredLexiconDefinesIsLeftAlone()
    {
        var options = new KaikkiImportOptions { HandAuthored = HandAuthored() };

        // espada is a noun in the hand-authored lexicon (feminine, the sword); Wiktionary also has the matador, who is masculine.
        var result = Import(options, Line("espada", "noun", """[{"glosses":["sword"],"tags":["masculine"]}]""", """[{"form":"espadas","tags":["plural"]}]"""));

        Assert.Empty(result.Entries);
        Assert.Equal(KaikkiImporter.HandAuthoredReason, Assert.Single(result.SkippedByReason).Key);
    }

    [Fact]
    public void AVerbThatAHandAuthoredLexiconDefinesStillAddsItsForms()
    {
        var options = new KaikkiImportOptions { HandAuthored = HandAuthored() };

        var result = Import(options, Line("comer", "verb", forms: """[{"form":"comía","tags":["first-person","imperfect","indicative","singular"]}]"""));

        Assert.Contains(result.Entries, entry => entry.Form == "comía");
        Assert.Contains(result.Entries, entry => entry.Form == "comer");
    }

    [Fact]
    public void TheSpellingOfAHandAuthoredFunctionWordIsReservedForHeadWordsAndForms()
    {
        var options = new KaikkiImportOptions { HandAuthored = HandAuthored() };

        // The note "la" and the noun "si" would read every article and conjunction as a noun too; "una" and "uno" are forms of unir.
        var result = Import(
            options,
            Line("la", "noun", """[{"glosses":["la (note)"],"tags":["masculine"]}]"""),
            Line("si", "noun", """[{"glosses":["si (note)"],"tags":["masculine"]}]"""),
            Line("unir", "verb", forms: """[{"form":"unen","tags":["indicative","plural","present","third-person"]},{"form":"una","tags":["present","singular","subjunctive","third-person"]},{"form":"uno","tags":["first-person","indicative","present","singular"]}]"""));

        Assert.Equal(["unen", "unir"], Forms(result));
        Assert.Equal(2, result.SkippedByReason[KaikkiImporter.FunctionWordReason]);
    }

    [Fact]
    public void WithoutAHandAuthoredLexiconNothingIsDeferredTo()
    {
        var result = Import(Line("espada", "noun", """[{"glosses":["sword"],"tags":["feminine"]}]"""));

        Assert.Contains(result.Entries, entry => entry.Lemma == "espada");
    }

    [Fact]
    public void ReadingTheHandAuthoredLexiconsCollectsLemmasAndFunctionWordForms()
    {
        var hand = HandAuthored();

        Assert.Equal(["hand.json"], hand.FileNames);
        Assert.Contains("espada", hand.Lemmas);
        Assert.Contains("el", hand.Lemmas);
        Assert.Contains("1", hand.Lemmas);
        Assert.Equal(["bien", "la", "si", "una", "uno"], hand.FunctionWordForms.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("espada", hand.FunctionWordForms);
    }

    [Fact]
    public void AFolderIsReadWithoutSidecarsAndWithoutImportedFiles()
    {
        using var directory = new TempDirectory();
        const string Hand = """{"schemaVersion":1,"generated_by":"hand-authored","entries":[{"form":"hola","lemma":"hola","pos":"interj","features":{}}]}""";
        const string Imported = """{"schemaVersion":1,"generated_by":"wiktionary-import","entries":[{"form":"gato","lemma":"gato","pos":"noun","features":{"number":"sg","gender":"m"}}]}""";
        File.WriteAllText(directory.PathOf("a-hand.json"), Hand);
        File.WriteAllText(directory.PathOf("b-imported.json"), Imported);
        File.WriteAllText(directory.PathOf("b-imported.source.json"), "{ not a lexicon");

        var hand = HandAuthoredLemmas.Read(directory.Directory);

        Assert.Equal(["a-hand.json"], hand.FileNames);
        Assert.Equal(["hola"], hand.Lemmas);
    }

    [Fact]
    public void AMissingHandAuthoredFileIsAnError()
    {
        Assert.Throws<FileNotFoundException>(() => HandAuthoredLemmas.Read("/nonexistent/lexicon.json"));
    }

    // ---- The output stays canonical ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ImportingTheSameLinesTwiceGivesTheSameBytes()
    {
        var lines = new[]
        {
            Line("hablar", "verb", forms: HablarForms),
            Line("gato", "noun", """[{"glosses":["cat"],"tags":["masculine"]}]""", """[{"form":"gatos","tags":["plural"]},{"form":"gata","tags":["feminine"]}]"""),
            Line("rojo", "adj", forms: """[{"form":"roja","tags":["feminine"]},{"form":"rojos","tags":["masculine","plural"]}]"""),
        };

        var first = KaikkiImporter.ToJson(Import(lines).Entries);
        var second = KaikkiImporter.ToJson(Import(lines.Reverse().ToArray()).Entries);

        Assert.Equal(first, second);
        Assert.Equal(Import(lines).Entries, NlpLexicon.Load(first).Entries);
    }
}
