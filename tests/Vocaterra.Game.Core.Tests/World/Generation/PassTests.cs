using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>
/// The pass through the east ring (biomes-plan 3.6, B08a): the road from the village to the east edge, the `paso` site, and the rockfall of
/// existing blocks that closes the pass until B08b opens it. The walk is a column graph: a body steps up at most one block between neighbouring
/// columns (eight ways), drops any height, and walks through leaves.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class PassTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private const int RockfallFloorY = 50;

    /// <summary>The prop id of the rockfall (<c>scene.json</c>): B08b removes its cells when the pass opens.</summary>
    internal const string RockfallProp = "rockfall";

    private static int EastEdge(Scene scene) => scene.Grid.SizeX - 1;

    private static bool IsLeaf(BlockId id) => id is BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves;

    /// <summary>The y of the highest block a body can stand on in the column (leaves ignored), or -1 for water or no ground.</summary>
    private static int Floor(VoxelGrid grid, int x, int z)
    {
        for (var y = grid.SizeY - 1; y >= 0; y--)
        {
            var block = grid.GetBlock(x, y, z);
            if (block == BlockId.Water)
            {
                return -1;
            }

            if (!IsLeaf(block) && grid.CanStandOn(x, y, z))
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>
    /// The columns a walker reaches from <paramref name="start"/>: up one block, any drop, eight ways. <paramref name="allowed"/> limits the columns, and
    /// <paramref name="maxFloor"/> the height the walker may climb to.
    /// </summary>
    private static HashSet<(int X, int Z)> Reach(VoxelGrid grid, (int X, int Z) start, Func<int, int, bool>? allowed = null, int maxFloor = int.MaxValue)
    {
        var floors = new int[grid.SizeX * grid.SizeZ];
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                floors[(z * grid.SizeX) + x] = Floor(grid, x, z);
            }
        }

        var seen = new HashSet<(int X, int Z)> { start };
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var (cx, cz) = queue.Dequeue();
            var here = floors[(cz * grid.SizeX) + cx];
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = cx + dx;
                    var nz = cz + dz;
                    if ((dx == 0 && dz == 0) || (uint)nx >= (uint)grid.SizeX || (uint)nz >= (uint)grid.SizeZ || seen.Contains((nx, nz)))
                    {
                        continue;
                    }

                    var there = floors[(nz * grid.SizeX) + nx];
                    if (there < 0 || there > here + 1 || there > maxFloor || (allowed is not null && !allowed(nx, nz)))
                    {
                        continue;
                    }

                    seen.Add((nx, nz));
                    queue.Enqueue((nx, nz));
                }
            }
        }

        return seen;
    }

    /// <summary>A fresh copy of the route with the rockfall's blocks removed (the pass as B08b leaves it).</summary>
    private static Scene OpenedPass()
    {
        var scene = TestGame.Route(Seed);
        foreach (var cell in scene.PropCells[RockfallProp])
        {
            Assert.True(scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air));
        }

        return scene;
    }

    private static (int X, int Z) RoadStart(Scene scene)
    {
        var village = scene.Point("village");
        return (village.X + 24, village.Z);
    }

    [Fact]
    public void Given_ThePassOpened_When_TheRoadIsWalkedFromTheVillage_Then_EveryStepIsWalkableAndItReachesTheEastEdge()
    {
        var scene = OpenedPass();
        var grid = scene.Grid;
        var village = scene.Point("village");
        var road = grid.BlocksOfKind(BlockId.Path).Where(b => b.X >= village.X + 20).Select(b => (b.X, b.Z)).ToHashSet();
        var start = road.Where(c => c.X <= village.X + 26).OrderBy(c => Math.Abs(c.Z - village.Z)).First();

        var walked = Reach(grid, start, (x, z) => road.Contains((x, z)));

        Assert.Contains(walked, c => c.X == EastEdge(scene));
        var east = walked.Where(c => c.X == EastEdge(scene)).Select(c => c.Z).ToList();
        output.WriteLine($"the road reaches the edge at z {east.Min()}..{east.Max()}; {walked.Count} road columns walked, {road.Count} in all east of the village");
    }

    /// <summary>The height up to which a walker counts as in the pass: the pass's own floor at the site `paso` and six blocks over it (the heap is at least three high; the flanks beyond are cliffs and the ring).</summary>
    private static int PassBand(Scene scene) => Floor(scene.Grid, scene.Point("paso").X, scene.Point("paso").Z) + 6;

    [Fact]
    public void Given_TheRockfall_When_AWalkerStaysInThePassAndSetsOutFromTheRoad_Then_NoRouteReachesThePasoSiteOrTheEastEdge()
    {
        var scene = TestGame.Route(Seed);
        var walked = Reach(scene.Grid, RoadStart(scene), maxFloor: PassBand(scene));

        Assert.DoesNotContain(walked, c => c.X >= scene.Point("paso").X - 4);
        Assert.Contains(walked, c => c.X >= scene.Point("paso.foot").X); // the walk does get as far as the foot of the pass
        var paso = scene.Point("paso");
        Assert.DoesNotContain((paso.X, paso.Z), walked);
        Assert.NotEmpty(scene.PropCells[RockfallProp]);
    }

    [Fact]
    public void Given_ThePassOpened_When_AWalkerStaysInThePassAndSetsOutFromTheRoad_Then_TheEastEdgeAndThePasoSiteAreReached()
    {
        var scene = OpenedPass();
        var walked = Reach(scene.Grid, RoadStart(scene), maxFloor: PassBand(scene));

        Assert.Contains(walked, c => c.X == EastEdge(scene));
        var paso = scene.Point("paso");
        Assert.Contains((paso.X, paso.Z), walked);
    }

    [Fact]
    public void Given_TheRockfall_When_ItsCellsAreRead_Then_EveryCellIsStoneAndTheRoadIsUnderIt()
    {
        var scene = TestGame.Route(Seed);
        var cells = scene.PropCells[RockfallProp];
        Assert.All(cells, c => Assert.True(scene.Grid.GetBlock(c.X, c.Y, c.Z) is BlockId.Stone or BlockId.Cobble or BlockId.MossyStone));
        var opened = OpenedPass();
        var road = opened.Grid.BlocksOfKind(BlockId.Path);
        Assert.Contains(road, p => cells.Any(c => c.X == p.X && c.Z == p.Z));
    }

    [Fact]
    public void Given_TheNewRoad_When_ItsBlocksAreChecked_Then_TheLandRulesProtectEveryColumnAsRoad()
    {
        var scene = TestGame.Route(Seed);
        var rules = LandRules.Create(TestGame.Data.Land, TestGame.Data.Scene, scene);
        var eastRoad = scene.Grid.BlocksOfKind(BlockId.Path).Where(b => b.X >= scene.Point("village").X + 30).ToList();

        Assert.NotEmpty(eastRoad);
        Assert.Contains(eastRoad, b => b.X >= scene.Point("paso").X - 4);
        Assert.All(eastRoad, b => Assert.True(rules.IsProtected(b.X, b.Z), $"({b.X}, {b.Z}) is a road block outside the land rules' protection"));
    }

    [Fact]
    public void Given_TheRockfallsFloorY_When_ItsColumnsAreRead_Then_EachStandsOnSolidGroundWithNoGapUnderIt()
    {
        var scene = TestGame.Route(Seed);
        var cells = scene.PropCells[RockfallProp];

        var columns = cells.GroupBy(static c => (c.X, c.Z)).ToList();
        Assert.NotEmpty(columns);
        foreach (var column in columns)
        {
            var ys = column.Select(static c => c.Y).Order().ToList();
            Assert.True(scene.Grid.IsSolid(column.Key.X, ys[0] - 1, column.Key.Z), $"({column.Key.X}, {column.Key.Z}): the heap's lowest cell {ys[0]} has no ground under it");
            Assert.Equal(ys.Count, ys[^1] - ys[0] + 1); // one unbroken stack
        }

        Assert.Equal(RockfallFloorY, VoxelTestSupport.RouteDraft.Props.Single(static p => p.Name == RockfallProp).FloorY);
        Assert.True(cells.Min(static c => c.Y) > RockfallFloorY);
    }
}
