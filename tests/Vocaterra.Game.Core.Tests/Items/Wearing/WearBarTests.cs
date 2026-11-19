using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>The wear bar's fraction and colour (I07): none at no wear, shorter and redder as the item wears, empty and red when worn out.</summary>
public sealed class WearBarTests
{
    private static readonly ItemDefinition Axe = TestItems.Catalogue.Get(TestItems.Axe);

    [Fact]
    public void Given_AnItemWithNoWear_When_ItsBarIsAsked_Then_ThereIsNone() =>
        Assert.Null(WearBar.Of(new ItemStack(TestItems.Axe, 1), Axe));

    [Fact]
    public void Given_AnItemThatCannotWear_When_ItsBarIsAsked_Then_ThereIsNone() =>
        Assert.Null(WearBar.Of(new ItemStack(TestItems.Apple, 5, 3), TestItems.Catalogue.Get(TestItems.Apple)));

    [Fact]
    public void Given_NothingInTheSlot_When_TheBarIsAsked_Then_ThereIsNone() =>
        Assert.Null(WearBar.Of(ItemStack.Empty, Axe));

    [Fact]
    public void Given_AnUnknownItem_When_TheBarIsAsked_Then_ThereIsNone() =>
        Assert.Null(WearBar.Of(new ItemStack((ItemId)999, 1, 5), null));

    [Theory]
    [InlineData(35, 0.9)]
    [InlineData(175, 0.5)]
    [InlineData(263, 0.248571)]
    [InlineData(350, 0.0)]
    public void Given_AnAxeWornSomeOfItsThreeHundredAndFifty_When_ItsBarIsAsked_Then_TheRemainingShareIsTheRestOfItsDurability(int wear, double remaining) =>
        Assert.Equal(remaining, WearBar.Of(new ItemStack(TestItems.Axe, 1, wear), Axe)!.Value.Remaining, 6);

    [Fact]
    public void Given_ALittleWear_When_TheColourIsAsked_Then_ItIsAlmostGreen()
    {
        var bar = WearBar.Of(new ItemStack(TestItems.Axe, 1, 1), Axe)!.Value;

        Assert.True(bar.Green > 200 && bar.Red < 40, $"{bar}");
    }

    [Fact]
    public void Given_HalfTheDurability_When_TheColourIsAsked_Then_ItIsYellow()
    {
        var bar = WearBar.Of(new ItemStack(TestItems.Axe, 1, 175), Axe)!.Value;

        Assert.Equal((255, 255), (bar.Red, bar.Green));
    }

    [Fact]
    public void Given_AWornOutItem_When_TheColourIsAsked_Then_ItIsRed()
    {
        var bar = WearBar.Of(new ItemStack(TestItems.Axe, 1, 350), Axe)!.Value;

        Assert.Equal((255, 0), (bar.Red, bar.Green));
    }

    [Fact]
    public void Given_MoreWear_When_TheBarsAreCompared_Then_RedNeverFallsAndGreenNeverRises()
    {
        var previous = WearBar.Of(new ItemStack(TestItems.Axe, 1, 1), Axe)!.Value;

        for (var wear = 2; wear <= 350; wear++)
        {
            var bar = WearBar.Of(new ItemStack(TestItems.Axe, 1, wear), Axe)!.Value;
            Assert.True(bar.Red >= previous.Red && bar.Green <= previous.Green && bar.Remaining < previous.Remaining, $"wear {wear}");
            previous = bar;
        }
    }

    [Fact]
    public void Given_AWearBeyondTheDurability_When_TheBarIsAsked_Then_ItIsEmptyNotNegative() =>
        Assert.Equal(0.0, WearBar.Of(new ItemStack(TestItems.Axe, 1, 999), Axe)!.Value.Remaining);
}
