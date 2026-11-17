using System.Diagnostics;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>
/// A region on procedural ground (B15): the sample town, built from a layout in test data and placed by a test away from the valley, stands on the world's own land, is the same town wherever it is placed,
/// meets the land round it with no step beyond the bound, and goes through the catalog, the blend and the composite source as the valley does.
/// </summary>
public sealed class ProceduralReliefRegionTests(ITestOutputHelper output)
{
    private static ProceduralReliefRegion Town => SampleTown.Town();

    private static WorldComposition World => Composed.Value;

    private static readonly Lazy<WorldComposition> Composed = new(() =>
        WorldFactory.Compose(SampleTown.Data, SampleTown.Seed, new RegionCatalog([RegionTestWorld.Valley(), Town])));

    private static int Hills(int lx, int lz) => 30 + (lx / 12 % 3) + (lz / 20 % 2);

    [Fact]
    public void Given_TheSampleTownAndAPlacement_When_ItIsBuilt_Then_ItsBoundsAreItsLayoutsSizeAtThePlacement()
    {
        var town = Town;
        var p = town.Placement;

        Assert.Equal((SampleTown.First.ChunkX, SampleTown.First.ChunkZ), (p.ChunkX, p.ChunkZ));
        Assert.Equal(new RegionBounds(p.ChunkX, p.ChunkY, p.ChunkZ, 6, 6, 6), town.Bounds);
        Assert.Equal((1264, 1359, 96, 191), (town.Bounds.MinX, town.Bounds.MaxX, town.Bounds.MinZ, town.Bounds.MaxZ));
        Assert.Equal((p.OriginY, p.OriginY + 95), (town.Bounds.MinY, town.Bounds.MaxY));
        Assert.Equal(SampleTown.Id, town.Id);
        Assert.Equal(new BlockPos(1264, p.OriginY, 96), town.Origin);
        Assert.True(town.Covers(new VoxelChunkCoord(79, p.ChunkY + 5, 11)));
        Assert.False(town.Covers(new VoxelChunkCoord(85, p.ChunkY, 6)));
    }

    [Fact]
    public void Given_ALayoutOfAnotherSize_When_ItIsPlacedAtAnotherPlace_Then_TheBoundsAreItsAndNothingIsAConstant()
    {
        var json = SampleTown.Json.Replace("\"size\": { \"x\": 96, \"y\": 96, \"z\": 96 }", "\"size\": { \"x\": 128, \"y\": 112, \"z\": 160 }", StringComparison.Ordinal);
        var layout = SceneLayout.Parse(json);
        var placement = new RegionPlacement(-300, 2, 500);
        var ground = new SampleTown.RelativeGround(placement, 20, Hills);

        var region = ProceduralReliefRegion.Create("wide", layout, placement, ground, 42, 32, 16);

        Assert.Equal(new RegionBounds(-300, 2, 500, 8, 7, 10), region.Bounds);
        Assert.Equal(new BlockPos(-4800, 32, 8000), region.Origin);
        Assert.Equal((48, 48), (region.Point("square").Local.X, region.Point("square").Local.Z));
        Assert.Equal(new BlockPos(-4800 + 48, region.Point("square").World.Y, 8000 + 48), region.Point("square").World);
    }

    [Fact]
    public void Given_TheTownsFootprint_When_ItIsFitted_Then_TheFloorIsUnderTheLowestGroundByAtMostTheFooting()
    {
        var town = Town;
        var free = SampleTown.FreeGround(town.Bounds);
        var lowest = int.MaxValue;
        var highest = int.MinValue;
        for (var z = town.Bounds.MinZ; z <= town.Bounds.MaxZ; z++)
        {
            for (var x = town.Bounds.MinX; x <= town.Bounds.MaxX; x++)
            {
                lowest = Math.Min(lowest, free(x, z));
                highest = Math.Max(highest, free(x, z));
            }
        }

        var floor = town.Bounds.MinY;
        output.WriteLine($"the first town: ground {lowest} to {highest}, floor {floor} (layer {town.Placement.ChunkY})");
        Assert.InRange(lowest - floor, ProceduralReliefRegion.Footing, ProceduralReliefRegion.Footing + 15);
        Assert.True(highest - floor <= town.Layout.Terrain.Ceiling);
    }

