using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>Tests split out of HeadwaterTests (B28), so they run beside the others: ColumnsBeyondTheHeadwaters.</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class HeadwaterRiversTests(ITestOutputHelper output) : HeadwaterTestsBase(output)
{
    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    public void Given_TheValleyWorld_When_ColumnsBeyondTheHeadwatersAreRead_Then_TheRiversAreWholeAndTheDrainageRunsRoundTheMargin(ulong seed)
    {
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var blend = TestGame.Data.WorldGen.Blend;
        var clearance = TestGame.Data.WorldGen.Height.DrainageData!.RegionClearance;
        var beyond = blend.Margin + blend.Headwater.Distance;
        var (whole, nearest, rivers) = (0, int.MaxValue, 0);
        for (var z = b.MinZ - 2400; z <= b.MaxZ + 2400; z += 4)
        {
            for (var x = b.MinX - 2400; x <= b.MaxX + 2400; x += 4)
            {
                var d = b.DistanceTo(x, z);
                if (d == 0)
                {
                    continue;
                }

                var shape = RegionTestWorld.Shape(world, x, z);
                if (shape.RiverWeight > 0 || shape.LakeWeight > 0)
                {
                    nearest = Math.Min(nearest, d);
                    rivers++;
                }

                if (d >= beyond && shape.RiverWeight > 3000)
                {
                    whole++;
                }
            }
        }

        Assert.True(nearest > blend.Margin + clearance, $"water {nearest} blocks from the footprint: the network keeps {blend.Margin + clearance} blocks clear");
        Assert.True(rivers > 0, "there is water within 2 400 blocks of the valley");
        Assert.True(whole > 0, "beyond the headwaters a river is its whole self");
    }
}
