using NJsonSchema;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>What the content check and the schemas say about equipment (I03a): the groups, and every worn item against them.</summary>
public sealed class EquipmentValidatorTests
{
    private static string Slots(string wear = "head") => $$"""{ "id": "{{wear}}-group", "slots": ["{{wear}}"], "accepts": ["clothing"], "noDuplicates": false, "statsApply": true }""";

    private static EquipmentData Data(params string[] groups) => EquipmentData.Parse($$"""{ "schemaVersion": 1, "groups": [ {{string.Join(",", groups)}} ] }""");

    private static IReadOnlyList<string> Problems(EquipmentData data, IItemCatalogue? items = null) => EquipmentValidator.Validate(data, items ?? TestItems.Catalogue);

    [Fact]
    public void Given_TheShippedContent_When_ItIsChecked_Then_NoEquipmentProblemIsReported()
    {
        Assert.Empty(EquipmentValidator.Validate(TestGame.Data.Equipment, TestGame.Data.Items));
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void Given_TheTestGear_When_ItIsCheckedAgainstTheShippedGroups_Then_NoProblemIsReported() =>
        Assert.Empty(EquipmentValidator.Validate(TestGame.Data.Equipment, GearItems.Catalogue));

    [Fact]
    public void Given_TwoGroupsWithOneId_When_TheyAreChecked_Then_TheDuplicateIsReported()
    {
        var problems = Problems(Data(Slots(), Slots()));

        Assert.Contains("game/equipment.json: the group id 'head-group' is used twice.", problems);
    }

    [Fact]
    public void Given_AGroupThatIsMalformed_When_ItIsChecked_Then_EachFaultIsReported()
    {
        var data = Data(
            """{ "id": "Bad Id", "layer": 12, "slots": ["head", "head"], "labels": { "head": "Cabeza" }, "accepts": ["Not A Tag"], "noDuplicates": false, "statsApply": true }""",
            """{ "id": "empty", "count": 0, "wear": "x", "accepts": [], "noDuplicates": false, "statsApply": true }""");

        var problems = Problems(data);

        Assert.Contains(problems, static p => p.Contains("'Bad Id'", StringComparison.Ordinal) && p.Contains("not a lowercase key", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("the layer 12", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("the slot 'head'", StringComparison.Ordinal) && p.Contains("named twice", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("the label 'Cabeza'", StringComparison.Ordinal));
        Assert.Contains(problems, static p => p.Contains("the tag 'Not A Tag'", StringComparison.Ordinal));
        Assert.Contains("game/equipment.json: the group 'empty' has no slots.", problems);
        Assert.Contains("game/equipment.json: the group 'empty' accepts no tag, so nothing can be worn in it.", problems);
    }

    [Fact]
    public void Given_AWornItemWhoseWearKindNoGroupHas_When_ItIsChecked_Then_TheBadSlotIsReported()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)240, "test-wings", 1, "test-wings", ItemHeld.Sprite, ["clothing"], Wear: "wings"));

        var problems = Problems(TestGame.Data.Equipment, items);

        Assert.Contains("game/items.json: 'test-wings' is worn on 'wings', and no slot of game/equipment.json is for that.", problems);
    }

