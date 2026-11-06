using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>The icons of the station's fixtures (W07): painted in the atlas as V05 painted the coin, found by a key, with a plain tile for any key without art.</summary>
public sealed class StationIconTests
{
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
    [InlineData("bowl", BlockCell.IconBowl)]
    [InlineData("spoon", BlockCell.IconSpoon)]
    [InlineData("oven", BlockCell.IconOven)]
    public void TheThreeFixturesHaveTheirOwnIcons(string key, BlockCell expected)
    {
        Assert.Equal(expected, BlockAtlasLayout.IconForKey(key));
        Assert.InRange(Painted(expected), 20, 230); // a drawing, neither empty nor a filled square
    }

    [Fact]
    public void AnItemsNameFindsTheItemsIconAndAnyOtherKeyTheOnePlainTile()
    {
        Assert.Equal(BlockCell.IconCoin, BlockAtlasLayout.IconForKey("coin"));
        Assert.Equal(BlockCell.IconAxe, BlockAtlasLayout.IconForKey("axe"));
        Assert.Equal(BlockCell.IconPlain, BlockAtlasLayout.IconForKey("masa")); // a made thing has no art yet
        Assert.Equal(BlockCell.IconPlain, BlockAtlasLayout.IconForKey("hand"));
        Assert.Equal(BlockCell.IconPlain, BlockAtlasLayout.IconForKey(string.Empty));
        Assert.InRange(Painted(BlockCell.IconPlain), 100, 220);
        Assert.All(ItemDefinition.All, static d => Assert.NotEqual(BlockCell.IconPlain, BlockAtlasLayout.IconForKey(d.Name)));
    }

    [Fact]
    public void TheNewCellsFitTheAtlasAndDifferFromEachOther()
    {
        Assert.True(BlockAtlasLayout.CellCount <= BlockAtlasLayout.Columns * BlockAtlasLayout.ColourRows);
        var cells = new[] { BlockCell.IconBowl, BlockCell.IconSpoon, BlockCell.IconOven, BlockCell.IconPlain };
        var pictures = cells.Select(static c => Convert.ToHexString(BlockArt.Pixels(c))).Distinct().ToList();
        Assert.Equal(cells.Length, pictures.Count);
    }
}
