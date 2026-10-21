using System.Diagnostics;
using Vocaterra.Simulation.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Simulation.Tests.Voxel;

/// <summary>The hand-authored draft route: the seed-42 golden, determinism, the tree scatter and the layout's promises.</summary>
public sealed class SceneTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private const string GoldenDigest = "dc14aa93794873e37068c11a6e12c950ab5f7c9363a9893a3724318d0c095726";

    [Fact]
    public void Seed42SceneMatchesTheGoldenAndPrintsTheRoute()
    {
        var layout = VoxelTestSupport.RouteDraft; // parsed once, outside the timing
        var stopwatch = Stopwatch.StartNew();
        var scene = SceneBuilder.Build(layout, Seed);
        var built = stopwatch.Elapsed.TotalMilliseconds;
        var digest = scene.Grid.ComputeDigestHex();
        var digested = stopwatch.Elapsed.TotalMilliseconds - built;

        output.WriteLine($"scene: {scene.Name}, seed {scene.Seed}, {scene.Grid.SizeX} x {scene.Grid.SizeY} x {scene.Grid.SizeZ} blocks, {scene.Grid.Chunks.Count} chunks, {scene.TreeCount} trees");
        output.WriteLine($"build time: {built:F1} ms (layout already parsed), first world digest: {digested:F1} ms");
        output.WriteLine($"golden digest: {digest}");
        output.WriteLine($"spawn {scene.Spawn.Feet} yaw {scene.Spawn.Yaw}");
        foreach (var npc in scene.Npcs)
        {
            output.WriteLine($"npc {npc.Name} at {npc.Feet} yaw {npc.Yaw}");
        }

        foreach (var (name, point) in scene.Points)
        {
            output.WriteLine($"point {name} = {point}");
        }

        output.WriteLine(AsciiMap3D.TopDown(scene));
        output.WriteLine(AsciiMap3D.Legend);
        Assert.Equal(GoldenDigest, digest);
    }

    [Fact]
    public void TheSceneIsBuiltFromAFixedChunkGrid()
    {
        var grid = VoxelTestSupport.BuildRoute(Seed).Grid;
        Assert.Equal((96, 32, 96), (grid.SizeX, grid.SizeY, grid.SizeZ));
        Assert.Equal((6, 2, 6), (grid.ChunksX, grid.ChunksY, grid.ChunksZ));
        Assert.Equal(72, grid.Chunks.Count);
    }

    [Fact]
    public void TheSceneBuildsIdenticallyTwice()
    {
        var a = VoxelTestSupport.BuildRoute(Seed);
        var b = VoxelTestSupport.BuildRoute(Seed);
        Assert.Equal(a.Grid.ComputeDigestHex(), b.Grid.ComputeDigestHex());
        Assert.Equal(a.Spawn, b.Spawn);
        Assert.Equal(a.Npcs, b.Npcs);
        Assert.Equal(a.Points.Select(p => (p.Key, p.Value)), b.Points.Select(p => (p.Key, p.Value)));
        Assert.Equal(a.TreeCount, b.TreeCount);
    }

    [Fact]
    public void AnotherSeedChangesOnlyTheTreeScatter()
    {
        var a = VoxelTestSupport.BuildRoute(Seed);
        var b = VoxelTestSupport.BuildRoute(7);
        Assert.NotEqual(a.Grid.ComputeDigestHex(), b.Grid.ComputeDigestHex());

        var differing = 0;
        for (var y = 0; y < a.Grid.SizeY; y++)
        {
            for (var z = 0; z < a.Grid.SizeZ; z++)
            {
                for (var x = 0; x < a.Grid.SizeX; x++)
                {
                    var p = a.Grid.GetBlock(x, y, z);
                    var q = b.Grid.GetBlock(x, y, z);
                    if (p != q)
                    {
                        differing++;
                        Assert.True(IsTree(p) || p == BlockId.Air, $"({x}, {y}, {z}) differs but seed 42 has {p}");
                        Assert.True(IsTree(q) || q == BlockId.Air, $"({x}, {y}, {z}) differs but seed 7 has {q}");
                    }
                }
            }
        }

        Assert.True(differing > 100, "two seeds should plant different forests");
        Assert.Equal(a.Spawn, b.Spawn);
        Assert.Equal(a.Npcs, b.Npcs);
        Assert.Equal(a.Points.Select(p => (p.Key, p.Value)), b.Points.Select(p => (p.Key, p.Value)));
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(123456789UL)]
    public void TreesNeverStandOnOrOverhangTheRoute(ulong seed)
    {
        var scene = VoxelTestSupport.BuildRoute(seed);
        var layout = VoxelTestSupport.RouteDraft;
        var grid = scene.Grid;
        Assert.True(scene.TreeCount > 60, "the forest should be a forest");

        // The route: the legend's reserved columns, every column that holds a built block, and the named places.
        var route = new List<(int X, int Z)>();
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                var reserved = layout.Legend[layout.FeatureAt(x, z)].Reserved;
                var built = false;
                for (var y = 0; y < grid.SizeY && !built; y++)
                {
                    built = IsBuilt(grid.GetBlock(x, y, z));
                }

                if (reserved || built)
                {
                    route.Add((x, z));
                }
            }
        }

        route.Add((scene.Spawn.Feet.X, scene.Spawn.Feet.Z));
        route.AddRange(scene.Npcs.Select(n => (n.Feet.X, n.Feet.Z)));
        route.AddRange(scene.Points.Values.Select(p => (p.X, p.Z)));
        Assert.True(route.Count > 500);

        var margin = layout.Trees.Margin;
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    var block = grid.GetBlock(x, y, z);
                    if (block != BlockId.Trunk && block != BlockId.Leaves)
                    {
                        continue;
                    }

                    var nearest = route.Min(r => Math.Max(Math.Abs(r.X - x), Math.Abs(r.Z - z)));
                    var needed = block == BlockId.Trunk ? margin : margin - 2;
                    Assert.True(nearest >= needed, $"{block} at ({x}, {y}, {z}) is {nearest} columns from the route");
                }
            }
        }
    }

    [Fact]
    public void TrunksKeepTheirSpacing()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var trunks = new List<(int X, int Z)>();
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                if (Enumerable.Range(0, grid.SizeY).Any(y => grid.GetBlock(x, y, z) == BlockId.Trunk))
                {
                    trunks.Add((x, z));
                }
            }
        }

        Assert.Equal(scene.TreeCount, trunks.Count);
        var spacing = VoxelTestSupport.RouteDraft.Trees.Spacing;
        for (var i = 0; i < trunks.Count; i++)
        {
            for (var j = i + 1; j < trunks.Count; j++)
            {
                Assert.True(Math.Max(Math.Abs(trunks[i].X - trunks[j].X), Math.Abs(trunks[i].Z - trunks[j].Z)) >= spacing);
            }
        }
    }

    [Fact]
    public void TheNamedPointsAndNpcsSitWhereTheRouteSaysTheyDo()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        foreach (var name in new[] { "camp", "stall", "river", "village", "workbench", "firering", "signpost", "bridge", "house1", "house2", "house3" })
        {
            Assert.True(scene.Points.ContainsKey(name), $"missing point {name}");
        }

        Assert.Equal(BlockId.Grass, grid.GetBlock(scene.Point("camp").X, scene.Point("camp").Y, scene.Point("camp").Z));
        Assert.Equal(BlockId.Water, grid.GetBlock(scene.Point("river").X, scene.Point("river").Y, scene.Point("river").Z));
        Assert.Equal(BlockId.Stall, grid.GetBlock(scene.Point("stall").X, scene.Point("stall").Y + 1, scene.Point("stall").Z));
        Assert.Equal(BlockId.Table, grid.GetBlock(scene.Point("workbench").X, scene.Point("workbench").Y + 1, scene.Point("workbench").Z));
        Assert.Equal(BlockId.Sign, grid.GetBlock(scene.Point("signpost").X, scene.Point("signpost").Y + 3, scene.Point("signpost").Z));
        Assert.Equal(BlockId.Air, grid.GetBlock(scene.Point("firering").X, scene.Point("firering").Y + 1, scene.Point("firering").Z));
        Assert.Equal(BlockId.FireRing, grid.GetBlock(scene.Point("firering").X + 1, scene.Point("firering").Y + 1, scene.Point("firering").Z));

        Assert.Equal(["guide", "shopkeeper", "villager"], scene.Npcs.Select(n => n.Name));
        var places = scene.Npcs.Select(n => n.Feet).Append(scene.Spawn.Feet);
        foreach (var feet in places)
        {
            Assert.False(grid.IsSolid(feet.X, feet.Y, feet.Z));
            Assert.False(grid.IsSolid(feet.X, feet.Y + 1, feet.Z));
            Assert.True(grid.IsSolid(feet.X, feet.Y - 1, feet.Z));
        }

        Assert.Equal(1, scene.Npcs.Count(n => n.Name == "guide"));
        var counts = CountBlocks(grid);
        Assert.True(counts[BlockId.BerryBush] >= 3, "the camp needs at least three berry bushes for the objective");
        Assert.Equal(3 * 5 * 5, counts[BlockId.Planks]); // three 5 x 5 house floors
        Assert.Equal(3 * 2, counts[BlockId.Door]); // a two-block doorway each
        Assert.Equal(3, counts[BlockId.Stall]); // the counter
        Assert.Equal(1, counts[BlockId.Sign]);
    }

    [Fact]
    public void ThePlayerCanWalkFromTheCampToTheFarBankOverTheBridge()
    {
        // Pathfind on the grid (a feet cell with head room, a solid cell under it, steps of up to one block up and three down, and no
        // wading: water is closed), then drive the real body along the cells. The bridge is the only way over the river.
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var start = scene.Spawn.Feet;
        var target = new BlockPos(66, grid.HighestBlock(66, 91) + 1, 91);
        var path = WalkPath(grid, start, target);
        Assert.NotNull(path);
        output.WriteLine($"path length {path.Count} cells, through the village: {path.Any(p => Math.Abs(p.X - scene.Point("village").X) < 5 && Math.Abs(p.Z - scene.Point("village").Z) < 5)}");
        Assert.Contains(path, p => grid.GetBlock(p.X, p.Y - 1, p.Z) == BlockId.Bridge);

        var sim = VoxelSimulation.Create(scene);
        foreach (var cell in path.Skip(1))
        {
            var goalX = (cell.X * Units) + (Units / 2);
            var goalZ = (cell.Z * Units) + (Units / 2);
            var reached = false;
            for (var tick = 0; tick < 240 && !reached; tick++)
            {
                var dx = Math.Sign(goalX - sim.Player.CenterX) * (Math.Abs(goalX - sim.Player.CenterX) > 200 ? 1 : 0);
                var dz = Math.Sign(goalZ - sim.Player.CenterZ) * (Math.Abs(goalZ - sim.Player.CenterZ) > 200 ? 1 : 0);
                reached = dx == 0 && dz == 0;
                if (!reached)
                {
                    sim.Step([VoxelCommand.Move(dx, dz)]);
                }
            }

            Assert.True(reached, $"the body never reached {cell} (at {sim.Player.FeetBlock})");
        }

        for (var i = 0; i < 60; i++)
        {
            sim.Step();
        }

        Assert.Equal(target.Z, sim.Player.FeetBlock.Z);
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void ALayoutThatPutsAPropOnUnevenGroundIsRejected()
    {
        var json = File.ReadAllText(VoxelTestSupport.RouteDraftPath())
            .Replace("{\"type\": \"fire_ring\", \"x\": 22, \"z\": 22, \"name\": \"firering\"}", "{\"type\": \"fire_ring\", \"x\": 22, \"z\": 22, \"name\": \"firering\"}, {\"type\": \"house\", \"x\": 4, \"z\": 63, \"width\": 5, \"depth\": 5, \"facing\": \"south\"}", StringComparison.Ordinal);
        var layout = SceneLayout.Parse(json);
        var error = Assert.Throws<SceneLayoutException>(() => SceneBuilder.Build(layout, Seed));
        Assert.Contains("flat", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "missing")]
    [InlineData("not json", "not valid JSON")]
    public void MalformedLayoutsAreRejectedWithAMessage(string json, string fragment)
    {
        var error = Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json));
        Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
    }

    private static bool IsTree(BlockId block) => block is BlockId.Trunk or BlockId.Leaves;

    private static bool IsBuilt(BlockId block) =>
        block is BlockId.Path or BlockId.Planks or BlockId.Door or BlockId.Table or BlockId.Torch or BlockId.Stall or BlockId.Roof or BlockId.Fence
            or BlockId.Wall or BlockId.FireRing or BlockId.Sign or BlockId.Bridge or BlockId.Bush or BlockId.BerryBush or BlockId.Water;

    private static Dictionary<BlockId, int> CountBlocks(VoxelGrid grid)
    {
        var counts = Enum.GetValues<BlockId>().ToDictionary(b => b, _ => 0);
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    counts[grid.GetBlock(x, y, z)]++;
                }
            }
        }

        return counts;
    }

    private const int Units = VoxelTestSupport.Units;

    /// <summary>A breadth-first walk over feet cells; returns the cells from the start to the target, or null.</summary>
    private static List<BlockPos>? WalkPath(VoxelGrid grid, BlockPos start, BlockPos target)
    {
        var previous = new Dictionary<BlockPos, BlockPos>();
        var queue = new Queue<BlockPos>();
        queue.Enqueue(start);
        previous[start] = start;
        (int, int)[] steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell.X == target.X && cell.Z == target.Z && cell.Y == target.Y)
            {
                var path = new List<BlockPos>();
                for (var c = cell; ; c = previous[c])
                {
                    path.Add(c);
                    if (c == start)
                    {
                        break;
                    }
                }

                path.Reverse();
                return path;
            }

            foreach (var (sx, sz) in steps)
            {
                for (var dy = 1; dy >= -3; dy--)
                {
                    var next = new BlockPos(cell.X + sx, cell.Y + dy, cell.Z + sz);
                    if (!CanStand(grid, next) || previous.ContainsKey(next))
                    {
                        continue;
                    }

                    var low = cell.Y + Math.Min(dy, 0);
                    var high = cell.Y + Math.Max(dy, 0) + 1;
                    var clear = Enumerable.Range(low, high - low + 1).All(y => !grid.IsSolid(next.X, y, next.Z))
                        && (dy <= 0 || !grid.IsSolid(cell.X, cell.Y + 2, cell.Z));
                    if (clear)
                    {
                        previous[next] = cell;
                        queue.Enqueue(next);
                        break;
                    }
                }
            }
        }

        return null;
    }

    private static bool CanStand(VoxelGrid grid, BlockPos feet) =>
        grid.InBounds(feet.X, feet.Y, feet.Z)
        && !grid.IsSolid(feet.X, feet.Y, feet.Z)
        && !grid.IsSolid(feet.X, feet.Y + 1, feet.Z)
        && grid.IsSolid(feet.X, feet.Y - 1, feet.Z)
        && grid.GetBlock(feet.X, feet.Y, feet.Z) != BlockId.Water
        && grid.GetBlock(feet.X, feet.Y + 1, feet.Z) != BlockId.Water;
}