    [Fact]
    public void Given_TheTownOnAGroundThatIsTheSameRelativeToItsOrigin_When_ItIsPlacedAtTwoOffsets_Then_ItIsTheSameTownRelativeToItsOrigin()
    {
        var layout = SampleTown.Layout;
        var a = new RegionPlacement(10, 3, 20);
        var b = new RegionPlacement(-45, 1, 777);
        var ra = ProceduralReliefRegion.Create("a", layout, a, new SampleTown.RelativeGround(a, 0, Hills), 42, 32, 16);
        var rb = ProceduralReliefRegion.Create("b", layout, b, new SampleTown.RelativeGround(b, 0, Hills), 42, 32, 16);

        // Every chunk is the same blocks and water, the ground's edge profile is the same height above the floor, and every place is the same place.
        for (var cy = 0; cy < 6; cy++)
        {
            for (var cz = 0; cz < 6; cz++)
            {
                for (var cx = 0; cx < 6; cx++)
                {
                    var one = ra.Copy(new VoxelChunkCoord(a.ChunkX + cx, a.ChunkY + cy, a.ChunkZ + cz));
                    var two = rb.Copy(new VoxelChunkCoord(b.ChunkX + cx, b.ChunkY + cy, b.ChunkZ + cz));
                    Assert.True(one.Blocks.SequenceEqual(two.Blocks), $"chunk {cx},{cy},{cz}");
                    Assert.True(one.WaterLevels.SequenceEqual(two.WaterLevels), $"water {cx},{cy},{cz}");
                }
            }
        }

        for (var lz = -20; lz < 116; lz += 3)
        {
            for (var lx = -20; lx < 116; lx += 3)
            {
                Assert.Equal(ra.EdgeHeight(a.OriginX + lx, a.OriginZ + lz) - a.OriginY, rb.EdgeHeight(b.OriginX + lx, b.OriginZ + lz) - b.OriginY);
                Assert.Equal(ra.SmoothedEdgeHeight(a.OriginX + lx, a.OriginZ + lz) - a.OriginY, rb.SmoothedEdgeHeight(b.OriginX + lx, b.OriginZ + lz) - b.OriginY);
            }
        }

        Assert.Equal(ra.Points.Select(static p => (p.Name, p.Local)), rb.Points.Select(static p => (p.Name, p.Local)));
        Assert.All(ra.Points, p => Assert.Equal(new BlockPos(a.OriginX + p.Local.X, a.OriginY + p.Local.Y, a.OriginZ + p.Local.Z), p.World));
        Assert.All(rb.Points, p => Assert.Equal(new BlockPos(b.OriginX + p.Local.X, b.OriginY + p.Local.Y, b.OriginZ + p.Local.Z), p.World));
        Assert.NotEqual(ra.TemplateDigest, rb.TemplateDigest); // the same town in another place is another template
        Assert.Equal(32, ra.TemplateDigest.Length);
    }

