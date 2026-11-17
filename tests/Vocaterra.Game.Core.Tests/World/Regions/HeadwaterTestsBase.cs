using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The shared parts of the test classes split out of HeadwaterTests (B28: xUnit runs the tests of one class one after another, so the slow ones are in classes of their own): the fields and helpers.</summary>
public abstract class HeadwaterTestsBase(ITestOutputHelper output)
{
    /// <summary>The largest step between two neighbouring columns the terrain is held to.</summary>
    internal const int StepBound = 9;

    public static TheoryData<string, ulong> Regions => new()
    {
        { "valley", 42UL },
        { "valley", 7UL },
        { "valley", 2026UL },
        { "small", 42UL },
    };

    internal static (WorldComposition World, RegionBounds Bounds, GameData Data) Setup(string which, ulong seed) => which == "valley"
        ? (RegionTestWorld.Composition(seed), RegionTestWorld.Valley(seed).Bounds, TestGame.Data)
        : (SmallWorld.Value, RegionTestWorld.Small().Bounds, RegionTestWorld.WithoutCorridors());

    internal static readonly Lazy<WorldComposition> SmallWorld = new(() => WorldFactory.Compose(RegionTestWorld.WithoutCorridors(), 42, new RegionCatalog([RegionTestWorld.Small()])));

    // The share of the free world's water at a distance from the footprint: none within the margin, a smoothstep over the headwater distance beyond it.
    internal static int Share(RegionBlendData blend, int distance) =>
        distance <= blend.Margin ? 0 : IntMath.Smooth((distance - blend.Margin) * IntMath.One / blend.Headwater.Distance);

    internal sealed record Counts(int Water, int Spring, int Samples, int WidestStep, string Where, int NearestWater, int Carved);

    internal static RegionBlendData Blend(GameData data) => data.WorldGen.Blend;

    internal static Counts Scan(WorldComposition world, RegionBounds b, GameData data, ulong seed)
    {
        var blend = data.WorldGen.Blend;
        var reach = blend.Margin + blend.Headwater.Distance + 8;
        var (water, spring, samples, widest, where, nearest, carved) = (0, 0, 0, 0, string.Empty, int.MaxValue, 0);
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
                if (shape.RiverWeight > 0 && d > blend.Margin)
                {
                    carved++;
                    Assert.InRange(shape.NaturalHeight - shape.Height, 0, 2 + (int)((long)share * 55 / IntMath.One)); // the channel's floor rises to the ground towards the region: a valley that shallows to nothing, not a trench
                }

                if (shape.Water && d > blend.Margin)
                {
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

        return new Counts(water, spring, samples, widest, where, nearest, carved);
    }

    protected ITestOutputHelper Output => output;
}
