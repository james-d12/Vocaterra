using Vocaterra.Game.Core.Tests.TestSupport;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The rest of HeadwaterTests's tests (B28 split the slow ones into classes of their own, sharing HeadwaterTestsBase).</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class HeadwaterTests(ITestOutputHelper output) : HeadwaterTestsBase(output)
{
    [Theory]
    [MemberData(nameof(Regions))]
    public void Given_ARegion_When_TheWaterNearItIsRead_Then_NoRiverOrLakeReachesTheMarginAndTheRestThinsOutIntoSpringsAndPonds(string which, ulong seed)
    {
        var (world, bounds, data) = Setup(which, seed);

        var counts = Scan(world, bounds, data, seed);

        Output.WriteLine($"{which} seed {seed}: {counts.Water} water columns beyond the margin within the headwaters ({counts.Spring} in the inner half, the nearest {counts.NearestWater} blocks from the footprint) of {counts.Samples} sampled; the largest step {counts.WidestStep} at {counts.Where}");
        Assert.True(counts.WidestStep <= StepBound, $"a step of {counts.WidestStep} at {counts.Where}");
    }
}
