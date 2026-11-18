using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>Some layers of a column made from one window (B07): each chunk is exactly what generating it alone gives, in a region's column, beside it and far away, and a layer outside the world is left out.</summary>
public sealed class GenerateLayersTests
{
    [Fact]
    public void Given_SomeLayersOfColumnsOfEveryKind_When_TheyAreMadeTogether_Then_EachEqualsTheChunkMadeAlone()
    {
        var world = RegionTestWorld.Composition().World;
        var compared = 0;
        foreach (var (cx, cz) in new[] { (12, 12), (23, 3), (24, 10), (-1, 12), (60, -20), (200, 90) })
        {
            var layers = world.GenerateLayers(cx, cz, -3, 6);
            Assert.Equal(10, layers.Count);
            for (var i = 0; i < layers.Count; i++)
            {
                var coord = new VoxelChunkCoord(cx, -3 + i, cz);
                Assert.Equal(coord, layers[i].Coord);
                Assert.Equal(world.Source.Generate(coord).ComputeDigestHex(), layers[i].ComputeDigestHex());
                compared++;
            }
        }

        Assert.Equal(60, compared);
    }

    [Fact]
    public void Given_LayersPartlyOutsideTheWorldOrNone_When_TheyAreAsked_Then_OnlyTheOnesInsideComeBack()
    {
        var world = RegionTestWorld.Composition().World;

        Assert.Equal(2, world.GenerateLayers(60, -20, -6, -3).Count); // layers -4 and -3
        Assert.Equal(2, world.GenerateLayers(60, -20, 18, 25).Count); // layers 18 and 19
        Assert.Empty(world.GenerateLayers(60, -20, 30, 40));
        Assert.Empty(world.GenerateLayers(60, -20, 5, 4));
    }
}
