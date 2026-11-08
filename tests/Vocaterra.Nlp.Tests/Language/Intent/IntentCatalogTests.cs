namespace Vocaterra.Nlp.Language.Tests.Intent;

using System.Text.Json;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Intent;

public class IntentCatalogTests
{
    private static string Single(string intentMembers)
    {
        return "{\"schemaVersion\":1,\"intents\":[{" + intentMembers + "}]}";
    }

    [Fact]
    public void LoadsAndSortsIntentsByOrdinalId()
    {
        var catalog = IntentFixtures.Catalog;

        Assert.Equal(["ask_price", "buy_item", "greet", "offer_item"], catalog.Intents.Select(i => i.Id.Value));
        Assert.All(catalog.Intents, intent => Assert.False(intent.Id.IsUnknown));
    }

    [Fact]
    public void ExposesRequiredSlotsSortedAndEmptyForUnknownIntents()
    {
        var catalog = IntentFixtures.Catalog;

        Assert.Equal(["item", "qty"], catalog.RequiredSlots("offer_item"));
        Assert.Equal(["item", "qty"], catalog.RequiredSlots(new IntentId("offer_item")));
        Assert.Empty(catalog.RequiredSlots("greet"));
        Assert.Empty(catalog.RequiredSlots("does_not_exist"));
        Assert.Empty(catalog.RequiredSlots(IntentId.Unknown));
    }

    [Fact]
    public void FindReturnsTheDefinitionWithItsParts()
    {
        var buy = IntentFixtures.Catalog.Find("buy_item");

        Assert.NotNull(buy);
        Assert.Equal(["quiero comprar una espada", "compro una espada", "necesito una espada"], buy.Examples);
        var rule = Assert.Single(buy.KeywordRules);
        Assert.Equal(["comprar"], rule.Lemmas);
        Assert.Equal(["item"], rule.RequiresSlots);
        Assert.Equal("item", rule.SlotFromNoun);
        Assert.Null(rule.SlotFromNumber);
        Assert.Null(IntentFixtures.Catalog.Find("missing"));
    }

    [Fact]
    public void GrammarsAreIdentifiedByCanonicalPatternAndCollectedInIntentOrder()
    {
        var offer = IntentFixtures.Catalog.Find("offer_item");

        Assert.NotNull(offer);
        Assert.Equal(
            ["tengo [NUM:qty] [NOUN:item]", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]"],
            offer.Grammars.Select(g => g.Id));
        Assert.Equal(offer.Grammars.Select(g => g.Id), IntentFixtures.Catalog.Grammars.Select(g => g.Id));
    }

    [Fact]
    public void QuantityInRequiresSlotsImpliesNumberSlot()
    {
        var catalog = IntentCatalog.Load(Single(
            "\"intentId\":\"buy\",\"keywordRules\":[{\"lemmas\":[\"Comprar\"],\"requiresSlots\":[\"qty\",\"item\"],\"slotFromNoun\":\"item\"}]"));

        var rule = Assert.Single(catalog.Intents[0].KeywordRules);
        Assert.Equal("qty", rule.SlotFromNumber);
        Assert.Equal(["comprar"], rule.Lemmas);
        Assert.Equal(["item", "qty"], rule.RequiresSlots);
    }

    [Fact]
    public void OptionalMembersMayBeOmitted()
    {
        // M82: the intent needs one of slotGrammars, keywordRules or examples; the other two members may be omitted.
        var catalog = IntentCatalog.Load(Single("\"intentId\":\"grammar_only\",\"slotGrammars\":[\"hola\"]"));

        var intent = Assert.Single(catalog.Intents);
        Assert.Empty(intent.RequiredSlots);
        Assert.Single(intent.Grammars);
        Assert.Empty(intent.KeywordRules);
        Assert.Empty(intent.Examples);
    }

    [Fact]
    public void AnIntentWithOnlyKeywordRulesLoads()
    {
        var catalog = IntentCatalog.Load(Single("\"intentId\":\"rule_only\",\"keywordRules\":[{\"lemmas\":[\"comprar\"]}]"));

        var intent = Assert.Single(catalog.Intents);
        Assert.Empty(intent.RequiredSlots);
        Assert.Empty(intent.Grammars);
        Assert.Single(intent.KeywordRules);
        Assert.Empty(intent.Examples);
    }

