namespace Slg.Response.Tests.Templates;

using System.Text.Json;
using Slg.Core.Determinism;
using Slg.Response.Templates;

public class DialogueCatalogTests
{
    internal static DialogueCatalog SampleCatalog()
    {
        return new DialogueCatalog(
        [
            new KeyValuePair<string, VariantPool>("greet", new VariantPool("greet", [
                new DialogueTemplate("greet.1", "¡Hola, viajero!"),
                new DialogueTemplate("greet.2", "Buenas, ¿qué necesitas?"),
                new DialogueTemplate("greet.3", "Bienvenido a mi herrería."),
                new DialogueTemplate("greet.4", "¡Adelante!"),
                new DialogueTemplate("greet.5", "Hola. ¿Buscas algo?"),
            ])),
            new KeyValuePair<string, VariantPool>("buy_item", new VariantPool("buy_item", [
                new DialogueTemplate("buy_item.1", "¿Tienes {qty:words} {item:plural-if(qty)}?"),
            ])),
        ]);
    }

    [Fact]
    public void JsonIsCanonicalWithSortedIntentKeys()
    {
        var json = SampleCatalog().ToJson();
        Assert.StartsWith("{\"pools\":{\"buy_item\":{\"poolId\":\"buy_item\",\"variants\":[{\"lineId\":\"buy_item.1\",\"text\":\"¿Tienes {qty:words} {item:plural-if(qty)}?\"}]},\"greet\":{", json, StringComparison.Ordinal);
        Assert.Equal(json, CanonicalJson.Serialize(SampleCatalog()));
    }

    [Fact]
    public void JsonRoundTripsToAnEqualCatalogWithIdenticalBytes()
    {
        var catalog = SampleCatalog();
        var json = catalog.ToJson();
        var copy = DialogueCatalog.FromJson(json);
        Assert.Equal(catalog, copy);
        Assert.Equal(catalog.GetHashCode(), copy.GetHashCode());
        Assert.Equal(json, copy.ToJson());
        Assert.Equal(json, CanonicalJson.Serialize(CanonicalJson.Deserialize<DialogueCatalog>(json)));
    }

    [Fact]
    public void InsertionOrderDoesNotAffectTheCatalog()
    {
        var reversed = new DialogueCatalog(SampleCatalog().Pools.Reverse());
        Assert.Equal(SampleCatalog(), reversed);
        Assert.Equal(SampleCatalog().ToJson(), reversed.ToJson());
        Assert.Equal(["buy_item", "greet"], reversed.Pools.Keys);
    }

    [Fact]
    public void LookupByIntentAndByPoolId()
    {
        var catalog = SampleCatalog();
        Assert.True(catalog.TryGetPool("greet", out var pool));
        Assert.Equal(5, pool.Variants.Count);
        Assert.False(catalog.TryGetPool("nope", out _));
        Assert.True(catalog.TryGetPoolById("buy_item", out var byId));
        Assert.Single(byId.Variants);
        Assert.False(catalog.TryGetPoolById("nope", out _));
    }

