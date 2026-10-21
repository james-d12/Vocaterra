namespace Vocaterra.Nlp.Language.Tests.Intent;

using System.Text.Json;
using Vocaterra.Nlp.Language.Intent;

/// <summary>The additive schema of M27a: examples as objects with expected error tags, and per-intent lint waivers.</summary>
public class IntentExampleTests
{
    private static string Single(string intentMembers)
    {
        return "{\"schemaVersion\":1,\"intents\":[{" + intentMembers + "}]}";
    }

    [Fact]
    public void StringExamplesLoadAsExamplesWithoutExpectations()
    {
        var intent = IntentCatalog.Load(Single("\"intentId\":\"a\",\"examples\":[\"hola\",\"adiós\"]")).Intents[0];

        Assert.Equal(["hola", "adiós"], intent.Examples);
        Assert.Equal([new IntentExample("hola"), new IntentExample("adiós")], intent.ExampleSpecs);
        Assert.All(intent.ExampleSpecs, example => Assert.False(example.HasExpectation));
        Assert.True(intent.Lint.IsEmpty);
    }

    [Fact]
    public void ObjectExamplesCarryTheirExpectationAndPrompt()
    {
        var intent = IntentCatalog.Load(Single(
            "\"intentId\":\"a\",\"examples\":[\"yo soy Sol\",{\"text\":\"yo es Rosa\",\"errors\":[\"noun.number\",\"conj.person\",\"conj.person\"],\"prompt\":\"p1\"},{\"text\":\"soy Mar\",\"errors\":[]}]"))
            .Intents[0];

        Assert.Equal(["yo soy Sol", "yo es Rosa", "soy Mar"], intent.Examples);
        var expected = intent.ExampleSpecs[1];
        Assert.Equal(["conj.person", "noun.number"], expected.ExpectedErrors);
        Assert.Equal("p1", expected.Prompt);
        Assert.True(expected.HasExpectation);
        Assert.Empty(intent.ExampleSpecs[2].ExpectedErrors!);
        Assert.True(intent.ExampleSpecs[2].HasExpectation);
        Assert.False(intent.ExampleSpecs[0].HasExpectation);
    }

    [Fact]
    public void BothFormsRoundTripAndStringOnlyCatalogsSerializeAsBefore()
    {
        var plain = Single("\"intentId\":\"a\",\"requiredSlots\":[],\"slotGrammars\":[\"hola\"],\"keywordRules\":[],\"examples\":[\"hola\",\"hola amigo\"]");
        Assert.Equal(plain, IntentCatalog.Load(plain).ToJson());

        var mixed = Single(
            "\"intentId\":\"a\",\"requiredSlots\":[],\"slotGrammars\":[\"hola\"],\"keywordRules\":[],"
            + "\"examples\":[\"hola\",{\"text\":\"yo es Rosa\",\"errors\":[\"conj.person\"]},{\"text\":\"ok\",\"errors\":[],\"prompt\":\"p\"},{\"text\":\"x\",\"prompt\":\"p\"}],"
            + "\"lint\":{\"allowOverlap\":[\"b\",\"c\"],\"allowRuleOnly\":[\"hola\"],\"allowHomograph\":[\"costar\"]}");
        var catalog = IntentCatalog.Load(mixed);
        var json = catalog.ToJson();

        Assert.Equal(mixed, json);
        Assert.Equal(json, IntentCatalog.Load(json).ToJson());
        Assert.Equal(catalog.ContentHash(), IntentCatalog.Load(json).ContentHash());
        Assert.Equal(catalog.Intents[0].ExampleSpecs, IntentCatalog.Load(json).Intents[0].ExampleSpecs);
    }

    [Fact]
    public void ObjectExampleWithoutExpectationOrPromptIsWrittenAsAString()
    {
        var catalog = IntentCatalog.Load(Single("\"intentId\":\"a\",\"examples\":[{\"text\":\"hola\"}]"));

        Assert.Contains("\"examples\":[\"hola\"]", catalog.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"lint\"", catalog.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void LintWaiversAreSortedDistinctAndExposed()
    {
        var intent = IntentCatalog.Load(Single("\"intentId\":\"a\",\"lint\":{\"allowOverlap\":[\"thank\",\"ask\",\"thank\"]}")).Intents[0];

        Assert.Equal(["ask", "thank"], intent.Lint.AllowOverlap);
        Assert.Null(intent.Lint.AllowRuleOnly);
        Assert.False(intent.Lint.IsEmpty);
    }

    [Fact]
    public void DuplicateExampleTextsKeepTheFirst()
    {
        var intent = IntentCatalog.Load(Single("\"intentId\":\"a\",\"examples\":[{\"text\":\"hola\",\"errors\":[\"x\"]},\"hola\"]")).Intents[0];

        var only = Assert.Single(intent.ExampleSpecs);
        Assert.Equal(["x"], only.ExpectedErrors);
    }

    [Theory]
    [InlineData("\"examples\":[{\"errors\":[\"a.b\"]}]")]
    [InlineData("\"examples\":[{\"text\":\"hola\",\"erors\":[\"a.b\"]}]")]
    [InlineData("\"examples\":[{\"text\":\"hola\",\"errors\":\"a.b\"}]")]
    [InlineData("\"examples\":[{\"text\":\"hola\",\"errors\":[1]}]")]
    [InlineData("\"examples\":[{\"text\":5}]")]
    [InlineData("\"examples\":[5]")]
    [InlineData("\"examples\":[{\"text\":\"hola\",\"errors\":[\" \"]}]")]
    public void MalformedExamplesAreRejectedWithAJsonException(string members)
    {
        Assert.ThrowsAny<JsonException>(() => IntentCatalog.Load(Single("\"intentId\":\"a\"," + members)));
    }

    [Theory]
    [InlineData("\"examples\":[{\"text\":\" \"}]")]
    [InlineData("\"examples\":[{\"text\":\"hola\",\"prompt\":\" \"}]")]
    [InlineData("\"lint\":{\"allowOverlap\":[\" \"]}")]
    public void EmptyPartsAreRejectedWithAFormatException(string members)
    {
        Assert.Throws<FormatException>(() => IntentCatalog.Load(Single("\"intentId\":\"a\"," + members)));
    }

    [Fact]
    public void WaiversAndExpectationsDoNotChangeResolution()
    {
        // The same grammars and rules resolve the same text whatever the examples and waivers say.
        const string Body = "\"intentId\":\"thank\",\"slotGrammars\":[\"gracias\"],\"keywordRules\":[{\"lemmas\":[\"gracias\"]}]";
        var plain = IntentCatalog.Load(Single(Body + ",\"examples\":[\"gracias\"]"));
        var rich = IntentCatalog.Load(Single(Body + ",\"examples\":[{\"text\":\"gracias\",\"errors\":[]}],\"lint\":{\"allowOverlap\":[\"x\"]}"));

        Assert.Equal(plain.Intents[0].Grammars.Select(g => g.Id), rich.Intents[0].Grammars.Select(g => g.Id));
        Assert.Equal(plain.Intents[0].KeywordRules, rich.Intents[0].KeywordRules);
        Assert.Equal(plain.Intents[0].Examples, rich.Intents[0].Examples);
    }
}