    [Fact]
    public void Given_TheTownOnTheRealGroundAtTwoPlaces_When_ItIsBuilt_Then_TheSitesAreTheSameAndEachHouseIsTheSameHouseOnItsFloor()
    {
        var first = SampleTown.At(SampleTown.First);
        var second = SampleTown.At(SampleTown.Second);

        Assert.Equal(first.Points.Select(static p => (p.Name, p.Local.X, p.Local.Z)), second.Points.Select(static p => (p.Name, p.Local.X, p.Local.Z)));
        Assert.NotEqual(first.Placement.ChunkY * 1000 + first.Point("square").World.Y, second.Placement.ChunkY * 1000 + second.Point("square").World.Y);
        foreach (var house in new[] { "house.a", "house.b" })
        {
            var (fa, fb) = (first.Point(house), second.Point(house));
            for (var dz = -2; dz <= 2; dz++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    for (var dy = 0; dy <= 9; dy++)
                    {
                        Assert.Equal(
                            SampleTown.BlockAt(first, fa.World.X + dx, fa.World.Y + dy, fa.World.Z + dz),
                            SampleTown.BlockAt(second, fb.World.X + dx, fb.World.Y + dy, fb.World.Z + dz));
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_TheTownsGround_When_ItIsComparedWithTheProceduralWorld_Then_OnlyColumnsNearASiteOrThePathAreLevelled()
    {
        var town = Town;
        var free = SampleTown.FreeGround(town.Bounds);
        var layout = town.Layout;
        var (differing, equal) = (0, 0);
        foreach (var (lx, lz) in Footprint())
        {
            var (x, z) = (town.Bounds.MinX + lx, town.Bounds.MinZ + lz);
            if (town.EdgeHeight(x, z) == free(x, z))
            {
                equal++;
                continue;
            }

            differing++;
            var nearSite = layout.Sites.Any(s => s.Radius > 0 && Distance(lx, lz, s.X, s.Z) < s.Radius + s.Blend + 1);
            var nearRoad = lx >= 0 && Math.Abs(lz - 48) <= 5; // the road runs along row 48 from the west gate to the east gate, a path of half width 1 with a blend of 3
            Assert.True(nearSite || nearRoad, $"column ({lx}, {lz}) is levelled ({free(x, z)} to {town.EdgeHeight(x, z)}) but is near no site and not on the road");
        }

        output.WriteLine($"the first town: {equal} columns are the world's own ground, {differing} are levelled");
        Assert.True(equal > differing * 2, "most of the town is the world's own ground");
        Assert.True(differing > 100, "the levelling is real: the sites and the road change the ground");
    }

    [Theory]
    [InlineData(79, 6)] // rolling land, 7 blocks of relief under the town
    [InlineData(56, -25)] // flat land
    [InlineData(98, 30)] // hills: 43 blocks of relief under the town
    public void Given_TheTownsBorderAndMargin_When_TheGroundIsSampled_Then_NoStepPassesTheBoundAndTheBorderIsTheWorldsOwnGround(int chunkX, int chunkZ)
    {
        var town = SampleTown.At((chunkX, chunkZ));
        var world = WorldFactory.Compose(SampleTown.Data, SampleTown.Seed, new RegionCatalog([RegionTestWorld.Valley(), town]));
        var b = town.Bounds;
        var free = SampleTown.FreeGround(b);
        var blend = SampleTown.Data.WorldGen.Blend;
        var onBorder = 0;
        var offBorder = 0;
        for (var t = 0; t < 96; t++)
        {
            foreach (var (x, z) in new[] { (b.MinX + t, b.MinZ), (b.MinX + t, b.MaxZ), (b.MinX, b.MinZ + t), (b.MaxX, b.MinZ + t) })
            {
                _ = Math.Abs(town.EdgeHeight(x, z) - free(x, z)) <= 1 ? onBorder++ : offBorder++;
            }
        }

        var maxStep = 0;
        for (var d = 0; d <= blend.Margin + 8; d++)
        {
            for (var t = 0; t <= b.MaxX - b.MinX; t += 2)
            {
                foreach (var (x, z, nz) in new[] { (b.MinX + t, b.MinZ - d, -1), (b.MinX + t, b.MaxZ + d, 1) })
                {
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(world, x, z).Height - RegionTestWorld.Shape(world, x, z + nz).Height));
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(world, x, z).Height - RegionTestWorld.Shape(world, x + 1, z).Height));
                }
            }

            for (var t = 0; t <= b.MaxZ - b.MinZ; t += 2)
            {
                foreach (var (x, z, nx) in new[] { (b.MinX - d, b.MinZ + t, -1), (b.MaxX + d, b.MinZ + t, 1) })
                {
                    maxStep = Math.Max(maxStep, Math.Abs(RegionTestWorld.Shape(world, x, z).Height - RegionTestWorld.Shape(world, x + nx, z).Height));
                }
            }
        }

        output.WriteLine($"the town at chunk ({chunkX}, {chunkZ}), ground {town.EdgeHeight(town.Bounds.MinX + 48, town.Bounds.MinZ + 48)} at its square: {onBorder} of {onBorder + offBorder} border columns are the world's own ground (within 1), the largest step across border and margin is {maxStep}");
        Assert.True(maxStep <= 9, $"a step of {maxStep}");
        Assert.True(onBorder >= (onBorder + offBorder) - 8, $"{offBorder} border columns are not the world's own ground");
        Assert.InRange(RegionTestWorld.Shape(world, b.MinX - 1, b.MinZ + 40).Height - town.EdgeHeight(b.MinX, b.MinZ + 40), -1, 1);
        Assert.InRange(RegionTestWorld.Shape(world, b.MaxX + 1, b.MinZ + 40).Height - town.EdgeHeight(b.MaxX, b.MinZ + 40), -1, 1);
    }

