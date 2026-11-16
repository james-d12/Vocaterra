using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The procedural water (B03, H07, D-game-26): every water block is a source at the sea level, so the sea, the rivers and the lakes are at rest from the start (waking every water block
/// changes nothing), and every edge of the water can be left by a body in it.
/// </summary>
public sealed class ProceduralWaterTests
{
    private const int Layers = 6; // y 0 to 95: the sea at 32, the shores and the trees round them
    private const int SeaLevel = 32;

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
    public void Given_AGeneratedShore_When_ItsColumnsAreRead_Then_EveryColumnBelowTheSeaIsWaterUpToTheSeaAndNothingIsWetAboveIt(ulong seed, Feature feature)
    {
        var grid = WaterGrid(seed, feature);
        var (wet, opened) = (0, 0);
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                if (grid.GetBlock(x, SeaLevel, z) == BlockId.Air)
                {
                    // Open at the sea level only in a cave entrance (B11), which is a shaft the width of a column (ground at least 4 blocks over the sea) and stays clear of water.
                    opened++;
                    continue;
                }

                Assert.NotEqual(BlockId.Water, grid.GetBlock(x, SeaLevel + 1, z));
                if (grid.GetBlock(x, SeaLevel, z) != BlockId.Water)
                {
                    continue;
                }

                wet++;
                var y = SeaLevel;
                while (y > 0 && grid.GetBlock(x, y - 1, z) == BlockId.Water)
                {
                    y--;
                }

                Assert.True(y > 0 && BlockDefinition.IsSolid(grid.GetBlock(x, y - 1, z)), $"the water at {x}, {z} rests on {grid.GetBlock(x, y - 1, z)}");
            }
        }

        Assert.True(wet > 100, $"{wet} water columns round the {feature}");
        Assert.True(opened < grid.SizeX * grid.SizeZ / 10, $"{opened} columns round the {feature} are open at the sea level");
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
                    if (y > SeaLevel + 2)
                    {
                        break;
                    }

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
    public void Given_ChunksOfTheSeaAndTheLand_When_TheyAreGenerated_Then_NoWaterIsAnythingButASource()
    {
        foreach (var seed in ProceduralTestWorld.Seeds)
        {
            var (cx, cz) = ProceduralTestWorld.Find(seed, Feature.Coast);
            var region = new GeneratedRegion(ProceduralTestWorld.Source(seed), cx - 1, cz - 1, 3, 3);

            Assert.All(region.Chunks, chunk => Assert.False(chunk.HasWaterLevels));
            Assert.Contains(region.Chunks, chunk => chunk.Blocks.ToArray().Contains(BlockId.Water));
        }
    }
}
