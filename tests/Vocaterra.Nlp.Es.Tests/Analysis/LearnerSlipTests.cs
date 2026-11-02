namespace Vocaterra.Nlp.Es.Tests.Analysis;

using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M58, found by the game's V11: two learner slips the core misread. (a) A stem change missed on a form that is also a noun (<c>cuánto costa</c>: the coast) gave
/// <c>gender.agree</c> on <c>cuánto</c> and no intent; it is <c>costar</c>'s <c>conj.irregular</c> (expected <c>cuesta</c>) where the sentence calls for a verb.
/// (b) A plural made with <c>-s</c> on a noun that takes <c>-es</c> (<c>hay pans</c>) gave <c>vocab.unknown</c>; it is the plural noun, <c>noun.number</c> expected
/// <c>panes</c>. Each slip gave the old result before M58; the clean neighbours are tests as well. Runs on the lexicon of the bundle, which knows <c>costa</c> as a noun.
/// </summary>
public class LearnerSlipTests
{
    // The game's price and availability grammars (intents ask_price and ask_availability), so the words are bound as at the baker's prompt.
    private const string PriceGrammar = "cuánto [VERB:costar] [ART]? [NUM:qty]? [NOUN:item]?";
    private const string AvailabilityGrammar = "hay [ART]? [NUM:qty]? [NOUN:item] (en el pueblo|aquí)?";
    private const string HelpGrammar = "me [VERB:poder] [VERB:ayudar]";

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

    private static string Described(string sentence, params string[] grammars)
    {
        var parsed = grammars.Select(static g => SlotGrammar.Parse(g)).ToList();
        return string.Join(';', Analyzer.Value.Analyze(TestTokens.From(sentence), parsed).Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}"));
    }

    // ---- (a) a stem change missed on a form that is also a noun --------------------------------------------------------------------------------------------

