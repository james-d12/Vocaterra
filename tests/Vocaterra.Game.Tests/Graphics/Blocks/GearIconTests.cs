using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>The icons of the three materials and the seven pieces of clothing (I08a): each item names its own key, the atlas draws it (no fallback tile), and no two pictures are alike.</summary>
public sealed class GearIconTests
{
    private static readonly string[] Keys = ["cloth", "leather", "iron", "hat", "cap", "shirt", "jacket", "coat", "trousers", "shoes"];

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
    [InlineData("cloth", BlockCell.IconCloth)]
    [InlineData("leather", BlockCell.IconLeather)]
    [InlineData("iron", BlockCell.IconIron)]
    [InlineData("hat", BlockCell.IconHat)]
    [InlineData("cap", BlockCell.IconCap)]
    [InlineData("shirt", BlockCell.IconShirt)]
    [InlineData("jacket", BlockCell.IconJacket)]
    [InlineData("coat", BlockCell.IconCoat)]
    [InlineData("trousers", BlockCell.IconTrousers)]
    [InlineData("shoes", BlockCell.IconShoes)]
    public void Given_AGearItemsIconKey_When_ItIsLooked_Then_TheAtlasHasItsOwnPaintedCell(string key, BlockCell expected)
    {
        Assert.Equal(expected, BlockAtlasLayout.IconForKey(key));
        Assert.NotEqual(BlockCell.IconPlain, BlockAtlasLayout.IconForKey(key));
        Assert.InRange(Painted(expected), 40, 230); // a drawing: neither empty nor a filled square
        Assert.True(BlockArtGear.Draws(expected));
    }

    [Fact]
    public void Given_EveryItemOfTheCatalogue_When_ItsIconIsLooked_Then_ItResolvesAndNoTwoItemsShareACell()
    {
        var cells = TestItems.Catalogue.All.Select(static d => BlockAtlasLayout.IconForKey(d.Icon)).ToList();

        Assert.DoesNotContain(BlockCell.IconPlain, cells);
        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.All(TestItems.Catalogue.All, static d => Assert.Contains(d.Icon, BlockAtlasLayout.IconKeys));
        Assert.All(Keys, key => Assert.Contains(key, TestItems.Catalogue.All.Select(static d => d.Icon)));
    }

    [Fact]
    public void Given_TheTenPictures_When_TheyAreCompared_Then_NoTwoAreAlike()
    {
        var pictures = Keys.Select(static k => Convert.ToHexString(BlockArt.Pixels(BlockAtlasLayout.IconForKey(k)))).Distinct().ToList();

        Assert.Equal(Keys.Length, pictures.Count);
    }

    [Fact]
    public void Given_TheGearCells_When_TheAtlasIsMade_Then_TheyFitTheColourRowsAndStayUiOnly()
    {
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows);
        foreach (var cell in BlockArtGear.Cells)
        {
            Assert.True(BlockAtlasLayout.IsUiOnly((int)cell % BlockAtlasLayout.Columns, (int)cell / BlockAtlasLayout.Columns), cell.ToString()); // never in the world's mip levels
        }
    }

    [Fact]
    public void Given_TheIconRows_When_TheyAreRead_Then_EachIsSixteenByTheAtlasCellSize()
    {
        Assert.All(BlockArtGear.Cells, static cell => Assert.Equal(BlockAtlasLayout.CellSize * BlockAtlasLayout.CellSize * 4, BlockArt.Pixels(cell).Length));
    }
}