    [Fact]
    public void InvalidCatalogsAreRejected()
    {
        var pool = new VariantPool("p", [new DialogueTemplate("p.1", "hola")]);
        var other = new VariantPool("q", [new DialogueTemplate("p.1", "adiós")]);
        Assert.Throws<ArgumentException>(() => new DialogueCatalog([new("a", pool), new("a", pool)]));
        Assert.Throws<ArgumentException>(() => new DialogueCatalog([new("a", pool), new("b", pool)]));
        Assert.Throws<ArgumentException>(() => new DialogueCatalog([new("a", pool), new("b", other)]));
        Assert.Throws<ArgumentException>(() => new DialogueCatalog([new(" ", pool)]));
        Assert.Throws<ArgumentException>(() => new VariantPool("p", []));
        Assert.Throws<ArgumentException>(() => new VariantPool("", [new DialogueTemplate("a", "b")]));
        Assert.Throws<ArgumentException>(() => new VariantPool("p", [new DialogueTemplate("a", "b"), new DialogueTemplate("a", "c")]));
        Assert.Throws<ArgumentException>(() => new DialogueTemplate("", "text"));
        Assert.Throws<ArgumentException>(() => new DialogueTemplate("id", " "));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"pools\":[]}")]
    [InlineData("{\"other\":{}}")]
    [InlineData("[]")]
    [InlineData("{\"pools\":{\"a\":{\"poolId\":\"p\",\"variants\":[]}}}")]
    [InlineData("{\"pools\":{\"a\":{\"poolId\":\"p\",\"variants\":[{\"lineId\":\"x\",\"text\":\"t\"}]},\"a\":{\"poolId\":\"q\",\"variants\":[{\"lineId\":\"y\",\"text\":\"t\"}]}}}")]
    [InlineData("{\"pools\":{\"a\":{\"poolId\":\"p\",\"variants\":[{\"lineId\":\"x\"}]}}}")]
    public void InvalidJsonIsRejected(string json)
    {
        Assert.ThrowsAny<Exception>(() => DialogueCatalog.FromJson(json));
    }

    [Fact]
    public void EmptyCatalogRoundTrips()
    {
        Assert.Equal("{\"pools\":{}}", DialogueCatalog.Empty.ToJson());
        Assert.Equal(DialogueCatalog.Empty, DialogueCatalog.FromJson("{\"pools\":{}}"));
    }

    [Fact]
    public void SelectionUsesNextIntOfTheSeededGenerator()
    {
        var catalog = SampleCatalog();
        var pool = catalog.Pools["greet"];
        for (long counter = 0; counter < 20; counter++)
        {
            var expected = pool.Variants[SeededRng.ForInteraction(2026, "blacksmith", counter).NextInt(pool.Variants.Count)];
            var chosen = DialogueSelector.Select(catalog, "greet", SeededRng.ForInteraction(2026, "blacksmith", counter));
            Assert.Equal(expected, chosen);
        }
    }

    [Fact]
    public void SelectionIsStableForASeedAndVariesAcrossSeeds()
    {
        var catalog = SampleCatalog();
        var first = Enumerable.Range(0, 50)
            .Select(i => DialogueSelector.Select(catalog, "greet", SeededRng.ForInteraction(7, "blacksmith", i)).LineId)
            .ToList();
        var second = Enumerable.Range(0, 50)
            .Select(i => DialogueSelector.Select(catalog, "greet", SeededRng.ForInteraction(7, "blacksmith", i)).LineId)
            .ToList();
        Assert.Equal(first, second);
        Assert.Equal(5, first.Distinct().Count());
    }

    [Fact]
    public void SelectionMatchesGoldenValuesForAFixedSeed()
    {
        var catalog = SampleCatalog();
        var chosen = Enumerable.Range(0, 12)
            .Select(i => DialogueSelector.Select(catalog, "greet", SeededRng.ForInteraction(2026, "blacksmith", i)).LineId);
        Assert.Equal(
            ["greet.4", "greet.5", "greet.3", "greet.2", "greet.5", "greet.4", "greet.2", "greet.2", "greet.1", "greet.3", "greet.3", "greet.4"],
            chosen);
    }

    [Fact]
    public void SelectionConsumesExactlyOneDrawEvenForASingleVariant()
    {
        var catalog = SampleCatalog();
        var used = SeededRng.ForInteraction(1, "npc", 1);
        var reference = SeededRng.ForInteraction(1, "npc", 1);
        Assert.Equal("buy_item.1", DialogueSelector.Select(catalog, "buy_item", used).LineId);
        reference.NextInt(1);
        Assert.Equal(reference.NextUInt64(), used.NextUInt64());
    }

    [Fact]
    public void UnknownIntentThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => DialogueSelector.Select(SampleCatalog(), "missing", new SeededRng(1)));
    }

    [Fact]
    public void MalformedJsonSurfacesAsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => DialogueCatalog.FromJson("{\"pools\":"));
    }
}