    // Where a verb is expected: after cuánto, whose determiner reading cannot agree with the noun (before M58: gender.agree cuánta); after the noun of a phrase that
    // opens the utterance; after a subject pronoun or a clitic. Before M58 each gave no conj.irregular (cuánto costa: gender.agree; the others: nothing).
    [Theory]
    [InlineData("cuánto costa", "conj.irregular|costa|cuesta")]
    [InlineData("cuánto costa el pan", "conj.irregular|costa|cuesta")]
    [InlineData("cuánto costa una manzana", "conj.irregular|costa|cuesta")]
    [InlineData("cuánto costa los panes", "conj.person|costa|cuestan;conj.irregular|costa|cuesta")]
    [InlineData("el pan costa dos monedas", "conj.irregular|costa|cuesta")]
    [InlineData("la manzana costa una moneda", "conj.irregular|costa|cuesta")]
    [InlineData("el pan me costa dos monedas", "conj.irregular|costa|cuesta")]
    [InlineData("yo jugo en el pueblo", "conj.irregular|jugo|juego")]
    [InlineData("me jugo la vida", "conj.irregular|jugo|juego")]
    [InlineData("tú mentes", "conj.irregular|mentes|mientes")]
    [InlineData("él mente", "conj.irregular|mente|miente")]
    [InlineData("yo cabo aquí", "conj.irregular|cabo|quepo")]
    public void AStemChangeMissedOnAFormThatIsANounIsTheVerbWhereAVerbIsExpected(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // The game's price grammar binds the verb; before M58 it matched nothing, the analysis gave gender.agree and the intent was unknown.
    [Theory]
    [InlineData("cuánto costa", "conj.irregular|costa|cuesta")]
    [InlineData("cuánto costa el pan", "conj.irregular|costa|cuesta")]
    [InlineData("cuánto costa dos panes", "conj.person|costa|cuestan;conj.irregular|costa|cuesta")]
    public void ThePriceGrammarMatchesTheSlipAndReportsIt(string sentence, string expected)
    {
        var result = Analyzer.Value.Analyze(TestTokens.From(sentence), [SlotGrammar.Parse(PriceGrammar)]);
        Assert.Equal(PriceGrammar, result.MatchedGrammarId);
        Assert.Equal(expected, string.Join(';', result.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}")));
        Assert.Contains(result.Tokens[1].Readings, static r => r.Lemma == "costar");
        Assert.Equal("costar", result.Words![1].Reading!.Lemma);
    }

    // The clean neighbours: the real forms, the noun where it is one (after a determiner or a preposition, alone, after cuánta that agrees with it), and the
    // noun's own phrase. cuánto costan keeps its conj.irregular from before M58 (costan is no word).
    [Theory]
    [InlineData("cuánto cuesta el pan", "")]
    [InlineData("cuánto cuestan los panes", "")]
    [InlineData("cuánto cuesta", "")]
    [InlineData("el pan cuesta dos monedas", "")]
    [InlineData("cuánto costan los panes", "conj.irregular|costan|cuestan")]
    [InlineData("la costa", "")]
    [InlineData("en la costa", "")]
    [InlineData("vivo en la costa", "")]
    [InlineData("las costas", "")]
    [InlineData("costa", "")]
    [InlineData("cuánta costa hay", "")]
    [InlineData("el jugo cuesta dos monedas", "")]
    [InlineData("jugo de naranja", "")]
    [InlineData("me gusta el jugo", "")]
    [InlineData("la mente", "")]
    [InlineData("al cabo de un rato", "")]
    public void TheRealFormsAndTheNounWhereItIsOneStayClean(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A form that is a word of another class (solo, sola, esto; podo and pode, forms of podar) is read as the slip only by a grammar that matches nothing without it
    // and names the verb: yo solo quiero pan is the adverb after a subject pronoun, not soler's suelo.
    [Theory]
    [InlineData("yo solo quiero pan", "")]
    [InlineData("yo sola hago el trabajo", "")]
    [InlineData("esto es pan", "")]
    [InlineData("qué es esto", "")]
    [InlineData("yo podo el árbol", "")]
    public void AFormThatIsAWordOfAnotherClassStaysThatWord(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("me pode ayudar", "conj.irregular|pode|puede")]
    [InlineData("me podes ayudar", "conj.irregular|podes|puedes")]
    [InlineData("me puede ayudar", "")]
    public void AGrammarThatNamesTheVerbReadsAVerbFormOfAnotherVerbAsTheSlip(string sentence, string expected)
    {
        var result = Analyzer.Value.Analyze(TestTokens.From(sentence), [SlotGrammar.Parse(HelpGrammar)]);
        Assert.Equal(HelpGrammar, result.MatchedGrammarId);
        Assert.Equal(expected, string.Join(';', result.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}")));
    }

    // A grammar that takes the word as written beats one that needs the slip, whichever comes first.
    [Theory]
    [InlineData("[VERB:costar]", "[NOUN:item]")]
    [InlineData("[NOUN:item]", "[VERB:costar]")]
    public void AGrammarThatTakesTheWordAsWrittenWinsOverOneThatNeedsTheSlip(string first, string second)
    {
        var result = Analyzer.Value.Analyze(TestTokens.From("costa"), [SlotGrammar.Parse(first), SlotGrammar.Parse(second)]);
        Assert.Equal("[NOUN:item]", result.MatchedGrammarId);
        Assert.Empty(result.Errors);
        Assert.DoesNotContain(result.Tokens[0].Readings, static r => r.Lemma == "costar");
    }

    // ---- (b) a plural made with -s on a noun that takes -es ------------------------------------------------------------------------------------------------

    // Before M58 each gave vocab.unknown and matched no grammar.
    [Theory]
    [InlineData("hay pans", "noun.number|pans|panes")]
    [InlineData("hay dos pans", "noun.number|pans|panes")]
    [InlineData("quiero flors", "noun.number|flors|flores")]
    [InlineData("tengo dos relojs", "noun.number|relojs|relojes")]
    [InlineData("hay muchas luzes", "noun.number|luzes|luces")]
    [InlineData("hay luzs", "noun.number|luzs|luces")]
    [InlineData("los doctors", "noun.number|doctors|doctores")]
    [InlineData("dos cancions", "noun.number|cancions|canciones")]
    public void APluralWithTheWrongEndingIsANumberSlipOfAKnownNoun(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("hay pans", "noun.number|pans|panes")]
    [InlineData("hay dos pans", "noun.number|pans|panes")]
    [InlineData("hay pans aquí", "noun.number|pans|panes")]
    public void TheAvailabilityGrammarMatchesTheSlipWithItsNoun(string sentence, string expected)
    {
        var result = Analyzer.Value.Analyze(TestTokens.From(sentence), [SlotGrammar.Parse(AvailabilityGrammar)]);
        Assert.Equal(AvailabilityGrammar, result.MatchedGrammarId);
        Assert.Equal(expected, string.Join(';', result.Errors.Select(static e => $"{e.Tag}|{e.Actual}|{e.Expected}")));
        Assert.Contains(result.Slots, static s => s is { Name: "item", Value: "pan" });
        Assert.Equal(1.0, result.Confidence);
    }

    // The article's number is the check's to name: one noun.number on the word, not two.
    [Fact]
    public void AnArticleOfTheOtherNumberNamesItsOwnCorrection()
    {
        Assert.Equal("noun.number|pans|pan", Described("hay un pans"));
    }

    // The clean neighbours, words that end in -s, nouns that take -s (loans: robots) and real unknown words.
    [Theory]
    [InlineData("hay panes", "")]
    [InlineData("hay dos panes", "")]
    [InlineData("hay un pan", "")]
    [InlineData("hay pan", "")]
    [InlineData("quiero más pan", "")]
    [InlineData("el mes que viene", "")]
    [InlineData("el país", "")]
    [InlineData("los lunes", "")]
    [InlineData("hay flores", "")]
    [InlineData("hay glurps", "vocab.unknown|glurps|")]
    [InlineData("quiero un blorf", "vocab.unknown|blorf|")]
    [InlineData("hay sombrs", "vocab.unknown|sombrs|")]
    public void RealPluralsStayCleanAndUnknownWordsStayUnknown(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    // A name is no slip: the grammar binds it as [WORD], and it is neither unknown nor a number mistake.
    [Fact]
    public void ANameSpelledLikeASlipIsAName()
    {
        var result = Analyzer.Value.Analyze(TestTokens.From("me llamo pans"), [SlotGrammar.Parse("me llamo [WORD:name]")]);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Slots, static s => s is { Name: "name", Value: "pans" });
    }

    // ---- determinism ---------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSameSentenceGivesTheSameAnalysis()
    {
        var grammars = new[] { SlotGrammar.Parse(PriceGrammar), SlotGrammar.Parse(AvailabilityGrammar) };
        foreach (var sentence in new[] { "cuánto costa el pan", "hay pans", "el pan costa dos monedas", "la costa" })
        {
            var first = Vocaterra.Nlp.Core.Determinism.CanonicalJson.Serialize(Analyzer.Value.Analyze(TestTokens.From(sentence), grammars));
            var second = Vocaterra.Nlp.Core.Determinism.CanonicalJson.Serialize(BuildAnalyzer().Analyze(TestTokens.From(sentence), grammars));
            Assert.Equal(first, second);
        }
    }

    // ---- M66 (5, 6, 7) ---------------------------------------------------------------------------------------------------------------------------------------------

    // (5) The slip reading of costa after the phrase that opens the utterance held only for a noun of its number (el pan costa); a plural noun gave nothing before M66.
    // It is the verb of either number, so the stem change and the person are both reported, as after cuánto (cuánto costa los panes), and the recast is cuestan.
    [Theory]
    [InlineData("los panes costa dos monedas", "conj.person|costa|cuestan;conj.irregular|costa|cuesta")]
    [InlineData("las manzanas costa una moneda", "conj.person|costa|cuestan;conj.irregular|costa|cuesta")]
    public void AStemChangeMissedAfterAPluralPhraseIsTheVerbOfThePlural(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("vivo en la costa")]
    [InlineData("la costa es bonita")]
    [InlineData("los panes cuestan dos monedas")]
    [InlineData("las costas son bonitas")]
    public void TheCoastAndTheRealPluralStayClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // (6) The plural slip covered nouns only: azuls gave vocab.unknown before M66. An adjective that ends in l, r, n, d, z or j takes -es as well.
    [Theory]
    [InlineData("las casas azuls", "noun.number|azuls|azules")]
    [InlineData("azuls", "noun.number|azuls|azules")]
    [InlineData("los niños felizs", "noun.number|felizs|felices")]
    public void APluralWithTheWrongEndingIsANumberSlipOfAKnownAdjective(string sentence, string expected)
    {
        Assert.Equal(expected, Described(sentence));
    }

    [Theory]
    [InlineData("las casas azules")]
    [InlineData("azules")]
    [InlineData("la casa azul")]
    public void TheRealPluralOfAnAdjectiveStaysClean(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }

    // (7) ti, the prepositional pronoun (as mí), was missing from the lexicon: para ti gave vocab.unknown before M66 (lexicon/supplement.json).
    [Theory]
    [InlineData("es para ti")]
    [InlineData("tengo pan para ti")]
    [InlineData("es para mí")]
    public void TiIsAWordOfTheLexicon(string sentence)
    {
        Assert.Equal(string.Empty, Described(sentence));
    }
}