    [Fact]
    public void AnIntentWithOnlyExamplesLoads()
    {
        var catalog = IntentCatalog.Load(Single("\"intentId\":\"example_only\",\"examples\":[\"hola\"]"));

        var intent = Assert.Single(catalog.Intents);
        Assert.Empty(intent.RequiredSlots);
        Assert.Empty(intent.Grammars);
        Assert.Empty(intent.KeywordRules);
        Assert.Equal(["hola"], intent.Examples);
    }

    [Theory]
    [InlineData("\"intentId\":\"bare\"")]
    [InlineData("\"intentId\":\"bare\",\"requiredSlots\":[\"item\"]")]
    [InlineData("\"intentId\":\"bare\",\"slotGrammars\":[],\"keywordRules\":[],\"examples\":[]")]
    public void AnIntentWithNoGrammarRuleOrExampleIsRefused(string members)
    {
        var error = Assert.Throws<FormatException>(() => IntentCatalog.Load(Single(members)));

        Assert.Contains("Intent 'bare'", error.Message, StringComparison.Ordinal);
        Assert.Contains("slotGrammars", error.Message, StringComparison.Ordinal);
        Assert.Contains("keywordRules", error.Message, StringComparison.Ordinal);
        Assert.Contains("examples", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToJsonRoundTripsAndIsStable()
    {
        var json = IntentFixtures.Catalog.ToJson();
        var reloaded = IntentCatalog.Load(json);

        Assert.Equal(json, reloaded.ToJson());
        Assert.Equal(IntentFixtures.Catalog.ContentHash(), reloaded.ContentHash());
        Assert.Equal(64, IntentFixtures.Catalog.ContentHash().Length);
        Assert.StartsWith("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"ask_price\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateGrammarsAndExamplesAreDroppedAndSlotsSorted()
    {
        var catalog = IntentCatalog.Load(Single(
            "\"intentId\":\"a\",\"requiredSlots\":[\"z\",\"b\",\"b\"],\"slotGrammars\":[\"tengo  [NUM:qty] [NOUN:item]\",\"tengo [NUM:qty] [NOUN:item]\"],"
            + "\"examples\":[\"hola\",\"hola\"]"));

        var intent = catalog.Intents[0];
        Assert.Equal(["b", "z"], intent.RequiredSlots);
        Assert.Single(intent.Grammars);
        Assert.Equal(["hola"], intent.Examples);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"intents\":[]}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"has space\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"unknown\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\"},{\"intentId\":\"a\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\",\"slotGrammars\":[\"[BOGUS:x]\"]}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\",\"examples\":[\" \"]}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\",\"requiredSlots\":[\"two words\"]}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\",\"keywordRules\":[{\"lemmas\":[]}]}]}")]
    [InlineData("{\"schemaVersion\":1,\"intents\":[{\"intentId\":\"a\",\"keywordRules\":[{\"lemmas\":[\"comprar\"],\"requiresSlots\":[\"item\"]}]}]}")]
    public void InvalidCatalogsAreRejected(string json)
    {
        Assert.Throws<FormatException>(() => IntentCatalog.Load(json));
    }

    [Fact]
    public void MalformedJsonIsRejected()
    {
        Assert.ThrowsAny<JsonException>(() => IntentCatalog.Load("{\"schemaVersion\":1,\"intents\":["));
    }

    // M81: an intent with examples and no slot grammar or keyword rule is a valid catalog entry (the embedding rung reads its examples), so the loader's
    // acceptance of the removed grammars is by design; the fuzz oracle's tolerance (ExampleOnlyIntent) relies on it.
    [Fact]
    public void AnIntentWithOnlyExamplesLoadsAndKeepsThemForTheEmbeddingRung()
    {
        var catalog = IntentCatalog.Load(Single("\"intentId\":\"describe\",\"requiredSlots\":[],\"slotGrammars\":[],\"keywordRules\":[],\"examples\":[\"es muy grande\",\"es pequeño\"]"));

        var intent = Assert.Single(catalog.Intents);
        Assert.Empty(intent.Grammars);
        Assert.Empty(intent.KeywordRules);
        Assert.Equal(2, intent.ExampleSpecs.Count);
    }
}
