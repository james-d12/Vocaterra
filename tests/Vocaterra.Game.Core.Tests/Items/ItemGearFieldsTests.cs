using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The gear fields of an item row (I03a): <c>wear</c>, <c>stats</c> and <c>look</c> are optional, read in a fixed order, and refused when malformed.</summary>
public sealed class ItemGearFieldsTests
{
    private static ItemCatalogue Parse(string row)
    {
        // The shipped rows with one more of the test's own: the catalogue needs every row the code names.
        var shipped = File.ReadAllText(Repo.Path("content", "game-v1", "game", "items.json"));
        var at = shipped.LastIndexOf(']');
        return ItemCatalogue.Parse(shipped[..at].TrimEnd() + "," + row + "\n  ]\n}");
    }

    [Fact]
    public void Given_TheShippedItems_When_TheyAreRead_Then_NoneHasGearFieldsYet()
    {
        Assert.All(TestItems.Catalogue.All, static d =>
        {
            Assert.Null(d.Wear);
            Assert.Null(d.Stats);
            Assert.Null(d.Look);
        });
    }

    [Fact]
    public void Given_ARowWithAllThreeFields_When_ItIsRead_Then_TheyAreParsedAndSortedByKey()
    {
        var catalogue = Parse("""{ "name": "test-coat", "id": 150, "stack": 1, "icon": "wood", "tags": ["clothing"], "wear": "body", "stats": { "warmth": 40, "armour": 1.5 }, "look": { "sleeve": "#ff8000", "body": "#abc" } }""");

        var coat = catalogue.Find("test-coat")!;

        Assert.Equal("body", coat.Wear);
        Assert.Equal([("armour", 1.5), ("warmth", 40d)], coat.Stats!.Select(static p => (p.Key, p.Value)));
        Assert.Equal(["body", "sleeve"], coat.Look!.Parts.Keys);
        Assert.Equal(new LookColour(0xaa, 0xbb, 0xcc), coat.Look.Parts["body"]);
        Assert.Equal("#ff8000", coat.Look.Parts["sleeve"].ToString());
    }

    [Theory]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "wear": "Body" }""", "wear")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "stats": { "Warm": 1 } }""", "stat key")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "stats": { "warm-th": 1 } }""", "stat key")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "stats": { "warmth": "hot" } }""", "not a number")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "stats": { "warmth": 1e9 } }""", "must be a number from")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "look": { "body": "red" } }""", "not a colour")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "look": { "body": "#12345" } }""", "not a colour")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "look": { "body": 12 } }""", "not a colour")]
    [InlineData("""{ "name": "test-x", "id": 150, "stack": 1, "icon": "wood", "look": { "Body": "#123456" } }""", "part")]
    public void Given_AMalformedGearField_When_TheRowIsRead_Then_TheLoadFailsNamingTheRow(string row, string fragment)
    {
        var error = Assert.Throws<InvalidDataException>(() => Parse(row));

        Assert.Contains("test-x", error.Message, StringComparison.Ordinal);
        Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#000", 0, 0, 0)]
    [InlineData("#FFF", 255, 255, 255)]
    [InlineData("#1a2B3c", 0x1a, 0x2b, 0x3c)]
    public void Given_AColourText_When_ItIsParsed_Then_ItIsThreeBytes(string text, int r, int g, int b)
    {
        Assert.True(LookColour.TryParse(text, out var colour));
        Assert.Equal(new LookColour((byte)r, (byte)g, (byte)b), colour);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("#12345678")]
    [InlineData("#+12345")]
    public void Given_TextThatIsNotAColour_When_ItIsParsed_Then_ItIsRefused(string? text) => Assert.False(LookColour.TryParse(text, out _));
}
