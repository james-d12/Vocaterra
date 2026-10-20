using Vocaterra.Nlp.ContentBuilder;
using NlpLexicon = Vocaterra.Nlp.Nlp.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>Tests for the frequency list and the lemma frequency filter of <c>import-lexicon --frequency --top</c> on small synthetic inputs.</summary>
public class FrequencyFilterTests
{
    private static string Noun(string word, string gender, params string[] plurals)
    {
        var forms = string.Join(',', plurals.Select(static plural => "{\"form\":\"" + plural + "\",\"tags\":[\"plural\"]}"));
        return "{\"word\":\"" + word + "\",\"lang_code\":\"es\",\"pos\":\"noun\",\"senses\":[{\"glosses\":[\"x\"],\"tags\":[\"" + gender + "\"]}],\"forms\":[" + forms + "]}";
    }

    private static string Verb(string infinitive, params string[] thirdPersonPresent)
    {
        var forms = string.Join(',', thirdPersonPresent.Select(static form => "{\"form\":\"" + form + "\",\"tags\":[\"indicative\",\"present\",\"singular\",\"third-person\"]}"));
        return "{\"word\":\"" + infinitive + "\",\"lang_code\":\"es\",\"pos\":\"verb\",\"senses\":[{\"glosses\":[\"x\"]}],\"forms\":[" + forms + "]}";
    }

    private static string Adverb(string word)
    {
        return "{\"word\":\"" + word + "\",\"lang_code\":\"es\",\"pos\":\"adv\",\"senses\":[{\"glosses\":[\"x\"]}]}";
    }

    private static KaikkiImportResult Import(string frequencyText, int? top, params string[] lines)
    {
        var options = new KaikkiImportOptions { Frequency = FrequencyList.Parse(new StringReader(frequencyText)), Top = top };
        return KaikkiImporter.Import(new StringReader(string.Join('\n', lines)), options);
    }

