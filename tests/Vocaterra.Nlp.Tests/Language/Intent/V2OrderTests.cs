using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.SlotGrammar;

namespace Vocaterra.Nlp.Tests.Language.Intent;

/// <summary>M88: an intent may declare <c>order: "v2"</c>; the loader records it, and the expansion is a stub until M40.</summary>
public class V2OrderTests
{
    private const string Json = """{"schemaVersion":1,"intents":[{"intentId":"buy","order":"v2","slotGrammars":["[VERB:querer] [NOUN:item]"]}]}""";

    [Fact]
    public void TheV2ExpansionIsAStubThatThrowsNotSupported()
    {
        var intent = IntentCatalog.Load(Json, null, supportsV2Order: true).Find("buy")!;

        Assert.Equal("v2", intent.Order);
        var ex = Assert.Throws<NotSupportedException>(() => V2GrammarExpansion.Expand(intent));
        Assert.Contains("M40", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlagSurvivesTheCanonicalRoundTrip()
    {
        var catalog = IntentCatalog.Load(Json, null, supportsV2Order: true);

        Assert.Equal(catalog.ToJson(), IntentCatalog.Load(catalog.ToJson(), null, supportsV2Order: true).ToJson());
    }

    [Fact]
    public void ALoaderThatDoesNotDeclareV2SupportRefusesTheFlag()
    {
        Assert.Throws<FormatException>(() => IntentCatalog.Load(Json));
    }
}