    [Fact]
    public void Given_TheValleyAndTheTown_When_TheCatalogIsValidated_Then_ItIsSoundAndRefusesRegionsThatOverlapCrowdOrLeaveTheWorld()
    {
        var valley = RegionTestWorld.Valley();
        var town = Town;
        var limits = WorldLimits.Symmetric(WorldLimits.DefaultHalfExtent, -64, 320);
        var margin = SampleTown.Data.WorldGen.Blend.Margin;
        var data = SampleTown.Data.WorldGen;
        var layout = SampleTown.Layout;

        Assert.Empty(new RegionCatalog([valley, town]).Validate(limits, margin));
        Assert.Equal(town, new RegionCatalog([valley, town]).RegionAt(town.Point("square").World.X, town.Point("square").World.Z));
        Assert.Equal(valley, new RegionCatalog([valley, town]).RegionAt(10, 10));

        var overlapping = ProceduralReliefRegion.Create("overlapping", layout, ProceduralReliefRegion.Fit(layout, SampleTown.First.ChunkX + 2, SampleTown.First.ChunkZ + 2, data, 42), data, 42);
        var crowding = ProceduralReliefRegion.Create("crowding", layout, ProceduralReliefRegion.Fit(layout, SampleTown.First.ChunkX + 8, SampleTown.First.ChunkZ, data, 42), data, 42); // 32 blocks between: closer than two margins
        var twin = ProceduralReliefRegion.Create(SampleTown.Id, layout, ProceduralReliefRegion.Fit(layout, SampleTown.Second.ChunkX, SampleTown.Second.ChunkZ, data, 42), data, 42);
        var farOut = new RegionPlacement(1900, 3, 0);
        var outside = ProceduralReliefRegion.Create("outside", layout, farOut, new SampleTown.RelativeGround(farOut, 0, Hills), 42, 32, 16);

        Assert.Contains(new RegionCatalog([valley, town, overlapping]).Validate(limits, margin), static p => p.Contains("overlap", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([valley, town, crowding]).Validate(limits, margin), static p => p.Contains("closer", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([valley, town, twin, twin]).Validate(limits, margin), static p => p.Contains("called", StringComparison.Ordinal));
        Assert.Contains(new RegionCatalog([valley, outside]).Validate(limits, margin), static p => p.Contains("outside", StringComparison.Ordinal));
    }

    [Fact]
    public void Given_TheCompositeSourceOverTheValleyAndTheTown_When_ChunksAreGenerated_Then_EachRegionServesItsOwnAndTheRestIsProcedural()
    {
        var valley = RegionTestWorld.Valley();
        var town = Town;
        var source = World.Source;
        var p = town.Placement;

        foreach (var coord in new[] { new VoxelChunkCoord(p.ChunkX, p.ChunkY, p.ChunkZ), new VoxelChunkCoord(p.ChunkX + 3, p.ChunkY + 1, p.ChunkZ + 4), new VoxelChunkCoord(p.ChunkX + 5, p.ChunkY + 5, p.ChunkZ + 5) })
        {
            Assert.Equal(town.Copy(coord).ComputeDigestHex(), source.Generate(coord).ComputeDigestHex());
        }

        var inside = town.Copy(new VoxelChunkCoord(p.ChunkX + 3, p.ChunkY + 1, p.ChunkZ + 4));
        if (!inside.IsEmpty)
        {
            Assert.Same(inside, source.Generate(new VoxelChunkCoord(p.ChunkX + 3, p.ChunkY + 1, p.ChunkZ + 4))); // a chunk of air is a fresh value each time
        }

        Assert.Same(valley.Copy(new VoxelChunkCoord(12, 2, 13)), source.Generate(new VoxelChunkCoord(12, 2, 13)));
        Assert.True(source.Generate(new VoxelChunkCoord(p.ChunkX + 2, p.ChunkY + 6, p.ChunkZ + 2)).IsEmpty); // above the town
        Assert.Equal(World.Procedural.Generate(new VoxelChunkCoord(p.ChunkX + 2, p.ChunkY - 1, p.ChunkZ + 2)).ComputeDigestHex(), source.Generate(new VoxelChunkCoord(p.ChunkX + 2, p.ChunkY - 1, p.ChunkZ + 2)).ComputeDigestHex()); // under it
        Assert.Equal(World.Procedural.Generate(new VoxelChunkCoord(p.ChunkX + 6, p.ChunkY, p.ChunkZ)).ComputeDigestHex(), source.Generate(new VoxelChunkCoord(p.ChunkX + 6, p.ChunkY, p.ChunkZ)).ComputeDigestHex()); // beside it

        foreach (var (cx, cz) in new[] { (p.ChunkX + 1, p.ChunkZ + 2), (p.ChunkX + 6, p.ChunkZ + 1), (3, 3) })
        {
            var column = source.GenerateColumn(cx, cz);
            for (var i = 0; i < column.Count; i++)
            {
                var coord = new VoxelChunkCoord(cx, source.MinChunkY + i, cz);
                Assert.Equal(source.Generate(coord).ComputeDigestHex(), column[i].ComputeDigestHex());
            }
        }

        Assert.Equal(2, source.Regions.Regions.Count);
        Assert.Equal(town, source.Regions.Covering(new VoxelChunkCoord(p.ChunkX + 1, p.ChunkY + 1, p.ChunkZ + 1)));
    }

    [Fact]
    public void Given_TheTownInAStreamedWorld_When_ThePlacesAreAsked_Then_TheTownIsTheRegionAndItsDigestIsPartOfTheWorlds()
    {
        var town = Town;
        var data = SampleTown.Data;
        var scene = WorldFactory.Create(data, 42, WorldMode.Streamed, null, [town]);
        var square = town.Point("square").World;

        var places = scene.Places!;
        Assert.Equal(SampleTown.Id, places.RegionAt(square.X, square.Z)!.Id);
        Assert.Equal(WorldFactory.ValleyRegionId, places.RegionAt(100, 100)!.Id);
        Assert.Null(places.RegionAt(square.X - 400, square.Z));
        Assert.Equal(town.EdgeHeight(square.X, square.Z), places.NaturalSurfaceAt(square.X, square.Z));
        Assert.Equal(square.Y, places.NaturalSurfaceAt(square.X, square.Z)); // a site levels the ground at its own height
        Assert.True(places.IsUnderground(square.X, square.Y - 2, square.Z));
        Assert.False(places.IsUnderground(square.X, square.Y + 1, square.Z));
        Assert.True(scene.Grid.IsSolid(square.X, square.Y, square.Z)); // the streamed grid reads the town's chunks through the composite source
        Assert.False(scene.Grid.IsSolid(square.X, square.Y + 1, square.Z));

        var again = WorldFactory.Create(data, 42, WorldMode.Streamed, null, [town]);
        var valleyOnly = WorldFactory.Create(data, 42, WorldMode.Streamed);
        Assert.Equal(scene.TemplateDigest, again.TemplateDigest);
        Assert.NotEqual(valleyOnly.TemplateDigest, scene.TemplateDigest);
        Assert.Equal(valleyOnly.TemplateDigest, WorldFactory.Create(data, 42, WorldMode.Streamed, null, []).TemplateDigest);
    }

    [Fact]
    public void Given_ATownBuiltForAnotherSeedOrABoundedWorld_When_TheWorldIsMade_Then_ItIsRefused()
    {
        var data = SampleTown.Data;
        var placement = new RegionPlacement(300, 3, 300);
        var foreign = ProceduralReliefRegion.Create("foreign", SampleTown.Layout, placement, new SampleTown.RelativeGround(placement, 0, Hills), 7, 32, 16);

        Assert.Equal(7UL, foreign.Seed);
        Assert.Throws<ArgumentException>(() => WorldFactory.Create(data, 42, WorldMode.Streamed, null, [foreign]));
        Assert.Throws<ArgumentException>(() => WorldFactory.Create(data, 42, WorldMode.Bounded, null, [Town]));
        Assert.Throws<ArgumentException>(() => WorldFactory.Create(data, 42, WorldMode.Streamed, null, [Town, Town]));
    }

    [Fact]
    public void Given_TheTownsPlaces_When_TheyAreRead_Then_EachCarriesItsLocalAndItsWorldCoordinates()
    {
        var town = Town;
        var layout = town.Layout;
        var o = town.Origin;

        Assert.Equal(new[] { "gate.east", "gate.west", "house.a", "house.b", "lamp.north", "lamp.south", "plot.a", "plot.b", "square" }, town.Points.Select(static p => p.Name));
        foreach (var site in layout.Sites)
        {
            var point = town.Point(site.Name);
            Assert.Equal((site.X, site.Z), (point.Local.X, point.Local.Z));
            Assert.Equal((o.X + site.X, o.Y + point.Local.Y, o.Z + site.Z), (point.World.X, point.World.Y, point.World.Z));
            Assert.Equal(town.EdgeHeight(point.World.X, point.World.Z), point.World.Y); // a place stands on the ground top of its column
            Assert.NotEqual(BlockId.Air, SampleTown.BlockAt(town, point.World.X, point.World.Y, point.World.Z));
        }

        Assert.Equal(new BlockPos(o.X + 21 + 2, town.Point("house.a").World.Y, o.Z + 37 + 2), town.Point("house.a").World);
        Assert.Equal(town.Point("square").World, town.Placement.ToWorld(town.Point("square").Local));
        Assert.Equal(town.Point("square").Local, town.Placement.ToLocal(town.Point("square").World));
        Assert.Throws<KeyNotFoundException>(() => town.Point("nowhere"));
    }

    [Fact]
    public void Given_ALayoutWithWaterOrGroundThatDoesNotFit_When_ItIsPlaced_Then_ItIsRefused()
    {
        var layout = SampleTown.Layout;
        var low = new RegionPlacement(300, 0, 300);
        var tall = new RegionPlacement(300, 0, 300);
        var lake = SceneLayout.Parse(SampleTown.Json.Replace("\"props\": [", "\"lake\": { \"site\": \"square\", \"rx\": 10, \"rz\": 6, \"depth\": 3 },\n  \"props\": [", StringComparison.Ordinal));
        var flooded = SceneLayout.Parse(SampleTown.Json.Replace("\"waterLevel\": 1", "\"waterLevel\": 30", StringComparison.Ordinal));

        Assert.Contains("lake", Assert.Throws<ArgumentException>(() => ProceduralReliefRegion.Create("lake", lake, low, new SampleTown.RelativeGround(low, 30, Hills), 42, 32, 16)).Message, StringComparison.Ordinal);
        Assert.Contains("water level", Assert.Throws<ArgumentException>(() => ProceduralReliefRegion.Create("flooded", flooded, low, new SampleTown.RelativeGround(low, 30, Hills), 42, 32, 16)).Message, StringComparison.Ordinal);
        Assert.Contains("sea level", Assert.Throws<ArgumentException>(() => ProceduralReliefRegion.Create("sunk", layout, low, new SampleTown.RelativeGround(low, 0, Hills), 42, 32, 16)).Message, StringComparison.Ordinal);
        Assert.Contains("ceiling", Assert.Throws<ArgumentException>(() => ProceduralReliefRegion.Create("high", layout, tall, new SampleTown.RelativeGround(tall, 80, Hills), 42, 32, 16)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheTownAndTheLandRoundIt_When_ChunksAreGeneratedFromManyThreads_Then_EveryChunkIsTheSameAsInASequentialRun()
    {
        var p = Town.Placement;
        var coords = new List<VoxelChunkCoord>();
        for (var cz = p.ChunkZ - 2; cz < p.ChunkZ + 8; cz++)
        {
            for (var cx = p.ChunkX - 2; cx < p.ChunkX + 8; cx++)
            {
                for (var cy = p.ChunkY - 1; cy < p.ChunkY + 7; cy += 2)
                {
                    coords.Add(new VoxelChunkCoord(cx, cy, cz));
                }
            }
        }

        var sequential = coords.Select(c => World.Source.Generate(c).ComputeDigestHex()).ToArray();
        var parallel = new string[coords.Count];
        _ = Parallel.For(0, coords.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => parallel[i] = World.Source.Generate(coords[i]).ComputeDigestHex());

        Assert.Equal(sequential, parallel);
    }

    [Fact]
    public void Given_TheSampleTown_When_ItIsBuiltAgain_Then_ItIsTheSameTemplateAndTheBuildTakesAFewHundredMilliseconds()
    {
        var layout = SampleTown.Layout;
        var placement = Town.Placement;
        var watch = Stopwatch.StartNew();
        var again = ProceduralReliefRegion.Create(SampleTown.Id, layout, placement, SampleTown.Data.WorldGen, SampleTown.Seed);
        watch.Stop();

        output.WriteLine($"a 96 x 96 x 96 town region: built, frozen and its edge profile worked out in {watch.ElapsedMilliseconds} ms (Debug)");
        Assert.Equal(Town.TemplateDigest, again.TemplateDigest);
        Assert.True(watch.ElapsedMilliseconds < 5000);
    }

    [Fact]
    public void Given_ALayoutWithNoRelief_When_ItIsParsed_Then_ItIsFlatAtTheValleyHeightAndAMalformedReliefIsStillRefused()
    {
        var layout = SampleTown.Layout;

        Assert.All(layout.Terrain.Relief, static h => Assert.Equal(36, h));
        Assert.Equal((6, 6), (layout.Terrain.ReliefColumns, layout.Terrain.ReliefRows));
        var bad = SampleTown.Json.Replace("\"terrain\": {", "\"terrain\": { \"relief\": [\"22\"],", StringComparison.Ordinal);
        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(bad));
    }

    private static IEnumerable<(int X, int Z)> Footprint() =>
        Enumerable.Range(0, 96).SelectMany(z => Enumerable.Range(0, 96).Select(x => (x, z)));

    private static int Distance(int x0, int z0, int x1, int z1) => (int)IntMath.ISqrt((long)(((x0 - x1) * (x0 - x1)) + ((z0 - z1) * (z0 - z1))));
}
