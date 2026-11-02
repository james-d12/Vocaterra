namespace Vocaterra.Nlp.Language.Tests.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M59 (d), found by the game's V12: a word that is a preposition of elapsed time and a finite verb (<c>hace</c>, of <c>hace dos días</c> and of <c>hacer</c>) was read as
/// the preposition whenever nothing decided it, because the charitable fallback takes the first class in reading order and the lexicon lists the preposition first;
/// a literal <c>hace</c> in a slot grammar decides nothing (the author wrote a form). So <c>¿qué hace?</c> reported the preposition and hid the verb from the host's
/// person check. The reading resolver now reads the preposition only before a phrase of time and the verb elsewhere, through the pack's
/// <see cref="IAnalysisRules.IsElapsedTimePreposition"/> and <see cref="IAnalysisRules.IsElapsedTimeNoun"/>. On a small lexicon whose <c>hace</c> lists the preposition
/// first, as the bundle's does.
/// </summary>
public class ElapsedTimePrepositionTests
{
    private static LexiconEntry Entry(string form, string lemma, string pos, string? person = null, string? number = null, string? tense = null, string? mood = null, string? gender = null)
    {
        return new LexiconEntry(form, lemma, pos, new EntryFeatures(person, number, tense, mood, gender));
    }

    private static readonly Lexicon TestLexicon = SpanishLexicon.FromEntries(
    [
        Entry("hace", "hace", "prep"),
        Entry("hace", "hacer", "verb", "3", "sg", "pres", "ind"),
        Entry("haces", "hacer", "verb", "2", "sg", "pres", "ind"),
        Entry("hacer", "hacer", "verb"),
        Entry("hizo", "hacer", "verb", "3", "sg", "pret", "ind"),
        Entry("va", "ir", "verb", "3", "sg", "pres", "ind"),
        Entry("ir", "ir", "verb"),
        Entry("comí", "comer", "verb", "1", "sg", "pret", "ind"),
        Entry("comer", "comer", "verb"),
        Entry("qué", "qué", "pron", "3", "sg"),
        Entry("a", "a", "prep"),
        Entry("un", "un", "art", number: "sg", gender: "m"),
        Entry("dos", "2", "num", number: "pl"),
        Entry("mucho", "mucho", "adj", number: "sg", gender: "m"),
        Entry("mucho", "mucho", "adv"),
        Entry("buen", "bueno", "adj", number: "sg", gender: "m"),
        Entry("días", "día", "noun", number: "pl", gender: "m"),
        Entry("año", "año", "noun", number: "sg", gender: "m"),
        Entry("rato", "rato", "noun", number: "sg", gender: "m"),
        Entry("tiempo", "tiempo", "noun", number: "sg", gender: "m"),
        Entry("frío", "frío", "noun", number: "sg", gender: "m"),
        Entry("pan", "pan", "noun", number: "sg", gender: "m"),
        Entry("hacha", "hacha", "noun", number: "sg", gender: "f"),
        Entry("domingo", "domingo", "noun", number: "sg", gender: "m"),
    ]);

    private static readonly LinguisticAnalyzer Analyzer = new(TestLexicon, SpanishLanguagePack.Instance);

    private static AnalysisResult Analyze(string text, params string[] grammars)
    {
        return Analyzer.Analyze(TestTokens.From(text), grammars.Select(static g => SlotGrammar.Parse(g)).ToList());
    }

    // The reported reading of hace (result.Words, the reading a host reads).
    private static string HaceIn(string text, params string[] grammars)
    {
        var result = Analyze(text, grammars);
        var reading = result.Words!.Single(static w => w.Token.Text == "hace").Reading!;
        return $"{reading.Pos}/{reading.Lemma}";
    }

    [Theory]
    [InlineData("qué hace domingo", "qué hace (domingo|luz)")]
    [InlineData("qué hace domingo", "qué hace [WORD:name]")]
    [InlineData("qué hace", "qué hace")]
    [InlineData("hace pan", "hace [NOUN:item]")]
    public void ALiteralHaceThatNoTimeFollowsBindsTheVerb(string text, string grammar)
    {
        var result = Analyze(text, grammar);
        Assert.Equal(grammar, result.MatchedGrammarId);
        Assert.Equal("verb/hacer", HaceIn(text, grammar));
    }

    [Theory]
    [InlineData("qué hace")]
    [InlineData("qué hace domingo")]
    [InlineData("hace frío")]
    [InlineData("hace buen tiempo")]
    [InlineData("hace mucho frío")]
    [InlineData("hace un hacha")]
    public void HaceThatNoTimeFollowsIsTheVerb(string text)
    {
        Assert.Equal("verb/hacer", HaceIn(text));
    }

    [Theory]
    [InlineData("hace dos días")]
    [InlineData("hace un año")]
    [InlineData("hace un rato comí")]
    [InlineData("hace mucho tiempo")]
    [InlineData("hace tiempo")]
    [InlineData("hace mucho")]
    public void HaceBeforeAPhraseOfTimeIsThePreposition(string text)
    {
        Assert.Equal("prep/hace", HaceIn(text));
        Assert.Equal("prep/hace", HaceIn(text, "hace [NUM]? [ART]? [ADJ]? [NOUN]? [VERB]?"));
    }

    // M59 (c): the same word after "ir a" is the verb, so the near-future check sees it.
    [Fact]
    public void HaceAfterIrAIsTheVerbAndExpectsTheInfinitive()
    {
        var result = Analyze("va a hace un hacha", "[VERB:ir] a [VERB:hacer] [ART]? [NOUN:item]");
        Assert.Equal("verb/hacer", HaceIn("va a hace un hacha"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(("conj.tense", "hacer", "hace"), (error.Tag, error.Expected, error.Actual));
    }
}
