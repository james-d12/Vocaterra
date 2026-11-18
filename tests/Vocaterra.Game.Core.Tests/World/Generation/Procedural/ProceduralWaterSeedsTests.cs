using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Flow;

namespace Vocaterra.Game.Core.Tests.World.Generation.Procedural;

/// <summary>
/// The water checks of B03, widened for B14 over three seeds (42, 7 and 2026): in every sampled window of generated chunks (the land round the east end of the pass, two rivers, two lakes and two
/// stretches of the sea's coast for each seed) every water block is at rest as its chunk loads (the flow rule would change none of them and none of their neighbours), and every water edge can be left
/// by a body in the water (<c>BankBesideWater</c>, D-game-26: a floor no more than a step above shallow water, or the hop out above deep water). A failure names the window and its first cells.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ProceduralWaterSeedsTests
{
    private const int Layers = 14; // y 0 to 223: the sea at 32, the rivers and lakes at their levels of their own, the shores and the trees round them

    /// <summary>Where the windows are: for each seed the pass, then two of each water feature (the n-th river, lake or coast from the origin).</summary>
    public static TheoryData<ulong, string, int> Windows
    {
        get
        {
            var data = new TheoryData<ulong, string, int>();
            foreach (var seed in ProceduralTestWorld.Seeds)
            {
                data.Add(seed, "pass", 0);
                foreach (var feature in new[] { Feature.River, Feature.Lake, Feature.Coast })
                {
                    data.Add(seed, feature.ToString(), 0);
                    data.Add(seed, feature.ToString(), 4);
                }
            }

            return data;
        }
    }

    private static VoxelGrid Window(ulong seed, string where, int skip)
    {
        if (where == "pass")
        {
            // The corridor of the pass ends at x 479 (block), z 208: chunks 26 to 30 east and 12 to 14 south are the cut, its shoulders and the land it opens on.
            return ProceduralTestWorld.Grid(RegionTestWorld.Composition(seed).Procedural, 26, 12, 5, 3, Layers);
        }

        var (cx, cz) = ProceduralTestWorld.Find(seed, Enum.Parse<Feature>(where), skip * 7);
        return ProceduralTestWorld.Grid(ProceduralTestWorld.Source(seed), cx - 1, cz - 1, 3, 3, Layers);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void Given_AWindowOfGeneratedChunks_When_ItsWaterIsWokenAndItsEdgesScanned_Then_NoWaterFlowsAndEveryEdgeCanBeLeft(ulong seed, string where, int skip)
    {
        var grid = Window(seed, where, skip);

        var wet = 0;
        var changing = new List<string>();
        for (var z = 1; z < grid.SizeZ - 1; z++)
        {
            for (var x = 1; x < grid.SizeX - 1; x++)
            {
                for (var y = 1; y < grid.SizeY - 1; y++)
                {
                    var now = FlowCell.Read(grid, x, y, z);
                    wet += now.Block == BlockId.Water ? 1 : 0;
                    var next = FlowRules.Next(grid, x, y, z);
                    if (next != now)
                    {
                        changing.Add($"({x}, {y}, {z}) is {now.Block} and would become {next}");
                    }
                }
            }
        }

        if (where != "pass")
        {
            Assert.True(wet > 100, $"seed {seed}, {where} {skip}: {wet} water blocks in the window");
        }

        Assert.True(changing.Count == 0, $"seed {seed}, {where} {skip}: {changing.Count} cells would flow:{Environment.NewLine}{string.Join(Environment.NewLine, changing.Take(10))}");

        var edges = UnexitableEdges.Scan(grid);
        Assert.True(edges.Count == 0, $"seed {seed}, {where} {skip}: {edges.Count} water edges cannot be left:{Environment.NewLine}{string.Join(Environment.NewLine, UnexitableEdges.Stretches(edges))}");
    }
}
