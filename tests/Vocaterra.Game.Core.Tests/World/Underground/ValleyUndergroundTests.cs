using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Generation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Core.World.Underground;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Underground;

/// <summary>
/// The underground in the valley's own stone (B13): the same valley built with and without it differs only in buried stone. Nothing within the distances of the data of any air or water cell, and
/// nothing within the margins of the sites, paths, props, village, pass and rockfall, changes; the strata, ores and caves are there in the deep stone; the caves meet the procedural ones at y 0
/// without a seam; and the bounded and the streamed valley hold the same blocks.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ValleyUndergroundTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private static readonly UndergroundData Data = TestGame.Data.Underground!;
    private static readonly ValleySpec Valley = Data.Valley!;
    private static readonly Lazy<Pair> Built = new(() => new Pair(Seed));

    private static Pair Fixture => Built.Value;

    [Fact]
    public void Given_TheShippedData_When_ItIsRead_Then_ItHasAValleyAndTheValidatorAcceptsIt()
    {
        Assert.NotNull(Data.Valley);
        Assert.Empty(UndergroundValidator.Validate(Data, TestGame.Data.WorldGen, TestGame.Data.Scene));
        Assert.Equal(8, Valley.SurfaceDistance);
        Assert.Equal(-1, Data.Caves.UnderRegionCeiling);
    }

    [Fact]
    public void Given_ValleyNumbersThatDoNotHoldTogether_When_TheyAreChecked_Then_TheValidatorNamesEachOne()
    {
        string[] Problems(ValleySpec valley) => [.. UndergroundValidator.Validate(Data with { Valley = valley }, TestGame.Data.WorldGen, TestGame.Data.Scene)];
        Assert.Contains(Problems(Valley with { SurfaceDistance = 2 }), static p => p.Contains("surfaceDistance", StringComparison.Ordinal));
        Assert.Contains(Problems(Valley with { WaterDistance = 7 }), static p => p.Contains("waterDistance", StringComparison.Ordinal));
        Assert.Contains(Problems(Valley with { BorderMargin = -1 }), static p => p.Contains("borderMargin", StringComparison.Ordinal));
        Assert.Contains(Problems(Valley with { VillageSite = "nowhere" }), static p => p.Contains("villageSite", StringComparison.Ordinal));
        Assert.Contains(Problems(Valley with { PassPaths = ["no-such-path"] }), static p => p.Contains("no-such-path", StringComparison.Ordinal));
        Assert.Contains(Problems(Valley with { PassProps = ["no-such-prop"] }), static p => p.Contains("no-such-prop", StringComparison.Ordinal));
        Assert.Empty(UndergroundValidator.Validate(Data with { Valley = null }, TestGame.Data.WorldGen, TestGame.Data.Scene));
    }

    [Fact]
    public void Given_ABundleWithoutAValleySection_When_TheUndergroundIsMade_Then_TheValleyStaysPlainStone()
    {
        Assert.Null(ValleyUnderground.Create(Data with { Valley = null }, Seed));
        Assert.Null(ValleyUnderground.Create(null, Seed));
    }

    [Fact]
    public void Given_TheValleyWithAndWithoutItsUnderground_When_TheyAreCompared_Then_OnlyPlainStoneChanged()
    {
        var f = Fixture;
        var changed = 0;
        var seen = new HashSet<BlockId>();
        for (var i = 0; i < f.Bare.Length; i++)
        {
            if (f.Bare[i] != f.With[i])
            {
                changed++;
                Assert.Equal(BlockId.Stone, f.Bare[i]);
                _ = seen.Add(f.With[i]);
            }
        }

        output.WriteLine($"{changed} of {f.Bare.Length} blocks changed; they became {string.Join(", ", seen.Select(static b => b.ToString()).Order())}");
        Assert.True(changed > 500_000, $"{changed} blocks changed");
    }

    [Fact]
    public void Given_TheChangedBlocks_When_TheirDistanceToAirAndWaterIsMeasured_Then_NoneLiesWithin8OfAirOrWater()
    {
        // The squared distance, over the whole grid, to the nearest air or water cell of the plain valley; the sky above the grid counts as air.
        var f = Fixture;
        var toAir = Distance.Squared(f.Bare, f.SizeX, f.SizeY, f.SizeZ, static b => !BlockDefinition.IsSolid(b), skyAbove: true);
        var toWater = Distance.Squared(f.Bare, f.SizeX, f.SizeY, f.SizeZ, static b => b == BlockId.Water, skyAbove: false);
        var airReach = Valley.SurfaceDistance * Valley.SurfaceDistance;
        var waterReach = Valley.WaterDistance * Valley.WaterDistance;
        var (nearestAir, nearestWater) = (int.MaxValue, int.MaxValue);
        for (var i = 0; i < f.Bare.Length; i++)
        {
            if (f.Bare[i] == f.With[i])
            {
                continue;
            }

            nearestAir = Math.Min(nearestAir, toAir[i]);
            nearestWater = Math.Min(nearestWater, toWater[i]);
        }

        output.WriteLine($"the nearest changed block is {Math.Sqrt(nearestAir):F1} from air and {Math.Sqrt(nearestWater):F1} from water");
        Assert.True(nearestAir > airReach, $"a changed block lies {Math.Sqrt(nearestAir):F2} blocks from air (the data keeps {Valley.SurfaceDistance})");
        Assert.True(nearestWater > waterReach, $"a changed block lies {Math.Sqrt(nearestWater):F2} blocks from water (the data keeps {Valley.WaterDistance})");
        Assert.True(nearestAir > 64 && nearestWater > 64);
    }

    [Fact]
    public void Given_TheValleysSurface_When_ItsBlocksAreCompared_Then_EveryColumnTopAndEveryExposedFaceIsAsItWas()
    {
        var f = Fixture;
        var (sx, sy, sz) = (f.SizeX, f.SizeY, f.SizeZ);
        var exposed = 0;
        for (var y = 0; y < sy; y++)
        {
            for (var z = 0; z < sz; z++)
            {
                for (var x = 0; x < sx; x++)
                {
                    var i = (((y * sz) + z) * sx) + x;
                    if (f.Bare[i] == f.With[i] || !BlockDefinition.IsSolid(f.Bare[i]))
                    {
                        continue;
                    }

                    // A changed block next to air, water or the sky would be a changed face of the surface.
                    foreach (var (dx, dy, dz) in Neighbours)
                    {
                        var (nx, ny, nz) = (x + dx, y + dy, z + dz);
                        if (ny >= sy)
                        {
                            Assert.Fail($"({x}, {y}, {z}) changed under the sky");
                        }

                        if (nx >= 0 && ny >= 0 && nz >= 0 && nx < sx && nz < sz && BlockDefinition.IsSolid(f.Bare[(((ny * sz) + nz) * sx) + nx]) is false)
                        {
                            Assert.Fail($"({x}, {y}, {z}) changed beside the open cell ({nx}, {ny}, {nz})");
                        }
                    }
                }
            }
        }

        for (var z = 0; z < sz; z++)
        {
            for (var x = 0; x < sx; x++)
            {
                Assert.Equal(f.Plain.Grid.HighestBlock(x, z), f.Deep.Grid.HighestBlock(x, z));
                var top = f.Plain.Grid.HighestBlock(x, z);
                Assert.Equal(f.Plain.Grid.GetBlock(x, top, z), f.Deep.Grid.GetBlock(x, top, z));
                exposed++;
            }
        }

        Assert.Equal(sx * sz, exposed);
    }

    [Fact]
    public void Given_TheValleysSitesPathsPropsVillageAndPass_When_TheirMarginsAreMeasured_Then_NoBlockWithinThemChanged()
    {
        var f = Fixture;
        var changed = f.ChangedColumns();
        var layout = TestGame.Data.Scene;
        var (sx, sy, sz) = (f.SizeX, f.SizeY, f.SizeZ);

        bool AnyChanged(int x, int y, int z, int margin)
        {
            for (var cy = Math.Max(0, y - margin); cy <= Math.Min(sy - 1, y + margin); cy++)
            {
                for (var cz = Math.Max(0, z - margin); cz <= Math.Min(sz - 1, z + margin); cz++)
                {
                    for (var cx = Math.Max(0, x - margin); cx <= Math.Min(sx - 1, x + margin); cx++)
                    {
                        if (changed[(((cy * sz) + cz) * sx) + cx])
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // Sites and the layout's own points, at the surface (the ones the scene registers by name).
        foreach (var (name, point) in f.Plain.Points)
        {
            Assert.False(AnyChanged(point.X, point.Y, point.Z, Valley.SiteMargin), $"a block changed within {Valley.SiteMargin} of the point '{name}' {point}");
        }

        Assert.False(AnyChanged(f.Plain.Spawn.Feet.X, f.Plain.Spawn.Feet.Y, f.Plain.Spawn.Feet.Z, Valley.SiteMargin), "a block changed near the spawn");
        foreach (var npc in f.Plain.Npcs)
        {
            Assert.False(AnyChanged(npc.Feet.X, npc.Feet.Y, npc.Feet.Z, Valley.SiteMargin), $"a block changed near {npc.Name}");
        }

        // Paths: every trodden block.
        var paths = f.Plain.Grid.BlocksOfKind(BlockId.Path);
        Assert.True(paths.Count > 500, $"{paths.Count} path blocks");
        foreach (var path in paths)
        {
            Assert.False(AnyChanged(path.X, path.Y, path.Z, Valley.PathMargin), $"a block changed within {Valley.PathMargin} of the path block {path}");
        }

        // Props: every cell of every named prop.
        var props = 0;
        foreach (var (name, cells) in f.Plain.PropCells)
        {
            foreach (var cell in cells)
            {
                props++;
                Assert.False(AnyChanged(cell.X, cell.Y, cell.Z, Valley.PropMargin), $"a block changed within {Valley.PropMargin} of the prop '{name}' at {cell}");
            }
        }

        Assert.True(props > 1000, $"{props} prop cells");

        // The village: its level ground and blend, from the surface.
        var village = layout.Sites.Single(site => site.Name == Valley.VillageSite);
        var radius = village.Radius + village.Blend;
        for (var z = village.Z - radius; z <= village.Z + radius; z += 2)
        {
            for (var x = village.X - radius; x <= village.X + radius; x += 2)
            {
                Assert.False(AnyChanged(x, f.Plain.Grid.HighestBlock(x, z), z, Valley.VillageMargin), $"a block changed within {Valley.VillageMargin} of the village at ({x}, {z})");
            }
        }

        // The pass: the rockfall's cells and the road from the gate to the edge, with the larger margin.
        Assert.Contains(Valley.PassProps, name => f.Plain.PropCells.ContainsKey(name));
        foreach (var name in Valley.PassProps)
        {
            foreach (var cell in f.Plain.PropCells[name])
            {
                Assert.False(AnyChanged(cell.X, cell.Y, cell.Z, Valley.PassMargin), $"a block changed within {Valley.PassMargin} of the rockfall at {cell}");
            }
        }

        var gate = f.Plain.Points["paso.gate"];
        var road = paths.Where(p => p.X >= gate.X).ToList();
        Assert.True(road.Count > 100, $"{road.Count} road blocks east of the gate");
        foreach (var block in road)
        {
            Assert.False(AnyChanged(block.X, block.Y, block.Z, Valley.PassMargin), $"a block changed within {Valley.PassMargin} of the pass road at {block}");
        }
    }

    [Fact]
    public void Given_TheValleysEdge_When_ItsBorderIsRead_Then_NothingChangedInTheBorderMargin()
    {
        var f = Fixture;
        var m = Valley.BorderMargin;
        for (var y = 0; y < f.SizeY; y++)
        {
            for (var z = 0; z < f.SizeZ; z++)
            {
                for (var x = 0; x < f.SizeX; x++)
                {
                    if (x >= m && z >= m && x < f.SizeX - m && z < f.SizeZ - m)
                    {
                        x = f.SizeX - m - 1;
                        continue;
                    }

                    var i = (((y * f.SizeZ) + z) * f.SizeX) + x;
                    Assert.True(f.Bare[i] == f.With[i], $"({x}, {y}, {z}) changed in the border margin");
                }
            }
        }
    }

    [Fact]
    public void Given_TheValleysDeepStone_When_ItsBlocksAreCounted_Then_TheStrataOresAndCavesAreThere()
    {
        var f = Fixture;
        var counts = new Dictionary<BlockId, int>();
        var carved = 0;
        for (var i = 0; i < f.Bare.Length; i++)
        {
            if (f.Bare[i] == f.With[i])
            {
                continue;
            }

            counts[f.With[i]] = counts.GetValueOrDefault(f.With[i]) + 1;
            if (f.With[i] == BlockId.Air)
            {
                carved++;
            }
        }

        output.WriteLine(string.Join(", ", counts.OrderBy(static p => p.Key).Select(static p => $"{p.Key} {p.Value}")));
        foreach (var rock in new[] { BlockId.Slate, BlockId.Granite, BlockId.Limestone, BlockId.CopperOre, BlockId.IronOre, BlockId.TinOre })
        {
            Assert.True(counts.GetValueOrDefault(rock) > 100, $"{rock}: {counts.GetValueOrDefault(rock)} blocks");
        }

        Assert.True(carved > 5000, $"{carved} cave blocks");
        Assert.False(counts.ContainsKey(BlockId.Lava), "no lava: the lava level is under the valley");
        Assert.False(counts.ContainsKey(BlockId.Bedrock));
    }

    [Fact]
    public void Given_TheValleysBottomLayerAndTheProceduralOneUnderIt_When_TheirCavesAreCompared_Then_ThereIsNoSeamAtY0()
    {
        var f = Fixture;
        var composition = WorldFactory.Compose(TestGame.Data, Seed, new Vocaterra.Game.Core.World.Regions.RegionCatalog([Vocaterra.Game.Core.World.Regions.AuthoredReliefRegion.Freeze("valley", f.Deep, edgeBlurRadius: TestGame.Data.WorldGen.Blend.Smoothing.BlurRadius)]));
        var world = new UndergroundWorld(Data, Seed);
        var reach = f.Reach;
        var (open, through, mismatches, rockMismatch) = (0, 0, 0, 0);
        for (var cz = 1; cz < 23; cz++)
        {
            for (var cx = 1; cx < 23; cx++)
            {
                var below = composition.Source.Generate(new VoxelChunkCoord(cx, -1, cz));
                for (var lz = 0; lz < 16; lz++)
                {
                    for (var lx = 0; lx < 16; lx++)
                    {
                        var (x, z) = ((cx << 4) + lx, (cz << 4) + lz);
                        if (reach[(z * f.SizeX) + x] < 0)
                        {
                            continue; // the layer is not open here (the border, a margin)
                        }

                        var field = world.Caves.KindAt(x, 0, z) != CaveKind.None;
                        var valley = f.With[(z * f.SizeX) + x] == BlockId.Air;
                        var under = world.Caves.KindAt(x, -1, z) != CaveKind.None;
                        var procedural = below.Blocks[ChunkData.Index(lx, 15, lz)] is BlockId.Air or BlockId.Lava;
                        mismatches += (field != valley ? 1 : 0) + (under != procedural ? 1 : 0);
                        if (field)
                        {
                            open++;
                        }

                        if (field && under)
                        {
                            through++;
                        }

                        // The rock beside the caves continues too: slate where the dither says so.
                        var rock = f.With[(z * f.SizeX) + x];
                        if (!field && rock is not (BlockId.Air) && world.Strata.RockAt(x, 0, z) != rock && f.Bare[(z * f.SizeX) + x] == BlockId.Stone && !IsOre(rock))
                        {
                            rockMismatch++;
                        }
                    }
                }
            }
        }

        output.WriteLine($"y 0: {open} open columns, {through} continue into the layer below; {mismatches} mismatches");
        Assert.Equal(0, mismatches);
        Assert.Equal(0, rockMismatch);
        Assert.True(open > 500, $"{open} open columns at y 0");
        Assert.True(through > 100, $"{through} caves cross y 0");
    }

    [Fact]
    public void Given_TheValleyBuiltTwiceAndAsTheBoundedAndTheStreamedWorld_When_TheirBlocksAreCompared_Then_TheyAreTheSame()
    {
        var f = Fixture;
        var again = ValleyBuild.Build(TestGame.Data.Scene, Seed);
        Assert.Equal(f.Deep.Grid.ComputeDigestHex(), again.Grid.ComputeDigestHex());
        var bounded = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Bounded);
        Assert.Equal(f.Deep.Grid.ComputeDigestHex(), bounded.Grid.ComputeDigestHex());
        Assert.Equal(SceneTests.GoldenDigest, bounded.Grid.ComputeDigestHex());
        var streamed = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Streamed);
        foreach (var (cx, cy, cz) in new[] { (3, 0, 4), (12, 1, 12), (7, 2, 18), (20, 0, 9), (12, 3, 2), (5, 5, 5) })
        {
            var coord = new VoxelChunkCoord(cx, cy, cz);
            Assert.True(bounded.Grid.GetChunk(coord).Blocks.SequenceEqual(streamed.Grid.GetChunk(coord).Blocks), $"chunk {coord} differs between the bounded and the streamed valley");
        }

        Assert.Equal(bounded.TemplateDigest, streamed.TemplateDigest);
    }

    [Fact]
    public void Given_TheSlidingWindow_When_ItIsComparedWithABruteForceMinimum_Then_TheyAgree()
    {
        var (sx, sz) = (37, 23);
        var values = new int[sx * sz];
        var rng = new Random(5);
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = rng.Next(5) == 0 ? int.MaxValue : rng.Next(-20, 100);
        }

        foreach (var radius in new[] { 0, 1, 3, 8, 16, 40 })
        {
            var fast = ValleyUnderground.WindowMin(values, sx, sz, radius);
            for (var z = 0; z < sz; z++)
            {
                for (var x = 0; x < sx; x++)
                {
                    var expected = int.MaxValue;
                    for (var dz = -radius; dz <= radius; dz++)
                    {
                        for (var dx = -radius; dx <= radius; dx++)
                        {
                            if (x + dx >= 0 && x + dx < sx && z + dz >= 0 && z + dz < sz)
                            {
                                expected = Math.Min(expected, values[((z + dz) * sx) + x + dx]);
                            }
                        }
                    }

                    Assert.Equal(expected, fast[(z * sx) + x]);
                }
            }
        }
    }

    [Fact]
    public void Given_AnyThreadCountAskedFor_When_TheLayingDegreeIsWorkedOut_Then_ItIsAtLeastOneAndAtMostTwo()
    {
        Assert.Equal(2, ValleyUnderground.MaxThreads);
        Assert.Equal(ValleyUnderground.MaxThreads, Fixture.Underground.Threads);
        foreach (var requested in new[] { int.MinValue, -3, 0, 1, 2, 3, 8, 64, int.MaxValue })
        {
            var degree = ValleyUnderground.DegreeFor(requested);
            Assert.InRange(degree, 1, 2);
            Assert.Equal(Math.Clamp(requested, 1, 2), degree);
        }
    }

    [Fact]
    public void Given_TheValleyLaidOnOneThreadAndOnTwo_When_TheirBlocksAreCompared_Then_TheyAreTheSame()
    {
        var f = Fixture;
        var one = SceneBuilder.Build(TestGame.Data.Scene, Seed, null, new ValleyUnderground(new UndergroundWorld(Data, Seed), Valley) { Threads = 1 });
        Assert.Equal(f.Deep.Grid.ComputeDigestHex(), one.Grid.ComputeDigestHex());
    }

    private static bool IsOre(BlockId block) => block is BlockId.CopperOre or BlockId.IronOre or BlockId.TinOre;

    private static readonly (int, int, int)[] Neighbours = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    /// <summary>The valley built twice (plain stone and with its underground) and flattened to arrays.</summary>
    private sealed class Pair
    {
        public Pair(ulong seed)
        {
            var layout = TestGame.Data.Scene;
            Plain = ValleyBuild.Bare(layout, seed);
            var world = new UndergroundWorld(Data, seed);
            Underground = new ValleyUnderground(world, Valley) { Observer = r => Reach = r };
            Deep = SceneBuilder.Build(layout, seed, null, Underground);
            (SizeX, SizeY, SizeZ) = (Plain.Grid.SizeX, Plain.Grid.SizeY, Plain.Grid.SizeZ);
            Bare = Flatten(Plain.Grid);
            With = Flatten(Deep.Grid);
        }

        public Scene Plain { get; }

        public Scene Deep { get; }

        public ValleyUnderground Underground { get; }

        public int SizeX { get; }

        public int SizeY { get; }

        public int SizeZ { get; }

        public BlockId[] Bare { get; }

        public BlockId[] With { get; }

        public int[] Reach { get; private set; } = [];

        public bool[] ChangedColumns()
        {
            var changed = new bool[Bare.Length];
            for (var i = 0; i < Bare.Length; i++)
            {
                changed[i] = Bare[i] != With[i];
            }

            return changed;
        }

        private static BlockId[] Flatten(VoxelGrid grid)
        {
            var blocks = new BlockId[grid.SizeX * grid.SizeY * grid.SizeZ];
            foreach (var chunk in grid.Chunks)
            {
                if (chunk.IsEmpty)
                {
                    continue;
                }

                var origin = chunk.Coord.Origin;
                var span = chunk.Blocks;
                for (var i = 0; i < span.Length; i++)
                {
                    var (x, y, z) = (origin.X + (i & 15), origin.Y + (i >> 8), origin.Z + ((i >> 4) & 15));
                    blocks[(((y * grid.SizeZ) + z) * grid.SizeX) + x] = span[i];
                }
            }

            return blocks;
        }
    }
}
