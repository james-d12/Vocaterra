using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The wear a stack carries (I07): what keeps it with the item, what keeps two wears apart.</summary>
public sealed class ItemStackWearTests
{
    [Fact]
    public void Given_AStackWithoutWear_When_ItIsMade_Then_ItIsNew()
    {
        Assert.Equal(0, new ItemStack(TestItems.Axe, 1).Wear);
        Assert.Equal(0, ItemStack.Empty.Wear);
    }

    [Fact]
    public void Given_TwoStacksOfOneItemAtOneWear_When_TheyAreCompared_Then_TheyMayMerge() =>
        Assert.True(new ItemStack(TestItems.Axe, 1, 5).CanMergeWith(new ItemStack(TestItems.Axe, 1, 5)));

    [Fact]
    public void Given_TwoStacksOfOneItemAtDifferentWear_When_TheyAreCompared_Then_TheyNeverMerge() =>
        Assert.False(new ItemStack(TestItems.Axe, 1, 5).CanMergeWith(new ItemStack(TestItems.Axe, 1, 6)));

    [Fact]
    public void Given_AnEmptyStack_When_ItIsCompared_Then_ItMergesWithNothing()
    {
        Assert.False(ItemStack.Empty.CanMergeWith(new ItemStack(TestItems.Axe, 1)));
        Assert.False(new ItemStack(TestItems.Axe, 1).CanMergeWith(ItemStack.Empty));
    }

    [Fact]
    public void Given_AWornStack_When_ItsCountChanges_Then_TheWearStays()
    {
        var stack = new ItemStack(TestItems.Apple, 7, 3);

        Assert.Equal(new ItemStack(TestItems.Apple, 4, 3), stack.WithCount(4));
        Assert.Equal(ItemStack.Empty, stack.WithCount(0));
    }

    [Fact]
    public void Given_AWornStack_When_ItsWearIsSet_Then_NegativeWearIsNone() =>
        Assert.Equal(0, new ItemStack(TestItems.Axe, 1, 9).WithWear(-4).Wear);
}
