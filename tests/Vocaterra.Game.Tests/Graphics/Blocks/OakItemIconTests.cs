using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>The icons of the seven oak pieces (K05): each item names its own key, the atlas draws it in a UI-only cell, and no two pictures are alike.</summary>
public sealed class OakItemIconTests
{
    private static readonly string[] Keys = ["oak-log", "oak-stairs", "oak-slab", "oak-fence", "oak-fence-gate", "oak-door", "oak-trapdoor"];

    private static int Painted(BlockCell cell)
    {
        var p = BlockArt.Pixels(cell);
        var n = 0;
        for (var i = 3; i < p.Length; i += 4)
        {
            n += p[i] > 0 ? 1 : 0;
        }

        return n;
    }

    [Theory]
    [InlineData("oak-log", BlockCell.IconOakLog)]
    [InlineData("oak-stairs", BlockCell.IconOakStairs)]
    [InlineData("oak-slab", BlockCell.IconOakSlab)]
    [InlineData("oak-fence", BlockCell.IconOakFence)]
    [InlineData("oak-fence-gate", BlockCell.IconOakFenceGate)]
    [InlineData("oak-door", BlockCell.IconOakDoor)]
    [InlineData("oak-trapdoor", BlockCell.IconOakTrapdoor)]
    public void Given_AnOakItemsIconKey_When_ItIsLooked_Then_TheAtlasHasItsOwnPaintedCell(string key, BlockCell expected)
    {
        Assert.Equal(expected, BlockAtlasLayout.IconForKey(key));
        Assert.Equal(key, TestItems.Catalogue.Find(key)!.Icon);
        Assert.InRange(Painted(expected), 60, 200); // recognisable at hotbar size: neither a speck nor a filled square
        Assert.True(BlockArtBuilding.Draws(expected));
        Assert.True(BlockAtlasLayout.IsUiOnly((int)expected % BlockAtlasLayout.Columns, (int)expected / BlockAtlasLayout.Columns));
        Assert.Equal(BlockAtlasLayout.CellSize * BlockAtlasLayout.CellSize * 4, BlockArt.Pixels(expected).Length);
    }

    [Fact]
    public void Given_TheOakIconsAndTheOtherItemIcons_When_ThePicturesAreCompared_Then_NoTwoAreAlikeAndTheyFitTheAtlas()
    {
        var pictures = TestItems.Catalogue.All.Select(static d => Convert.ToHexString(BlockArt.Pixels(BlockAtlasLayout.IconFor(d)))).ToList();

        Assert.Equal(pictures.Count, pictures.Distinct().Count());
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows);
        Assert.Equal(Keys.Length, BlockArtBuilding.Cells.Count);
    }
}