    private static string[] Lemmas(KaikkiImportResult result)
    {
        return [.. result.Entries.Select(static entry => entry.Lemma).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // ---- The list ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void WordsAreCaseFoldedAndKeepTheirAccents()
    {
        var list = FrequencyList.Parse(new StringReader("Él 5\nél 3\nel 7\n\n  \nQUÉ 2\n"));

        Assert.True(list.TryGetCount("él", out var accented));
        Assert.True(list.TryGetCount("el", out var plain));
        Assert.True(list.TryGetCount("qué", out var upper));
        Assert.False(list.TryGetCount("que", out _));
        Assert.Equal(8, accented);
        Assert.Equal(7, plain);
        Assert.Equal(2, upper);
        Assert.Equal(4, list.LinesRead);
        Assert.Equal(3, list.DistinctWords);
        Assert.Equal(17, list.TotalCount);
    }

    [Fact]
    public void ATabOrSeveralSpacesSeparateTheWordFromTheCount()
    {
        var list = FrequencyList.Parse(new StringReader("casa\t12\nperro   4\r\n"));

        Assert.True(list.TryGetCount("casa", out var casa));
        Assert.True(list.TryGetCount("perro", out var perro));
        Assert.Equal(12, casa);
        Assert.Equal(4, perro);
    }

    [Theory]
    [InlineData("casa")]
    [InlineData("casa x")]
    [InlineData("casa 1 2")]
    [InlineData("casa -3")]
    [InlineData("casa 1.5")]
    public void AMalformedLineIsReportedWithItsNumber(string bad)
    {
        var exception = Assert.Throws<FormatException>(() => FrequencyList.Parse(new StringReader("hola 5\n" + bad + "\n")));

        Assert.Contains("line 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFileReadsUtf8()
    {
        using var directory = new TempDirectory();
        File.WriteAllText(directory.PathOf("freq.txt"), "mañana 9\ncanción 4\n");

        var list = FrequencyList.ReadFile(directory.PathOf("freq.txt"));

        Assert.True(list.TryGetCount("mañana", out var count));
        Assert.Equal(9, count);
    }

    // ---- The ranking ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheFrequencyOfALemmaIsTheSumOfTheCountsOfItsDistinctForms()
    {
        // casa: casa 100 + casas 50. A form that two entries of one lemma share (azul as an adjective and as a noun) counts once.
        var azulAdjective = "{\"word\":\"azul\",\"lang_code\":\"es\",\"pos\":\"adj\",\"senses\":[{\"glosses\":[\"blue\"]}],\"forms\":[{\"form\":\"azules\",\"tags\":[\"plural\"]}]}";
        var azulNoun = Noun("azul", "masculine", "azules");

        var casa = Import("casa 100\ncasas 50\n", 1, Noun("casa", "feminine", "casas"));
        var azul = Import("azul 10\nazules 5\n", 1, azulAdjective, azulNoun);

        Assert.Equal(150, casa.Filter!.CutoffFrequency);
        Assert.Equal(15, azul.Filter!.CutoffFrequency);
    }

    [Fact]
    public void AFormSharedByTwoLemmasCountsForBoth()
    {
        // casa is the noun and also the third person of casar: both lemmas earn the 100.
        var result = Import("casa 100\ncasar 10\n", 2, Noun("casa", "feminine", "casas"), Verb("casar", "casa"));

        Assert.Equal(["casa", "casar"], Lemmas(result));
        Assert.Equal(100, result.Filter!.CutoffFrequency);
    }

    [Fact]
    public void TheTopLemmasAreKeptAndTiesBreakByOrdinalLemmaOrder()
    {
        var lines = new[]
        {
            Noun("perro", "masculine"),
            Noun("gato", "masculine"),
            Noun("casa", "feminine"),
            Noun("raro", "masculine"),
        };

        var result = Import("casa 80\nperro 50\ngato 50\nraro 1\n", 2, lines);

        Assert.Equal(["casa", "gato"], Lemmas(result));
        Assert.Equal(2, result.Filter!.LemmasKept);
        Assert.Equal(2, result.Filter.LemmasDroppedBelowCutoff);
        Assert.Equal(50, result.Filter.CutoffFrequency);
        Assert.Equal(2, result.Filter.Top);
    }

    [Fact]
    public void TheOrderOfTheInputDoesNotChangeTheResult()
    {
        var lines = new[] { Noun("perro", "masculine"), Noun("gato", "masculine"), Noun("casa", "feminine") };
        const string Frequency = "casa 80\nperro 50\ngato 50\n";

        var forward = Import(Frequency, 2, lines);
        var backward = Import(Frequency, 2, lines.Reverse().ToArray());

        Assert.Equal(KaikkiImporter.ToJson(forward.Entries), KaikkiImporter.ToJson(backward.Entries));
    }

    [Fact]
    public void AskingForMoreLemmasThanHaveAFrequencyKeepsOnlyThoseThatDo()
    {
        var result = Import("casa 80\nperro 50\n", 10, Noun("casa", "feminine"), Noun("perro", "masculine"), Noun("zorro", "masculine"));

        Assert.Equal(["casa", "perro"], Lemmas(result));
        Assert.Equal(3, result.Filter!.LemmasConsidered);
        Assert.Equal(2, result.Filter.LemmasWithFrequency);
        Assert.Equal(1, result.Filter.LemmasDroppedNoFrequency);
        Assert.Equal(0, result.Filter.LemmasDroppedBelowCutoff);
    }

    [Fact]
    public void WithoutTopEveryLemmaIsKeptEvenWithoutAFrequency()
    {
        var result = Import("casa 80\n", null, Noun("casa", "feminine"), Noun("zorro", "masculine"));

        Assert.Equal(["casa", "zorro"], Lemmas(result));
        Assert.Equal(2, result.Filter!.LemmasKept);
        Assert.Equal(1, result.Filter.LemmasWithFrequency);
        Assert.Null(result.Filter.Top);
        Assert.Equal(0, result.Filter.CutoffFrequency);
    }

    [Fact]
    public void WithoutAFrequencyListTheFilterReportIsAbsent()
    {
        var result = KaikkiImporter.Import(new StringReader(Noun("casa", "feminine")));

        Assert.Null(result.Filter);
    }

    [Fact]
    public void ACountIsLookedUpCaseFoldedButAccentSensitive()
    {
        // papá has its own count; the word papa (potato) is another lemma with another count.
        var result = Import("Papá 10\npapa 5\n", 1, Noun("papá", "masculine"), Noun("papa", "feminine"));

        Assert.Equal(["papá"], Lemmas(result));
        Assert.Equal(10, result.Filter!.CutoffFrequency);
    }

    [Fact]
    public void ALemmaWhoseOwnHeadWordIsNotInTheListCannotBeKeptOnTheStrengthOfItsOtherForms()
    {
        // perar is a rare verb whose first person singular is pero, one of the most frequent words; it would otherwise outrank real vocabulary.
        var perar = Verb("perar", "pero");

        var withTop = Import("pero 1000\nperro 5\n", 1, perar, Noun("perro", "masculine"));
        var withoutTop = Import("pero 1000\nperro 5\n", null, perar, Noun("perro", "masculine"));

        Assert.Equal(["perro"], Lemmas(withTop));
        Assert.Equal(1, withTop.Filter!.LemmasDroppedUnattestedHeadWord);
        Assert.Equal(["perar", "perro"], Lemmas(withoutTop));
    }

    [Fact]
    public void TheImportedEntryCountIsNotChangedByTheFilter()
    {
        var result = Import("casa 80\n", 1, Noun("casa", "feminine"), Noun("zorro", "masculine"));

        Assert.Equal(2, result.SpanishEntries);
        Assert.Equal(2, result.ImportedEntries);
        Assert.Equal(["casa"], Lemmas(result));
    }

    [Fact]
    public void AdverbsAndOtherLemmasWithoutFormsCountTheirHeadWordOnly()
    {
        var result = Import("ahora 7\nantes 3\n", 1, Adverb("antes"), Adverb("ahora"));

        Assert.Equal(["ahora"], Lemmas(result));
    }

    [Fact]
    public void TheSummaryNamesTheFilterNumbers()
    {
        var result = Import("casa 80\nperro 50\n", 1, Noun("casa", "feminine"), Noun("perro", "masculine"), Noun("zorro", "masculine"));

        var summary = result.Summary();

        Assert.Contains("frequency list: 2 lines, 2 distinct words, 130 occurrences\n", summary, StringComparison.Ordinal);
        Assert.Contains("lemmas: 3 considered, 2 with a frequency, 1 kept (top 1: 1 below the cutoff of 80, 0 whose head word is not in the list, 1 without a frequency)\n", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ToJsonOfTheFilteredEntriesIsAValidCanonicalLexicon()
    {
        var result = Import("casa 80\nperro 50\n", 2, Noun("casa", "feminine", "casas"), Noun("perro", "masculine", "perros"));

        var json = KaikkiImporter.ToJson(result.Entries);

        Assert.Equal(result.Entries, NlpLexicon.Load(json).Entries);
        Assert.Equal(json, KaikkiImporter.ToJson(NlpLexicon.Load(json).Entries));
    }

    [Fact]
    public void TopNeedsAFrequencyListAndAPositiveNumber()
    {
        Assert.Throws<ArgumentException>(() => KaikkiImporter.Import(new StringReader(string.Empty), new KaikkiImportOptions { Top = 5 }));
        var list = FrequencyList.Parse(new StringReader("casa 1\n"));
        Assert.Throws<ArgumentOutOfRangeException>(() => KaikkiImporter.Import(new StringReader(string.Empty), new KaikkiImportOptions { Frequency = list, Top = 0 }));
    }

    [Fact]
    public void MaxAndTheFilterWorkTogether()
    {
        var list = FrequencyList.Parse(new StringReader("casa 80\nperro 50\n"));
        var options = new KaikkiImportOptions { Frequency = list, Top = 5, MaxEntries = 1 };

        var result = KaikkiImporter.Import(new StringReader(string.Join('\n', Noun("casa", "feminine"), Noun("perro", "masculine"))), options);

        Assert.Equal(1, result.ImportedEntries);
        Assert.Equal(["casa"], Lemmas(result));
    }
}
