using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>The equip and unequip rules of <see cref="Equipment"/> (I03a): what the slot accepts, the swap, the first free slot, and that a refusal changes nothing.</summary>
public sealed class EquipmentTests
{
    private static Equipment Fresh() => new(GearItems.Data, GearItems.Catalogue);

    private static Inventory Bag(params (int Slot, ItemId Item)[] items)
    {
        var bag = new Inventory(GearItems.Catalogue);
        foreach (var (slot, item) in items)
        {
            bag.SetSlot(slot, new ItemStack(item, 1));
        }

        return bag;
    }

    private static string Snapshot(Equipment equipment, Inventory bag) => string.Join(',', equipment.Slots.Select(static s => s.IsEmpty ? "-" : $"{(int)s.Item}")) + "|" + string.Join(',', bag.Slots.Select(static s => s.IsEmpty ? "-" : $"{(int)s.Item}x{s.Count}"));

    private static void AssertRefused(EquipResult expected, EquipResult actual, Equipment equipment, Inventory bag, string before)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(before, Snapshot(equipment, bag));
    }

    [Fact]
    public void Given_AShirtInTheBag_When_ItIsEquippedToTheClothingBodySlot_Then_ItIsWornAndTheBagSlotIsEmpty()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt));

        var result = equipment.Equip(bag, 5, GearItems.Slot("clothing", "body"));

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(GearItems.Shirt, equipment.Get(GearItems.Slot("clothing", "body")).Item);
        Assert.True(bag.Slots[5].IsEmpty);
    }

    [Fact]
    public void Given_AHatForTheHead_When_ItIsPutInTheLegsSlot_Then_ItIsRefusedAsTheWrongSlotAndNothingChanges()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Hat));
        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.WrongSlot, equipment.Equip(bag, 5, GearItems.Slot("clothing", "legs")), equipment, bag, before);
    }

    [Fact]
    public void Given_ArmourForTheBody_When_ItIsPutInTheClothingGroup_Then_ItIsRefusedAsTheWrongKind()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Plate));
        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.WrongKind, equipment.Equip(bag, 5, GearItems.Slot("clothing", "body")), equipment, bag, before);
        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 5, GearItems.Slot("armour", "body")));
    }

    [Fact]
    public void Given_AnItemThatCannotBeWorn_When_ItIsEquipped_Then_ItIsRefusedAsNotWearable()
    {
        var equipment = Fresh();
        var bag = Bag((5, TestItems.Apple));
        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.NotWearable, equipment.Equip(bag, 5, GearItems.Slot("clothing", "body")), equipment, bag, before);
    }

    [Fact]
    public void Given_AnEmptyBagSlotOrOneThatDoesNotExistOrASlotOutsideTheData_When_AnEquipIsAsked_Then_EachHasItsReason()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt));
        var before = Snapshot(equipment, bag);
        var body = GearItems.Slot("clothing", "body");

        AssertRefused(EquipResult.NothingToEquip, equipment.Equip(bag, 6, body), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchBagSlot, equipment.Equip(bag, Inventory.SlotCount, body), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchBagSlot, equipment.Equip(bag, -1, body), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchSlot, equipment.Equip(bag, 5, new EquipSlot(99, 0)), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchSlot, equipment.Equip(bag, 5, new EquipSlot(0, 9)), equipment, bag, before);
    }

    [Fact]
    public void Given_AStackOfMoreThanOneOfAWearableItem_When_ItIsEquipped_Then_ItIsRefusedAndTheStackStays()
    {
        var shirts = TestItems.With(new ItemDefinition(GearItems.Shirt, "test-shirt", 5, "test-shirt", ItemHeld.Sprite, ["clothing"], Wear: "body"));
        var equipment = new Equipment(GearItems.Data, shirts);
        var bag = new Inventory(shirts);
        bag.SetSlot(5, new ItemStack(GearItems.Shirt, 3));

        Assert.Equal(EquipResult.StackTooLarge, equipment.Equip(bag, 5, GearItems.Slot("clothing", "body")));
        Assert.Equal(3, bag.Slots[5].Count);
        Assert.True(equipment.IsEmpty);
    }

    [Fact]
    public void Given_ASlotThatIsWorn_When_AnotherItemIsEquippedThere_Then_TheyAreSwappedAndTheOldOneTakesTheBagSlot()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt), (9, GearItems.Scarf));
        var body = GearItems.Slot("clothing", "body");
        _ = equipment.Equip(bag, 5, body);
        Assert.Equal(GearItems.Shirt, equipment.Get(body).Item);

        var result = equipment.Equip(bag, 9, body);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(GearItems.Scarf, equipment.Get(body).Item);
        Assert.Equal(GearItems.Shirt, bag.Slots[9].Item);
        Assert.True(bag.Slots[5].IsEmpty);
    }

    [Fact]
    public void Given_AnItemThatBothGroupsTake_When_ItIsPutInTheCosmeticLayer_Then_ItIsWornThereToo()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Scarf), (6, GearItems.Shirt));

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 5, GearItems.Slot("cosmetic", "body")));
        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 6, GearItems.Slot("clothing", "body")));
        Assert.Equal(2, equipment.Worn().Count());
    }

    [Fact]
    public void Given_ARingWornInOneAccessorySlot_When_ARingIsEquippedInAnother_Then_ItIsADuplicateAndNothingChanges()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Ring), (6, GearItems.Ring), (7, GearItems.Charm));
        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 5, GearItems.Nth("accessories", 0)));
        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.Duplicate, equipment.Equip(bag, 6, GearItems.Nth("accessories", 1)), equipment, bag, before);
        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 7, GearItems.Nth("accessories", 1)));
    }

    [Fact]
    public void Given_ARingWornInAnAccessorySlot_When_ItIsReplacedInTheSameSlot_Then_ItIsNotADuplicate()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Ring), (6, GearItems.Ring));
        _ = equipment.Equip(bag, 5, GearItems.Nth("accessories", 0));

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 6, GearItems.Nth("accessories", 0)));
        Assert.Equal(GearItems.Ring, bag.Slots[6].Item);
    }

    [Fact]
    public void Given_APackAndAShield_When_TheyAreEquipped_Then_TheyGoToTheBackpackAndTheOffHand()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Pack), (6, GearItems.Shield));

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 5, GearItems.Nth("backpack", 0)));
        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 6, GearItems.Nth("offhand", 0)));
        Assert.Equal(EquipResult.WrongSlot, Fresh().CanWear(Bag(), GearItems.Catalogue.Get(GearItems.Shield), GearItems.Nth("backpack", 0)));
    }

    [Fact]
    public void Given_AWornShirt_When_ItIsUnequippedToAChosenSlot_Then_ItIsThereAndTheSlotIsEmpty()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt));
        var body = GearItems.Slot("clothing", "body");
        _ = equipment.Equip(bag, 5, body);

        var result = equipment.Unequip(bag, body, 20);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(GearItems.Shirt, bag.Slots[20].Item);
        Assert.True(equipment.Get(body).IsEmpty);
    }

    [Fact]
    public void Given_AWornShirt_When_ItIsUnequippedWithNoSlotNamed_Then_ItTakesTheFirstFreeSlot()
    {
        var equipment = Fresh();
        var bag = Bag((0, TestItems.Apple), (1, TestItems.Apple), (5, GearItems.Shirt));
        var body = GearItems.Slot("clothing", "body");
        _ = equipment.Equip(bag, 5, body);

        Assert.Equal(EquipResult.Done, equipment.Unequip(bag, body));

        Assert.Equal(GearItems.Shirt, bag.Slots[2].Item);
    }

    [Fact]
    public void Given_AFullBag_When_AnItemIsUnequippedToTheFirstFreeSlot_Then_ItIsRefusedAndStaysWorn()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt));
        var body = GearItems.Slot("clothing", "body");
        _ = equipment.Equip(bag, 5, body);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            bag.SetSlot(i, new ItemStack(TestItems.Apple, 1));
        }

        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.BagFull, equipment.Unequip(bag, body), equipment, bag, before);
    }

    [Fact]
    public void Given_AWornShirt_When_ItIsUnequippedToATakenOrMissingSlotOrFromAnEmptySlot_Then_EachHasItsReasonAndNothingChanges()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt), (6, TestItems.Apple));
        var body = GearItems.Slot("clothing", "body");
        _ = equipment.Equip(bag, 5, body);
        var before = Snapshot(equipment, bag);

        AssertRefused(EquipResult.BagSlotOccupied, equipment.Unequip(bag, body, 6), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchBagSlot, equipment.Unequip(bag, body, Inventory.SlotCount), equipment, bag, before);
        AssertRefused(EquipResult.NothingWorn, equipment.Unequip(bag, GearItems.Slot("clothing", "head")), equipment, bag, before);
        AssertRefused(EquipResult.NoSuchSlot, equipment.Unequip(bag, new EquipSlot(-1, 0)), equipment, bag, before);
    }

    [Fact]
    public void Given_AnEmptyAndAWornSlotThatBothTakeAnItem_When_TheFirstSlotIsFound_Then_TheEmptyOneComesFirst()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt), (6, GearItems.Scarf));
        var scarf = GearItems.Catalogue.Get(GearItems.Scarf);
        Assert.Equal(GearItems.Slot("clothing", "body"), equipment.FindSlotFor(bag, scarf));
        _ = equipment.Equip(bag, 5, GearItems.Slot("clothing", "body"));

        Assert.Equal(GearItems.Slot("armour", "body"), equipment.FindSlotFor(bag, scarf));
        Assert.Null(equipment.FindSlotFor(bag, GearItems.Catalogue.Get(TestItems.Apple)));
    }

    [Fact]
    public void Given_ARuleAddedToTheList_When_AnItemIsEquipped_Then_TheRuleCanRefuseItWithNoEditOfTheEquipment()
    {
        var equipment = new Equipment(GearItems.Data, GearItems.Catalogue, [.. EquipRules.Standard(), new RefuseAllRule()]);
        var bag = Bag((5, GearItems.Shirt));

        Assert.Equal(EquipResult.WrongKind, equipment.Equip(bag, 5, GearItems.Slot("clothing", "body")));
        Assert.True(equipment.IsEmpty);
    }

    [Fact]
    public void Given_WornItems_When_TheyAreCapturedAndRestored_Then_AnotherEquipmentHoldsTheSame()
    {
        var equipment = Fresh();
        var bag = Bag((5, GearItems.Shirt), (6, GearItems.Ring));
        _ = equipment.Equip(bag, 5, GearItems.Slot("clothing", "body"));
        _ = equipment.Equip(bag, 6, GearItems.Nth("accessories", 2));
        var other = Fresh();

        other.Restore(equipment.Capture());

        Assert.Equal(equipment.Slots, other.Slots);
        other.Restore(null);
        Assert.True(other.IsEmpty);
        _ = Assert.Throws<ArgumentException>(() => other.Restore([ItemStack.Empty]));
    }

    private sealed class RefuseAllRule : IEquipRule
    {
        public EquipResult Check(in EquipChange change, Equipment equipment, Inventory bag) => EquipResult.WrongKind;
    }
}
