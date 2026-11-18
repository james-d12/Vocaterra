using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>
/// The pass road is one road (B08c): from the village's levelled ground to the valley's east edge it is path blocks end to end, no step along it
/// is over one block, a walk along it keeps to it, and the first leg from the village square takes a few hundred ticks, not the three thousand of a detour.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class PassRoadTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private static Scene Opened()
    {
        var scene = TestGame.Route(Seed);
        foreach (var cell in scene.PropCells[PassTests.RockfallProp])
        {
            Assert.True(scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air));
        }

        return scene;
    }

    private static int Top(VoxelGrid grid, int x, int z)
    {
        for (var y = grid.SizeY - 1; y >= 0; y--)
        {
            if (grid.GetBlock(x, y, z) is not (BlockId.Air or BlockId.Leaves or BlockId.SpruceLeaves or BlockId.BirchLeaves) && grid.IsSolid(x, y, z))
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>The road's columns: path blocks east of the village's levelled ground (the first road block is the gate's).</summary>
    private static HashSet<(int X, int Z)> Road(Scene scene) =>
        scene.Grid.BlocksOfKind(BlockId.Path).Where(b => b.X >= scene.Point("village").X + 24).Select(b => (b.X, b.Z)).ToHashSet();

    private static readonly string[] Sites = ["paso.gate", "paso.rise", "paso.foot", "paso", "paso.edge"];

    /// <summary>The shortest walk over road columns with no step over one block (up or down), in blocks (a diagonal is 1.414), from the gate to any column of the east edge.</summary>
    private static double ShortestOnRoad(Scene scene, HashSet<(int X, int Z)> road, out int edgeColumns)
    {
        var grid = scene.Grid;
        var gate = scene.Point("paso.gate");
        var start = road.Where(c => c.X == gate.X).OrderBy(c => Math.Abs(c.Z - gate.Z)).First();
        var dist = new Dictionary<(int X, int Z), double> { [start] = 0 };
        var queue = new PriorityQueue<(int X, int Z), double>();
        queue.Enqueue(start, 0);
        var best = double.MaxValue;
        edgeColumns = 0;
        while (queue.TryDequeue(out var c, out var d))
        {
            if (d > dist[c])
            {
                continue;
            }

            if (c.X == grid.SizeX - 1)
            {
                best = Math.Min(best, d);
                edgeColumns++;
            }

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var n = (c.X + dx, c.Z + dz);
                    if ((dx == 0 && dz == 0) || !road.Contains(n) || Math.Abs(Top(grid, n.Item1, n.Item2) - Top(grid, c.X, c.Z)) > 1)
                    {
                        continue;
                    }

                    var nd = d + (dx != 0 && dz != 0 ? 1.4142 : 1);
                    if (!dist.TryGetValue(n, out var old) || nd < old)
                    {
                        dist[n] = nd;
                        queue.Enqueue(n, nd);
                    }
                }
            }
        }

        return best;
    }

    private static double CentreLineLength(Scene scene)
    {
        var points = new List<(int X, int Z)> { (318, 217), (332, 217), (336, 215), (340, 212), (352, 209), (372, 208), (383, 208) };
        var sum = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            sum += Math.Sqrt(Math.Pow(points[i].X - points[i - 1].X, 2) + Math.Pow(points[i].Z - points[i - 1].Z, 2));
        }

        Assert.Equal(scene.Point("paso.gate").X, points[0].X);
        Assert.Equal(scene.Point("paso.edge").X, points[^1].X);
        return sum;
    }

    [Fact]
    public void Given_ThePassOpened_When_TheRoadIsWalkedFromTheGateToTheEdge_Then_NoStepIsOverOneBlockAndTheWayIsAboutTheRoadsLength()
    {
        var scene = Opened();
        var road = Road(scene);
        var shortest = ShortestOnRoad(scene, road, out var edge);
        var length = CentreLineLength(scene);

        output.WriteLine($"{road.Count} road columns; the shortest step-one walk from the gate to the edge is {shortest:0.0} blocks, the centre line {length:0.0}");
        Assert.True(edge > 0, "no road column of the east edge is reached from the gate with steps of at most one block");
        Assert.True(shortest <= length * 1.3, $"the walk is {shortest:0.0} blocks for a road of {length:0.0}");
    }

    [Fact]
    public void Given_TheRoad_When_ItsColumnsAreRead_Then_EveryOneIsAPathBlockAndEachSiteOnItStandsOnOne()
    {
        var scene = Opened();
        var road = Road(scene);
        foreach (var name in Sites)
        {
            var p = scene.Point(name);
            Assert.Contains(road, c => Math.Abs(c.X - p.X) <= 1 && Math.Abs(c.Z - p.Z) <= 3);
        }

        // End to end: every x from the gate to the edge has road columns, and each column's top is a path block.
        for (var x = scene.Point("paso.gate").X; x < scene.Grid.SizeX; x++)
        {
            Assert.Contains(road, c => c.X == x);
        }

        Assert.All(road, c => Assert.Equal(BlockId.Path, scene.Grid.GetBlock(c.X, Top(scene.Grid, c.X, c.Z), c.Z)));
    }

    [Fact]
    public void Given_ThePassOpened_When_AWalkerSetsOutFromTheVillageAndGoesToTheGateAndOn_Then_TheLegsTakeTheRoadAndNoDetour()
    {
        var scene = Opened();
        var sim = VoxelSimulation.Create(scene, TestItems.Catalogue);
        var look = new LookControl();
        look.Set(90, 0);
        var log = new List<string>();
        var gate = scene.Point("paso.gate");
        var edge = scene.Point("paso.edge");
        var script = new VoxelScript(sim, look, [VoxelStep.Teleport(300, 217), VoxelStep.WalkTo(gate.X, gate.Z), VoxelStep.WalkTo(edge.X, edge.Z)], static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        var ticks = 0;
        var firstLeg = -1;
        while (!script.IsDone && ticks < 20000)
        {
            script.Fill(commands);
            commands.Insert(0, VoxelCommand.Look(look.Yaw, look.Pitch));
            sim.Step(commands);
            ticks++;
            if (firstLeg < 0 && log.Any(l => l.StartsWith($"walk-to {gate.X},{gate.Z}: arrived", StringComparison.Ordinal)))
            {
                firstLeg = ticks;
            }
        }

        foreach (var l in log)
        {
            output.WriteLine(l);
        }

        Assert.True(script.IsDone);
        Assert.DoesNotContain(log, static l => l.Contains("gave up", StringComparison.Ordinal));
        Assert.InRange(firstLeg, 1, 600);
        Assert.True(ticks < 2400, $"{ticks} ticks in all");
    }
}
