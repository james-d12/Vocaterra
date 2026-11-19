using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Streaming;

/// <summary>
/// The natural surface heights the residency's workers make with each column (B44): the 18 x 18 tile cut from a group's window, or made alone, is the height model's answer (<c>NaturalSurfaceAt</c>) for every block column; a sky tile built from them is the tile
/// built by evaluating the height model, and so are the meshes drawn from it, also after block edits (edits never move the natural surface); the residency puts them in its store when a column is installed and lets them go with the column.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class NaturalHeightsTests
{
    private static readonly Lazy<AuthoredReliefRegion> Region2026 = new(static () => AuthoredReliefRegion.Freeze(WorldFactory.ValleyRegionId, RouteScene.Build(2026), keepChunks: true));

    private static readonly Lazy<WorldComposition> Seed2026 = new(static () => WorldFactory.Compose(TestGame.Data, 2026, new RegionCatalog([Region2026.Value])));

    public static TheoryData<int> Seeds => new() { 42, 2026 };

    private static WorldComposition Of(int seed) => seed == 42 ? StreamedWorlds.Composition : Seed2026.Value;

    // Columns over the valley (chunk columns 0 to 23 are blocks 0 to 383), its edge, the land round it and far off: the groups' edges (4 x 4 chunk columns) and middles, the sea and the mountains of both seeds.
    private static readonly (int X, int Z)[] Columns =
    [
        (0, 0), (3, 3), (4, 4), (7, 7), (8, 8), (11, 12), (15, 12), (19, 13), (23, 23), (24, 24), (-1, -1), (-4, -4), (-5, 2), (27, 0), (30, 31), (40, 40), (41, 43), (63, 63), (64, -64), (-50, 70),
        (120, 120), (121, 120), (-120, -121), (200, 90), (203, 91), (-200, 300), (400, -400), (600, 20), (-700, -30), (900, 900), (1500, -1500), (-2000, 1200),
    ];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_SampledColumnsOfASeed_When_TheTileIsCutFromTheGroupOrMadeAlone_Then_EveryHeightIsTheNaturalSurfaceOfItsBlockColumn(int seed)
    {
        var composition = Of(seed);
        var world = composition.World;
        var places = composition.Places;
        var lowest = int.MaxValue;
        var highest = int.MinValue;
        var cut = 0;
        foreach (var (cx, cz) in Columns)
        {
            var (minX, minZ) = ColumnGroupCache.OriginOf(cx, cz);
            var group = world.Bands.ShapeGroup(minX, minZ, ColumnGroupCache.Span);
            var fromGroup = group.NaturalTile(cx, cz);
            var alone = world.NaturalTile(cx, cz, null);
            Assert.NotNull(fromGroup);
            cut++;
            Assert.Equal(ColumnNaturalHeights.Side * ColumnNaturalHeights.Side, alone.Length);
            Assert.Equal(alone, fromGroup);
            Assert.Equal(alone, world.NaturalTile(cx, cz, group));
            for (var dz = 0; dz < ColumnNaturalHeights.Side; dz++)
            {
                for (var dx = 0; dx < ColumnNaturalHeights.Side; dx++)
                {
                    var (x, z) = (((cx << VoxelChunk.Shift) - 1) + dx, ((cz << VoxelChunk.Shift) - 1) + dz);
                    var natural = places.NaturalSurfaceAt(x, z);
                    Assert.True(natural == alone[(dz * ColumnNaturalHeights.Side) + dx], $"seed {seed}, chunk column ({cx}, {cz}), block column ({x}, {z}): {natural} against {alone[(dz * ColumnNaturalHeights.Side) + dx]}");
                    lowest = Math.Min(lowest, natural);
                    highest = Math.Max(highest, natural);
                }
            }
        }

        Assert.Equal(Columns.Length, cut);
        Assert.True(highest - lowest > 60, $"the sample spans only {lowest} to {highest}: it should hold the sea and the mountains");
    }

    [Fact]
    public void Given_AColumnTheGroupDoesNotHold_When_ItsTileIsAsked_Then_TheGroupGivesNothingAndTheWorldMakesIt()
    {
        var world = StreamedWorlds.Composition.World;
        var group = world.Bands.ShapeGroup(40, 40, ColumnGroupCache.Span);

        Assert.Null(group.NaturalTile(39, 40));
        Assert.Null(group.NaturalTile(44, 43));
        Assert.Equal(world.NaturalTile(44, 43, null), world.NaturalTile(44, 43, group));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Given_ResidentColumns_When_SkyTilesAndMeshesAreBuiltFromTheCachedHeightsAndFromTheHeightModel_Then_TheyAreIdenticalAlsoAfterEdits(int seed)
    {
        var composition = Of(seed);
        var scene = seed == 42 ? StreamedWorlds.Scene() : RouteScene.Build(2026).WithWorld(WorldFactory.StreamedGrid(TestGame.Data, composition.Source), composition.Places, Region2026.Value.TemplateDigest, composition.Source, composition.World);
        var grid = scene.Grid;
        var places = composition.Places;
        var world = composition.World;
        // A block of columns by the valley's edge and one of open land, with the ring round each (one chunk column of sky tile border).
        (int X, int Z)[] tested = [(40, 40), (41, 40), (40, 41), (41, 41), (22, 12), (23, 12)];
        foreach (var (cx, cz) in tested)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var cy = grid.Limits.MinChunkY; cy < grid.Limits.EndChunkY; cy++)
                    {
                        _ = grid.GetChunk(new VoxelChunkCoord(cx + dx, cy, cz + dz));
                    }
                }
            }
        }

        var store = new ColumnNaturalHeights();
        foreach (var (cx, cz) in tested)
        {
            var (minX, minZ) = ColumnGroupCache.OriginOf(cx, cz);
            store.Put(cx, cz, world.NaturalTile(cx, cz, world.Bands.ShapeGroup(minX, minZ, ColumnGroupCache.Span)));
        }

        var cached = new SkyExposure(grid, places.NaturalSurfaceAt);
        cached.SetNaturalHeights(store);
        var computed = new SkyExposure(grid, places.NaturalSurfaceAt);
        var source = new GridBlockSource(grid);
        var meshed = 0;

        void CompareAll(string when)
        {
            foreach (var (cx, cz) in tested)
            {
                var a = cached.Tile(cx, cz);
                var b = computed.Tile(cx, cz);
                Assert.Equal(b.Highest, a.Highest);
                for (var z = b.OriginZ; z < b.OriginZ + SkySurface.Side; z++)
                {
                    for (var x = b.OriginX; x < b.OriginX + SkySurface.Side; x++)
                    {
                        Assert.True(b.At(x, z) == a.At(x, z), $"{when}: seed {seed}, column ({cx}, {cz}), block column ({x}, {z}): {a.At(x, z)} against {b.At(x, z)}");
                    }
                }

                for (var cy = 0; cy < 10; cy++)
                {
                    var withCached = ChunkMesher.Build(source, cx, cy, cz, 0, new MeshOptions(true, 100, null, LightBand.Day, a));
                    var withComputed = ChunkMesher.Build(source, cx, cy, cz, 0, new MeshOptions(true, 100, null, LightBand.Day, b));
                    foreach (var (one, two) in new[] { (withCached.Opaque, withComputed.Opaque), (withCached.Alpha, withComputed.Alpha) })
                    {
                        Assert.True(one.Vertices.ToArray().AsSpan().SequenceEqual(two.Vertices.ToArray()), $"{when}: seed {seed}, chunk ({cx}, {cy}, {cz}) meshes differ");
                        meshed += one.Vertices.Length;
                    }
                }
            }
        }

        CompareAll("before edits");
        Assert.True(cached.TileSources.FromHeights >= tested.Length && cached.TileSources.Computed == 0);
        Assert.True(computed.TileSources.FromHeights == 0 && computed.TileSources.Computed >= tested.Length);

        // Edits: a block taken from the open surface of each column and a block put in the air above it; they move the tiles (the walk down the blocks), never the natural heights, and the tiles built again from the cache still equal the computed ones.
        foreach (var (cx, cz) in tested)
        {
            var (x, z) = ((cx << VoxelChunk.Shift) + 5, (cz << VoxelChunk.Shift) + 6);
            var top = places.NaturalSurfaceAt(x, z);
            _ = grid.SetBlock(x, top, z, BlockId.Air);
            _ = grid.SetBlock(x, top - 1, z, BlockId.Air);
            _ = grid.SetBlock(x + 3, top + 2, z, BlockId.Planks);
        }

        CompareAll("after edits");
        foreach (var (cx, cz) in tested)
        {
            Assert.True(store.TryGet(cx, cz, out var held));
            Assert.Equal(world.NaturalTile(cx, cz, null), held);
        }

        Assert.True(meshed > 10_000, $"only {meshed} vertices were compared");
    }

    [Fact]
    public void Given_AResidencyWithWorkers_When_ItSettlesAndThePlayerMovesAway_Then_EveryResidentColumnHoldsItsHeightsAndTheStoreKeepsOnlyColumnsThePlanWants()
    {
        var scene = StreamedWorlds.Scene();
        using var pool = new JobPool(2);
        var residency = ChunkResidencyTestsBase.Make(scene, pool);
        HangGuard.Settled(limit => residency.Settle(200, 70, 200, limit));

        var store = residency.NaturalHeights;
        var columns = GridResidency.Resident(scene.Grid).Select(static c => (c.X, c.Z)).Distinct().ToArray();
        Assert.NotEmpty(columns);
        foreach (var (cx, cz) in columns)
        {
            Assert.True(store.TryGet(cx, cz, out var tile), $"no heights for resident column ({cx}, {cz})");
            Assert.Equal(StreamedWorlds.Composition.World.NaturalTile(cx, cz, null), tile);
        }

        var made = residency.NaturalHeightSources;
        Assert.Equal(residency.ColumnsGenerated, made.Cut + made.Computed);
        Assert.Equal(columns.Length, store.Count);
        Assert.Equal(columns.Length * ((ColumnNaturalHeights.Side * ColumnNaturalHeights.Side * sizeof(int)) + 24L), store.Bytes);

        HangGuard.Settled(limit => residency.Settle(700, 90, 300, limit));

        var plan = residency.Plan!;
        Assert.NotEqual(0, store.Count);
        foreach (var (cx, cz) in columns)
        {
            Assert.Equal(plan.TryGetLayers(cx, cz, out _, out _), store.TryGet(cx, cz, out _));
        }

        var now = GridResidency.Resident(scene.Grid).Select(static c => (c.X, c.Z)).Distinct().ToHashSet();
        Assert.True(store.Count <= now.Count, $"{store.Count} columns of heights for {now.Count} resident columns");
    }
}
