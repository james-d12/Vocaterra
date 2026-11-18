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

/// <summary>The rest of FarViewTests's tests (B28 split the slow ones into classes of their own, sharing FarViewTestsBase).</summary>
public sealed class FarViewTests(ITestOutputHelper output) : FarViewTestsBase(output)
{
    [Fact]
    public void Given_TheClimateColoursTurnedOff_When_TheViewIsReconfigured_Then_EveryTileIsMadeAgainBecauseTheColoursAreInTheirVertices()
    {
        // B17c: the climate colours are baked into the far tiles' vertex colours, so the setting that swaps the ground function drops and rebuilds them; the same function again keeps them.
        using var pool = new JobPool(0);
        Func<int, int, int, (int Temperature, int Humidity)> ground = static (_, _, _) => (10, 20);
        var view = Make(pool, Small with { Ground = ground });
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000));
        var cached = view.Stats.Cached;
        Assert.True(cached > 0);

        view.Reconfigure(Small with { Ground = ground, Range = 1536 }, Release);
        Assert.Equal(cached, view.Stats.Cached); // the same ground function: the tiles are kept
        Assert.Empty(_released);

        view.Reconfigure(Small with { Ground = null }, Release);
        Assert.Equal(0, view.Stats.Cached);
        Assert.Equal(cached, _released.Count);
        Assert.True(view.Settle(192, 192, Voxel, Release, 120_000)); // and built again with the colour map's middle
        Assert.True(view.Stats.Cached > 0);
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
    public void Given_AReachScale_When_TilesAreSelected_Then_TheRangeReachesFurtherAtTheSidesOfThePicture()
    {
        var plain = FarTileSelector.Select(Small, 192, 192, Voxel);
        var wide = FarTileSelector.Select(Small, 192, 192, Voxel, reachScale: 1.5);
        var camera = new FirstPersonCamera();

        Assert.True(wide.Count > plain.Count);
        Assert.Contains(wide, k => k.NearestDistance(Small, 192, 192) > Small.Range);
        Assert.InRange(FarTerrainRenderer.ReachFor(camera), 1.0, 1.7); // the planar fog ends at range over the cosine of the half field of view
    }

    [Fact]
    public void Given_VoxelViewDistances_When_TheFarNearPlaneIsAsked_Then_ItIsASeventhOfThemBetween12And64Blocks()
    {
        Assert.Equal(12f, FarTerrainRenderer.NearPlaneFor(64));
        Assert.Equal(400 / 7f, FarTerrainRenderer.NearPlaneFor(400), 3);
        Assert.Equal(64f, FarTerrainRenderer.NearPlaneFor(2000));
    }
}
