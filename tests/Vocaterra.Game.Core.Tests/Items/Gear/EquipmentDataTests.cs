using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>The shipped slot groups and the reading of <c>game/equipment.json</c> (I03a).</summary>
public sealed class EquipmentDataTests
{
    private static readonly EquipmentData Shipped = TestGame.Data.Equipment;

    [Fact]
    public void Given_TheShippedFile_When_ItIsRead_Then_ItHasTheSixGroupsOfThePlan()
    {
        Assert.Equal(["clothing", "armour", "cosmetic", "accessories", "offhand", "backpack"], Shipped.Groups.Select(static g => g.Id));
        Assert.Equal(4 + 4 + 4 + 3 + 1 + 1, Shipped.SlotCount);
    }

    [Fact]
    public void Given_TheShippedGroups_When_TheirFlagsAreRead_Then_TheyAreTheOnesTheTableSets()
    {
        EquipmentGroup Group(string id) => Shipped.Groups[Shipped.IndexOf(id)];

        Assert.Equal([1, 2, 3], new[] { "clothing", "armour", "cosmetic" }.Select(id => Group(id).Layer));
        Assert.Null(Group("accessories").Layer);
        Assert.False(Group("cosmetic").StatsApply);
        Assert.True(Group("clothing").StatsApply);
        Assert.True(Group("accessories").NoDuplicates);
        Assert.False(Group("clothing").NoDuplicates);
        Assert.Equal(["clothing", "armour"], Group("cosmetic").Accepts);
        Assert.Equal(["head", "body", "legs", "feet"], Group("armour").Slots.Select(static s => s.Name));
        Assert.Equal(["accessory1", "accessory2", "accessory3"], Group("accessories").Slots.Select(static s => s.Name));
        Assert.All(Group("accessories").Slots, static s => Assert.Equal("accessory", s.Wear));
        Assert.Equal("back", Group("backpack").Slots.Single().Wear);
        Assert.Equal("back", Group("backpack").Slots.Single().Name);
    }

    [Fact]
    public void Given_TheBodySlots_When_TheirLabelsAreRead_Then_TheyAreTheLexiconsNouns()
    {
        var clothing = Shipped.Groups[Shipped.IndexOf("clothing")];

        Assert.Equal(["cabeza", "cuerpo", "pierna", "pie"], clothing.Slots.Select(static s => s.Label));
        Assert.Null(Shipped.Groups[Shipped.IndexOf("accessories")].Slots[0].Label); // the lexicon has no word for it yet: I04
    }

    [Fact]
    public void Given_EverySlot_When_ItsFlatIndexIsTakenAndMappedBack_Then_ItIsTheSameSlot()
    {
        var all = Shipped.AllSlots().ToList();

        Assert.Equal(Shipped.SlotCount, all.Count);
        for (var i = 0; i < all.Count; i++)
        {
            Assert.Equal(i, Shipped.FlatIndex(all[i]));
            Assert.Equal(all[i], Shipped.SlotAt(i));
        }

        Assert.False(Shipped.Contains(new EquipSlot(0, 4)));
        Assert.False(Shipped.Contains(new EquipSlot(-1, 0)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Shipped.SlotAt(Shipped.SlotCount));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Shipped.Spec(new EquipSlot(9, 0)));
    }

    [Fact]
    public void Given_ASeventhGroupInTheData_When_ItIsRead_Then_ItIsJustAnotherGroupAndNothingNamesIt()
    {
        var data = EquipmentData.Parse(
            """
            { "schemaVersion": 1, "groups": [
              { "id": "clothing", "layer": 1, "slots": ["head"], "accepts": ["clothing"], "noDuplicates": false, "statsApply": true },
              { "id": "rings", "count": 2, "wear": "finger", "label": "dedo", "accepts": ["ring"], "noDuplicates": true, "statsApply": true } ] }
            """);

        Assert.Equal(3, data.SlotCount);
        Assert.Equal(new EquipSlot(1, 1), data.SlotAt(2));
        Assert.Equal("finger2", data.Spec(new EquipSlot(1, 1)).Name);
        Assert.Equal("dedo", data.Spec(new EquipSlot(1, 0)).Label);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "groups": [] }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "id": "a", "accepts": [], "noDuplicates": false, "statsApply": true } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "id": "a", "slots": ["x"], "count": 2, "wear": "x", "accepts": [], "noDuplicates": false, "statsApply": true } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "groups": [ { "id": "a", "slots": ["x"], "labels": { "y": "mano" }, "accepts": [], "noDuplicates": false, "statsApply": true } ] }""")]
    [InlineData("not json")]
    public void Given_AMalformedFile_When_ItIsRead_Then_TheLoadFailsNamingTheFile(string json)
    {
        var error = Assert.Throws<InvalidDataException>(() => EquipmentData.Parse(json));

        Assert.Contains("game/equipment.json", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheShippedData_When_ItsBagSlotsAreRead_Then_TheBaseIsThirty()
    {
        Assert.Equal(30, Shipped.BagSlots);
    }

    [Fact]
    public void Given_AFileWithBagSlotsOrWithout_When_ItIsRead_Then_TheValueOrTheDefaultIsTaken()
    {
        var with = EquipmentData.Parse("""{ "schemaVersion": 1, "bagSlots": 20, "groups": [ { "id": "a", "slots": ["x"], "accepts": ["a"], "noDuplicates": false, "statsApply": true } ] }""");
        var without = EquipmentData.Parse("""{ "schemaVersion": 1, "groups": [ { "id": "a", "slots": ["x"], "accepts": ["a"], "noDuplicates": false, "statsApply": true } ] }""");

        Assert.Equal(20, with.BagSlots);
        Assert.Equal(Inventory.DefaultBaseSlots, without.BagSlots);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(101)]
    public void Given_ABaseBelowTheHotbarOrAbsurdlyLarge_When_TheDataIsValidated_Then_ItIsReported(int bagSlots)
    {
        var data = new EquipmentData(Shipped.Groups, bagSlots);

        var problems = EquipmentValidator.Validate(data, GearItems.Catalogue);

        Assert.Contains(problems, static p => p.Contains("bagSlots", StringComparison.Ordinal));
        Assert.Empty(EquipmentValidator.Validate(Shipped, GearItems.Catalogue));
    }
}
