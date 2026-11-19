using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.Tests.UI.Common;

/// <summary>Where the wear bar sits in a slot and how much of it is filled (I07): none at no wear, a track along the bottom, a fill of the share left.</summary>
public sealed class WearBarLayoutTests
{
    private static readonly ItemDefinition Axe = new((ItemId)32, "axe", 1, "axe", ItemHeld.Sprite, ["tool"], Durability: new DurabilitySpec(100, "use"));
    private static readonly PixelBox Cell = new(10, 20, 50, 50);

    private static WearBar? BarOf(int wear) => WearBar.Of(new ItemStack((ItemId)32, 1, wear), Axe);

    [Fact]
    public void Given_NoWear_When_TheLayoutIsAsked_Then_NothingIsDrawn() =>
        Assert.Null(WearBarLayout.Of(Cell, BarOf(0)));

    [Fact]
    public void Given_SomeWear_When_TheLayoutIsAsked_Then_TheTrackLiesInsideTheCellAlongItsBottom()
    {
        var layout = WearBarLayout.Of(Cell, BarOf(10))!.Value;

        Assert.Equal(new PixelBox(13, 64, 44, 3), layout.Track);
        Assert.True(layout.Track.X >= Cell.X && layout.Track.Right <= Cell.Right && layout.Track.Bottom <= Cell.Bottom);
    }

    [Theory]
    [InlineData(10, 40)]
    [InlineData(50, 22)]
    [InlineData(90, 4)]
    [InlineData(99, 1)]
    [InlineData(100, 0)]
    public void Given_SomeWear_When_TheLayoutIsAsked_Then_TheFillIsTheShareLeftOfTheTrack(int wear, int width)
    {
        var layout = WearBarLayout.Of(Cell, BarOf(wear))!.Value;

        Assert.Equal(width, layout.Fill.Width);
        Assert.Equal(layout.Track.X, layout.Fill.X);
        Assert.Equal(layout.Track.Y, layout.Fill.Y);
    }

    [Fact]
    public void Given_ALittleAndALotOfWear_When_TheColoursAreAsked_Then_TheFirstIsGreenerAndTheSecondRedder()
    {
        var little = WearBarLayout.Of(Cell, BarOf(5))!.Value;
        var lot = WearBarLayout.Of(Cell, BarOf(95))!.Value;

        Assert.True(little.Green > little.Red);
        Assert.True(lot.Red > lot.Green);
    }

    [Fact]
    public void Given_ACellTooSmallForABar_When_TheLayoutIsAsked_Then_NothingIsDrawn() =>
        Assert.Null(WearBarLayout.Of(new PixelBox(0, 0, 8, 8), BarOf(10)));
}
