using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>The icons of the four armour pieces, the five accessories, the shield, the lantern and the two bags (I08b): each item names its own key, the atlas draws it (no fallback tile, none borrowed), and no two pictures are alike.</summary>
public sealed class CarriedGearIconTests
{
    private static readonly string[] Keys = ["helmet", "cuirass", "greaves", "boots", "ring", "bracelet", "belt", "necklace", "amulet", "shield", "lantern", "sack", "backpack"];

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
    [InlineData("helmet", BlockCell.IconHelmet)]
    [InlineData("cuirass", BlockCell.IconCuirass)]
    [InlineData("greaves", BlockCell.IconGreaves)]
    [InlineData("boots", BlockCell.IconBoots)]
    [InlineData("ring", BlockCell.IconRing)]
    [InlineData("bracelet", BlockCell.IconBracelet)]
    [InlineData("belt", BlockCell.IconBelt)]
    [InlineData("necklace", BlockCell.IconNecklace)]
    [InlineData("amulet", BlockCell.IconAmulet)]
    [InlineData("shield", BlockCell.IconShield)]
    [InlineData("lantern", BlockCell.IconLantern)]
    [InlineData("sack", BlockCell.IconSack)]
    [InlineData("backpack", BlockCell.IconBackpack)]
    public void Given_AGearItemsIconKey_When_ItIsLooked_Then_TheAtlasHasItsOwnPaintedCell(string key, BlockCell expected)
    {
        Assert.Equal(expected, BlockAtlasLayout.IconForKey(key));
        Assert.InRange(Painted(expected), 40, 230); // a drawing: neither empty nor a filled square
        Assert.True(BlockArtCarried.Draws(expected));
        Assert.False(BlockArtGear.Draws(expected));
    }

    [Fact]
    public void Given_TheCarriedItems_When_TheirIconKeysAreRead_Then_EachNamesItsOwnItemAndNoneIsBorrowed()
    {
        var icons = TestItems.Catalogue.All.Where(static d => Keys.Contains(d.Name)).Select(static d => d.Icon).ToList();

        Assert.Equal(Keys.Order(), icons.Order());
        Assert.Equal(Keys.Length, icons.Distinct().Count());
        Assert.Equal("torch", TestItems.Catalogue.Find("torch")!.Icon); // the torch keeps its own picture
    }

    [Fact]
    public void Given_ThePictures_When_TheyAreComparedWithEachOtherAndTheFirstHalfOfTheGear_Then_NoTwoAreAlike()
    {
        var all = Keys.Concat(["cloth", "leather", "iron", "hat", "cap", "shirt", "jacket", "coat", "trousers", "shoes", "torch"]);

        var pictures = all.Select(static k => Convert.ToHexString(BlockArt.Pixels(BlockAtlasLayout.IconForKey(k)))).Distinct().ToList();

        Assert.Equal(all.Count(), pictures.Count);
    }

    [Fact]
    public void Given_TheCarriedCells_When_TheAtlasIsMade_Then_TheyFitTheColourRowsAndStayUiOnly()
    {
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows);
        foreach (var cell in BlockArtCarried.Cells)
        {
            Assert.True(BlockAtlasLayout.IsUiOnly((int)cell % BlockAtlasLayout.Columns, (int)cell / BlockAtlasLayout.Columns), cell.ToString());
            Assert.Equal(BlockAtlasLayout.CellSize * BlockAtlasLayout.CellSize * 4, BlockArt.Pixels(cell).Length);
        }

        Assert.Equal(Keys.Length, BlockArtCarried.Cells.Count);
    }
}
