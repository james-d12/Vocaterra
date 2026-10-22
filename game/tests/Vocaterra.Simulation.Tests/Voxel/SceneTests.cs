using System.Diagnostics;
using Vocaterra.Simulation.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Simulation.Tests.Voxel;

/// <summary>The generated world (S13): the seed-42 golden, determinism, the promises of the layout (the heights, the sites, the river, the trail) and the forest.</summary>
public sealed class SceneTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private const string GoldenDigest = "8048644059674b5335ef2c32e230defbbe8402a32f72f7ee10324a36fc80cb47";

    private const int Units = VoxelTestSupport.Units;

    [Fact]
    public void Seed42SceneMatchesTheGoldenAndLoadsInUnderThreeSeconds()
    {
        var layout = VoxelTestSupport.RouteDraft; // parsed once, outside the timing
        var stopwatch = Stopwatch.StartNew();
        var scene = SceneBuilder.Build(layout, Seed);
        var built = stopwatch.Elapsed.TotalMilliseconds;
        var digest = scene.Grid.ComputeDigestHex();
        var digested = stopwatch.Elapsed.TotalMilliseconds - built;

        output.WriteLine($"scene: {scene.Name}, seed {scene.Seed}, {scene.Grid.SizeX} x {scene.Grid.SizeY} x {scene.Grid.SizeZ} blocks, {scene.Grid.Chunks.Count} chunks ({scene.Grid.Chunks.Count(c => c.IsEmpty)} empty), {scene.TreeCount} trees");
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

        Assert.Equal(GoldenDigest, digest);
        Assert.True(built + digested < 3000, $"the world takes {built + digested:F0} ms to build and digest (the budget is 3 000 ms)");
    }

    [Fact]
    public void TheSceneIsBuiltFromAFixedChunkGrid()
    {
        var grid = VoxelTestSupport.BuildRoute(Seed).Grid;
        Assert.Equal((384, 128, 384), (grid.SizeX, grid.SizeY, grid.SizeZ));
        Assert.Equal((24, 8, 24), (grid.ChunksX, grid.ChunksY, grid.ChunksZ));
        Assert.Equal(4608, grid.Chunks.Count);
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

    [Theory]
    [InlineData(7UL)]
    [InlineData(123456789UL)]
    public void AnotherSeedChangesTheLandAndNeverMovesASite(ulong seed)
    {
        var a = VoxelTestSupport.BuildRoute(Seed);
        var b = VoxelTestSupport.BuildRoute(seed);
        Assert.NotEqual(a.Grid.ComputeDigestHex(), b.Grid.ComputeDigestHex());
        Assert.NotEqual(a.TreeCount, b.TreeCount);

        // Every site, every named point, the NPCs and the spawn are where they were (and as high).
        Assert.Equal(a.Spawn, b.Spawn);
        Assert.Equal(a.Npcs, b.Npcs);
        Assert.Equal(a.Points.Select(p => (p.Key, p.Value)), b.Points.Select(p => (p.Key, p.Value)));

        // The level ground of the camps and the village, and everything built on it, is the same block for block.
        foreach (var name in new[] { "camp.player", "camp.ledge", "camp.guide", "village" })
        {
            var site = VoxelTestSupport.RouteDraft.Site(name)!;
            for (var z = site.Z - site.Radius; z <= site.Z + site.Radius; z++)
            {
                for (var x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                {
                    if (((x - site.X) * (x - site.X)) + ((z - site.Z) * (z - site.Z)) > site.Radius * site.Radius)
                    {
                        continue;
                    }

                    for (var y = (site.Y ?? 30) - 3; y <= (site.Y ?? 30) + 12; y++)
                    {
                        // The undergrowth on a site's meadow is the seed's (S14): it is not part of the site, which is the level ground and what is built on it.
                        var one = a.Grid.GetBlock(x, y, z);
                        var other = b.Grid.GetBlock(x, y, z);
                        Assert.True(one == other || (BlockDefinition.IsDecor(one) || one == BlockId.Air) && (BlockDefinition.IsDecor(other) || other == BlockId.Air), $"{name}: ({x}, {y}, {z}) differs between seed 42 and seed {seed}");
                    }
                }
            }
        }

        // The land elsewhere does change.
        var differing = 0;
        for (var z = 0; z < a.Grid.SizeZ; z += 3)
        {
            for (var x = 0; x < a.Grid.SizeX; x += 3)
            {
                if (a.Grid.HighestBlock(x, z) != b.Grid.HighestBlock(x, z))
                {
                    differing++;
                }
            }
        }

        Assert.True(differing > 2000, $"only {differing} sampled columns differ: two seeds should shape different hills and forests");
    }

    [Fact]
    public void TheMountainsHaveTheHeightsOfTheBrief()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var highest = 0;
        var snowAbove100 = 0;
        var snowBelow95 = 0;
        var stoneTops = 0;
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                if (grid.GetBlock(x, grid.HighestBlock(x, z), z) == BlockId.Water)
                {
                    continue; // the bed of the pond above the falls is sand, under water
                }

                var y = Ground(grid, x, z);
                highest = Math.Max(highest, y);
                var top = grid.GetBlock(x, y, z);
                if (top == BlockId.Snow)
                {
                    if (y >= 100)
                    {
                        snowAbove100++;
                    }

                    if (y < 95)
                    {
                        snowBelow95++;
                    }
                }

                if (y >= 86)
                {
                    stoneTops++;
                    Assert.True(top is BlockId.Stone or BlockId.Snow, $"the ground at ({x}, {y}, {z}) is {top}, not bare stone or snow");
                }
            }
        }

        output.WriteLine($"highest ground {highest}, snow columns above 100: {snowAbove100}, columns of rock or snow above 85: {stoneTops}");
        Assert.InRange(highest, 110, 125);
        Assert.True(snowAbove100 > 2000, "the peaks carry snow");
        Assert.Equal(0, snowBelow95);
        Assert.True(stoneTops > 5000, "there are high rock faces");
    }

    [Fact]
    public void TheCampsAndTheVillageStandAtTheHeightsOfTheBrief()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        Assert.InRange(scene.Point("camp.player").Y, 65, 75);
        Assert.InRange(scene.Point("camp.guide").Y, 30, 50);
        Assert.InRange(scene.Point("village").Y, 30, 40);
        Assert.Equal(scene.Point("camp.player").Y + 1, scene.Spawn.Feet.Y);

        // The valley floor (the meadow round the village, the clearing of the guide) is at 30 to 45.
        foreach (var name in new[] { "camp.guide", "village", "stall", "workbench", "signpost" })
        {
            Assert.InRange(scene.Point(name).Y, 30, 45);
        }

        // The spawn faces south-east, over the valley.
        Assert.Equal(Angles.FromDegrees(135), scene.Spawn.Yaw);
        Assert.Equal(scene.Point("camp.player").X, scene.Spawn.Feet.X);
        Assert.Equal(scene.Point("camp.player").Z, scene.Spawn.Feet.Z);
    }

    [Fact]
    public void TheNamedSitesAndNpcsSitWhereTheLayoutSaysTheyDo()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        foreach (var name in new[] { "camp.player", "camp.ledge", "camp.guide", "village", "stall", "river", "bridge", "lake", "waterfall", "trail", "workbench", "firering", "firering.player", "signpost", "house1", "house2", "house3", "camp", "tent" })
        {
            Assert.True(scene.Points.ContainsKey(name), $"missing point {name}");
        }

        Assert.Contains(grid.GetBlock(scene.Point("camp.guide").X, scene.Point("camp.guide").Y, scene.Point("camp.guide").Z), new[] { BlockId.Grass, BlockId.Path }); // the road starts at the camp
        Assert.Equal(BlockId.Water, grid.GetBlock(scene.Point("river").X, scene.Point("river").Y, scene.Point("river").Z));
        Assert.Equal(BlockId.Water, grid.GetBlock(scene.Point("lake").X, scene.Point("lake").Y, scene.Point("lake").Z));
        Assert.Equal(BlockId.Water, grid.GetBlock(scene.Point("waterfall").X, scene.Point("waterfall").Y, scene.Point("waterfall").Z));
        Assert.Equal(BlockId.Water, grid.GetBlock(scene.Point("bridge").X, scene.Point("bridge").Y, scene.Point("bridge").Z));
        Assert.Equal(BlockId.Bridge, grid.GetBlock(scene.Point("bridge").X, scene.Point("bridge").Y + 3, scene.Point("bridge").Z));
        Assert.Equal(BlockId.Path, grid.GetBlock(scene.Point("trail").X, scene.Point("trail").Y, scene.Point("trail").Z));
        Assert.Equal(BlockId.Stall, grid.GetBlock(scene.Point("stall").X, scene.Point("stall").Y + 1, scene.Point("stall").Z));
        Assert.Equal(BlockId.Table, grid.GetBlock(scene.Point("workbench").X, scene.Point("workbench").Y + 1, scene.Point("workbench").Z));
        Assert.Equal(BlockId.Sign, grid.GetBlock(scene.Point("signpost").X, scene.Point("signpost").Y + 3, scene.Point("signpost").Z));
        foreach (var ring in new[] { "firering", "firering.player" })
        {
            var centre = scene.Point(ring);
            Assert.Equal(BlockId.Air, grid.GetBlock(centre.X, centre.Y + 1, centre.Z)); // cold: nothing in the middle
            Assert.Equal(BlockId.FireRing, grid.GetBlock(centre.X + 1, centre.Y + 1, centre.Z));
        }

        // The cold fire ring and the tent are at the player's camp, the workbench and the guide at the guide's.
        Assert.InRange(Distance(scene.Point("firering.player"), scene.Point("camp.player")), 1, 12);
        Assert.InRange(Distance(scene.Point("tent"), scene.Point("camp.ledge")), 1, 10);
        Assert.InRange(Distance(scene.Point("tent"), scene.Point("camp.player")), 1, 18);
        Assert.InRange(Distance(scene.Point("workbench"), scene.Point("camp.guide")), 1, 12);
        Assert.InRange(Distance(scene.Point("stall"), scene.Point("village")), 1, 20);
        Assert.Equal(BlockId.Canvas, grid.GetBlock(scene.Point("tent").X, scene.Point("tent").Y + 3, scene.Point("tent").Z));

        Assert.Equal(["guide", "shopkeeper", "villager"], scene.Npcs.Select(n => n.Name));
        Assert.InRange(Distance(scene.Npc("guide").Feet, scene.Point("camp.guide")), 0, 12);
        Assert.InRange(Distance(scene.Npc("shopkeeper").Feet, scene.Point("village")), 0, 20);
        Assert.InRange(Distance(scene.Npc("villager").Feet, scene.Point("village")), 0, 20);
        var places = scene.Npcs.Select(n => n.Feet).Append(scene.Spawn.Feet);
        foreach (var feet in places)
        {
            Assert.False(grid.IsSolid(feet.X, feet.Y, feet.Z));
            Assert.False(grid.IsSolid(feet.X, feet.Y + 1, feet.Z));
            Assert.True(grid.IsSolid(feet.X, feet.Y - 1, feet.Z));
        }

        var counts = CountBlocks(grid);
        Assert.True(counts[BlockId.BerryBush] >= 3, "the camp needs at least three berry bushes for the objective");
        Assert.Equal(3 * 5 * 5, counts[BlockId.Planks]); // three 5 x 5 house floors
        Assert.Equal(3 * 2, counts[BlockId.Door]); // a two-block doorway each
        Assert.Equal(3, counts[BlockId.Stall]); // the counter
        Assert.Equal(3, counts[BlockId.Sign]); // the post column of the signpost's three boards
        Assert.Equal(5, counts[BlockId.SignTiendaA] + counts[BlockId.SignTiendaB] + counts[BlockId.SignRio] + counts[BlockId.SignMontanaA] + counts[BlockId.SignMontanaB]); // the names: two, one and two cells
        Assert.Equal(2 * 8, counts[BlockId.FireRing]); // two rings of eight stones
        Assert.True(TrunkBlocks(grid) > 10_000, "a dense forest has many trunk blocks");
    }

    [Fact]
    public void TheRiverRunsDownhillFromTheWaterfallThroughTheValleyIntoTheLake()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var layout = VoxelTestSupport.RouteDraft;
        var surfaces = new List<int>();
        foreach (var (x, z) in layout.River!.Path)
        {
            var surface = WaterSurface(grid, x, z);
            Assert.True(surface > 0, $"no water at the river waypoint ({x}, {z})");
            surfaces.Add(surface);
        }

        output.WriteLine("river surface from the waterfall to the lake: " + string.Join(" ", surfaces));
        for (var i = 1; i < surfaces.Count; i++)
        {
            Assert.True(surfaces[i] <= surfaces[i - 1], $"the water runs uphill at waypoint {i}: {surfaces[i - 1]} then {surfaces[i]}");
        }

        Assert.True(surfaces[0] >= 40, "the river starts high, at the foot of the falls");
        Assert.True(surfaces[^1] <= layout.WaterLevel + 2, "the river ends in the lake");

        // The lake is level at the world's water level; the waterfall is a sheet of water from the plateau's lip down to the pool.
        var lake = scene.Point("lake");
        Assert.Equal(layout.WaterLevel, WaterSurface(grid, lake.X, lake.Z));
        var fall = scene.Point("waterfall");
        Assert.Equal(layout.Waterfall!.Top, fall.Y);
        var waterBlocks = Enumerable.Range(0, grid.SizeY).Count(y => grid.GetBlock(fall.X, y, fall.Z) == BlockId.Water);
        Assert.True(waterBlocks >= 40, $"the falling water is {waterBlocks} blocks tall");
        Assert.Equal(BlockId.Water, grid.GetBlock(fall.X, fall.Y - 20, fall.Z));
    }

    [Fact]
    public void TheBridgeSpansTheRiverAndItsTwoLandingsAreLevel()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var west = scene.Point("bridge.west");
        var east = scene.Point("bridge.east");
        Assert.Equal(west.Y, east.Y);
        var deck = west.Y;
        var bridge = scene.Point("bridge");
        var decks = 0;
        for (var x = west.X; x <= east.X; x++)
        {
            if (grid.GetBlock(x, deck, bridge.Z) == BlockId.Bridge)
            {
                decks++;
            }
        }

        Assert.Equal(BlockId.Bridge, grid.GetBlock(bridge.X, deck, bridge.Z));
        Assert.Equal(BlockId.Water, grid.GetBlock(bridge.X, bridge.Y, bridge.Z)); // the river runs under the middle of the deck
        Assert.True(deck - bridge.Y >= 2, "the deck stands above the water");

        Assert.InRange(decks, 5, 13);
        Assert.Equal(BlockId.Path, grid.GetBlock(west.X, deck, bridge.Z));
        Assert.Equal(BlockId.Path, grid.GetBlock(east.X, deck, bridge.Z));
    }

    [Fact]
    public void ThePlayerCanWalkTheTrailAndTheRoadFromTheCampToTheVillageAndBackToTheRiver()
    {
        // Pathfind on the grid (a feet cell with head room, a solid cell under it, steps of up to one block up and three down, and no wading: water is
        // closed) from waypoint to waypoint of the layout's trail and road, then drive the real body along the cells. The bridge is the only dry way over.
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var layout = VoxelTestSupport.RouteDraft;
        var waypoints = new List<(int X, int Z)> { (scene.Spawn.Feet.X, scene.Spawn.Feet.Z) };
        foreach (var path in layout.Paths)
        {
            waypoints.AddRange(path.Via);
            var to = layout.Site(path.To)!;
            waypoints.Add((to.X, to.Z));
        }

        var sim = VoxelSimulation.Create(scene);
        var crossed = false;
        var totalCells = 0;
        for (var i = 1; i < waypoints.Count; i++)
        {
            var from = sim.Player.FeetBlock;
            var target = new BlockPos(waypoints[i].X, FeetAt(grid, waypoints[i].X, waypoints[i].Z), waypoints[i].Z);
            var path = WalkPath(grid, from, target);
            Assert.True(path is not null, $"no way from {from} to the waypoint {target}");
            totalCells += path.Count;
            crossed |= path.Any(p => grid.GetBlock(p.X, p.Y - 1, p.Z) == BlockId.Bridge);
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
        }

        output.WriteLine($"walked {totalCells} cells from the camp to the village, over the bridge: {crossed}");
        Assert.True(crossed, "the road crosses the river by the bridge");
        var village = scene.Point("village");
        Assert.True(Math.Abs(sim.Player.FeetBlock.X - village.X) <= 2 && Math.Abs(sim.Player.FeetBlock.Z - village.Z) <= 2, $"the walk ended at {sim.Player.FeetBlock}");
        Assert.True(sim.Player.Grounded);
    }

    [Fact]
    public void TheWalkingTimesAtTheBodysSpeedFitTheBrief()
    {
        // Straight-line length of each leg of the layout at the body's walking speed (294 units a tick), the way the brief's targets are given.
        var layout = VoxelTestSupport.RouteDraft;
        double Seconds(PathSpec path)
        {
            var points = new List<(int X, int Z)> { (layout.Site(path.From)!.X, layout.Site(path.From)!.Z) };
            points.AddRange(path.Via);
            points.Add((layout.Site(path.To)!.X, layout.Site(path.To)!.Z));
            double length = 0;
            for (var i = 1; i < points.Count; i++)
            {
                var dx = points[i].X - points[i - 1].X;
                var dz = points[i].Z - points[i - 1].Z;
                length += Math.Sqrt((dx * dx) + (dz * dz));
            }

            return length / ((double)VoxelPhysics.WalkSpeed * VoxelPhysics.TicksPerSecond / Units);
        }

        var trail = Seconds(layout.Paths.Single(p => p.Name == "trail"));
        var road = Seconds(layout.Paths.Single(p => p.Name == "road"));
        output.WriteLine($"camp.player to camp.guide by the trail: {trail:F1} s; camp.guide to the village by the road: {road:F1} s");
        Assert.InRange(trail, 40, 70);
        Assert.InRange(road, 30, 60);

        // The village to the river: the stall to the nearest water, over the plaza and along the road, is 10 to 25 seconds' walk.
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var village = scene.Point("village");
        var nearest = double.MaxValue;
        foreach (var (x, z) in layout.River!.Path)
        {
            nearest = Math.Min(nearest, Math.Sqrt(((x - village.X) * (x - village.X)) + ((z - village.Z) * (z - village.Z))) - layout.River.HalfWidth);
        }

        var toRiver = nearest / ((double)VoxelPhysics.WalkSpeed * VoxelPhysics.TicksPerSecond / Units);
        output.WriteLine($"the village to the nearest river water: {toRiver:F1} s");
        Assert.InRange(toRiver, 10, 25);
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
        Assert.True(scene.TreeCount > 1800, "the forest should be a forest");

        // The route: every path block, every column that holds a built block, and the named places.
        var width = grid.SizeX;
        var route = new HashSet<int>();
        foreach (var kind in new[]
        {
            BlockId.Path, BlockId.Planks, BlockId.Door, BlockId.Table, BlockId.Torch, BlockId.Stall, BlockId.Roof, BlockId.Fence, BlockId.Wall, BlockId.FireRing,
            BlockId.Sign, BlockId.Bridge, BlockId.Bush, BlockId.BerryBush, BlockId.Timber, BlockId.Lantern, BlockId.Crate, BlockId.AwningRed, BlockId.AwningWhite,
            BlockId.Canvas, BlockId.Chest, BlockId.Farmland, BlockId.SignTiendaA, BlockId.SignRio, BlockId.SignMontanaA,
        })
        {
            foreach (var p in grid.BlocksOfKind(kind))
            {
                route.Add((p.Z * width) + p.X);
            }
        }

        route.Add((scene.Spawn.Feet.Z * width) + scene.Spawn.Feet.X);
        foreach (var n in scene.Npcs)
        {
            route.Add((n.Feet.Z * width) + n.Feet.X);
        }

        foreach (var p in scene.Points.Values)
        {
            route.Add((p.Z * width) + p.X);
        }

        Assert.True(route.Count > 800);
        var margin = layout.Trees.Margin;
        bool Near(int x, int z, int distance)
        {
            for (var dz = -distance; dz <= distance; dz++)
            {
                for (var dx = -distance; dx <= distance; dx++)
                {
                    if (route.Contains(((z + dz) * width) + x + dx))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        foreach (var trunk in AllTrunks(grid))
        {
            Assert.False(Near(trunk.X, trunk.Z, margin - 1), $"trunk at {trunk} is within {margin - 1} columns of the route");
        }

        var leaves = grid.BlocksOfKind(BlockId.Leaves);
        leaves.AddRange(grid.BlocksOfKind(BlockId.SpruceLeaves));
        leaves.AddRange(grid.BlocksOfKind(BlockId.BirchLeaves));
        foreach (var leaf in leaves)
        {
            Assert.False(Near(leaf.X, leaf.Z, margin - 3), $"leaves at {leaf} are on the route");
        }
    }

    [Fact]
    public void TrunksKeepTheirSpacingAndTheForestIsDense()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var grid = scene.Grid;
        var trunks = new HashSet<(int X, int Z)>();
        foreach (var p in AllTrunks(grid))
        {
            trunks.Add((p.X, p.Z));
        }

        Assert.Equal(scene.TreeCount, trunks.Count);
        var spacing = VoxelTestSupport.RouteDraft.Trees.Spacing;
        foreach (var (x, z) in trunks)
        {
            for (var dz = -(spacing - 1); dz <= spacing - 1; dz++)
            {
                for (var dx = -(spacing - 1); dx <= spacing - 1; dx++)
                {
                    Assert.False((dx != 0 || dz != 0) && trunks.Contains((x + dx, z + dz)), $"two trunks are closer than {spacing} columns at ({x}, {z})");
                }
            }
        }

        // The forest is dense on the valley floor: about one tree in twenty columns of grass at the least, and none above the tree line.
        var treeLine = VoxelTestSupport.RouteDraft.Trees.TreeLine;
        Assert.All(trunks, t => Assert.True(Ground(grid, t.X, t.Z) <= treeLine, $"a tree above the tree line at {t}"));
        var grassOnTheFloor = 0;
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                var y = Ground(grid, x, z);
                if (y <= 45 && grid.GetBlock(x, y, z) == BlockId.Grass)
                {
                    grassOnTheFloor++;
                }
            }
        }

        output.WriteLine($"{trunks.Count} trees over {grassOnTheFloor} columns of grass at or under 45");
        Assert.True(trunks.Count * 20 > grassOnTheFloor, "the valley floor is a dense forest");
    }

    [Fact]
    public void ALayoutThatPutsAPropOnUnevenGroundIsRejected()
    {
        var json = File.ReadAllText(VoxelTestSupport.RouteDraftPath())
            .Replace("\"props\": [", "\"props\": [ {\"type\": \"house\", \"site\": \"village\", \"dx\": 24, \"dz\": -3, \"width\": 5, \"depth\": 5, \"facing\": \"west\"},", StringComparison.Ordinal);
        var layout = SceneLayout.Parse(json);
        var error = Assert.Throws<SceneLayoutException>(() => SceneBuilder.Build(layout, Seed));
        Assert.Contains("flat", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "schemaVersion")]
    [InlineData("not json", "not valid JSON")]
    [InlineData("{\"schemaVersion\": 1, \"name\": \"old\"}", "schemaVersion must be 2")]
    [InlineData("{\"schemaVersion\": 2, \"size\": {\"x\": 50, \"y\": 32, \"z\": 64}}", "multiple of 16")]
    public void MalformedLayoutsAreRejectedWithAMessage(string json, string fragment)
    {
        var error = Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json));
        Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"site\": \"village\"", "\"site\": \"nowhere\"", "no site 'nowhere'")]
    [InlineData("\"mode\": \"grade\"", "\"mode\": \"sideways\"", "mode must be grade or follow")]
    [InlineData("\"block\": \"path\"", "\"block\": \"cheese\"", "unknown block 'cheese'")]
    public void ALayoutThatNamesWhatDoesNotExistIsRejected(string find, string replacement, string fragment)
    {
        var json = File.ReadAllText(VoxelTestSupport.RouteDraftPath()).Replace(find, replacement, StringComparison.Ordinal);
        var error = Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json));
        Assert.Contains(fragment, error.Message, StringComparison.Ordinal);
    }

    /// <summary>The y of the top block of the ground in a column: the highest block that is not a tree, a roof or air.</summary>
    private static int Ground(VoxelGrid grid, int x, int z)
    {
        var y = grid.HighestBlock(x, z);
        while (y > 0 && IsNotGround(grid.GetBlock(x, y, z)))
        {
            y--;
        }

        return y;
    }

    // A tree of any species, the undergrowth (S14), a bush, water and air are on the ground, not the ground.
    private static bool IsNotGround(BlockId block) =>
        block is BlockId.Air or BlockId.Water or BlockId.Bush or BlockId.BerryBush
        || BlockDefinition.IsLeaves(block) || BlockDefinition.IsTrunk(block) || BlockDefinition.IsDecor(block);

    private static int TrunkBlocks(VoxelGrid grid) =>
        grid.BlocksOfKind(BlockId.Trunk).Count + grid.BlocksOfKind(BlockId.SpruceTrunk).Count + grid.BlocksOfKind(BlockId.BirchTrunk).Count;

    private static List<BlockPos> AllTrunks(VoxelGrid grid)
    {
        var trunks = grid.BlocksOfKind(BlockId.Trunk);
        trunks.AddRange(grid.BlocksOfKind(BlockId.SpruceTrunk));
        trunks.AddRange(grid.BlocksOfKind(BlockId.BirchTrunk));
        return trunks;
    }

    private static int WaterSurface(VoxelGrid grid, int x, int z)
    {
        for (var y = grid.SizeY - 1; y >= 0; y--)
        {
            if (grid.GetBlock(x, y, z) == BlockId.Water)
            {
                return y;
            }
        }

        return -1;
    }

    private static int FeetAt(VoxelGrid grid, int x, int z) => Ground(grid, x, z) + 1;

    private static double Distance(BlockPos a, BlockPos b) => Math.Sqrt(((a.X - b.X) * (double)(a.X - b.X)) + ((a.Z - b.Z) * (double)(a.Z - b.Z)));

    private static Dictionary<BlockId, int> CountBlocks(VoxelGrid grid)
    {
        var counts = Enum.GetValues<BlockId>().ToDictionary(b => b, _ => 0);
        foreach (var chunk in grid.Chunks)
        {
            if (chunk.IsEmpty)
            {
                continue;
            }

            foreach (var block in chunk.Blocks)
            {
                counts[block]++;
            }
        }

        return counts;
    }

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
            if (cell.X == target.X && cell.Z == target.Z)
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

                    var low = next.Y;
                    var high = Math.Max(next.Y + 1, cell.Y + 1);
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