    [Fact]
    public void Given_AWornItemWithATagNoGroupAccepts_When_ItIsChecked_Then_TheUnknownTagIsReported()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)241, "test-hat", 1, "test-hat", ItemHeld.Sprite, ["decoration"], Wear: "head"));

        var problems = Problems(TestGame.Data.Equipment, items);

        Assert.Single(problems);
        Assert.Contains("'test-hat' is worn on 'head' but carries none of the tags", problems[0], StringComparison.Ordinal);
        Assert.Contains("clothing", problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AWornItemThatStacks_When_ItIsChecked_Then_TheStackLimitIsReported()
    {
        var items = TestItems.With(new ItemDefinition((ItemId)242, "test-sock", 20, "test-sock", ItemHeld.Sprite, ["clothing"], Wear: "feet"));

        Assert.Contains("game/items.json: 'test-sock' can be worn, so its stack limit must be 1 (it is 20).", Problems(TestGame.Data.Equipment, items));
    }

    [Fact]
    public void Given_StatsOrALookOnAnItemThatCannotBeWorn_When_ItIsChecked_Then_TheItemIsReported()
    {
        var stats = new Dictionary<string, double> { ["warmth"] = 5 };
        var items = TestItems.With(new ItemDefinition((ItemId)243, "test-rock", 99, "test-rock", ItemHeld.Sprite, ["material"], Stats: stats));

        Assert.Contains("game/items.json: 'test-rock' has stats or a look but no wear, so it can never be worn.", Problems(TestGame.Data.Equipment, items));
    }

    [Fact]
    public void Given_ABundleWhoseGroupsLackTheShirtsKind_When_TheWholeContentIsChecked_Then_TheGameDataValidatorRunsTheCheck()
    {
        var data = TestGame.Data.WithItems(GearItems.Catalogue).WithEquipment(Data(Slots("head")));

        var problems = GameDataValidator.Validate(data);

        Assert.Contains(problems, static p => p.Contains("'test-shirt' is worn on 'body'", StringComparison.Ordinal));
    }

    private static JsonSchema Schema(string file) => JsonSchema.FromFileAsync(Repo.Path("content", "schema", file)).GetAwaiter().GetResult();

    private static int SchemaErrors(string file, string json) => Schema(file).Validate(json).Count;

    [Fact]
    public void Given_TheShippedFile_When_ItIsCheckedAgainstTheSchema_Then_ItPasses() =>
        Assert.Equal(0, SchemaErrors("equipment.schema.json", File.ReadAllText(Repo.Path("content", "game-v1", "game", "equipment.json"))));

    [Theory]
    [InlineData("""{ "id": "a", "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // neither slots nor count
    [InlineData("""{ "id": "a", "slots": ["head"], "count": 2, "wear": "x", "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // both
    [InlineData("""{ "id": "a", "count": 2, "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // count without wear
    [InlineData("""{ "id": "A", "slots": ["head"], "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // id not a key
    [InlineData("""{ "id": "a", "slots": ["Head"], "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // bad slot name
    [InlineData("""{ "id": "a", "slots": ["head", "head"], "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // slot twice
    [InlineData("""{ "id": "a", "slots": ["head"], "accepts": [], "noDuplicates": false, "statsApply": true }""")] // accepts nothing
    [InlineData("""{ "id": "a", "slots": ["head"], "accepts": ["X"], "noDuplicates": false, "statsApply": true }""")] // tag not a key
    [InlineData("""{ "id": "a", "slots": ["head"], "labels": { "head": "Cabeza" }, "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // label not a noun
    [InlineData("""{ "id": "a", "slots": ["head"], "layer": 0, "accepts": ["x"], "noDuplicates": false, "statsApply": true }""")] // layer outside 1 to 9
    [InlineData("""{ "id": "a", "slots": ["head"], "accepts": ["x"], "statsApply": true }""")] // flag missing
    [InlineData("""{ "id": "a", "slots": ["head"], "accepts": ["x"], "noDuplicates": false, "statsApply": true, "colour": "red" }""")] // unknown field
    public void Given_AGroupThatIsMalformed_When_ItIsCheckedAgainstTheSchema_Then_ItIsRefused(string group) =>
        Assert.NotEqual(0, SchemaErrors("equipment.schema.json", $$"""{ "schemaVersion": 1, "groups": [ {{group}} ] }"""));

    [Theory]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "wear": "head", "stats": { "warmth": 5 }, "look": { "head": "#ff0000" } }""", true)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a" }""", true)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "wear": "Head" }""", false)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "stats": { "Warmth": 5 } }""", false)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "stats": { "warmth": "five" } }""", false)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "stats": { "warmth": 100001 } }""", false)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "look": { "head": "red" } }""", false)]
    [InlineData("""{ "name": "a", "id": 1, "stack": 1, "icon": "a", "look": { "Head": "#fff" } }""", false)]
    public void Given_AnItemRowWithGearFields_When_ItIsCheckedAgainstTheItemsSchema_Then_OnlyWellFormedOnesPass(string row, bool passes) =>
        Assert.Equal(passes, SchemaErrors("items.schema.json", $$"""{ "schemaVersion": 1, "items": [ {{row}} ] }""") == 0);
}
