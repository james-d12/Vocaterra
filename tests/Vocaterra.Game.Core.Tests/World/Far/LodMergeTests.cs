using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The far view's columns of runs (B07c): merging four finer columns into a coarser one as Distant Horizons does, and tidying a column to its cap.</summary>
public sealed class LodMergeTests
{
    private static LodSegment Run(int top, int bottom, BlockId block, byte sky = 15, byte light = 0) => new(top, bottom, block, sky, light);

    private static LodSegment[] Meadow(int ground) => [Run(ground, ground, BlockId.Grass), Run(ground - 1, ground - 3, BlockId.Dirt), Run(ground - 4, -64, BlockId.Stone)];

    private static List<LodSegment> Merge(LodSegment[] a, LodSegment[] b, LodSegment[] c, LodSegment[] d)
    {
        var into = new LodSegment[LodGrid.MaxSegments];
        var count = LodMerge.Merge(a, b, c, d, into);
        return [.. into.Take(count)];
    }

    [Fact]
    public void Given_FourEqualColumns_When_TheyAreMerged_Then_TheColumnIsTheSame()
    {
        var merged = Merge(Meadow(40), Meadow(40), Meadow(40), Meadow(40));

        Assert.Equal(Meadow(40), merged);
    }

    [Fact]
    public void Given_ColumnsOfDifferentHeights_When_TheyAreMerged_Then_TheTopFollowsTheMajorityAndATieGoesToTheSolid()
    {
        var three = Merge(Meadow(40), Meadow(40), Meadow(40), Meadow(44));
        var tie = Merge(Meadow(40), Meadow(40), Meadow(44), Meadow(44));

        Assert.Equal(40, three[0].Top);
        Assert.Equal(BlockId.Grass, three[0].Block);
        Assert.Equal(44, tie[0].Top);
        Assert.Equal(BlockId.Grass, tie[0].Block);
        Assert.Equal(BlockId.Stone, tie[^1].Block);
    }

    [Fact]
    public void Given_ACrownOnHalfTheCell_When_TheColumnsAreMerged_Then_TheCrownStaysOnlyWhereHalfHaveIt()
    {
        LodSegment[] Tree(int ground) => [Run(ground + 9, ground + 5, BlockId.Leaves), Run(ground + 4, ground + 1, BlockId.Trunk), .. Meadow(ground)];
        var half = Merge(Tree(40), Tree(40), Meadow(40), Meadow(40));
        var one = Merge(Tree(40), Meadow(40), Meadow(40), Meadow(40));

        Assert.Equal(BlockId.Leaves, half[0].Block);
        Assert.Equal(49, half[0].Top);
        Assert.Equal(BlockId.Grass, one[0].Block);
    }

    [Fact]
    public void Given_WaterOverABed_When_TheColumnsAreMerged_Then_TheWaterRunStaysOverTheBed()
    {
        LodSegment[] Lake() => [Run(32, 21, BlockId.Water), Run(20, 20, BlockId.Sand), Run(19, 17, BlockId.Sand), Run(16, -64, BlockId.Stone)];

        var merged = Merge(Lake(), Lake(), Lake(), Lake());

        Assert.Equal(BlockId.Water, merged[0].Block);
        Assert.Equal(32, merged[0].Top);
        Assert.Equal(21, merged[0].Bottom);
    }

    [Fact]
    public void Given_MoreRunsThanTheCap_When_TheyAreTidied_Then_TheThinnestAreDroppedAndTheLowestStays()
    {
        var runs = new List<LodSegment>();
        for (var k = 0; k < 11; k++)
        {
            runs.Add(Run(100 - (k * 6), 100 - (k * 6) - (k == 4 ? 0 : 3), k % 2 == 0 ? BlockId.Leaves : BlockId.Planks));
        }

        var into = new LodSegment[16];
        var count = LodMerge.Cap(runs.ToArray(), into);

        Assert.Equal(LodGrid.MaxSegments, count);
        Assert.Equal(runs[0], into[0]);
        Assert.Equal(runs[^1], into[count - 1]);
        Assert.DoesNotContain(runs[4], into.Take(count));
    }

    [Fact]
    public void Given_AColumnWithTreesAndWater_When_TheGroundIsAsked_Then_ItIsTheTopOfTheMassOnTheBottomRun()
    {
        Assert.Equal(40, LodMerge.GroundTop([Run(49, 45, BlockId.Leaves), Run(44, 41, BlockId.Trunk), .. Meadow(40)]));
        Assert.Equal(20, LodMerge.GroundTop([Run(32, 21, BlockId.Water), Run(20, 20, BlockId.Sand), Run(19, 17, BlockId.Sand), Run(16, -64, BlockId.Stone)]));
        Assert.Equal(int.MinValue, LodMerge.GroundTop([]));
    }
}
