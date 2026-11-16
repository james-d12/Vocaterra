using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>The worn bag and the bag rule (I03b): a bag adds its capacity to the base, and cannot come off, or be swapped for a smaller one, while a slot it would drop holds an item; a refusal changes nothing.</summary>
public sealed class BagCapacityRuleTests
{
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");

    private static (Equipment Equipment, Inventory Bag) Fresh(params (int Slot, ItemId Item)[] items)
    {
        var bag = new Inventory(GearItems.Catalogue);
        foreach (var (slot, item) in items)
        {
            bag.SetSlot(slot, new ItemStack(item, 1));
        }

        return (new Equipment(GearItems.Data, GearItems.Catalogue), bag);
    }

    private static string Snapshot(Equipment equipment, Inventory bag) =>
        string.Join(',', equipment.Slots.Select(static s => s.IsEmpty ? "-" : $"{(int)s.Item}")) + "|" + bag.SlotCount + "|" + string.Join(',', bag.Slots.Select(static s => s.IsEmpty ? "-" : $"{(int)s.Item}x{s.Count}"));

    [Fact]
    public void Given_NoBag_When_TheEquipmentIsBuilt_Then_TheBagHasTheBaseOfThirty()
    {
        var (equipment, bag) = Fresh();

        Assert.Equal(0, equipment.Capacity);
        Assert.Equal(30, bag.SlotCount);
    }

    [Theory]
    [InlineData(210, 40)] // the small bag adds 10
    [InlineData(211, 50)] // the large one adds 20
    public void Given_ABagInTheBag_When_ItIsEquipped_Then_TheActiveCountGrowsByItsCapacity(int bagItem, int expected)
    {
        var (equipment, bag) = Fresh((3, (ItemId)bagItem));

        var result = equipment.Equip(bag, 3, Back);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(expected, bag.SlotCount);
        Assert.Equal(expected - 30, equipment.Capacity);
    }

