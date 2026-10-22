using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Simulation.Tests.Voxel;

/// <summary>
/// The generator's parts on small 64 x 32 x 64 worlds (S13): the integer helpers, the noise, the polylines, and then the sites, stamps, paving, river,
/// lake, graded path and waterfall of a layout, each on a world small enough to build in a few milliseconds.
/// </summary>
public sealed class TerrainTests
{
    private const string FlatRelief = "\"9999\", \"9999\", \"9999\", \"9999\"";

    // A 64 x 32 x 64 layout: flat valley floor at 36 would not fit, so the height ceiling is 124 of 128; the world is 64 x 128 x 64.
    private static string Mini(
        string relief = FlatRelief,
        string sites = "\"a\": { \"x\": 16, \"z\": 16, \"y\": 36, \"radius\": 6, \"blend\": 4 }, \"b\": { \"x\": 48, \"z\": 48, \"y\": 36, \"radius\": 6, \"blend\": 4 }",
        string extra = "",
        string props = "",
        string spawn = "{ \"site\": \"a\", \"facing\": 90 }") =>
        "{ \"schemaVersion\": 2, \"name\": \"mini\", \"size\": { \"x\": 64, \"y\": 128, \"z\": 64 }, \"waterLevel\": 30,"
        + $" \"terrain\": {{ \"relief\": [{relief}], \"valley\": 36 }},"
        + $" \"sites\": {{ {sites} }}, {extra} \"props\": [ {props} ], \"npcs\": [], \"spawn\": {spawn} }}";

    private static Scene Build(string json, ulong seed = 1) => SceneBuilder.Build(SceneLayout.Parse(json), seed);

    private static int Ground(VoxelGrid grid, int x, int z)
    {
        var y = grid.HighestBlock(x, z);
        while (y > 0 && grid.GetBlock(x, y, z) is BlockId.Air or BlockId.Water)
        {
            y--;
        }

        return y;
    }

