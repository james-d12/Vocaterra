namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Packs;

public class IntentResolverTests
{
    private static readonly FakeEmbeddingModel Fake = new();

    private static IntentResolution Resolve(string text, IEmbeddingModel? model = null, params string[] allowed)
    {
        return IntentFixtures.Resolve(text, model, withCatalogGrammars: false, options: null, allowed);
    }

    private static void AssertUnresolved(IntentResolution resolution)
    {
        Assert.Equal(new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0), resolution);
    }

    // Rung 1: grammar
    [Fact]
    public void GrammarRungResolvesItsOwnExample()
    {
        var resolution = Resolve("tengo dos espadas");

        Assert.Equal(new IntentId("offer_item"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
        Assert.Equal(1.0, resolution.Margin);
        Assert.Equal(1.0, resolution.Confidence);
        Assert.Equal(SlotMap.From([new("item", "espada"), new("qty", "2")]), resolution.Slots);
    }

    [Fact]
    public void GrammarRungUsesTheAnalyzersMatchWhenItWasGivenTheGrammars()
    {
        var analysis = IntentFixtures.Analyze("tú tienes dos espadas", withCatalogGrammars: true);
        var resolver = new IntentResolver(IntentFixtures.Catalog, SpanishLanguagePack.Instance);

        var resolution = resolver.Resolve(IntentFixtures.Normalize("tú tienes dos espadas"), analysis, IntentFixtures.Prompt());

        Assert.Equal("[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]", analysis.MatchedGrammarId);
        Assert.Equal(new IntentId("offer_item"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
        Assert.Equal(SlotMap.From([new("item", "espada"), new("qty", "2")]), resolution.Slots);
        Assert.Equal(analysis.Confidence, resolution.Confidence);
    }

    [Fact]
    public void GrammarMatchedByTheResolverEqualsTheOneMatchedByTheAnalyzer()
    {
        var own = IntentFixtures.Resolve("tengo dos espadas", withCatalogGrammars: false);
        var viaAnalyzer = IntentFixtures.Resolve("tengo dos espadas", withCatalogGrammars: true);

        Assert.Equal(own, viaAnalyzer);
    }

    [Fact]
    public void GrammarRungIgnoresAgreementErrorsAndReportsAnalysisConfidence()
    {
        var resolution = Resolve("yo tiene dos espada");

        Assert.Equal(new IntentId("offer_item"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
        Assert.Equal(SlotMap.From([new("item", "espada"), new("qty", "2")]), resolution.Slots);
        Assert.Equal(1.0, resolution.Confidence);
    }

    [Fact]
    public void GrammarOfAnIntentThatIsNotAllowedIsIgnored()
    {
        var analysis = IntentFixtures.Analyze("tengo dos espadas", withCatalogGrammars: true);
        Assert.NotNull(analysis.MatchedGrammarId);
        var resolver = new IntentResolver(IntentFixtures.Catalog, SpanishLanguagePack.Instance);

        var resolution = resolver.Resolve(IntentFixtures.Normalize("tengo dos espadas"), analysis, IntentFixtures.Prompt("buy_item", "greet"));

        AssertUnresolved(resolution);
    }

    // Rung 2: rules
    [Fact]
    public void RulesRungResolvesItsOwnExample()
    {
        var resolution = Resolve("quiero comprar una espada");

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(IntentRung.Rules, resolution.Rung);
        Assert.Equal(1.0, resolution.Margin);
        Assert.Equal(0.9, resolution.Confidence);
        Assert.Equal(SlotMap.From([new("item", "espada")]), resolution.Slots);
    }

    [Fact]
    public void RulesMatchConjugatedFormsAndPluralNouns()
    {
        var resolution = Resolve("compro dos espadas");

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(IntentRung.Rules, resolution.Rung);
        Assert.Equal("espada", resolution.Slots["item"]);
    }

    [Fact]
    public void RuleWithoutItsRequiredNounDoesNotFire()
    {
        AssertUnresolved(Resolve("quiero comprar"));
    }

    [Fact]
    public void FavorOfPorFavorIsNotTakenAsTheItem()
    {
        AssertUnresolved(Resolve("por favor quiero comprar"));
        Assert.Equal("espada", Resolve("por favor quiero comprar una espada").Slots["item"]);
        Assert.Equal("espada", Resolve("quiero comprar una espada por favor").Slots["item"]);
    }

    [Fact]
    public void RuleWithOptionalNounLeavesSlotOutWhenThereIsNone()
    {
        var resolution = Resolve("cuánto cuesta");

        Assert.Equal(new IntentId("ask_price"), resolution.Intent);
        Assert.Equal(IntentRung.Rules, resolution.Rung);
        Assert.Equal(SlotMap.Empty, resolution.Slots);
    }

    [Fact]
    public void QuantityComesFromTheFirstNumeralWhenTheRuleListsIt()
    {
        var catalog = IntentCatalog.Load("""
            { "schemaVersion": 1, "intents": [
              { "intentId": "buy", "keywordRules": [
                { "lemmas": ["comprar"], "requiresSlots": ["item", "qty"], "slotFromNoun": "item" } ] } ] }
            """);
        var resolver = new IntentResolver(catalog, SpanishLanguagePack.Instance);

        var withQuantity = resolver.Resolve(
            IntentFixtures.Normalize("compro dos espadas"),
            IntentFixtures.Analyze("compro dos espadas"),
            IntentFixtures.Prompt());
        var without = resolver.Resolve(
            IntentFixtures.Normalize("compro espadas"),
            IntentFixtures.Analyze("compro espadas"),
            IntentFixtures.Prompt());

        Assert.Equal(IntentRung.Rules, withQuantity.Rung);
        Assert.Equal(SlotMap.From([new("item", "espada"), new("qty", "2")]), withQuantity.Slots);
        AssertUnresolved(without);
    }

    [Fact]
    public void KeywordTokenIsNotTakenAsTheSlotNoun()
    {
        var catalog = IntentCatalog.Load("""
            { "schemaVersion": 1, "intents": [
              { "intentId": "price", "keywordRules": [
                { "lemmas": ["precio"], "requiresSlots": ["item"], "slotFromNoun": "item" } ] } ] }
            """);
        var resolver = new IntentResolver(catalog, SpanishLanguagePack.Instance);

        var resolution = resolver.Resolve(
            IntentFixtures.Normalize("cuál es el precio de la espada"),
            IntentFixtures.Analyze("cuál es el precio de la espada"),
            IntentFixtures.Prompt());

        Assert.Equal(IntentRung.Rules, resolution.Rung);
        Assert.Equal(SlotMap.From([new("item", "espada")]), resolution.Slots);
    }

    [Fact]
    public void MoreSpecificRuleWinsAndEqualRulesOfDifferentIntentsAreAmbiguous()
    {
        var catalog = IntentCatalog.Load("""
            { "schemaVersion": 1, "intents": [
              { "intentId": "a_general", "keywordRules": [ { "lemmas": ["comprar"] } ] },
              { "intentId": "b_specific", "keywordRules": [ { "lemmas": ["comprar", "espada"] } ] },
              { "intentId": "c_tie", "keywordRules": [ { "lemmas": ["querer", "escudo"] } ] },
              { "intentId": "d_tie", "keywordRules": [ { "lemmas": ["comprar", "escudo"] } ] } ] }
            """);
        var resolver = new IntentResolver(catalog, SpanishLanguagePack.Instance);

        IntentResolution Run(string text) =>
            resolver.Resolve(IntentFixtures.Normalize(text), IntentFixtures.Analyze(text), IntentFixtures.Prompt());

        Assert.Equal(new IntentId("b_specific"), Run("quiero comprar una espada").Intent);
        Assert.Equal(new IntentId("a_general"), Run("quiero comprar").Intent);
        AssertUnresolved(Run("quiero comprar un escudo"));
    }

    [Fact]
    public void GrammarRungComesBeforeRules()
    {
        var catalog = IntentCatalog.Load("""
            { "schemaVersion": 1, "intents": [
              { "intentId": "a_rule", "keywordRules": [ { "lemmas": ["tener"] } ] },
              { "intentId": "z_grammar", "slotGrammars": ["tengo [NUM:qty] [NOUN:item]"] } ] }
            """);
        var resolver = new IntentResolver(catalog, SpanishLanguagePack.Instance);

        var resolution = resolver.Resolve(
            IntentFixtures.Normalize("tengo dos espadas"),
            IntentFixtures.Analyze("tengo dos espadas"),
            IntentFixtures.Prompt());

        Assert.Equal(new IntentId("z_grammar"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
    }

    [Fact]
    public void RulesRungComesBeforeEmbeddings()
    {
        var resolution = Resolve("quiero comprar una espada", Fake);

        Assert.Equal(IntentRung.Rules, resolution.Rung);
    }

    // Rung 3: embeddings
    [Fact]
    public void EmbeddingRungResolvesAParaphraseThatNeitherGrammarNorRulesCover()
    {
        var resolution = Resolve("necesito una espada nueva", Fake);

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(IntentRung.Embedding, resolution.Rung);
        Assert.InRange(resolution.Confidence, 0.82, 1.0);
        Assert.InRange(resolution.Margin, 0.06, 1.0);
        Assert.Equal(SlotMap.From([new("item", "espada")]), resolution.Slots);
    }

    [Fact]
    public void EmbeddingRungResolvesAnExampleItself()
    {
        var resolution = Resolve("hola buenos días", Fake);

        Assert.Equal(new IntentId("greet"), resolution.Intent);
        Assert.Equal(IntentRung.Embedding, resolution.Rung);
        Assert.Equal(1.0, resolution.Confidence, 6);
        Assert.Equal(SlotMap.Empty, resolution.Slots);
    }

    [Fact]
    public void WithoutAnEmbeddingModelTheLadderEndsAtUnknown()
    {
        AssertUnresolved(Resolve("necesito una espada nueva"));
    }

    [Fact]
    public void TextOutsideEveryRungIsUnknown()
    {
        AssertUnresolved(Resolve("el dragón vuela sobre la montaña", Fake));
    }

    [Fact]
    public void TopScoreInsideTheMarginBandResolvesToUnknown()
    {
        // 0.811 against threshold 0.80 +/- 0.02: Uncertain, so the answer is Unknown.
        Assert.Equal(MarginDecision.Uncertain, IntentResolverOptions.Default.EmbeddingTop1.Evaluate(0.811));
        AssertUnresolved(Resolve("necesito la espada", Fake));
    }

    [Theory]
    [InlineData(0.90, 0.60, true)]
    [InlineData(0.825, 0.10, true)]
    [InlineData(0.81, 0.10, false)]
    [InlineData(0.79, 0.10, false)]
    [InlineData(0.60, 0.10, false)]
    [InlineData(0.95, 0.88, true)]
    [InlineData(0.95, 0.895, false)]
    [InlineData(0.95, 0.92, false)]
    [InlineData(0.95, 0.94, false)]
    public void EmbeddingRungAppliesBothMargins(double top1, double second, bool accepted)
    {
        var model = new ScriptedEmbeddingModel(
            "mm",
            new Dictionary<string, double>
            {
                ["compro una espada"] = top1,
                ["hola amigo"] = second,
            });

        var resolution = Resolve("mm", model);

        if (accepted)
        {
            Assert.Equal(new IntentId("buy_item"), resolution.Intent);
            Assert.Equal(IntentRung.Embedding, resolution.Rung);
            Assert.Equal(top1, resolution.Confidence, 5);
            Assert.Equal(top1 - second, resolution.Margin, 5);
        }
        else
        {
            AssertUnresolved(resolution);
        }
    }

    [Fact]
    public void TheEmbeddingMarginIsAtMostOneWhenTheCosineGapIsLarger()
    {
        // A best score of 0.97 against a runner-up of -0.2 is a raw gap of 1.17 (found by O7, seed 5lSWF-U_rpTa); the margin is clamped to 1 and the rung still accepts.
        // Every other example scores -0.5, so the runner-up intent is greet at -0.2 and the raw gap is 1.17.
        var model = new ScriptedEmbeddingModel(
            "mm",
            new Dictionary<string, double> { ["compro una espada"] = 0.97, ["hola amigo"] = -0.2 },
            defaultSimilarity: -0.5);

        var resolution = Resolve("mm", model);

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(1.0, resolution.Margin, 5);
    }

    [Fact]
    public void IntentScoreIsTheBestExampleSoSiblingExamplesDoNotCompeteWithEachOther()
    {
        var model = new ScriptedEmbeddingModel(
            "mm",
            new Dictionary<string, double>
            {
                ["compro una espada"] = 0.90,
                ["necesito una espada"] = 0.89,
                ["quiero comprar una espada"] = 0.88,
                ["hola amigo"] = 0.50,
            });

        var resolution = Resolve("mm", model);

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(0.40, resolution.Margin, 5);
    }

    [Fact]
    public void SingleCandidateHasAGapOfOne()
    {
        var model = new ScriptedEmbeddingModel("mm", new Dictionary<string, double> { ["compro una espada"] = 0.90, ["hola amigo"] = 0.89 });

        var resolution = Resolve("mm", model, "buy_item");

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(1.0, resolution.Margin);
        Assert.Equal(0.90, resolution.Confidence, 5);
    }

    [Fact]
    public void EqualScoresBreakTiesByOrdinalIdWhenTheGapMarginAllowsIt()
    {
        var model = new ScriptedEmbeddingModel("mm", new Dictionary<string, double> { ["compro una espada"] = 0.90, ["hola amigo"] = 0.90 });
        var options = new IntentResolverOptions { EmbeddingGap = new Margin(-1.0, 0.0) };

        var resolution = IntentFixtures.Resolve("mm", model, options: options);

        Assert.Equal(new IntentId("buy_item"), resolution.Intent);
        Assert.Equal(0.0, resolution.Margin, 6);
    }

    [Fact]
    public void ThresholdsAreConfigurable()
    {
        var model = new ScriptedEmbeddingModel("mm", new Dictionary<string, double> { ["compro una espada"] = 0.60 });
        var strict = Resolve("mm", model);
        var lenient = IntentFixtures.Resolve("mm", model, options: new IntentResolverOptions { EmbeddingTop1 = new Margin(0.5, 0.01) });

        AssertUnresolved(strict);
        Assert.Equal(new IntentId("buy_item"), lenient.Intent);
    }

    [Fact]
    public void ModelWithWrongDimensionIsReportedAsInvalid()
    {
        var model = new WrongSizeModel();

        Assert.Throws<InvalidOperationException>(() => Resolve("necesito una espada nueva", model));
    }

    // Allowed intents
    [Fact]
    public void OnlyAllowedIntentsAreCandidates()
    {
        var all = Resolve("necesito una espada nueva", Fake);
        var greetOnly = Resolve("necesito una espada nueva", Fake, "greet");
        var buyOnly = Resolve("necesito una espada nueva", Fake, "buy_item");

        Assert.Equal(new IntentId("buy_item"), all.Intent);
        AssertUnresolved(greetOnly);
        Assert.Equal(new IntentId("buy_item"), buyOnly.Intent);
        Assert.Equal(1.0, buyOnly.Margin);
    }

    [Fact]
    public void AllowedListOrderDoesNotMatterAndUnknownIdsAreIgnored()
    {
        var model = new ScriptedEmbeddingModel("mm", new Dictionary<string, double> { ["compro una espada"] = 0.90, ["hola amigo"] = 0.70 });

        var first = Resolve("mm", model, "greet", "buy_item", "not_in_catalog");
        var second = Resolve("mm", model, "buy_item", "not_in_catalog", "greet");

        Assert.Equal(first, second);
        Assert.Equal(new IntentId("buy_item"), first.Intent);
        AssertUnresolved(Resolve("mm", model, "not_in_catalog"));
    }

    [Fact]
    public void RulesAndGrammarsRespectTheAllowedList()
    {
        AssertUnresolved(Resolve("quiero comprar una espada", null, "greet"));
        Assert.Equal(new IntentId("buy_item"), Resolve("quiero comprar una espada", null, "buy_item", "greet").Intent);
        Assert.Equal(new IntentId("offer_item"), Resolve("tengo dos espadas", null, "offer_item").Intent);
    }

    // Determinism
    [Fact]
    public void SameInputsGiveIdenticalResolutionsAcrossCallsAndInstances()
    {
        string[] texts =
        [
            "tengo dos espadas",
            "quiero comprar una espada",
            "necesito una espada nueva",
            "necesito la espada",
            "hola buenos días",
            "el dragón vuela sobre la montaña",
            string.Empty,
        ];
        var resolverA = new IntentResolver(IntentFixtures.Catalog, SpanishLanguagePack.Instance, new FakeEmbeddingModel());
        var resolverB = new IntentResolver(IntentCatalog.Load(IntentFixtures.Catalog.ToJson()), SpanishLanguagePack.Instance, new FakeEmbeddingModel());

        foreach (var text in texts)
        {
            var normalized = IntentFixtures.Normalize(text);
            var analysis = IntentFixtures.Analyze(text);
            var prompt = IntentFixtures.Prompt();
            var once = resolverA.Resolve(normalized, analysis, prompt);
            Assert.Equal(once, resolverA.Resolve(normalized, analysis, prompt));
            Assert.Equal(once, resolverB.Resolve(normalized, analysis, prompt));
            Assert.Equal(CanonicalJson.Serialize(once), CanonicalJson.Serialize(resolverB.Resolve(normalized, analysis, prompt)));
        }
    }

    [Fact]
    public void EmptyUtteranceIsUnknown()
    {
        AssertUnresolved(Resolve(string.Empty, Fake));
    }

    private sealed class WrongSizeModel : IEmbeddingModel
    {
        public int Dimension => 4;

        public string ModelHash => "wrong-size";

        public float[] Embed(string text) => [1f, 0f];
    }
}