    [Fact]
    public void Given_ABagWithNoCapacity_When_ItIsEquipped_Then_TheBagStaysAtTheBase()
    {
        var (equipment, bag) = Fresh((3, GearItems.Pack));

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 3, Back));
        Assert.Equal(30, bag.SlotCount);
    }

    [Fact]
    public void Given_AWornBagWithItemsOnlyInTheBaseSlots_When_ItIsUnequipped_Then_ItComesOffAndTheBagShrinks()
    {
        var (equipment, bag) = Fresh((3, GearItems.Sack));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(5, new ItemStack(TestItems.Apple, 1));

        var result = equipment.Unequip(bag, Back);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(30, bag.SlotCount);
        Assert.Equal(GearItems.Sack, bag.Slots[0].Item); // the first free slot of the slots that stay
        Assert.True(equipment.IsEmpty);
    }

    [Fact]
    public void Given_AnItemInASlotTheBagAdds_When_TheBagIsUnequipped_Then_ItIsRefusedAsNotEmptyAndNothingChanges()
    {
        var (equipment, bag) = Fresh((3, GearItems.Sack));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(39, new ItemStack(TestItems.Apple, 1));
        var before = Snapshot(equipment, bag);

        var result = equipment.Unequip(bag, Back);

        Assert.Equal(EquipResult.BagNotEmpty, result);
        Assert.Equal(before, Snapshot(equipment, bag));
        Assert.Equal(40, bag.SlotCount);
    }

    [Fact]
    public void Given_ABagWornWithItemsInItsSlots_When_ItIsSwappedForASmallerOne_Then_ItIsRefusedAndNothingChanges()
    {
        var (equipment, bag) = Fresh((3, GearItems.Rucksack), (4, GearItems.Sack));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(45, new ItemStack(TestItems.Apple, 1));
        var before = Snapshot(equipment, bag);

        var result = equipment.Equip(bag, 4, Back);

        Assert.Equal(EquipResult.BagNotEmpty, result);
        Assert.Equal(before, Snapshot(equipment, bag));
    }

    [Fact]
    public void Given_ABagWornWithItemsOnlyInTheSlotsTheSmallerBagKeeps_When_ItIsSwappedForTheSmallerOne_Then_ItIsAllowed()
    {
        var (equipment, bag) = Fresh((3, GearItems.Rucksack), (4, GearItems.Sack));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(35, new ItemStack(TestItems.Apple, 1)); // slot 35 stays in a bag of 40

        var result = equipment.Equip(bag, 4, Back);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(40, bag.SlotCount);
        Assert.Equal(GearItems.Sack, equipment.Get(Back).Item);
        Assert.Equal(GearItems.Rucksack, bag.Slots[4].Item); // the old bag took the freed slot
    }

    [Fact]
    public void Given_TheSmallerBagLiesInASlotTheSwapWouldDrop_When_ItIsSwappedIn_Then_ItIsRefused()
    {
        var (equipment, bag) = Fresh((3, GearItems.Rucksack));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(45, new ItemStack(GearItems.Sack, 1)); // a slot only the large bag gives
        var before = Snapshot(equipment, bag);

        Assert.Equal(EquipResult.BagNotEmpty, equipment.Equip(bag, 45, Back)); // the large bag would go to slot 45, which the small one does not have
        Assert.Equal(before, Snapshot(equipment, bag));
    }

    [Theory]
    [InlineData(210, 211, 50)] // small to large
    [InlineData(211, 211, 50)] // the same size again
    public void Given_AWornBagWithItemsEverywhere_When_ItIsSwappedForOneAtLeastAsLarge_Then_ItIsAlwaysAllowed(int worn, int incoming, int expected)
    {
        var (equipment, bag) = Fresh((3, (ItemId)worn), (4, (ItemId)incoming));
        _ = equipment.Equip(bag, 3, Back);
        for (var i = 5; i < bag.SlotCount; i++)
        {
            bag.SetSlot(i, new ItemStack(TestItems.Apple, 1));
        }

        var result = equipment.Equip(bag, 4, Back);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(expected, bag.SlotCount);
    }

    [Fact]
    public void Given_ABaseThatIsFullAndABagWorn_When_ItIsUnequipped_Then_ItIsRefusedAsBagFullBecauseItsOwnSlotsGoWithIt()
    {
        var (equipment, bag) = Fresh((3, GearItems.Sack));
        _ = equipment.Equip(bag, 3, Back);
        for (var i = 0; i < 30; i++)
        {
            bag.SetSlot(i, new ItemStack(TestItems.Apple, 1)); // the thirty slots that stay are full; the ten the bag adds are empty
        }

        var before = Snapshot(equipment, bag);

        Assert.Equal(EquipResult.BagFull, equipment.Unequip(bag, Back));
        Assert.Equal(before, Snapshot(equipment, bag));
        Assert.Equal(EquipResult.NoSuchBagSlot, equipment.Unequip(bag, Back, 31)); // slot 31 goes with the bag
        Assert.Equal(before, Snapshot(equipment, bag));
    }

    [Fact]
    public void Given_ASwapForASmallerBagIntoAnEmptyDroppedRange_When_ItIsDone_Then_TheOldBagTakesTheSlotTheNewOneLeft()
    {
        var (equipment, bag) = Fresh((0, GearItems.Rucksack), (1, GearItems.Sack));
        _ = equipment.Equip(bag, 0, Back);

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 1, Back));
        Assert.Equal(GearItems.Rucksack, bag.Slots[1].Item);
        Assert.Equal(40, bag.SlotCount);
        Assert.Equal(EquipResult.Done, equipment.Unequip(bag, Back, 0));
        Assert.Equal(30, bag.SlotCount);
        Assert.Equal(GearItems.Sack, bag.Slots[0].Item);
    }

    [Fact]
    public void Given_AnyOtherItem_When_ItIsWornOrTakenOff_Then_TheBagRuleHasNoSayAndTheCountIsKept()
    {
        var (equipment, bag) = Fresh((3, GearItems.Rucksack), (4, GearItems.Shirt));
        _ = equipment.Equip(bag, 3, Back);
        bag.SetSlot(49, new ItemStack(TestItems.Apple, 1));
        var body = GearItems.Slot("clothing", "body");

        Assert.Equal(EquipResult.Done, equipment.Equip(bag, 4, body));
        Assert.Equal(EquipResult.Done, equipment.Unequip(bag, body));
        Assert.Equal(50, bag.SlotCount);
    }

    [Fact]
    public void Given_TheStandardRules_When_TheyAreListed_Then_TheBagRuleIsAmongThem()
    {
        Assert.Contains(EquipRules.Standard(), static rule => rule is BagCapacityRule);
        Assert.Equal(12, (int)EquipResult.BagNotEmpty);
    }

    [Fact]
    public void Given_WornStacksWithABag_When_TheActiveCountIsAskedForThem_Then_ItIsTheBasePlusTheCapacityNeverAboveTheMaximum()
    {
        var (equipment, bag) = Fresh();
        var worn = new ItemStack[equipment.Data.SlotCount];
        Array.Fill(worn, ItemStack.Empty);
        worn[equipment.Data.FlatIndex(Back)] = new ItemStack(GearItems.Rucksack, 1);

        Assert.Equal(50, equipment.ActiveSlotsFor(bag, worn));
        Assert.Equal(30, equipment.ActiveSlotsFor(bag, null));
        Assert.Equal(20, equipment.CapacityOf(worn));
    }
}
