namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M62 (b), found by the game's V13: a colour adjective that is also a noun (roja, rojas, blanca, gris) put before its noun was read as a noun of its own, so quiero tres
/// rojas manzanas gave no word_order, with a grammar that matches it or without one. The colours go after the noun in neutral Spanish, so such a word right before a word
/// that can only be a noun is the adjective, and the word-order check reports it as it did for rojo and azul. The adjectives that go before the noun stay clean. Runs on the
/// lexicon of the bundle.
/// </summary>
public class PrenominalAdjectiveTests
{
    // The game's request grammar (V13), and the same with a place for an adjective before the noun, which a bundle may add to understand the slip.
    private const string Request = "[VERB:querer] [ART]? [NUM:qty]? [NOUN:item] [ADJ]? (por favor)?";
    private const string RequestEitherSide = "[VERB:querer] [ART]? [NUM:qty]? [ADJ]? [NOUN:item] [ADJ]? (por favor)?";

    private static readonly Lazy<LinguisticAnalyzer> Analyzer = new(BuildAnalyzer);

    private static LinguisticAnalyzer BuildAnalyzer()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "content", "lexicon");
        var entries = new List<LexiconEntry>();
        foreach (var path in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            entries.AddRange(Lexicon.ReadFile(File.ReadAllBytes(path)).Entries);
        }

        return new LinguisticAnalyzer(SpanishLexicon.FromEntries(entries), SpanishLanguagePack.Instance);
    }

    private static AnalysisResult Analyze(string sentence, params string[] grammars)
    {
        var parsed = grammars.Select(static g => SlotGrammar.Parse(g)).ToList();
        return Analyzer.Value.Analyze(TestTokens.From(sentence), parsed);
    }

    private static string Described(string sentence, params string[] grammars)
    {
        return string.Join(';', Analyze(sentence, grammars).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    [Theory]
    [InlineData("quiero tres rojas manzanas", "word_order|rojas manzanas|manzanas rojas")]
    [InlineData("quiero una roja manzana", "word_order|roja manzana|manzana roja")]
    [InlineData("quiero rojas manzanas", "word_order|rojas manzanas|manzanas rojas")]
    [InlineData("las rojas manzanas son caras", "word_order|rojas manzanas|manzanas rojas")]
    [InlineData("quiero dos blancas manzanas", "word_order|blancas manzanas|manzanas blancas")]
    [InlineData("quiero una azul manzana", "word_order|azul manzana|manzana azul")]
    public void AColourBeforeItsNounIsASlipOfOrder(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The game's request grammar has no place for the adjective before the noun, so it does not match and the intent is not resolved by it; the analysis reports the
    // order all the same. A grammar with that place matches, binds the slot and still reports the order.
    [Theory]
    [InlineData("quiero tres rojas manzanas", "word_order|rojas manzanas|manzanas rojas")]
    [InlineData("quiero una roja manzana", "word_order|roja manzana|manzana roja")]
    public void AGrammarThatMatchesTheSlipStillGetsTheOrder(string sentence, string expected)
    {
        var without = Analyze(sentence, Request);
        Assert.Null(without.MatchedGrammarId);
        Assert.Equal(expected, Described(sentence, Request));

        var with = Analyze(sentence, RequestEitherSide);
        Assert.NotNull(with.MatchedGrammarId);
        Assert.Contains(with.Slots, static s => s.Name == "item" && s.Value == "manzana");
        Assert.Equal(expected, Described(sentence, RequestEitherSide));
    }

    [Theory]
    [InlineData("una gran casa")]
    [InlineData("un buen pan")]
    [InlineData("la primera casa")]
    [InlineData("muchas manzanas")]
    [InlineData("otra casa")]
    [InlineData("quiero tres manzanas rojas")]
    [InlineData("quiero una manzana roja")]
    [InlineData("quiero una gran manzana")]
    [InlineData("una buena manzana roja")]
    [InlineData("una rosa roja")]
    [InlineData("quiero una rosa")]
    [InlineData("me gusta la roja")]
    [InlineData("la próxima semana")]
    [InlineData("las manzanas rojas y los panes blancos")]
    public void AdjectivesThatGoBeforeTheNounAndColoursAfterItAreClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    [Theory]
    [InlineData("quiero tres manzanas rojas")]
    [InlineData("quiero una manzana roja")]
    public void TheGamesRequestGrammarStillMatchesTheAdjectiveAfterItsNoun(string sentence)
    {
        var result = Analyze(sentence, Request);
        Assert.NotNull(result.MatchedGrammarId);
        Assert.Empty(result.Errors);
    }
}
