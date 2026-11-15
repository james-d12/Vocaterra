using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// The rivers and lakes near a region (B02b, D-game-34 item 18): none inside the blend margin; over the headwater distance beyond it a river narrows and shallows to a stream whose bed rises to meet the ground (it
/// starts at a spring) and a lake shrinks to a pond, so no trench ends in a wall and no lake is cut by a line. Read from the blend's own numbers, for the valley and for a region of another size and place.
/// </summary>
public sealed class HeadwaterTests(ITestOutputHelper output)
{
    /// <summary>The largest step between two neighbouring columns the terrain is held to.</summary>
    private const int StepBound = 9;

    public static TheoryData<string, ulong> Regions => new()
    {
        { "valley", 42UL },
        { "valley", 7UL },
        { "valley", 2026UL },
        { "small", 42UL },
    };

    private static (WorldComposition World, RegionBounds Bounds, GameData Data) Setup(string which, ulong seed) => which == "valley"
        ? (RegionTestWorld.Composition(seed), RegionTestWorld.Valley(seed).Bounds, TestGame.Data)
        : (SmallWorld.Value, RegionTestWorld.Small().Bounds, RegionTestWorld.WithoutCorridors());

    private static readonly Lazy<WorldComposition> SmallWorld = new(() => WorldFactory.Compose(RegionTestWorld.WithoutCorridors(), 42, new RegionCatalog([RegionTestWorld.Small()])));

    // The share of the free world's water at a distance from the footprint: none within the margin, a smoothstep over the headwater distance beyond it.
    private static int Share(RegionBlendData blend, int distance) =>
        distance <= blend.Margin ? 0 : IntMath.Smooth((distance - blend.Margin) * IntMath.One / blend.Headwater.Distance);

    private sealed record Counts(int Water, int Spring, int Samples, int WidestStep, string Where, int NearestWater);

    private static RegionBlendData Blend(GameData data) => data.WorldGen.Blend;

    private static Counts Scan(WorldComposition world, RegionBounds b, GameData data, ulong seed)
    {
        var blend = data.WorldGen.Blend;
        var reach = blend.Margin + blend.Headwater.Distance + 8;
        var free = new HeightModel(data.WorldGen, seed);
        var climate = world.Procedural.World.Climate;
        var (water, spring, samples, widest, where, nearest) = (0, 0, 0, 0, string.Empty, int.MaxValue);
        for (var z = b.MinZ - reach; z <= b.MaxZ + reach; z += 2)
        {
            for (var x = b.MinX - reach; x <= b.MaxX + reach; x += 2)
            {
                var d = b.DistanceTo(x, z);
                if (d == 0 || d > reach)
                {
                    continue;
                }

                samples++;
                var shape = RegionTestWorld.Shape(world, x, z);
                var share = Share(blend, d);
                if (d <= blend.Margin)
                {
                    Assert.True(shape.RiverWeight == 0 && shape.LakeWeight == 0 && !shape.Water, $"water inside the margin at {x},{z} (d {d})");
                }

                // The water is only ever the free world's, thinned: a river or lake weight never exceeds the share, and a column is under water only where the free world has water there.
                Assert.InRange(shape.RiverWeight, 0, share + 1);
                Assert.InRange(shape.LakeWeight, 0, share + 1);
                if (shape.Water && d > blend.Margin)
                {
                    Assert.True(free.Shape(x, z, climate.At(x, z)).Water, $"water at {x},{z} (d {d}) where the free world has none");
                    water++;
                    nearest = Math.Min(nearest, d);
                    var inner = d <= blend.Margin + (blend.Headwater.Distance / 2);
                    if (inner)
                    {
                        spring++;
                        Assert.InRange(data.WorldGen.Vertical.SeaLevel - shape.Height, 1, 3); // the first half of the way a stream is a shallow groove, not a trench
                    }
                }

                // No trench ends in a wall: the ground steps no more than the bound to its neighbours across the margin and the headwaters.
                var east = Math.Abs(shape.Height - RegionTestWorld.Shape(world, x + 1, z).Height);
                var south = Math.Abs(shape.Height - RegionTestWorld.Shape(world, x, z + 1).Height);
                if (Math.Max(east, south) > widest)
                {
                    (widest, where) = (Math.Max(east, south), $"{x},{z} (d {d})");
                }
            }
        }

        return new Counts(water, spring, samples, widest, where, nearest);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Given_ARegion_When_TheWaterNearItIsRead_Then_NoRiverOrLakeReachesTheMarginAndTheRestThinsOutIntoSpringsAndPonds(string which, ulong seed)
    {
        var (world, bounds, data) = Setup(which, seed);

        var counts = Scan(world, bounds, data, seed);

        output.WriteLine($"{which} seed {seed}: {counts.Water} water columns beyond the margin within the headwaters ({counts.Spring} in the inner half, the nearest {counts.NearestWater} blocks from the footprint) of {counts.Samples} sampled; the largest step {counts.WidestStep} at {counts.Where}");
        Assert.True(counts.WidestStep <= StepBound, $"a step of {counts.WidestStep} at {counts.Where}");
    }

    [Theory]
    [InlineData("valley", 42UL)]
    [InlineData("valley", 7UL)]
    public void Given_AValleyWhoseRingHasRivers_When_TheHeadwatersAreRead_Then_ThereIsWaterBeyondTheMarginAndItStartsShallow(string which, ulong seed)
    {
        var (world, bounds, data) = Setup(which, seed);

        var counts = Scan(world, bounds, data, seed);

        Assert.True(counts.Water > 200, $"{counts.Water} water columns in the headwaters: the seed was chosen for its rivers near the valley");
        Assert.True(counts.NearestWater > Blend(data).Margin + 20, $"the nearest water is {counts.NearestWater} blocks from the footprint, well beyond the margin: the stream begins at a spring");
    }

    [Fact]
    public void Given_TheFreeWorldAndTheValleyWorld_When_ColumnsBeyondTheHeadwatersAreCompared_Then_TheyAreTheSameWater()
    {
        var seed = 42UL;
        var world = RegionTestWorld.Composition(seed);
        var b = RegionTestWorld.Valley(seed).Bounds;
        var blend = TestGame.Data.WorldGen.Blend;
        var free = new HeightModel(TestGame.Data.WorldGen, seed);
        var climate = world.Procedural.World.Climate;
        var beyond = blend.Margin + blend.Headwater.Distance;
        var (same, total) = (0, 0);
        for (var z = b.MinZ - beyond - 600; z <= b.MaxZ + beyond + 600; z += 6)
        {
            for (var x = b.MinX - beyond - 600; x <= b.MaxX + beyond + 600; x += 6)
            {
                if (b.DistanceTo(x, z) < beyond)
                {
                    continue;
                }

                total++;
                same += RegionTestWorld.Shape(world, x, z) == free.Shape(x, z, climate.At(x, z)) ? 1 : 0;
            }
        }

        Assert.Equal(total, same); // beyond the margin and the headwaters the rivers and lakes are the free world's
    }
}
