using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Response.Templates;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Es.Tests.Packs;

/// <summary>M88: the additive members for a language with cases, polite positions, V2, capitals, <c>decl()</c> and <c>particle()</c>; Spanish's output does not change.</summary>
public class PackGapTests
{
    private static readonly ILanguagePack Spanish = SpanishLanguagePack.Instance;

    private static TemplateEngine Engine() => new(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed), Spanish);

    private static string Render(string text, params (string Name, string Value)[] slots) =>
        Engine().Render(new DialogueTemplate("t", text), SlotMap.From([.. slots.Select(static s => new KeyValuePair<string, string>(s.Name, s.Value))]));

    private static IReadOnlyList<AnalyzedToken> Tokens(string text) => Fixtures.Analyzer.Analyze(TestTokens.From(text)).Tokens;

    // ---- gap 1 and 2: case ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SpanishHasNoCasesAndNoV2()
    {
        Assert.Empty(Spanish.GrammaticalCases);
        Assert.False(Spanish.SupportsV2Order);
    }

    [Fact]
    public void ASpanishArticleWithANullCaseIsTheArticleWithoutACase()
    {
        var morphology = Spanish.CreateMorphology(SpanishLexicon.Seed);
        var functions = Spanish.CreateTemplateFunctions(SpanishLexicon.Seed, morphology);

        Assert.Equal(morphology.Article(Definiteness.Definite, "f", "sg", "agua"), morphology.Article(Definiteness.Definite, "f", "sg", "agua", null));
        Assert.Equal("el", morphology.Article(Definiteness.Definite, "f", "sg", "agua", null));
        Assert.Equal(functions.Article(Definiteness.Indefinite, "f", "sg", "espada"), functions.Article(Definiteness.Indefinite, "f", "sg", "espada", null));
    }

    [Fact]
    public void ASpanishArticleRefusesACase()
    {
        var morphology = Spanish.CreateMorphology(SpanishLexicon.Seed);
        var functions = Spanish.CreateTemplateFunctions(SpanishLexicon.Seed, morphology);

        Assert.Throws<NotSupportedException>(() => morphology.Article(Definiteness.Definite, "m", "sg", "libro", "acc"));
        Assert.Throws<NotSupportedException>(() => functions.Article(Definiteness.Definite, "m", "sg", "libro", "acc"));
    }

    [Fact]
    public void ASpanishNumberAgreesWithANullCaseAndRefusesACase()
    {
        Assert.Equal("una", Spanish.Numbers.Agree("uno", "f", null));
        Assert.Equal(Spanish.Numbers.Agree("veintiuno", "m"), Spanish.Numbers.Agree("veintiuno", "m", null));
        Assert.Throws<NotSupportedException>(() => Spanish.Numbers.Agree("uno", "f", "acc"));
    }

    [Fact]
    public void ATemplateRendersAsBeforeWithoutACaseArgument()
    {
        Assert.Equal("la espada", Render("{item:article(def)}", ("item", "espada")));
        Assert.Equal("una", Render("{qty:words(item)}", ("qty", "1"), ("item", "espada")));
    }

    [Theory]
    [InlineData("{item:article(def,acc)}")]
    [InlineData("{item:article(indef,dat)}")]
    [InlineData("{qty:words(m,acc)}")]
    public void ATemplateRejectsACaseArgumentWhenThePackHasNoCase(string template)
    {
        var ex = Assert.Throws<TemplateException>(() => Render(template, ("item", "espada"), ("qty", "1")));

        Assert.Contains("no grammatical case", ex.Message, StringComparison.Ordinal);
    }

    // ---- gap 3: polite positions and V2 ------------------------------------------------------------------------------------------------

    [Fact]
    public void SpanishAllowsTheTrailingPolitePhraseOnlyAndStaysByteIdentical()
    {
        var defaults = Spanish.GrammarDefaults;
        var grammar = SlotGrammarType.Parse("[VERB:querer] [NOUN:item]");

        Assert.Equal(PolitePositions.Trailing, defaults.PolitePositions);
        Assert.Equal(2, defaults.MaxPoliteTokens);
        Assert.NotNull(SlotGrammarMatcher.Match(Tokens("quiero agua por favor"), grammar, Spanish));
        Assert.Null(SlotGrammarMatcher.Match(Tokens("por favor quiero agua"), grammar, Spanish));
        Assert.Null(SlotGrammarMatcher.Match(Tokens("quiero por favor agua"), grammar, Spanish));
    }

    [Fact]
    public void AGrammarDefaultsRecordBuiltWithThreeArgumentsIsTrailingOnly()
    {
        var defaults = new GrammarDefaults(true, ["yo"], ["por", "favor"]);

        Assert.Equal(PolitePositions.Trailing, defaults.PolitePositions);
        Assert.Equal(2, defaults.MaxPoliteTokens);
        Assert.Equal(0, (defaults with { PolitePositions = PolitePositions.None }).MaxPoliteTokens);
        Assert.Equal(6, (defaults with { PolitePositions = PolitePositions.Trailing | PolitePositions.Leading | PolitePositions.AfterFiniteVerb }).MaxPoliteTokens);
    }

    [Fact]
    public void AnIntentWithOrderV2IsRefusedWhenThePackDoesNotSupportIt()
    {
        const string Json = """{"schemaVersion":1,"intents":[{"intentId":"buy","order":"v2","slotGrammars":["[VERB:querer] [NOUN:item]"]}]}""";

        var ex = Assert.Throws<FormatException>(() => IntentCatalog.Load(Json, Spanish.Folding, Spanish.SupportsV2Order));

        Assert.Contains("does not support V2", ex.Message, StringComparison.Ordinal);
        Assert.Throws<FormatException>(() => IntentCatalog.Load(Json));
    }

    [Fact]
    public void AnUnknownOrderIsRefusedAndNoOrderStaysNull()
    {
        const string Unknown = """{"schemaVersion":1,"intents":[{"intentId":"buy","order":"svo","slotGrammars":["[VERB:querer] [NOUN:item]"]}]}""";
        const string None = """{"schemaVersion":1,"intents":[{"intentId":"buy","slotGrammars":["[VERB:querer] [NOUN:item]"]}]}""";

        Assert.Throws<FormatException>(() => IntentCatalog.Load(Unknown, Spanish.Folding, supportsV2Order: true));
        var catalog = IntentCatalog.Load(None, Spanish.Folding, Spanish.SupportsV2Order);

        Assert.Null(catalog.Find("buy")!.Order);
        Assert.DoesNotContain("order", catalog.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void ALoaderRecordsOrderV2ForAPackThatSupportsIt()
    {
        const string Json = """{"schemaVersion":1,"intents":[{"intentId":"buy","order":"v2","slotGrammars":["[VERB:querer] [NOUN:item]"]}]}""";
        var catalog = IntentCatalog.Load(Json, Spanish.Folding, supportsV2Order: true);
        var intent = catalog.Find("buy")!;

        Assert.Equal(IntentCatalog.V2Order, intent.Order);
        Assert.Contains("\"order\":\"v2\"", catalog.ToJson(), StringComparison.Ordinal);
    }

    // ---- gap 4: capitals --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void SpanishRestoresNoCapitalsAndDoesNotListOrthCapital()
    {
        var text = TestTokens.From("yo tengo agua");
        var analysis = Fixtures.Analyzer.Analyze(text);

        Assert.Empty(Spanish.Recast.RestoredCapitals(text, analysis, SpanishLexicon.Seed));
        Assert.False(Spanish.ErrorTags.IsKnown(ErrorTags.OrthCapital));
        Assert.False(ErrorTags.IsKnown(ErrorTags.OrthCapital));
        Assert.DoesNotContain(ErrorTags.OrthCapital, ErrorTags.All);
    }

    // ---- gap 5: decl() and particle() -------------------------------------------------------------------------------------------------

    [Fact]
    public void SpanishDoesNotSupportDeclOrParticle()
    {
        var functions = Spanish.CreateTemplateFunctions(SpanishLexicon.Seed, Spanish.CreateMorphology(SpanishLexicon.Seed));

        Assert.False(functions.SupportsAdjectiveDeclension);
        Assert.False(functions.SupportsSeparableParticle);
        Assert.Throws<NotSupportedException>(() => functions.DeclineAdjective("bueno", "m", "sg", "nom", null));
        Assert.Throws<NotSupportedException>(() => functions.SeparableParticle("llamar"));
    }

    [Theory]
    [InlineData("{adj:decl(m,sg,nom)}", "decl() is not supported by this language")]
    [InlineData("{verb:particle}", "particle() is not supported by this language")]
    public void ATemplateWithDeclOrParticleIsRefusedWithAClearError(string template, string message)
    {
        var ex = Assert.Throws<TemplateException>(() => Render(template, ("adj", "bueno"), ("verb", "llamar")));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }
}
