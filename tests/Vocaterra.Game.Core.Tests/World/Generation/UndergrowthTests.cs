using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;
using Xunit.Abstractions;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>
/// The look's part of the world (S14): the undergrowth as blocks that nothing collides with and the block ray looks through, the three tree species,
/// the meadows along the water, the sight cone that keeps the ledge's view open, and the tree and plant placement in the digests.
/// </summary>
public sealed class UndergrowthTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private static readonly BlockId[] Plants = [BlockId.TallGrass, BlockId.Fern, BlockId.Reeds, BlockId.YellowFlower, BlockId.WhiteFlower];

    private static readonly BlockId[] Trunks = [BlockId.Trunk, BlockId.SpruceTrunk, BlockId.BirchTrunk];

    private static readonly BlockId[] Leaves = [BlockId.Leaves, BlockId.SpruceLeaves, BlockId.BirchLeaves];

    private static int Ground(VoxelGrid grid, int x, int z)
    {
        var y = grid.HighestBlock(x, z);
        while (y > 0 && (grid.GetBlock(x, y, z) == BlockId.Air || BlockDefinition.IsDecor(grid.GetBlock(x, y, z)) || BlockDefinition.IsLeaves(grid.GetBlock(x, y, z)) || BlockDefinition.IsTrunk(grid.GetBlock(x, y, z))))
        {
            y--;
        }

        return y;
    }

    // ------------------------------------------------------------------ the plants as blocks

    [Fact]
    public void ThePlantsAreWalkThroughAndTheyAreTheOnlyDecor()
    {
        foreach (var plant in Plants)
        {
            var definition = BlockDefinition.Get(plant);
            Assert.False(definition.Solid, $"{plant} must not collide");
            Assert.True(definition.Decor);
            Assert.False(definition.Gatherable);
            Assert.True(definition.Transparent);
            Assert.True(BlockDefinition.IsDecor(plant));
        }

        Assert.Equal(Plants.Length + 4, BlockDefinition.All.Count(static d => d.Decor)); // the five plants, wheat and the stall's three goods (S15)
        Assert.Equal(Plants.Length, BlockDefinition.All.Count(static d => BlockDefinition.IsUndergrowth(d.Id)));
        Assert.False(BlockDefinition.IsDecor(BlockId.Bush)); // the plain bush and the berry bush stay targets
        Assert.False(BlockDefinition.IsDecor(BlockId.BerryBush));
        Assert.False(BlockDefinition.IsDecor((BlockId)999));
    }

    [Fact]
    public void ThePlayerWalksThroughUndergrowthAtFullSpeed()
    {
        var plain = FlatSim();
        var grassy = FlatSim(g =>
        {
            for (var z = 20; z <= 31; z++)
            {
                g.SetBlock(32, GroundTop + 1, z, BlockId.TallGrass);
                g.SetBlock(32, GroundTop + 2, z, BlockId.Reeds);
            }
        });
        Run(plain, 40, VoxelCommand.Move(0, -1));
        Run(grassy, 40, VoxelCommand.Move(0, -1));
        Assert.Equal(plain.Player.CenterZ, grassy.Player.CenterZ);
        Assert.True(grassy.Player.Grounded);
        Assert.False(grassy.Grid.IsSolid(32, GroundTop + 1, 25));
    }

    [Theory]
    [InlineData(BlockId.TallGrass)]
    [InlineData(BlockId.Fern)]
    [InlineData(BlockId.Reeds)]
    [InlineData(BlockId.YellowFlower)]
    [InlineData(BlockId.WhiteFlower)]
    public void TheBlockRaySkipsUndergrowthAndHitsWhatStandsBehindIt(BlockId plant)
    {
        // The eye is at layer 11 in the flat scene and looks north: a plant at (32, 11, 31) and (32, 11, 30), a stone at (32, 11, 28).
        var sim = FlatSim(g =>
        {
            g.SetBlock(32, 11, 31, plant);
            g.SetBlock(32, 11, 30, plant);
            g.SetBlock(32, 11, 28, BlockId.Stone);
        });
        var hit = sim.Target;
        Assert.NotNull(hit);
        Assert.Equal(new BlockPos(32, 11, 28), hit.Value.Block);
        Assert.Equal(BlockId.Stone, hit.Value.Id);

        // With nothing behind the plants the ray finds nothing at all (it does not stop on the grass).
        var alone = FlatSim(g => g.SetBlock(32, 11, 31, plant));
        Assert.Null(alone.Target);

        // Looking down at grass on the ground hits the ground under it, and the cell to place in is the plant's own cell.
        var floor = FlatSim(g => g.SetBlock(32, GroundTop + 1, 32, plant));
        floor.Step([VoxelCommand.Look(0, -Angles.MaxPitch)]);
        var below = floor.Target;
        Assert.NotNull(below);
        Assert.Equal(new BlockPos(32, GroundTop, 32), below.Value.Block);
        Assert.Equal(new BlockPos(32, GroundTop + 1, 32), below.Value.Adjacent);
    }

    [Fact]
    public void UndergrowthNeverBlocksGatheringTheBerryBushBehindIt()
    {
        // A berry bush two blocks ahead with tall grass in the cells between the eye and the bush.
        var bush = new BlockPos(32, GroundTop + 2, 30);
        var sim = FlatSim(g =>
        {
            g.SetBlock(bush.X, bush.Y, bush.Z, BlockId.BerryBush);
            g.SetBlock(32, GroundTop + 2, 31, BlockId.TallGrass);
            g.SetBlock(32, GroundTop + 2, 32, BlockId.Fern);
        });
        Assert.Equal(bush, sim.Target!.Value.Block);
        var ticks = BlockDefinition.Get(BlockId.BerryBush).GatherTicks;
        for (var i = 0; i < ticks; i++)
        {
            sim.Step([VoxelCommand.Interact(bush.X, bush.Y, bush.Z)]);
        }

        Assert.Equal(1, sim.Inventory.Count(ItemId.Berry));
        Assert.Equal(BlockId.Bush, sim.Grid.GetBlock(bush.X, bush.Y, bush.Z));
        Assert.Equal(BlockId.TallGrass, sim.Grid.GetBlock(32, GroundTop + 2, 31)); // the grass in front is untouched
    }

    [Fact]
    public void PlantsCannotBeGatheredAndABlockPlacedIntoTheirCellTakesTheirPlace()
    {
        var sim = FlatSim(g => g.SetBlock(32, GroundTop + 1, 30, BlockId.TallGrass));
        for (var i = 0; i < 90; i++)
        {
            sim.Step([VoxelCommand.Interact(32, GroundTop + 1, 30)]);
        }

        Assert.Equal(BlockId.TallGrass, sim.Grid.GetBlock(32, GroundTop + 1, 30));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));

        // A torch placed in the grass's cell (it is next to the ground) replaces it.
        sim.Inventory.Add(ItemId.Torch, 1);
        sim.Step([VoxelCommand.Place(ItemId.Torch, 32, GroundTop + 1, 30)]);
        Assert.Equal(BlockId.Torch, sim.Grid.GetBlock(32, GroundTop + 1, 30));
    }

    [Fact]
    public void EveryTrunkIsGatheredForWoodAndEveryLeafIsATransparentSolid()
    {
        foreach (var trunk in Trunks)
        {
            var definition = BlockDefinition.Get(trunk);
            Assert.True(definition.Solid);
            Assert.True(definition.Gatherable);
            Assert.Equal(ItemId.Wood, definition.Drop);
            Assert.True(BlockDefinition.IsTrunk(trunk));
        }

        foreach (var leaf in Leaves)
        {
            var definition = BlockDefinition.Get(leaf);
            Assert.True(definition.Solid && definition.Transparent);
            Assert.True(BlockDefinition.IsLeaves(leaf));
        }

        Assert.False(BlockDefinition.IsTrunk(BlockId.Leaves));
        Assert.False(BlockDefinition.IsLeaves(BlockId.Trunk));
    }

    [Fact]
    public void ASpruceTrunkIsGatheredLikeAnOakTrunk()
    {
        var sim = FlatSim(g => g.SetBlock(32, GroundTop + 2, 30, BlockId.SpruceTrunk));
        for (var i = 0; i < BlockDefinition.TrunkGatherTicks; i++)
        {
            sim.Step([VoxelCommand.Interact(32, GroundTop + 2, 30)]);
        }

        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, GroundTop + 2, 30));
    }

    [Fact]
    public void TheBlockIdsOfTheNewKindsAreAppendedAfterTheOldOnesAndStayBelowSixtyFour()
    {
        Assert.Equal(22, (int)BlockId.Snow); // the last kind before S14
        var ids = Enum.GetValues<BlockId>().Select(static b => (int)b).ToArray();
        Assert.Equal(Enumerable.Range(0, ids.Length), ids.Order());
        Assert.True(ids.Max() < 64, "the mesher's kind masks hold one bit per kind");
        Assert.Equal(BlockId.SpruceTrunk, (BlockId)23);
        Assert.Equal(BlockId.Cobble, (BlockId)33);
        foreach (var definition in BlockDefinition.All)
        {
            Assert.Equal(definition.Id, BlockDefinition.FromName(definition.Name)!.Id);
        }
    }

    // ------------------------------------------------------------------ the generated undergrowth

    [Fact]
    public void TheSceneHoldsTensOfThousandsOfPlantsOfEveryKindAndTheCountIsTheScenes()
    {
        var scene = BuildRoute(Seed);
        var counts = new Dictionary<BlockId, int>();
        foreach (var chunk in scene.Grid.Chunks)
        {
            foreach (var block in chunk.Blocks)
            {
                if (BlockDefinition.IsUndergrowth(block))
                {
                    counts[block] = counts.GetValueOrDefault(block) + 1;
                }
            }
        }

        output.WriteLine(string.Join(", ", counts.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")) + $"; scene says {scene.PlantCount}");
        Assert.Equal(counts.Values.Sum(), scene.PlantCount);
        Assert.True(scene.PlantCount > 20000, "tall grass and flowers everywhere");
        foreach (var plant in Plants)
        {
            Assert.True(counts.GetValueOrDefault(plant) > 300, $"{plant}: {counts.GetValueOrDefault(plant)}");
        }

        Assert.True(counts[BlockId.TallGrass] > counts[BlockId.Fern], "tall grass is the commonest plant");
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    public void PlantsStandOnGrassOrSandAndNeverInWaterOnStoneSnowOrAPath(ulong seed)
    {
        var scene = BuildRoute(seed);
        var grid = scene.Grid;
        var checkedPlants = 0;
        for (var z = 0; z < grid.SizeZ; z++)
        {
            for (var x = 0; x < grid.SizeX; x++)
            {
                var y = grid.HighestBlock(x, z);
                if (y < 2)
                {
                    continue;
                }

                // Walk down through the plant (and the second block of tall reeds) to the ground.
                var top = y;
                while (top > 1 && BlockDefinition.IsUndergrowth(grid.GetBlock(x, top, z)))
                {
                    Assert.True(top - y < 2);
                    top--;
                }

                if (top == y)
                {
                    continue;
                }

                checkedPlants++;
                var ground = grid.GetBlock(x, top, z);
                Assert.True(ground is BlockId.Grass or BlockId.Sand, $"a plant on {ground} at ({x}, {top + 1}, {z})");
                if (grid.GetBlock(x, y, z) is BlockId.Reeds)
                {
                    continue;
                }

                Assert.NotEqual(BlockId.Water, grid.GetBlock(x, top + 1, z));
            }
        }

        Assert.True(checkedPlants > 15000);
    }

    [Fact]
    public void NothingGrowsOnAPathOrBesideAPropAndTheEdgeIsThinner()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var margin = RouteDraft.Undergrowth.RouteMargin;
        var hard = new List<BlockPos>();
        foreach (var kind in new[] { BlockId.Path, BlockId.Planks, BlockId.Table, BlockId.Stall, BlockId.Wall, BlockId.Roof, BlockId.Fence, BlockId.FireRing, BlockId.Sign, BlockId.Bridge })
        {
            hard.AddRange(grid.BlocksOfKind(kind));
        }

        hard.AddRange(scene.Points.Values);
        Assert.True(hard.Count > 1000);
        var near = 0;
        var ring = 0;
        foreach (var p in hard)
        {
            for (var dz = -(margin + 1); dz <= margin + 1; dz++)
            {
                for (var dx = -(margin + 1); dx <= margin + 1; dx++)
                {
                    var x = p.X + dx;
                    var z = p.Z + dz;
                    if (!grid.InBounds(x, 0, z))
                    {
                        continue;
                    }

                    var cell = grid.GetBlock(x, grid.HighestBlock(x, z), z);
                    var within = Math.Abs(dx) <= margin && Math.Abs(dz) <= margin;
                    if (within && BlockDefinition.IsUndergrowth(cell))
                    {
                        // The path columns and the prop columns are never planted. A point's own column on open ground is a route cell too.
                        near++;
                    }
                    else if (!within && BlockDefinition.IsUndergrowth(cell))
                    {
                        ring++;
                    }
                }
            }
        }

        output.WriteLine($"plants within {margin}: {near}; in the ring beyond it: {ring}");
        Assert.Equal(0, near);
        Assert.True(ring > 0, "the ring one column further holds some plants (a third as many)");
    }

    [Fact]
    public void ReedsStandOnTheBanksWithinTwoColumnsOfTheWater()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var reeds = grid.BlocksOfKind(BlockId.Reeds);
        Assert.True(reeds.Count > 300);
        foreach (var p in reeds.Take(2000))
        {
            var found = false;
            for (var dz = -2; dz <= 2 && !found; dz++)
            {
                for (var dx = -2; dx <= 2 && !found; dx++)
                {
                    // The water is in the column at any height (the sheet of the falls stands high above the pool's bank).
                    for (var y = 0; y < grid.SizeY && !found; y++)
                    {
                        found = grid.GetBlock(p.X + dx, y, p.Z + dz) == BlockId.Water;
                    }
                }
            }

            Assert.True(found, $"reeds at {p} are not by the water");
        }
    }

    [Fact]
    public void TheForegroundOfTheLedgeHoldsFlowersAndNothingTallerSoTheValleyShows()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var vista = RouteDraft.Trees.VistaList.Single();
        var site = RouteDraft.Site(vista.Site)!;
        var forward = Angles.Direction(Angles.FromDegrees(vista.YawDegrees), 0);
        var cosHalf = Angles.Cos(Angles.FromDegrees(vista.HalfAngleDegrees));
        var flowers = 0;
        for (var z = site.Z - 40; z <= site.Z + 40; z++)
        {
            for (var x = site.X - 40; x <= site.X + 40; x++)
            {
                var dx = x - site.X;
                var dz = z - site.Z;
                var d = Math.Sqrt((dx * dx) + (dz * dz));
                if (d > 38 || d < 1)
                {
                    continue;
                }

                var dot = (((long)dx * forward.X) + ((long)dz * forward.Z)) / (double)Angles.One;
                if (dot / d < cosHalf / (double)Angles.One + 0.02)
                {
                    continue; // outside the cone, with a little margin
                }

                var y = grid.HighestBlock(x, z);
                var block = grid.GetBlock(x, y, z);
                Assert.False(block is BlockId.TallGrass or BlockId.Fern or BlockId.Reeds, $"{block} stands in the ledge's foreground at ({x}, {y}, {z})");
                if (block is BlockId.YellowFlower or BlockId.WhiteFlower)
                {
                    flowers++;
                }
            }
        }

        Assert.True(flowers > 30, $"the foreground has {flowers} flowers");
    }

    // ------------------------------------------------------------------ the forest

    [Fact]
    public void TheForestHasOaksSpruceAndBirchAndEachStandsWhereItBelongs()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var spec = RouteDraft.Trees;
        var oaks = 0;
        var spruces = 0;
        var birches = 0;
        var lowSpruces = 0;
        var highOaks = 0;
        var lowest = int.MaxValue;
        foreach (var kind in Trunks)
        {
            foreach (var p in grid.BlocksOfKind(kind))
            {
                var ground = Ground(grid, p.X, p.Z);
                if (grid.GetBlock(p.X, p.Y - 1, p.Z) == kind)
                {
                    continue; // count each trunk once, at its lowest block
                }

                switch (kind)
                {
                    case BlockId.Trunk:
                        oaks++;
                        if (ground > spec.SpruceFrom + 20)
                        {
                            highOaks++;
                        }

                        break;
                    case BlockId.SpruceTrunk:
                        spruces++;
                        lowest = Math.Min(lowest, ground);
                        if (ground < spec.SpruceFrom)
                        {
                            lowSpruces++;
                        }

                        break;
                    default:
                        birches++;
                        Assert.True(ground < spec.SpruceFrom + 20, "birches stand in the lowlands");
                        break;
                }
            }
        }

        output.WriteLine($"{oaks} oaks, {spruces} spruces (the lowest on ground {lowest}), {birches} birches; the scene counts {scene.TreeCount} trees");
        Assert.Equal(scene.TreeCount, oaks + spruces + birches);
        Assert.True(oaks > 600 && spruces > 300 && birches > 80, "all three species make a forest");
        Assert.True(oaks > spruces && oaks > birches * 3, "the valley is mostly oaks, with a few birches");
        Assert.Equal(0, lowSpruces); // no spruce below where they start
        Assert.Equal(0, highOaks); // and no oak left among the high spruces
    }

    [Fact]
    public void NoBirchStandsNearASiteAndTheGuidesClearingKeepsItsOaks()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var away = RouteDraft.Trees.BirchAway;
        foreach (var p in grid.BlocksOfKind(BlockId.BirchTrunk))
        {
            foreach (var site in RouteDraft.Sites.Where(static s => s.Radius > 0))
            {
                var dx = p.X - site.X;
                var dz = p.Z - site.Z;
                Assert.True((dx * dx) + (dz * dz) > away * away, $"a birch at {p} is within {away} of {site.Name}");
            }
        }

        // The four trunks of the oak the scripted route gathers at (153, 42 to 45, 186) are oak, as they were in S13.
        for (var y = 42; y <= 45; y++)
        {
            Assert.Equal(BlockId.Trunk, grid.GetBlock(153, y, 186));
        }
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    public void TheCrownsAreBigAndRoundForOaksAndTieredConesForSpruces(ulong seed)
    {
        var scene = BuildRoute(seed);
        var grid = scene.Grid;
        var oakWidths = new List<int>();
        var spruceHeights = new List<int>();
        var rounds = 0;
        foreach (var p in grid.BlocksOfKind(BlockId.Trunk).Where(p => grid.GetBlock(p.X, p.Y - 1, p.Z) != BlockId.Trunk).Take(300))
        {
            var top = p.Y;
            while (grid.GetBlock(p.X, top + 1, p.Z) == BlockId.Trunk)
            {
                top++;
            }

            // The widest layer of leaves in the crown.
            var widest = 0;
            for (var y = top - 1; y <= top + 3; y++)
            {
                var width = 0;
                for (var dx = -3; dx <= 3; dx++)
                {
                    if (grid.GetBlock(p.X + dx, y, p.Z) == BlockId.Leaves)
                    {
                        width++;
                    }
                }

                widest = Math.Max(widest, width);
            }

            oakWidths.Add(widest);
            if (grid.GetBlock(p.X, top + 3, p.Z) == BlockId.Leaves)
            {
                rounds++; // a crown that reaches three layers over the trunk's top
            }
        }

        foreach (var p in grid.BlocksOfKind(BlockId.SpruceTrunk).Where(p => grid.GetBlock(p.X, p.Y - 1, p.Z) != BlockId.SpruceTrunk).Take(300))
        {
            var top = p.Y;
            while (grid.GetBlock(p.X, top + 1, p.Z) == BlockId.SpruceTrunk)
            {
                top++;
            }

            spruceHeights.Add(top - p.Y + 2);
            Assert.Equal(BlockId.SpruceLeaves, grid.GetBlock(p.X, top + 1, p.Z)); // the tip stands on the trunk
        }

        output.WriteLine($"oak crowns: mean widest {oakWidths.Average():F1}, {rounds} of {oakWidths.Count} reach the full height; spruces: mean height {spruceHeights.Average():F1}");
        Assert.True(oakWidths.Average() >= 5.0, "the oaks have large crowns (at least five blocks across at the widest, on average)");
        Assert.True(spruceHeights.Average() >= 9, "spruces are tall");
        Assert.True(rounds > oakWidths.Count / 2);
    }

    [Fact]
    public void TheShoreAndTheVillageAreMeadowsWithOnlyAFewTrees()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var spec = RouteDraft.Trees;
        var village = RouteDraft.Site("village")!;
        var near = 0;
        var far = 0;
        var nearArea = 0;
        var farArea = 0;
        foreach (var kind in Trunks)
        {
            foreach (var p in grid.BlocksOfKind(kind).Where(p => grid.GetBlock(p.X, p.Y - 1, p.Z) != kind))
            {
                var d = Math.Sqrt(((p.X - village.X) * (p.X - village.X)) + ((p.Z - village.Z) * (p.Z - village.Z)));
                if (d < spec.ClearingList[0].Radius)
                {
                    near++;
                }
                else if (d < spec.ClearingList[0].Radius + 60)
                {
                    far++;
                }
            }
        }

        // The area of the two rings, to compare the density.
        nearArea = (int)(Math.PI * spec.ClearingList[0].Radius * spec.ClearingList[0].Radius);
        farArea = (int)(Math.PI * (Math.Pow(spec.ClearingList[0].Radius + 60, 2) - Math.Pow(spec.ClearingList[0].Radius, 2)));
        var inside = near / (double)nearArea;
        var outside = far / (double)farArea;
        output.WriteLine($"trees per column: {inside:F4} inside the village meadow, {outside:F4} in the forest round it");
        Assert.True(inside * 5 < outside, "the village stands in a meadow");

        // The banks of the river: the density within the shore clearing is a fraction of the density a little further out.
        var water = new HashSet<(int, int)>();
        foreach (var (x, z) in RouteDraft.River!.Path)
        {
            water.Add((x, z));
        }

        int Distance(int x, int z) => water.Min(w => Math.Max(Math.Abs(w.Item1 - x), Math.Abs(w.Item2 - z)));
        int onBank = 0, inForest = 0;
        foreach (var p in grid.BlocksOfKind(BlockId.Trunk).Where(p => grid.GetBlock(p.X, p.Y - 1, p.Z) != BlockId.Trunk && p.Z is > 150 and < 280))
        {
            var d = Distance(p.X, p.Z);
            if (d <= spec.ShoreClear)
            {
                onBank++;
            }
            else if (d <= spec.ShoreClear + 25)
            {
                inForest++;
            }
        }

        output.WriteLine($"oaks within {spec.ShoreClear} of the river's line: {onBank}; in the 25 columns beyond: {inForest}");
        Assert.True(onBank * 8 < inForest, "the banks are meadow");
    }

    [Fact]
    public void NoCrownInTheLedgesSightConeStandsAboveTheSightLine()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var vista = RouteDraft.Trees.VistaList.Single();
        var site = RouteDraft.Site(vista.Site)!;
        var forward = Angles.Direction(Angles.FromDegrees(vista.YawDegrees), 0);
        var cosHalf = Angles.Cos(Angles.FromDegrees(vista.HalfAngleDegrees));
        var down = Angles.FromDegrees(vista.DownDegrees);
        var tan = (double)Angles.Sin(down) / Angles.Cos(down);
        var eye = (site.Y ?? 0) + 3;
        var inside = 0;
        foreach (var kind in Trunks)
        {
            foreach (var p in grid.BlocksOfKind(kind))
            {
                var dx = p.X - site.X;
                var dz = p.Z - site.Z;
                var d = Math.Sqrt((dx * dx) + (dz * dz));
                if (d > vista.Radius)
                {
                    continue;
                }

                var dot = (((long)dx * forward.X) + ((long)dz * forward.Z)) / (double)Angles.One;
                if (d > 0 && dot / d < (cosHalf / (double)Angles.One) + 0.02)
                {
                    continue;
                }

                inside++;
                var ground = Ground(grid, p.X, p.Z);

                // The trunk's own height is a lower bound of the crown's: the trunk top must be under the line (the crowns reach higher, so the check is a bit loose).
                Assert.True(p.Y <= eye - (d * tan) + 0.5, $"a trunk block at {p} (ground {ground}) stands above the sight line from the ledge ({eye - (d * tan):F1} at {d:F0} blocks)");
            }
        }

        output.WriteLine($"{inside} trunk blocks inside the cone, all under the sight line");
    }

    [Fact]
    public void TheSpawnLooksDownOverTheValleyAndTheLayoutRejectsAWildPitch()
    {
        var scene = BuildRoute(Seed);
        Assert.True(scene.Spawn.Pitch < 0, "the player wakes looking down over the valley");
        Assert.InRange(scene.Spawn.Pitch, -Angles.MaxPitch, 0);
        var json = File.ReadAllText(RouteDraftPath()).Replace("\"pitch\": -10", "\"pitch\": -85", StringComparison.Ordinal);
        Assert.Contains("pitch", Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(json)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLayoutRejectsAVistaWithoutAHeightAndAClearingAtAnUnknownSite()
    {
        var json = File.ReadAllText(RouteDraftPath());
        var noHeight = json.Replace("\"site\": \"camp.player\", \"yaw\": 135", "\"site\": \"village\", \"yaw\": 135", StringComparison.Ordinal);
        Assert.Contains("needs a y", Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(noHeight.Replace("\"village\": { \"x\": 292, \"z\": 218, \"y\": 37,", "\"village\": { \"x\": 292, \"z\": 218,", StringComparison.Ordinal))).Message, StringComparison.Ordinal);
        var unknown = json.Replace("\"clearings\": [ { \"site\": \"village\"", "\"clearings\": [ { \"site\": \"nowhere\"", StringComparison.Ordinal);
        Assert.Contains("nowhere", Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(unknown)).Message, StringComparison.Ordinal);
        var percent = json.Replace("\"grass\": 100", "\"grass\": 140", StringComparison.Ordinal);
        Assert.Contains("percents", Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(percent)).Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ determinism

    [Fact]
    public void TheForestAndTheUndergrowthComeFromTheSeedAndNothingElse()
    {
        var a = BuildRoute(Seed);
        var b = BuildRoute(Seed);
        Assert.Equal(a.PlantCount, b.PlantCount);
        Assert.Equal(a.TreeCount, b.TreeCount);
        Assert.Equal(a.Grid.ComputeDigestHex(), b.Grid.ComputeDigestHex());
        var other = BuildRoute(7);
        Assert.NotEqual(a.PlantCount, other.PlantCount);
        Assert.NotEqual(a.Grid.ComputeDigestHex(), other.Grid.ComputeDigestHex());
    }

    [Fact]
    public void TheLowRockOfTheForestIsMossyAndTheHighCragsAreBare()
    {
        var scene = BuildRoute(Seed);
        var grid = scene.Grid;
        var mossy = grid.BlocksOfKind(BlockId.MossyStone);
        Assert.True(mossy.Count > 1000);
        Assert.True(mossy.Max(static p => p.Y) < RouteDraft.Terrain.RockLine, "mossy stone stays below the rock line");
        var bareHigh = grid.BlocksOfKind(BlockId.Stone).Count(p => p.Y > RouteDraft.Terrain.RockLine + 10);
        Assert.True(bareHigh > 10000);
        Assert.DoesNotContain(mossy, p => p.Y > RouteDraft.Terrain.RockLine + 10);
    }
}
