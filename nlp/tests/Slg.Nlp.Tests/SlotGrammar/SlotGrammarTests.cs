namespace Slg.Nlp.Tests.SlotGrammar;

using Slg.Core.Stages;
using Slg.Core.Verdicts;
using Slg.Nlp.SlotGrammar;
using Slg.Nlp.Tests.Analysis;

public class SlotGrammarTests
{
    private static IReadOnlyList<AnalyzedToken> Tokens(string text)
    {
        return Fixtures.Analyzer.Analyze(TestTokens.From(text)).Tokens;
    }

    private static SlotGrammarMatch? Match(string text, string pattern)
    {
        return SlotGrammarMatcher.Match(Tokens(text), SlotGrammar.Parse(pattern));
    }

    private static string Slots(SlotGrammarMatch? match)
    {
        Assert.NotNull(match);
        return string.Join(",", match.Slots.Select(s => $"{s.Name}={s.Value}"));
    }

    [Fact]
    public void ParseProducesCanonicalPatternAndDefaultId()
    {
        var grammar = SlotGrammar.Parse("  [pron]?   tengo [NUM:qty]\t[NOUN:item] ");

        Assert.Equal("[PRON]? tengo [NUM:qty] [NOUN:item]", grammar.Pattern);
        Assert.Equal(grammar.Pattern, grammar.Id);
        Assert.Equal(grammar.Pattern, grammar.ToString());
    }

    [Fact]
    public void ParseAcceptsAnExplicitId()
    {
        Assert.Equal("have_item", SlotGrammar.Parse("tengo [NUM:qty] [NOUN:item]", "have_item").Id);
    }

