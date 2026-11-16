using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The procedural water (B03, H07, D-game-26, B02b round 3): the sea, every river and every lake hold water at a level of their own, every water block is a source (a river's surface steps down in falling water, built the way the valley's rapids
/// are), so the whole of it is at rest from the start (waking every water block changes nothing), a column of water is one run of water on solid ground, and every edge of the water can be left by a body in it.
/// </summary>
public sealed class ProceduralWaterTests
{
    private const int Layers = 14; // y 0 to 223: the sea at 32, rivers and lakes at their own levels, the shores and the trees round them

    public static TheoryData<ulong, Feature> Waters
    {
        get
        {
            var data = new TheoryData<ulong, Feature>();
            foreach (var seed in ProceduralTestWorld.Seeds)
            {
                foreach (var feature in new[] { Feature.Coast, Feature.River, Feature.Lake })
                {
                    data.Add(seed, feature);
                }
            }

            return data;
        }
    }

    private static VoxelGrid WaterGrid(ulong seed, Feature feature)
    {
        var (cx, cz) = ProceduralTestWorld.Find(seed, feature);
        return ProceduralTestWorld.Grid(ProceduralTestWorld.Source(seed), cx - 1, cz - 1, 3, 3, Layers);
    }

    [Theory]
    [MemberData(nameof(Waters))]
    public void Given_AGeneratedShore_When_ItsColumnsAreRead_Then_EveryColumnOfWaterIsOneRunOfWaterOnSolidGroundWithNothingWetAboveIt(ulong seed, Feature feature)
    {
        var grid = WaterGrid(seed, feature);
        var wet = 0;
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                var top = grid.SizeY - 1;
                while (top > 0 && grid.GetBlock(x, top, z) != BlockId.Water)
                {
                    top--;
                }

                if (grid.GetBlock(x, top, z) != BlockId.Water)
                {
                    continue;
                }

                wet++;
                var y = top;
                while (y > 0 && grid.GetBlock(x, y - 1, z) == BlockId.Water)
                {
                    y--;
                }

                Assert.True(y > 0 && BlockDefinition.IsSolid(grid.GetBlock(x, y - 1, z)), $"the water at {x}, {z} rests on {grid.GetBlock(x, y - 1, z)}");
                for (var above = top + 1; above < grid.SizeY; above++)
                {
                    Assert.NotEqual(BlockId.Water, grid.GetBlock(x, above, z));
                }
            }
        }

        Assert.True(wet > 100, $"{wet} water columns round the {feature}");
    }

    [Theory]
    [MemberData(nameof(Waters))]
    public void Given_AGeneratedShore_When_EveryWaterBlockAndItsNeighboursAreAskedWhatTheyBecome_Then_NoneChanges(ulong seed, Feature feature)
    {
        var grid = WaterGrid(seed, feature);
        var changing = new List<string>();
        for (var z = 1; z < grid.SizeZ - 1; z++)
        {
            for (var x = 1; x < grid.SizeX - 1; x++)
            {
                for (var y = 1; y < grid.SizeY - 1; y++)
                {
                    var now = FlowCell.Read(grid, x, y, z);
                    if (FlowRules.Next(grid, x, y, z) != now)
                    {
                        changing.Add($"({x}, {y}, {z}) is {now.Block} and would become {FlowRules.Next(grid, x, y, z)}");
                    }
                }
            }
        }

        Assert.True(changing.Count == 0, $"{changing.Count} cells would flow:{Environment.NewLine}{string.Join(Environment.NewLine, changing.Take(10))}");
    }

    [Theory]
    [MemberData(nameof(Waters))]
    public void Given_AGeneratedShore_When_ItsWaterEdgesAreScanned_Then_EveryOneCanBeLeftByWalkingOrTheHopOut(ulong seed, Feature feature)
    {
        var edges = UnexitableEdges.Scan(WaterGrid(seed, feature));

        Assert.True(edges.Count == 0, $"{edges.Count} water edges cannot be left:{Environment.NewLine}{string.Join(Environment.NewLine, UnexitableEdges.Stretches(edges))}");
    }

    [Fact]
    public void Given_ChunksOfTheSeaAndTheLand_When_TheyAreGenerated_Then_NoWaterIsAnythingButASourceExceptWhereASurfaceStepsDown()
    {
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var (cx, cz) = ProceduralTestWorld.Find(seed, Feature.Coast);
            var region = new GeneratedRegion(ProceduralTestWorld.Source(seed), cx - 1, cz - 1, 3, 3);

            Assert.Contains(region.Chunks, chunk => chunk.Blocks.ToArray().Contains(BlockId.Water));
        }

        var (rx, rz) = ProceduralTestWorld.Find(42, Feature.River);
        var river = new GeneratedRegion(ProceduralTestWorld.Source(42), rx - 1, rz - 1, 3, 3);
        Assert.All(river.Chunks, chunk => Assert.True(!chunk.HasWaterLevels || chunk.Blocks.ToArray().Contains(BlockId.Water)));
    }
}
