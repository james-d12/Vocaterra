namespace Vocaterra.Nlp.Es.Tests.Phonology;

/// <summary>Tests for the exception lexicon and its integration into the pipeline.</summary>
public class ExceptionLexiconTests
{
    private static readonly string[] RequiredWords =
    [
        "méxico", "mexicano", "oaxaca", "texas", "subrayar", "wifi", "whisky", "pizza", "hámster",
    ];

    [Fact]
    public void MexicoIsLookedUpAndPronouncedWithJota()
    {
        Assert.True(ExceptionLexicon.Default.TryGet("méxico", out var entry));
        Assert.Equal(0, entry.StressedSyllable);

        var result = TestHelpers.La("México");

        Assert.Equal(new[] { "m", "e", "x", "i", "k", "o" }, result.Phones);
        Assert.Equal("ˈme.xi.ko", result.ToIpaString());
        Assert.Equal(0, result.StressedSyllableIndex);
    }

    [Fact]
    public void TheSpellingRulesAloneWouldReadMexicoWithKs()
    {
        // The rule pipeline reads x as /ks/; only the lexicon entry gives the jota.
        var rules = WordPipeline.FromRules(new Vocaterra.Nlp.Core.Stages.Token("méxico", new TextSpan(0, 6), "México"), DialectConfig.LatinAmericanNeutral);

        Assert.Equal(new[] { "m", "e", "k", "s", "i", "k", "o" }, rules.Phones);
        Assert.DoesNotContain("s", TestHelpers.La("méxico").Phones);
    }

    [Fact]
    public void LexiconListsTheRequiredWordsAndAtLeastTenMoreLoanwords()
    {
        var words = ExceptionLexicon.Default.ListedWords;

        foreach (var required in RequiredWords)
        {
            Assert.Contains(required, words);
        }

        Assert.True(words.Count >= RequiredWords.Length + 10, $"expected at least {RequiredWords.Length + 10} entries, found {words.Count}");
    }

    [Fact]
    public void ListedWordsAreSortedOrdinalAndUnique()
    {
        var words = ExceptionLexicon.Default.ListedWords;

        Assert.Equal(words.OrderBy(static w => w, StringComparer.Ordinal), words);
        Assert.Equal(words.Count, words.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryEntryIsNormalizedHasOneStressAndOnlyKnownPhones()
    {
        foreach (var word in ExceptionLexicon.Default.ListedWords)
        {
            Assert.Equal(word, G2pNormalizer.Normalize(word).Text);
            Assert.True(ExceptionLexicon.Default.TryGet(word, out var entry));
            Assert.InRange(entry.StressedSyllable, 0, entry.Syllables.Count - 1);
            Assert.All(entry.Syllables.SelectMany(static s => s), static phone => Assert.True(PhoneInventory.IsKnown(phone), phone));
        }
    }

    [Fact]
    public void LookupIgnoresMissingAccents()
    {
        Assert.True(ExceptionLexicon.Default.TryGet("mexico", out var entry));
        Assert.Equal("méxico", entry.Word);
        Assert.True(ExceptionLexicon.Default.TryGet("hamster", out _));
        Assert.False(ExceptionLexicon.Default.TryGet("casa", out _));
    }

    [Fact]
    public void InflectedFormsOfFlaggedEntriesAreDerived()
    {
        Assert.Equal("me.xi.ˈka.na", TestHelpers.La("mexicana").ToIpaString());
        Assert.Equal("me.xi.ˈka.nos", TestHelpers.La("mexicanos").ToIpaString());
        Assert.Equal("me.xi.ˈka.nas", TestHelpers.La("mexicanas").ToIpaString());
        Assert.Equal("ˈpit.sas", TestHelpers.La("pizzas").ToIpaString());
        Assert.False(ExceptionLexicon.Default.TryGet("wifis", out _));
    }

    [Fact]
    public void ExceptionPhonesFollowTheDialect()
    {
        Assert.Equal("ˈʝas", TestHelpers.La("jazz").ToIpaString());
        Assert.Equal("ˈʝaθ", TestHelpers.Es("jazz").ToIpaString());
        Assert.Equal("ˈʃas", SpanishG2p.Phonemize("jazz", DialectConfig.Rioplatense).ToIpaString());
    }

    [Fact]
    public void ExceptionPhonesStillGoThroughTheAllophoneRules()
    {
        Assert.Equal("suβ.ra.ˈʝaɾ", TestHelpers.La("subrayar").ToIpaString());
        Assert.Equal("ˈweβ", TestHelpers.La("web").ToIpaString());
        Assert.Equal("ˈa.ɣwa ˈweβ", TestHelpers.La("agua web").ToIpaString());
        Assert.Equal("ˈraɣ.βi", TestHelpers.La("rugby").ToIpaString());
        Assert.Equal("ˈun ˈweβ", TestHelpers.La("un web").ToIpaString());
        Assert.Equal("ˈlos ˈtʃow", TestHelpers.La("los show").ToIpaString());
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("oaxaca")]
    [InlineData("jeans")]
    [InlineData("México")]
    [InlineData("subrayar")]
    [InlineData("whisky")]
    public void ExceptionAlignmentPartitionsTheWordEvenWhenSyllableCountsDiffer(string word)
    {
        var result = TestHelpers.La(word);

        var cursor = 0;
        foreach (var syllable in result.Syllables)
        {
            Assert.Equal(cursor, syllable.GraphemeSpan.Start);
            Assert.True(syllable.GraphemeSpan.Length > 0);
            cursor = syllable.GraphemeSpan.End;
        }

        Assert.Equal(result.Text.Length, cursor);
        Assert.Equal(result.Phones.Count, result.Syllables.Sum(static s => s.PhoneCount));
        Assert.Equal(result.Phones.Count, result.PhoneSpans.Count);
    }

    [Fact]
    public void ParserRejectsMalformedEntries()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => ExceptionLexicon.Parse("""{"entries":[{"word":"a","ipa":"a.ba"}]}"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ExceptionLexicon.Parse("""{"entries":[{"word":"a","ipa":"ˈa.ˈba"}]}"""));
        Assert.Throws<System.Text.Json.JsonException>(() => ExceptionLexicon.Parse("""{"entries":[{"word":"a","ipa":"ˈa"},{"word":"a","ipa":"ˈa"}]}"""));
        Assert.Throws<FormatException>(() => ExceptionLexicon.Parse("""{"entries":[{"word":"a","ipa":"ˈq"}]}"""));
    }
}