    // ------------------------------------------------------------------ integer helpers

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(2L)]
    [InlineData(15L)]
    [InlineData(16L)]
    [InlineData(17L)]
    [InlineData(1_000_000L)]
    [InlineData(2_147_483_647L)]
    [InlineData(4_611_686_014_132_420_609L)]
    public void TheIntegerSquareRootIsTheFloorOfTheRoot(long value)
    {
        var root = IntMath.ISqrt(value);
        Assert.True(root * root <= value);
        Assert.True((root + 1) * (root + 1) > value);
        Assert.Equal(0, IntMath.ISqrt(-5));
    }

    [Theory]
    [InlineData(7, 4, 1, 3)]
    [InlineData(-1, 4, -1, 3)]
    [InlineData(-4, 4, -1, 0)]
    [InlineData(-5, 4, -2, 3)]
    [InlineData(0, 4, 0, 0)]
    public void FloorDivisionAndModuloRoundTowardsMinusInfinity(int a, int b, int quotient, int remainder)
    {
        Assert.Equal(quotient, IntMath.DivDown(a, b));
        Assert.Equal(remainder, IntMath.Mod(a, b));
        Assert.Equal(a, (IntMath.DivDown(a, b) * b) + IntMath.Mod(a, b));
    }

    [Fact]
    public void SmoothAndRampRunFromZeroToOneWithoutOvershootAndInEitherDirection()
    {
        Assert.Equal(0, IntMath.Smooth(-50));
        Assert.Equal(IntMath.One, IntMath.Smooth(IntMath.One + 50));
        var last = -1;
        for (var t = 0; t <= IntMath.One; t += 64)
        {
            var s = IntMath.Smooth(t);
            Assert.True(s >= last && s is >= 0 and <= IntMath.One);
            last = s;
        }

        Assert.Equal(IntMath.One / 2, IntMath.Smooth(IntMath.One / 2));
        Assert.Equal(0, IntMath.Ramp(0, 0, 100));
        Assert.Equal(IntMath.One, IntMath.Ramp(100, 0, 100));
        Assert.Equal(IntMath.One, IntMath.Ramp(0, 100, 0)); // the from may be the larger: the weight rises as the value falls
        Assert.Equal(0, IntMath.Ramp(100, 100, 0));
        Assert.Equal(IntMath.One, IntMath.Ramp(5, 5, 5));
        Assert.Equal(15, IntMath.Lerp(10, 20, IntMath.One / 2));
    }

    [Fact]
    public void ACatmullRomStepNeverLeavesTheRangeOfItsTwoMiddlePoints()
    {
        for (var t = 0; t <= IntMath.One; t += 128)
        {
            var v = IntMath.CatmullRom(0, 100, 600, 100, t); // would overshoot
            Assert.InRange(v, 100, 600);
            var w = IntMath.CatmullRom(900, 600, 100, 0, t);
            Assert.InRange(w, 100, 600);
        }

        Assert.Equal(100, IntMath.CatmullRom(0, 100, 600, 700, 0));
        Assert.Equal(600, IntMath.CatmullRom(0, 100, 600, 700, IntMath.One));
    }

    // ------------------------------------------------------------------ noise

    [Fact]
    public void TheNoiseIsDeterministicInRangeAndSmooth()
    {
        var a = new TerrainNoise(42, "terrain");
        var b = new TerrainNoise(42, "terrain");
        var other = new TerrainNoise(43, "terrain");
        var differing = 0;
        var biggestStep = 0;
        for (var x = -50; x < 200; x++)
        {
            Assert.Equal(a.Hills(x, 7), b.Hills(x, 7));
            Assert.InRange(a.Hills(x, 7), -4096, 4095);
            Assert.InRange(a.Value(0, x, 7, 16), 0, 4095);
            if (a.Hills(x, 7) != other.Hills(x, 7))
            {
                differing++;
            }

            biggestStep = Math.Max(biggestStep, Math.Abs(a.Value(2, x + 1, 7, 16) - a.Value(2, x, 7, 16)));
        }

        Assert.True(differing > 200, "another seed gives other hills");
        Assert.True(biggestStep < 700, $"sixteen-block noise jumps {biggestStep} between neighbours");
        Assert.NotEqual(new TerrainNoise(42, "forest").Hills(3, 3), a.Hills(3, 3)); // the pass name is part of the stream
        Assert.InRange(a.Column(1, 5, 9), 0, 4095);
    }

    // ------------------------------------------------------------------ polylines

    [Fact]
    public void ALineKnowsItsLengthAndHowFarEachColumnIsFromIt()
    {
        var line = Polyline.Straight([(10, 10), (30, 10), (30, 40)]);
        Assert.Equal((20 + 30) * 16L, line.Length16);
        var dist = new int[64 * 64];
        Array.Fill(dist, int.MaxValue);
        var along = new int[64 * 64];
        var segment = new int[64 * 64];
        line.Rasterize(5, 64, 64, dist, along, segment);
        Assert.Equal(0, dist[(10 * 64) + 10]); // on the line
        Assert.Equal(0, along[(10 * 64) + 10]);
        Assert.Equal(16 * 16, along[(10 * 64) + 26] - along[(10 * 64) + 10]);
        Assert.Equal(3 * 16, dist[(13 * 64) + 20]); // three blocks below the first leg
        Assert.Equal(0, segment[(10 * 64) + 20]);
        Assert.Equal(1, segment[(30 * 64) + 30]);
        Assert.Equal(int.MaxValue, dist[(60 * 64) + 60]); // out of reach: untouched
    }

    [Fact]
    public void ACurveStaysInsideTheBoxOfEachPairOfWaypointsAndPassesThroughThem()
    {
        var curve = Polyline.Curve([(0, 0), (20, 30), (40, 10), (60, 40)], 4);
        Assert.True(curve.Count > 20);
        Assert.Equal((0 * 16) + 8, curve.X16[0]);
        Assert.Equal((60 * 16) + 8, curve.X16[^1]);
        Assert.Contains((40 * 16) + 8, curve.X16);
        for (var i = 1; i < curve.Count; i++)
        {
            Assert.True(curve.Cum16[i] > curve.Cum16[i - 1]);
        }
    }

    // ------------------------------------------------------------------ sites, stamps, paving

    [Fact]
    public void ASiteLevelsTheGroundToItsHeightAndSlopesIntoTheLandAroundIt()
    {
        var hills = "\"9ooo\", \"oooo\", \"oooo\", \"oooo\"";
        var scene = Build(Mini(hills, sites: "\"a\": { \"x\": 32, \"z\": 32, \"y\": 70, \"radius\": 8, \"blend\": 10 }", spawn: "{ \"site\": \"a\", \"facing\": 0 }"));
        var grid = scene.Grid;
        for (var z = 24; z <= 40; z++)
        {
            for (var x = 24; x <= 40; x++)
            {
                if (((x - 32) * (x - 32)) + ((z - 32) * (z - 32)) <= 64)
                {
                    Assert.Equal(70, Ground(grid, x, z));
                    Assert.Equal(BlockId.Grass, grid.GetBlock(x, 70, z));
                }
            }
        }

        // The slope joins the level ground to the land without a step of more than two blocks between neighbours near the disc.
        for (var x = 40; x < 52; x++)
        {
            Assert.InRange(Math.Abs(Ground(grid, x + 1, 32) - Ground(grid, x, 32)), 0, 5);
        }

        Assert.Equal(new BlockPos(32, 70, 32), scene.Point("a"));
        Assert.Equal(new BlockPos(32, 71, 32), scene.Spawn.Feet);
    }

    [Fact]
    public void AStampIsDrawnTurnedToFaceASideOnLevelGround()
    {
        const string Stamps = "\"stamps\": { \"L\": { \"legend\": { \"A\": \"roof\", \"B\": \"planks\", \"C\": \"wall\" }, \"layers\": [[\"AB \", \"C  \"], [\"  A\", \"   \"]] } },";
        string Props(string facing) => $"{{ \"type\": \"stamp\", \"stamp\": \"L\", \"site\": \"a\", \"dx\": 0, \"dz\": 0, \"facing\": \"{facing}\", \"name\": \"s\" }}";

        const string SpawnB = "{ \"site\": \"b\", \"facing\": 90 }";
        var north = Build(Mini(extra: Stamps, props: Props("north"), spawn: SpawnB));
        var g = north.Grid;
        var h = Ground(Build(Mini()).Grid, 16, 16);
        Assert.Equal(BlockId.Roof, g.GetBlock(16, h + 1, 16)); // A at (0, 0)
        Assert.Equal(BlockId.Planks, g.GetBlock(17, h + 1, 16)); // B at (1, 0)
        Assert.Equal(BlockId.Wall, g.GetBlock(16, h + 1, 17)); // C at (0, 1)
        Assert.Equal(BlockId.Roof, g.GetBlock(18, h + 2, 16)); // the second layer's A at (2, 0)

        // Turned east (a quarter turn clockwise): the drawing's row becomes a column. Width 3 and depth 2 swap.
        var east = Build(Mini(extra: Stamps, props: Props("east"), spawn: SpawnB)).Grid;
        Assert.Equal(BlockId.Roof, east.GetBlock(17, h + 1, 16)); // A: (c, r) = (0, 0) goes to (depth - 1 - r, c) = (1, 0)
        Assert.Equal(BlockId.Planks, east.GetBlock(17, h + 1, 17)); // B: (1, 0) goes to (1, 1)
        Assert.Equal(BlockId.Wall, east.GetBlock(16, h + 1, 16)); // C: (0, 1) goes to (0, 0)
        Assert.Equal(BlockId.Roof, east.GetBlock(17, h + 2, 18)); // the upper A: (2, 0) goes to (1, 2)

        var south = Build(Mini(extra: Stamps, props: Props("south"), spawn: SpawnB)).Grid;
        Assert.Equal(BlockId.Roof, south.GetBlock(18, h + 1, 17)); // A: (0, 0) goes to (width - 1 - c, depth - 1 - r) = (2, 1)
        Assert.Equal(BlockId.Wall, south.GetBlock(18, h + 1, 16)); // C: (0, 1) goes to (2, 0)
        var west = Build(Mini(extra: Stamps, props: Props("west"), spawn: SpawnB)).Grid;
        Assert.Equal(BlockId.Roof, west.GetBlock(16, h + 1, 18)); // A: (0, 0) goes to (r, width - 1 - c) = (0, 2)
        Assert.Equal(BlockId.Wall, west.GetBlock(17, h + 1, 18)); // C: (0, 1) goes to (1, 2)

        Assert.True(north.Points.ContainsKey("s"));
    }

    [Fact]
    public void AStampThatIsNotOnLevelGroundOrNotDefinedIsRejected()
    {
        const string Stamps = "\"stamps\": { \"L\": { \"legend\": { \"A\": \"roof\" }, \"layers\": [[\"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\", \"AAAAAAAA\"]] } },";
        var unlevel = Mini("\"9ooo\", \"oooo\", \"oooo\", \"oooo\"", extra: Stamps, props: "{ \"type\": \"stamp\", \"stamp\": \"L\", \"site\": \"a\", \"dx\": 22, \"dz\": 22 }");
        Assert.Contains("flat", Assert.Throws<SceneLayoutException>(() => Build(unlevel)).Message, StringComparison.Ordinal);
        var unknown = Mini(extra: Stamps, props: "{ \"type\": \"stamp\", \"stamp\": \"M\", \"site\": \"a\" }");
        Assert.Contains("no stamp 'M'", Assert.Throws<SceneLayoutException>(() => Build(unknown)).Message, StringComparison.Ordinal);
        var badLegend = Mini(extra: "\"stamps\": { \"L\": { \"legend\": { \"A\": \"roof\" }, \"layers\": [[\"AB\"]] } },");
        Assert.Contains("'B' is not in the legend", Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(badLegend)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PavingLaysItsBlockOnTheTopOfTheGroundAndKeepsTheHeights()
    {
        var plain = Build(Mini());
        var paved = Build(Mini(props: "{ \"type\": \"paving\", \"site\": \"a\", \"dx\": -2, \"dz\": -2, \"width\": 5, \"depth\": 3, \"block\": \"planks\" }"));
        for (var z = 14; z < 17; z++)
        {
            for (var x = 14; x < 19; x++)
            {
                var h = Ground(plain.Grid, x, z);
                Assert.Equal(h, Ground(paved.Grid, x, z));
                Assert.Equal(BlockId.Planks, paved.Grid.GetBlock(x, h, z));
            }
        }

        Assert.Equal(BlockId.Grass, paved.Grid.GetBlock(19, Ground(paved.Grid, 19, 16), 16));
    }

    // ------------------------------------------------------------------ water

    [Fact]
    public void ARiverIsCutIntoTheGroundAndNeverRunsUphill()
    {
        // A flat valley with noise: the river crosses the world; its surface follows the ground down, never up, and stays at the lake's level or above.
        var river = "\"river\": { \"halfWidth\": 2, \"depth\": 2, \"bank\": 3, \"path\": [[2, 32], [20, 30], [40, 34], [61, 32]] },";
        var scene = Build(Mini(extra: river));
        var grid = scene.Grid;
        var surfaces = new List<int>();
        foreach (var (x, z) in new[] { (2, 32), (20, 30), (40, 34), (61, 32) })
        {
            var surface = Enumerable.Range(0, grid.SizeY).Last(y => grid.GetBlock(x, y, z) == BlockId.Water);
            surfaces.Add(surface);
            Assert.True(surface >= 30, "never under the world's water level");
            Assert.True(Ground(grid, x, z + 3) >= surface + 1 && Ground(grid, x, z - 3) >= surface + 1, "the banks stand above the water");
            Assert.Equal(BlockId.Sand, grid.GetBlock(x, surface - 3, z));
        }

        for (var i = 1; i < surfaces.Count; i++)
        {
            Assert.True(surfaces[i] <= surfaces[i - 1], string.Join(" ", surfaces));
        }

        // The river is as wide as it was asked: water at the middle, none beyond halfWidth + 1.
        Assert.Equal(BlockId.Water, grid.GetBlock(2, surfaces[0], 32));
        Assert.Equal(BlockId.Water, grid.GetBlock(2, surfaces[0], 34));
        Assert.NotEqual(BlockId.Water, grid.GetBlock(2, surfaces[0], 36));
    }

    [Fact]
    public void ALakeFillsABasinToTheWorldsWaterLevelAndHasASandyShore()
    {
        var scene = Build(Mini(sites: "\"a\": { \"x\": 16, \"z\": 16, \"radius\": 3, \"blend\": 3 }, \"lake\": { \"x\": 40, \"z\": 36 }", extra: "\"lake\": { \"site\": \"lake\", \"rx\": 12, \"rz\": 9, \"depth\": 4 },"));
        var grid = scene.Grid;
        Assert.Equal(BlockId.Water, grid.GetBlock(40, 30, 36));
        Assert.Equal(BlockId.Air, grid.GetBlock(40, 31, 36));
        Assert.True(Ground(grid, 40, 36) <= 27, "the bed is under the surface");
        Assert.Equal(BlockId.Sand, grid.GetBlock(40, Ground(grid, 40, 36), 36));
        var waterColumns = 0;
        for (var z = 20; z < 52; z++)
        {
            for (var x = 24; x < 60; x++)
            {
                if (grid.GetBlock(x, 30, z) == BlockId.Water)
                {
                    waterColumns++;
                }
            }
        }

        Assert.InRange(waterColumns, 180, 480); // an ellipse of 12 x 9 is about 340 columns, and the shoreline wanders
        Assert.Equal(BlockId.Grass, grid.GetBlock(8, Ground(grid, 8, 8), 8));
    }

    [Fact]
    public void AWaterfallIsASheetOfWaterFromAPondOnThePlateauDownIntoThePool()
    {
        var river = "\"river\": { \"halfWidth\": 2, \"depth\": 2, \"bank\": 3, \"path\": [[32, 34], [32, 44], [34, 58]] },";
        var fall = "\"waterfall\": { \"site\": \"fall\", \"top\": 60, \"width\": 3, \"plateau\": 8, \"pool\": 5 },";
        var scene = Build(Mini(sites: "\"a\": { \"x\": 8, \"z\": 8, \"y\": 36, \"radius\": 3, \"blend\": 3 }, \"fall\": { \"x\": 32, \"z\": 28 }", extra: river + fall));
        var grid = scene.Grid;

        // The plateau is raised to the top behind the lip; the cliff's face is one blocks-wide step from the plateau to the pool's bank.
        Assert.InRange(Ground(grid, 32, 20), 59, 61);
        Assert.InRange(Ground(grid, 30, 22), 59, 61);
        var water = Enumerable.Range(0, grid.SizeY).Where(y => grid.GetBlock(32, y, 28) == BlockId.Water).ToList();
        Assert.Equal(60, water.Max());
        Assert.True(water.Count >= 20, $"{water.Count} blocks of falling water");
        Assert.Equal(water.Count, water.Max() - water.Min() + 1); // no gap in the sheet
        foreach (var x in new[] { 31, 32, 33 })
        {
            Assert.Equal(BlockId.Water, grid.GetBlock(x, 60, 26)); // the pond above the lip
        }

        Assert.Equal(60, scene.Point("fall").Y);
    }

    [Fact]
    public void AGradedPathChangesItsHeightEvenlyFromOneSiteToTheOtherAndNeverStepsMoreThanABlock()
    {
        var hills = "\"9999\", \"9oo9\", \"9oo9\", \"9999\"";
        var sites = "\"a\": { \"x\": 8, \"z\": 8, \"y\": 60, \"radius\": 4, \"blend\": 4 }, \"b\": { \"x\": 56, \"z\": 56, \"y\": 36, \"radius\": 4, \"blend\": 4 }";
        var path = "\"paths\": [ { \"name\": \"p\", \"from\": \"a\", \"to\": \"b\", \"mode\": \"grade\", \"halfWidth\": 1, \"via\": [[56, 8], [8, 56]] } ],";
        var scene = Build(Mini(hills, sites, path, spawn: "{ \"site\": \"a\", \"facing\": 0 }"));
        var grid = scene.Grid;

        // Walk the polyline a to (56,8) to (8,56) to b: the path blocks' heights fall by at most one a column and end at the second site's height.
        var points = new List<(int X, int Z)>();
        void Leg((int X, int Z) from, (int X, int Z) to)
        {
            var steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Z - from.Z));
            for (var i = 0; i <= steps; i++)
            {
                points.Add((from.X + ((to.X - from.X) * i / steps), from.Z + ((to.Z - from.Z) * i / steps)));
            }
        }

        Leg((8, 8), (56, 8));
        Leg((56, 8), (8, 56));
        Leg((8, 56), (56, 56));
        var heights = points.Select(p => Ground(grid, p.X, p.Z)).ToList();
        Assert.All(points, p => Assert.Equal(BlockId.Path, grid.GetBlock(p.X, Ground(grid, p.X, p.Z), p.Z)));
        Assert.Equal(60, heights[0]);
        Assert.Equal(36, heights[^1]);
        for (var i = 1; i < heights.Count; i++)
        {
            Assert.InRange(heights[i - 1] - heights[i], -1, 1);
        }

        Assert.True(heights.Count(h => h > 36 && h < 60) > 100, "the way down is long and gentle");
    }

    [Fact]
    public void ARoadThatFollowsTheGroundIsLevelAcrossItsWidthAndSteps()
    {
        var path = "\"paths\": [ { \"name\": \"p\", \"from\": \"a\", \"to\": \"b\", \"mode\": \"follow\", \"halfWidth\": 1, \"via\": [[40, 20]] } ],";
        var scene = Build(Mini(extra: path));
        var grid = scene.Grid;
        for (var x = 17; x < 40; x++)
        {
            var z = 16 + ((x - 16) * 4 / 24);
            var centre = Ground(grid, x, z);
            Assert.Equal(BlockId.Path, grid.GetBlock(x, centre, z));
            if (grid.GetBlock(x, Ground(grid, x, z + 1), z + 1) == BlockId.Path)
            {
                Assert.Equal(centre, Ground(grid, x, z + 1)); // level across the width
            }

            Assert.InRange(Math.Abs(Ground(grid, x + 1, z) - centre), 0, 1);
        }
    }

    // ------------------------------------------------------------------ seed

    [Fact]
    public void TheSeedChangesTheLandAndTheForestOfAMiniWorldButNotItsSites()
    {
        var relief = "\"9ooo\", \"oo9o\", \"o99o\", \"oooo\"";
        var one = Build(Mini(relief), seed: 1);
        var again = Build(Mini(relief), seed: 1);
        var two = Build(Mini(relief), seed: 2);
        Assert.Equal(one.Grid.ComputeDigestHex(), again.Grid.ComputeDigestHex());
        Assert.NotEqual(one.Grid.ComputeDigestHex(), two.Grid.ComputeDigestHex());
        Assert.Equal(one.Points.Select(p => (p.Key, p.Value)), two.Points.Select(p => (p.Key, p.Value)));
        Assert.Equal(one.Spawn, two.Spawn);
    }
}