    [Theory]
    [InlineData("(quiero|necesito) [ART]? [NOUN:item]", "(quiero|necesito) [ART]? [NOUN:item]")]
    [InlineData("[VERB:comprar] [NOUN:item]", "[VERB:comprar] [NOUN:item]")]
    [InlineData("[VERB:Comprar:action] [DET]?", "[VERB:comprar:action] [DET]?")]
    [InlineData("[VERB] [ADJ:color]", "[VERB] [ADJ:color]")]
    [InlineData("hola (por favor|)", "hola (por favor|)")]
    [InlineData("(a (b|c)?|d)", "(a (b|c)?|d)")]
    public void ParseRoundTripsCanonicalText(string pattern, string canonical)
    {
        Assert.Equal(canonical, SlotGrammar.Parse(pattern).Pattern);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[NOUN:item")]
    [InlineData("(a|b")]
    [InlineData("a|b")]
    [InlineData("a)")]
    [InlineData("[FOO:x]")]
    [InlineData("[NOUN:1bad]")]
    [InlineData("[NOUN:a:b]")]
    [InlineData("[VERB:a:b:c]")]
    [InlineData("[VERB::slot]")]
    [InlineData("? hola")]
    [InlineData("hola ?")]
    public void ParseRejectsMalformedPatterns(string pattern)
    {
        Assert.Throws<FormatException>(() => SlotGrammar.Parse(pattern));
    }

    [Fact]
    public void MatchesSlotsUsingLemmasAndTokenSpans()
    {
        var match = Match("tengo dos espadas", "tengo [NUM:qty] [NOUN:item]");

        Assert.NotNull(match);
        Assert.Equal("qty=2,item=espada", Slots(match));
        Assert.Equal(new TextSpan(6, 3), match.Slots[0].Span);
        Assert.Equal(new TextSpan(10, 7), match.Slots[1].Span);
        Assert.Equal(3, match.ExplicitTokenCount);
        Assert.Equal(
            SlotMap.From([new KeyValuePair<string, string>("item", "espada"), new KeyValuePair<string, string>("qty", "2")]),
            match.ToSlotMap());
    }

    [Fact]
    public void LiteralMatchesLemmaOrSurfaceFormIgnoringAccents()
    {
        Assert.NotNull(Match("tengo dos espadas", "tener [NUM:qty] [NOUN:item]"));
        Assert.NotNull(Match("tengo dos espadas", "TENGO [NUM:qty] [NOUN:item]"));
        Assert.NotNull(Match("adiós", "adios"));
        Assert.NotNull(Match("adios", "adiós"));
        Assert.NotNull(Match("tengo dos espadas", "tengo 2 [NOUN:item]"));
        Assert.Null(Match("tengo dos espadas", "vendo [NUM:qty] [NOUN:item]"));
    }

    [Fact]
    public void WholeSequenceMustBeConsumed()
    {
        Assert.Null(Match("tengo dos espadas rojas", "tengo [NUM:qty] [NOUN:item]"));
        Assert.Null(Match("tengo dos", "tengo [NUM:qty] [NOUN:item]"));
        Assert.Null(Match("dos espadas", "tengo [NUM:qty] [NOUN:item]"));
    }

    [Fact]
    public void OptionalElementsMatchPresentOrAbsent()
    {
        const string pattern = "quiero [ART]? [NOUN:item]";

        Assert.Equal("item=espada", Slots(Match("quiero la espada", pattern)));
        Assert.Equal("item=espada", Slots(Match("quiero espada", pattern)));
        Assert.Equal("item=espada", Slots(Match("quiero una espada", pattern)));
        Assert.Null(Match("quiero la", pattern));
    }

    [Fact]
    public void AlternativesAreTriedInOrder()
    {
        const string pattern = "(quiero|necesito|busco) [ART]? [NOUN:item]";

        Assert.Equal("item=llave", Slots(Match("necesito la llave", pattern)));
        Assert.Equal("item=llave", Slots(Match("busco llaves", pattern)));
        Assert.Null(Match("vendo la llave", pattern));
    }

    [Fact]
    public void AlternativesMaySpanSeveralTokensOrBeEmpty()
    {
        const string pattern = "hola (qué tal|) [NOUN:item]";

        Assert.Equal("item=amigo", Slots(Match("hola qué tal amigo", pattern)));
        Assert.Equal("item=amigo", Slots(Match("hola amigo", pattern)));
    }

    [Fact]
    public void VerbTokenMatchesAnyFormOfTheLemma()
    {
        const string pattern = "[VERB:comprar:action] [ART]? [NOUN:item]";

        Assert.Equal("action=comprar,item=espada", Slots(Match("compro una espada", pattern)));
        Assert.Equal("action=comprar,item=espada", Slots(Match("compré la espada", pattern)));
        Assert.Equal("action=comprar,item=espada", Slots(Match("comprar espada", pattern)));
        Assert.Null(Match("vendo una espada", pattern));
        Assert.Equal("item=espada", Slots(Match("vendo una espada", "[VERB] [ART]? [NOUN:item]")));
    }

    [Fact]
    public void ReadingBasedTypesMatchOnAnyReading()
    {
        Assert.Equal("qty=1,item=escudo", Slots(Match("un escudo", "[NUM:qty] [NOUN:item]")));
        Assert.Equal("qty=1,item=espada", Slots(Match("una espada", "[NUM:qty] [NOUN:item]")));
        Assert.Equal("item=escudo", Slots(Match("un escudo", "[ART] [NOUN:item]")));
        Assert.Equal("item=espada,color=rojo", Slots(Match("espada roja", "[NOUN:item] [ADJ:color]")));
        Assert.Equal("item=espada", Slots(Match("esta espada", "[DET] [NOUN:item]")));
        Assert.Null(Match("esta espada", "[ART] [NOUN:item]"));
        Assert.Equal("item=vino", Slots(Match("el vino", "[ART] [NOUN:item]")));
    }

    [Fact]
    public void DigitTokensFillNumericSlots()
    {
        Assert.Equal("qty=3,item=moneda", Slots(Match("tengo 3 monedas", "tengo [NUM:qty] [NOUN:item]")));
        Assert.Equal("qty=250,item=moneda", Slots(Match("tengo 250 monedas", "tengo [NUM:qty] [NOUN:item]")));
    }

    [Fact]
    public void LeadingSubjectPronounAndTrailingPorFavorAreAllowedByDefault()
    {
        const string pattern = "quiero [ART]? [NOUN:item]";

        Assert.Equal("item=espada", Slots(Match("yo quiero una espada", pattern)));
        Assert.Equal("item=espada", Slots(Match("quiero una espada por favor", pattern)));
        Assert.Equal("item=espada", Slots(Match("nosotros queremos una espada por favor", "querer [ART]? [NOUN:item]")));
        Assert.Null(Match("ellos quiero una espada hoy", pattern));
    }

    [Fact]
    public void ExplicitPronounTokenWinsOverTheDefaultAllowance()
    {
        var match = Match("yo tiene dos espada", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]");

        Assert.NotNull(match);
        Assert.Equal("qty=2,item=espada", Slots(match));
        Assert.Equal(4, match.ExplicitTokenCount);
    }

    [Fact]
    public void AgreementErrorsDoNotPreventAMatch()
    {
        Assert.Equal("qty=2,item=espada", Slots(Match("yo tiene dos espada", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]")));
        Assert.Equal("item=mesa", Slots(Match("el mesa", "[ART] [NOUN:item]")));
    }

    [Fact]
    public void UnknownWordsOnlyMatchLiterals()
    {
        Assert.Null(Match("tengo dos espadass", "tengo [NUM:qty] [NOUN:item]"));
        Assert.NotNull(Match("tengo dos espadass", "tengo dos espadass"));
    }

    [Fact]
    public void RepeatedSlotNamesKeepTheFirstValue()
    {
        var match = Match("espada y escudo", "[NOUN:item] y [NOUN:item]");

        Assert.Equal("item=espada", Slots(match));
    }

    [Fact]
    public void DuplicateSlotNamesAcrossAlternativesAreAllowed()
    {
        const string pattern = "(quiero [NOUN:item]|necesito [NOUN:item])";

        Assert.Equal("item=mapa", Slots(Match("necesito mapa", pattern)));
        Assert.Equal("item=mapa", Slots(Match("quiero mapa", pattern)));
    }

    [Fact]
    public void MatchingIsDeterministic()
    {
        var tokens = Tokens("yo quiero comprar dos espadas por favor");
        var grammar = SlotGrammar.Parse("[PRON]? querer [VERB:comprar:action] [NUM:qty]? [NOUN:item]");

        var first = SlotGrammarMatcher.Match(tokens, grammar);
        var second = SlotGrammarMatcher.Match(tokens, SlotGrammar.Parse(grammar.Pattern));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Slots, second.Slots);
        Assert.Equal("action=comprar,qty=2,item=espada", Slots(first));
    }

    // What the matcher says about each token: "index:Node,Node" for every token it consumed, in token order.
    private static string Bindings(SlotGrammarMatch? match)
    {
        Assert.NotNull(match);
        return string.Join(" ", match.Bindings.Select(b => $"{b.TokenIndex}:{string.Join("/", b.Nodes.Select(Describe))}"));
    }

    private static string Describe(GrammarNode node)
    {
        return node switch
        {
            TypedNode typed => typed.Tag,
            LiteralNode literal => literal.Word,
            DefaultSubjectNode => "subject",
            _ => node.GetType().Name,
        };
    }

    [Fact]
    public void AMatchBindsEveryTokenItConsumedToTheElementThatTookIt()
    {
        var match = Match("yo tengo dos espadas por favor", "[VERB:tener] [NUM:qty] [NOUN:item]");

        Assert.Equal("0:subject 1:VERB 2:NUM 3:NOUN 4:por 5:favor", Bindings(match));
    }

    [Fact]
    public void AMatchBindsALiteralToItsWord()
    {
        Assert.Equal("0:cuánto 1:VERB 2:ART 3:NOUN", Bindings(Match("cuánto cuesta la espada", "cuánto [VERB:costar] [ART]? [NOUN:item]")));
    }

    [Fact]
    public void ElementsThatTieForAWordAreAllBoundToIt()
    {
        // Both [NOUN]? and [VERB]? can take "vino" (a noun, and a form of venir) and the parses consume as many tokens either way, so the word is bound to both.
        var match = Match("quiero vino", "[VERB:querer] [NOUN]? [VERB]?");

        Assert.Equal("0:VERB 1:NOUN/VERB", Bindings(match));
    }

    [Fact]
    public void ABetterParseReplacesTheBindingsOfAWorseOne()
    {
        // [ADJ]? could take "espada" only if it is an adjective; the parse that gives it to [NOUN:item] consumes more explicit tokens and is the only one bound.
        var match = Match("compro dos espadas", "[VERB:comprar] [NUM:qty]? [NOUN:item]");

        Assert.Equal("0:VERB 1:NUM 2:NOUN", Bindings(match));
    }
}
