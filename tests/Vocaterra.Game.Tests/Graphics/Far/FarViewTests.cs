using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's tiles, cached and scheduled (B07): built on the shared workers, kept by key so walking builds only what newly becomes a leaf, stood in for by a coarser tile while the finer one builds, capped, and untouched by the player's edits.</summary>
public sealed class FarViewTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Small = new(320, MaxTiles: 400, TreeLevels: 1);

    // A small voxel zone, so a settled view is a few tiles (a tile takes a good deal longer to build in Debug).
    private const int Voxel = 64;

    private readonly List<FarTile> _released = [];

    private void Release(FarTile tile) => _released.Add(tile);

    private static FarView Make(JobPool pool, FarViewSettings? settings = null) => new(StreamedWorlds.Composition.World.Far, settings ?? Small, pool);

    [Fact]
    public void Given_AFarView_When_ItSettles_Then_EveryLeafIsBuiltAndInTheDrawListNearestFirst()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);

        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));

        var leaves = FarTileSelector.Select(Small, 192, 192, Voxel);
        Assert.Equal(0, view.Missing);
        Assert.Equal(0, view.Failed);
        Assert.Equal(leaves.Count, view.Draw.Count);
        Assert.Equal(leaves, view.Draw.Select(static t => t.Key).ToList());
        Assert.All(view.Draw, t => Assert.True(t.IsBuilt));
        var stats = view.Stats;
        Assert.Equal(leaves.Count, stats.Cached);
        Assert.Equal(leaves.Count, stats.Built);
        Assert.Equal(view.Draw.Sum(static t => (long)t.Triangles), stats.Triangles);
        Assert.True(stats.BuildMilliseconds > 0 && stats.Bytes > 0);
        output.WriteLine($"320 blocks: {leaves.Count} tiles, {stats.Triangles} triangles, {stats.Bytes / 1048576.0:0.0} MB, built in {stats.BuildMilliseconds:0} ms (longest {stats.LongestBuildMilliseconds:0.0} ms), Debug, one thread");
    }

    [Fact]
    public void Given_ASettledView_When_TheEyeMovesAFewBlocks_Then_NothingIsBuiltAgain()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var built = view.Stats.Built;

        view.Update(193.5, 192.5, Voxel, Release, null);
        view.Update(194, 191, Voxel, Release, null);

        Assert.Equal(built, view.Stats.Built);
        Assert.Equal(0, view.Missing);
    }

    [Fact]
    public void Given_ASettledView_When_TheEyeWalksAcrossATileEdge_Then_OnlyTheNewLeavesAreBuiltAndTheRestAreKept()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var before = view.Draw.Select(static t => t.Key).ToHashSet();
        var built = view.Stats.Built;

        Assert.True(view.Settle(192 + 30, 192, Voxel, Release, 120_000));

        var after = view.Draw.Select(static t => t.Key).ToHashSet();
        var fresh = after.Except(before).Count();
        Assert.Equal(built + fresh, view.Stats.Built);
        Assert.True(fresh < after.Count, "the tiles away from the edge are the same tiles");
        Assert.True(after.Intersect(before).Count() > after.Count / 2);
    }

    [Fact]
    public void Given_ALeafNotBuiltYetWhoseParentIs_When_TheViewIsAssembled_Then_TheParentStandsInBehind()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(5000, 5000, Voxel, Release, 120_000)); // far from the valley: coarse tiles near the eye
        var parents = view.Draw.Select(static t => t.Key).Where(static k => k.Level > 0).ToList();
        Assert.NotEmpty(parents);

        // Walk right up to a coarse tile: its children become the leaves and are not built yet; one inline job a frame is built.
        var lowest = parents.Min(static k => k.Level);
        var target = parents.Where(k => k.Level == lowest).OrderBy(k => k.NearestDistance(Small, 5000, 5000)).Last();
        var eyeX = target.MinX(Small) + (Small.SizeOf(target.Level) / 2.0);
        var eyeZ = target.MinZ(Small) + (Small.SizeOf(target.Level) / 2.0);
        view.Update(eyeX, eyeZ, Voxel, Release, 0.0);

        Assert.True(view.Missing > 0);
        Assert.True(view.FallbackCount > 0, "a built ancestor stands in for the leaves that are not built");
        Assert.All(view.Draw.Take(view.FallbackCount), t => Assert.True(t.IsBuilt));

        Assert.True(view.Settle(eyeX, eyeZ, Voxel, Release, 120_000));
        Assert.Equal(0, view.FallbackCount);
    }

    [Fact]
    public void Given_ACapOnTheTiles_When_TheEyeWalksFar_Then_TheFarthestUnusedTilesAreDroppedAndFreed()
    {
        using var pool = new JobPool(0);
        var capped = new FarViewSettings(320, MaxTiles: 14, TreeLevels: 1);
        var view = Make(pool, capped);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var first = view.Stats.Cached;

        Assert.True(view.Settle(192 + 3000, 192, Voxel, Release, 120_000));

        Assert.True(first > 12);
        Assert.InRange(view.Stats.Cached, 1, Math.Max(14, view.Draw.Count));
        Assert.NotEmpty(_released);
    }

    [Fact]
    public void Given_ASettledView_When_RealChunksComeIntoTheStore_Then_TheTileOverThemIsMadeAgainAndTheRestAreKept()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var nearest = view.Draw.First(static t => t.Key.Level == 0);
        var built = view.Stats.Built;
        Assert.True(nearest.Triangles > 0);

        // The real chunks over the tile's footprint are all air (a quarried-away hill, say): the tile must follow.
        view.World.IngestStack(nearest.Key.Tx, nearest.Key.Tz, (_, _, _) => null);
        view.Update(192, 192, Voxel, Release, null);

        Assert.True(view.Stale > 0 || view.Stats.Built > built);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        Assert.Equal(0, view.Stale);
        Assert.Equal(0, view.Failed);
        Assert.Equal(0, nearest.Triangles);
        Assert.InRange(view.Stats.Built - built, 1, 6); // that tile, and the coarser tiles that came to hold the real section above it
        Assert.Empty(_released);
    }

    [Fact]
    public void Given_ANewRange_When_TheViewIsReconfigured_Then_TheTilesStayForTheSameShapeAndGoForAnother()
    {
        using var pool = new JobPool(0);
        var view = Make(pool);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var cached = view.Stats.Cached;

        view.Reconfigure(Small.WithRange(1536), Release);
        Assert.Equal(cached, view.Stats.Cached); // the same shape: the tiles are kept
        Assert.Empty(_released);

        view.Reconfigure(Small with { Levels = Small.Levels - 1 }, Release);
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(cached, _released.Count);
    }

    [Fact]
    public void Given_TheFarViewOff_When_ItIsUpdated_Then_NothingIsBuiltOrDrawn()
    {
        using var pool = new JobPool(0);
        var view = Make(pool, FarViewSettings.Off);

        view.Update(0, 0, 400, Release, null);

        Assert.Empty(view.Draw);
        Assert.Equal(0, view.Stats.Built);
    }

    [Fact]
    public void Given_ATileOverTheValleyBeforeAndAfterAnEdit_When_ItIsBuilt_Then_TheyAreIdenticalBecauseEditsNeverShowAtADistance()
    {
        var scene = StreamedWorlds.Scene();
        var key = new FarTileKey(0, 1, 1); // 128 to 256 blocks: the valley's own ground
        var before = FarTileMesher.Build(scene.Streaming!.Far.FarWorld(new Vocaterra.Game.Core.World.Lod.LodStore(), Small.TreeLevels), Small, key);
        var cell = new Vocaterra.Game.Core.Voxel.BlockPos(160, 100, 160);
        Assert.True(scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, Vocaterra.Game.Core.Voxel.BlockId.Stone));
        Assert.True(scene.Grid.SetBlock(cell.X, 40, cell.Z, Vocaterra.Game.Core.Voxel.BlockId.Air));

        var after = FarTileMesher.Build(scene.Streaming.Far.FarWorld(new Vocaterra.Game.Core.World.Lod.LodStore(), Small.TreeLevels), Small, key);

        Assert.True(before.Vertices.SequenceEqual(after.Vertices));
    }

    [Fact]
    public void Given_ThePresets_When_TheirFarRangesAreRead_Then_HighReachesSixteenKilometresAndEachLevelIsFartherThanTheLast()
    {
        var ranges = new[] { QualityLevel.Low, QualityLevel.Medium, QualityLevel.High }.Select(static l => QualityPreset.For(l).FarRange).ToList();

        Assert.Equal(16384, ranges[2]);
        Assert.Equal(ranges.Order().ToList(), ranges);
        Assert.Equal(3, ranges.Distinct().Count());
        Assert.True(QualityPreset.For(QualityLevel.High).Far.Enabled);
        Assert.Equal(0, QualityPreset.For(QualityLevel.High).WithFarRange(0).FarRange);
        Assert.False(QualityPreset.For(QualityLevel.High).WithFarRange(0).Far.Enabled);
    }

    [Fact]
    public void Given_TheFarPass_When_ItsProjectionIsRead_Then_ItsNearPlaneIsAFewBlocksItsFarPlaneIsTheRangeAndDepthHoldsAtSixteenKilometres()
    {
        var camera = new FirstPersonCamera();
        camera.Update(new Vector3(0, 70, 0), 0, 0, 1920, 1080, 600f);

        var projection = FarTerrainRenderer.ProjectionFor(camera, 16384);

        var near = projection.M43 / projection.M33;
        var far = projection.M43 / (projection.M33 + 1f);
        Assert.Equal(FarTerrainRenderer.NearPlane, near, 2);
        Assert.InRange(far, 16384f, 17500f);
        // The depth buffer's step at the end of the range, with 24 bits: z squared over near times 2^24.
        var step = 16384.0 * 16384.0 / (near * 16_777_216.0);
        Assert.True(step < 2.0, $"the depth steps by {step:0.00} blocks at 16 km");
        Assert.True(camera.Projection.M22 / camera.Projection.M11 > 1.7, "the aspect is the camera's");
        Assert.Equal(camera.Projection.M22, projection.M22, 4);
    }

    [Fact]
    public void Given_ATallStreamedWorld_When_ThePeakAtThreeHundredIsInView_Then_TheFarPlaneCoversItsFarCornerWhileItsFootIsInView()
    {
        const int View = 400;
        var plane = VoxelRenderer.FarPlaneFor(View, 384);
        var peakAcross = View + 16.0; // the chunk's far edge across, the foot of the peak just in view
        var distance = Math.Sqrt((peakAcross * peakAcross) + ((300 + 16 - 70.0) * (300 + 16 - 70.0)));

        Assert.True(plane > distance, $"far plane {plane} against the peak's {distance}");
        Assert.True(plane > VoxelRenderer.FarPlaneFor(View), "the valley's plane would cut the peak");
        Assert.True(VoxelRenderer.FarPlaneFor(View) < distance + 100 || plane > VoxelRenderer.FarPlaneFor(View));
    }

    [Fact]
    public void Given_TheFarViewsHaze_When_ItsFogIsAsked_Then_ItReachesTheRangeInClearWeatherAndComesInWithTheWeather()
    {
        Assert.Equal(16384f, VoxelRenderer.FarFogEnd(400, 16384), 1);
        var end = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f }.Select(f => VoxelRenderer.FarFogEnd(400, 16384, f)).ToList();
        Assert.Equal(end.OrderDescending().ToList(), end);
        Assert.InRange(end[^1], 200f, 1500f);
        Assert.True(VoxelRenderer.FarFogStartFraction < VoxelRenderer.FogStartFraction);
        Assert.True(VoxelRenderer.FarFogEnd(400, 16384) * VoxelRenderer.FarFogStartFraction > 400, "the voxel land stays clear");
    }

    private sealed class BrokenDisk : Vocaterra.Game.Core.World.Lod.ILodPersistence
    {
        public byte[]? Load(Vocaterra.Game.Core.World.Lod.LodSectionKey key) => throw new InvalidOperationException("a disk that fails");

        public void Save(Vocaterra.Game.Core.World.Lod.LodSectionKey key, byte[] blob)
        {
        }
    }

    [Fact]
    public void Given_ATileWhoseBuildThrows_When_TheViewSettles_Then_TheKeyIsReleasedTheOthersAreDrawnAndTheSettleDoesNotWait()
    {
        using var pool = new JobPool(0);
        var view = new FarView(StreamedWorlds.Composition.World.Far, Small, pool, new Vocaterra.Game.Core.World.Lod.LodStore(new BrokenDisk()));

        Assert.True(view.Settle(192, 192, Voxel, Release, 60_000));

        Assert.True(view.Failed > 0);
        Assert.Equal(0, view.Missing);
        Assert.Equal(0, view.Stats.Building);
    }

    [Fact]
    public void Given_ASectionChangedWhileItsTileIsBuilding_When_TheViewSettles_Then_TheTileIsMadeAgain()
    {
        using var pool = new JobPool(0);
        var store = new Vocaterra.Game.Core.World.Lod.LodStore();
        var view = new FarView(StreamedWorlds.Composition.World.Far, Small, pool, store);
        view.Update(192, 192, Voxel, Release, 0); // everything is asked for and (the budget being nothing) at most one is built
        var key = FarTileSelector.Select(Small, 192, 192, Voxel)[^1];
        Assert.True(view.Stats.Building > 0);

        store.Changed.Enqueue(new Vocaterra.Game.Core.World.Lod.LodSectionKey(key.Level, key.Tx, 0, key.Tz));
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));

        Assert.Equal(0, view.Missing);
        Assert.True(view.Stats.Built > FarTileSelector.Select(Small, 192, 192, Voxel).Count - 1);
    }
}
