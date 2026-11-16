using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items;

/// <summary>The inventory's active slot count (I03b): the base with no bag, the array sized to the largest bag, and every operation seeing only the active slots.</summary>
public sealed class InventoryActiveSlotsTests
{
    private static Inventory Fresh(int baseSlots = Inventory.DefaultBaseSlots) => new(GearItems.Catalogue, baseSlots);

    [Fact]
    public void Given_NoBagWorn_When_TheInventoryIsBuilt_Then_ItHasTheBaseOfThirtyAndRoomForTheLargestBag()
    {
        var inventory = Fresh();

        Assert.Equal(30, inventory.BaseSlots);
        Assert.Equal(30, inventory.SlotCount);
        Assert.Equal(30 + GearItems.RucksackCapacity, inventory.MaxSlots);
        Assert.Equal(30, inventory.Slots.Count);
    }

    [Fact]
    public void Given_ABaseFromTheData_When_TheInventoryIsBuilt_Then_ItIsTheBaseAndNoNumberIsFixedInTheCode()
    {
        var inventory = Fresh(baseSlots: 20);

        Assert.Equal(20, inventory.SlotCount);
        Assert.Equal(40, inventory.MaxSlots);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Fresh(baseSlots: Inventory.HotbarSize - 1));
    }

    [Theory]
    [InlineData(GearItems.SackCapacity)]
    [InlineData(GearItems.RucksackCapacity)]
    public void Given_ABagOfACapacity_When_TheActiveCountGrows_Then_TheSlotsAreThereAndEmpty(int capacity)
    {
        var inventory = Fresh();

        inventory.SetActiveCount(30 + capacity);

        Assert.Equal(30 + capacity, inventory.Slots.Count);
        Assert.All(inventory.Slots, static slot => Assert.True(slot.IsEmpty));
        inventory.SetSlot(29 + capacity, new ItemStack(ItemId.Wood, 1));
        Assert.Equal(new ItemStack(ItemId.Wood, 1), inventory.Slots[29 + capacity]);
    }

    [Fact]
    public void Given_ASlotPastTheActiveCount_When_ItIsSet_Then_ItIsOutOfRange()
    {
        var inventory = Fresh();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetSlot(30, new ItemStack(ItemId.Wood, 1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => inventory.RemoveFromSlot(30, 1));
    }

    [Fact]
    public void Given_AFullBaseInventory_When_ItemsAreAdded_Then_OnlyActiveSlotsFillAndTheRestIsReportedAsNotFitting()
    {
        var inventory = Fresh();

        Assert.Equal(0, inventory.Add(ItemId.Berry, 99 * 30));
        Assert.False(inventory.CanAdd(ItemId.Berry, 1));
        Assert.Equal(7, inventory.Add(ItemId.Berry, 7));
        Assert.Equal(99 * 30, inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void Given_ABiggerActiveCount_When_ItemsAreAdded_Then_TheNewSlotsTakeThemAndReportFullAtTheNewCount()
    {
        var inventory = Fresh();
        inventory.SetActiveCount(30 + GearItems.RucksackCapacity);

        Assert.Equal(0, inventory.Add(ItemId.Berry, 99 * 50));
        Assert.Equal(1, inventory.Add(ItemId.Berry, 1));
        Assert.Equal(new ItemStack(ItemId.Berry, 99), inventory.Slots[49]);
    }

    [Fact]
    public void Given_AnItemInASlotThatWouldBeDropped_When_TheActiveCountShrinks_Then_ItIsRefusedAndNothingChanges()
    {
        var inventory = Fresh();
        inventory.SetActiveCount(40);
        inventory.SetSlot(35, new ItemStack(ItemId.Wood, 1));

        _ = Assert.Throws<InvalidOperationException>(() => inventory.SetActiveCount(30));

        Assert.Equal(40, inventory.SlotCount);
        Assert.Equal(new ItemStack(ItemId.Wood, 1), inventory.Slots[35]);
        Assert.False(inventory.IsEmptyFrom(30));
        Assert.True(inventory.IsEmptyFrom(36));
        inventory.SetSlot(35, ItemStack.Empty);
        inventory.SetActiveCount(30);
        Assert.Equal(30, inventory.SlotCount);
    }

    [Fact]
    public void Given_ACountOutsideTheBaseAndTheMaximum_When_ItIsSet_Then_ItIsOutOfRange()
    {
        var inventory = Fresh();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetActiveCount(29));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetActiveCount(inventory.MaxSlots + 1));
    }

    [Fact]
    public void Given_AnExchange_When_TheRoomIsOnlyInASlotTheBagDoesNotHave_Then_ItIsRefused()
    {
        var inventory = Fresh();
        Assert.Equal(0, inventory.Add(ItemId.Berry, 99 * 29));
        Assert.Equal(0, inventory.Add(ItemId.Coin, 5));

        Assert.False(inventory.CanExchange(ItemId.Coin, 0, ItemId.Wood, 1));
        Assert.True(inventory.CanExchange(ItemId.Coin, 5, ItemId.Wood, 1));
    }

    [Fact]
    public void Given_AnInventoryWithABigBag_When_ItIsCopied_Then_TheCopyHasTheSameCountAndIsIndependent()
    {
        var inventory = Fresh();
        inventory.SetActiveCount(40);
        inventory.SetSlot(39, new ItemStack(ItemId.Wood, 4));

        var copy = inventory.Copy();
        copy.SetSlot(39, ItemStack.Empty);

        Assert.Equal(40, copy.SlotCount);
        Assert.Equal(new ItemStack(ItemId.Wood, 4), inventory.Slots[39]);
    }
}
