namespace Vocaterra.Nlp.Language.Tests.SlotGrammar;

using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>The <c>[WORD:slot]</c> grammar token: parsing, matching and binding (the analyzer side is in <c>WordTokenAnalysisTests</c>).</summary>
public class WordTokenTests
{
    private static SlotGrammarMatch? Match(string text, string pattern)
    {
        return SlotGrammarMatcher.Match(Fixtures.Analyzer.Analyze(TestTokens.From(text)).Tokens, SlotGrammar.Parse(pattern), SpanishLanguagePack.Instance);
    }

    private static string Slots(SlotGrammarMatch? match)
    {
        Assert.NotNull(match);
        return string.Join(",", match.Slots.Select(s => $"{s.Name}={s.Value}"));
    }

    [Theory]
    [InlineData("[WORD:name]", "[WORD:name]")]
    [InlineData("[word:name]", "[WORD:name]")]
    [InlineData("[WORD]", "[WORD]")]
    [InlineData("soy [WORD:name]", "soy [WORD:name]")]
    [InlineData("(me llamo|soy) [WORD:name]?", "(me llamo|soy) [WORD:name]?")]
    public void ParseRoundTripsTheWordToken(string pattern, string canonical)
    {
        Assert.Equal(canonical, SlotGrammar.Parse(pattern).Pattern);
    }

    [Theory]
    [InlineData("[WORD:a:b]")]
    [InlineData("[WORD:1bad]")]
    [InlineData("[WORD:]")]
    [InlineData("[WORD:name")]
    public void ParseRejectsMalformedWordTokens(string pattern)
    {
        Assert.Throws<FormatException>(() => SlotGrammar.Parse(pattern));
    }

    [Fact]
    public void WordBindsAnyTokenOfLettersAsWrittenInTheNormalizedText()
    {
        Assert.Equal("name=ana", Slots(Match("me llamo ana", "me llamo [WORD:name]")));
        Assert.Equal("name=espada", Slots(Match("soy espada", "soy [WORD:name]")));
        Assert.Equal("name=josé", Slots(Match("soy josé", "soy [WORD:name]")));
        Assert.Equal("name=ñoño", Slots(Match("soy ñoño", "soy [WORD:name]")));
    }

    [Fact]
    public void WordMatchesWithoutASlotAndBindsNothing()
    {
        var match = Match("soy ana", "soy [WORD]");

        Assert.NotNull(match);
        Assert.Empty(match.Slots);
        Assert.Equal(2, match.ExplicitTokenCount);
    }

    [Fact]
    public void WordCountsAsOneExplicitTokenAndTheSpanIsTheTokenSpan()
    {
        var match = Match("mi nombre es ana", "mi nombre es [WORD:name]");

        Assert.NotNull(match);
        Assert.Equal(4, match.ExplicitTokenCount);
        Assert.Equal(new TextSpan(13, 3), match.Slots.Single().Span);
    }

    [Fact]
    public void WordSitsNextToTypedTokens()
    {
        Assert.Equal("qty=2,name=ana", Slots(Match("tengo dos ana", "tengo [NUM:qty] [WORD:name]")));
        Assert.Equal("name=ana,item=espada", Slots(Match("ana espada", "[WORD:name] [NOUN:item]")));
        Assert.Equal("name=ana,item=espada", Slots(Match("ana una espada", "[WORD:name] [ART]? [NOUN:item]")));
        Assert.Null(Match("ana", "[WORD:name] [NOUN:item]"));
        Assert.Null(Match("tengo dos", "tengo [NUM:qty] [WORD:name]"));
    }

    [Fact]
    public void WordDoesNotTakeNumbersOrPunctuation()
    {
        Assert.Null(Match("tengo 2 espadas", "tengo [WORD:x] [NOUN:item]"));
        Assert.Null(Match("tengo 12 espadas", "tengo [WORD:x] [NOUN:item]"));
        Assert.Null(Match("soy ana-maria", "soy [WORD:name]"));
        // A spelled numeral is a closed-class word (it agrees with its noun), so it is no name either.
        Assert.Null(Match("tengo dos espadas", "tengo [WORD:x] [NOUN:item]"));
    }

    [Fact]
    public void WordIsAWholeTokenOfTheSequence()
    {
        Assert.Null(Match("soy ana luis", "soy [WORD:name]"));
        Assert.Null(Match("soy", "soy [WORD:name]"));
    }

    [Fact]
    public void WordCanBeOptional()
    {
        Assert.Equal("name=ana", Slots(Match("ana", "[WORD:name]")));
        Assert.Equal("name=ana", Slots(Match("ana", "soy? [WORD:name]")));
    }

    [Fact]
    public void ANameKnownToTheLexiconBindsItsTokenText()
    {
        // "espada" is a noun of the seed lexicon; the name takes the token text, not the noun's lemma, and the bound token is recorded as a name.
        var match = Match("soy espada", "soy [WORD:name]");

        Assert.Equal("name=espada", Slots(match));
        Assert.Contains(1, match!.WordTokens);
    }
}
