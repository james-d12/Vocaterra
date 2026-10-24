using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Tests;

public sealed class BeaconArrowTests
{
    private const int W = 1280;
    private const int H = 720;
    private const int Margin = 56;

    // A camera at the origin facing north, level, 70 degrees of vertical field of view.
    private static ScreenProjection FacingNorth(int yaw = 0, int pitch = 0) => new(0, 0, 0, yaw, pitch, 70, W, H);

    [Fact]
    public void AnythingStraightAheadIsInTheMiddleOfTheScreen()
    {
        var camera = FacingNorth();
        var p = camera.Project(0, 0, -10);
        Assert.InRange(Math.Abs(p.X - (W / 2.0)), 0, 1e-9);
        Assert.InRange(Math.Abs(p.Y - (H / 2.0)), 0, 1e-9);
        Assert.Equal(10.0, p.Depth, 9);
        Assert.True(p.InFront);
    }

    [Fact]
    public void ARightHandPointIsToTheRightAndAnUpperOneIsHigherOnTheScreenWhichCountsDownwards()
    {
        var camera = FacingNorth();
        var right = camera.Project(3, 0, -10);
        Assert.True(right.X > W / 2.0);
        var up = camera.Project(0, 3, -10);
        Assert.True(up.Y < H / 2.0);
        Assert.True(camera.Project(-3, 0, -10).X < W / 2.0);
        Assert.True(camera.Project(0, -3, -10).Y > H / 2.0);
    }

    [Fact]
    public void ThePerspectiveShrinksWithDistanceAndTheFieldOfViewSetsTheEdge()
    {
        var camera = FacingNorth();
        var near = camera.Project(1, 0, -5).X - (W / 2.0);
        var far = camera.Project(1, 0, -20).X - (W / 2.0);
        Assert.Equal(near / 4.0, far, 6);
        // At 10 blocks the top edge of the screen is at tan(35 degrees) * 10 above the line of sight.
        var top = camera.Project(0, 10 * Math.Tan(35 * Math.PI / 180), -10);
        Assert.InRange(Math.Abs(top.Y), 0, 1e-6);
    }

    [Fact]
    public void ATargetOnScreenGetsNoArrow()
    {
        var arrow = EdgeArrow.For(FacingNorth().Project(2, 1, -12), W, H, Margin);
        Assert.True(arrow.OnScreen);
    }

    [Fact]
    public void ATargetToTheRightPutsTheArrowOnTheRightEdgePointingRight()
    {
        // Facing north, a target far to the east.
        var arrow = EdgeArrow.For(FacingNorth().Project(50, 0, -5), W, H, Margin);
        Assert.False(arrow.OnScreen);
        Assert.Equal(W - Margin, arrow.X, 6);
        Assert.Equal(H / 2.0, arrow.Y, 6);
        Assert.InRange(Math.Abs(arrow.Angle), 0, 1e-9); // 0 is to the right
    }

    [Fact]
    public void ATargetToTheLeftPutsTheArrowOnTheLeftEdgePointingLeft()
    {
        var arrow = EdgeArrow.For(FacingNorth().Project(-50, 0, -5), W, H, Margin);
        Assert.False(arrow.OnScreen);
        Assert.Equal(Margin, arrow.X, 6);
        Assert.Equal(H / 2.0, arrow.Y, 6);
        Assert.InRange(Math.Abs(Math.Abs(arrow.Angle) - Math.PI), 0, 1e-9);
    }

    [Fact]
    public void ATargetHighAbovePointsUpAndOneBelowPointsDownWithScreenYCountingDownwards()
    {
        var up = EdgeArrow.For(FacingNorth().Project(0, 80, -5), W, H, Margin);
        Assert.False(up.OnScreen);
        Assert.Equal(Margin, up.Y, 6);
        Assert.Equal(-Math.PI / 2, up.Angle, 9); // the sprite batch's angles grow clockwise on screen, so up is -90 degrees
        var down = EdgeArrow.For(FacingNorth().Project(0, -80, -5), W, H, Margin);
        Assert.Equal(H - Margin, down.Y, 6);
        Assert.Equal(Math.PI / 2, down.Angle, 9);
    }

    [Fact]
    public void ATargetBehindTheCameraPointsTheWayRoundAndNeverClaimsToBeOnScreen()
    {
        var camera = FacingNorth();
        // Behind and a little to the east: the shorter turn is to the right, so the arrow is on the right half.
        var behindRight = EdgeArrow.For(camera.Project(4, 0, 20), W, H, Margin);
        Assert.False(behindRight.OnScreen);
        Assert.True(behindRight.X > W / 2.0);
        var behindLeft = EdgeArrow.For(camera.Project(-4, 0, 20), W, H, Margin);
        Assert.False(behindLeft.OnScreen);
        Assert.True(behindLeft.X < W / 2.0);
        // Exactly behind: a defined answer (down), not a division by zero.
        var straightBehind = EdgeArrow.For(camera.Project(0, 0, 20), W, H, Margin);
        Assert.False(straightBehind.OnScreen);
        Assert.True(double.IsFinite(straightBehind.X) && double.IsFinite(straightBehind.Y) && double.IsFinite(straightBehind.Angle));
    }

    [Fact]
    public void TheArrowFollowsTheYawSoTurningTowardsTheTargetBringsItOnScreen()
    {
        // A target due east. Facing north it is off to the right; facing east it is straight ahead and on screen.
        var north = EdgeArrow.For(FacingNorth(0).Project(30, 0, 0), W, H, Margin);
        Assert.False(north.OnScreen);
        Assert.True(north.X > W / 2.0);
        var east = EdgeArrow.For(FacingNorth(Angles.FromDegrees(90)).Project(30, 0, 0), W, H, Margin);
        Assert.True(east.OnScreen);
        var south = EdgeArrow.For(FacingNorth(Angles.FromDegrees(180)).Project(30, 0, 0), W, H, Margin);
        Assert.False(south.OnScreen);
        Assert.True(south.X < W / 2.0); // facing south, east is on the left
    }

    [Fact]
    public void WhereverTheTargetIsTheArrowStaysInsideTheMargin()
    {
        for (var yaw = 0; yaw < 4096; yaw += 211)
        {
            for (var pitch = -1000; pitch <= 1000; pitch += 400)
            {
                var camera = new ScreenProjection(10, 12, 10, yaw, pitch, 70, W, H);
                for (var dx = -60; dx <= 60; dx += 15)
                {
                    for (var dy = -20; dy <= 20; dy += 10)
                    {
                        for (var dz = -60; dz <= 60; dz += 15)
                        {
                            var arrow = EdgeArrow.For(camera.Project(10 + dx, 12 + dy, 10 + dz), W, H, Margin);
                            Assert.InRange(arrow.X, Margin - 1e-6, W - Margin + 1e-6);
                            Assert.InRange(arrow.Y, Margin - 1e-6, H - Margin + 1e-6);
                            Assert.True(double.IsFinite(arrow.Angle));
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TheBeaconHoldsItsTargetAndClearsWhenThePlayerArrives()
    {
        var beacon = new VoxelBeacon();
        Assert.False(beacon.IsSet);
        Assert.False(beacon.ClearIfReached(0, 0));
        beacon.Set(new BlockPos(60, 7, 82), "río");
        Assert.True(beacon.IsSet);
        Assert.Equal("río", beacon.Label);
        Assert.False(beacon.ClearIfReached(20, 20));
        Assert.True(beacon.IsSet);
        Assert.False(beacon.ClearIfReached(60.5, 82.5 - VoxelBeacon.ReachedDistance - 0.5));
        Assert.True(beacon.ClearIfReached(60.5, 82.5 - VoxelBeacon.ReachedDistance + 0.5));
        Assert.False(beacon.IsSet);
        beacon.Set(new BlockPos(1, 2, 3));
        beacon.Clear();
        Assert.Null(beacon.Target);
        Assert.Null(beacon.Label);
    }

    [Fact]
    public void TheBeaconCanBeSetOnANamedPointOfTheScene()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var beacon = new VoxelBeacon();
        Assert.True(beacon.SetPoint(scene, "river", PointLabels.For("river")));
        Assert.Equal(scene.Point("river"), beacon.Target);
        Assert.Equal("río", beacon.Label);
        Assert.False(beacon.SetPoint(scene, "nowhere"));
        Assert.Equal(scene.Point("river"), beacon.Target); // a miss changes nothing
        foreach (var point in scene.Points.Keys)
        {
            Assert.NotEqual(string.Empty, PointLabels.For(point));
        }
    }

    [Fact]
    public void TheBeaconColumnIsTwoTranslucentBoxesOfSixFacesThatPulse()
    {
        var block = new BlockPos(60, 7, 82);
        var low = OverlayMeshes.Beacon(block, 0);
        var high = OverlayMeshes.Beacon(block, 30);
        Assert.Equal(12, low.FaceCount);
        var top = low.Vertices.ToArray().Max(static v => v.Y);
        Assert.Equal(block.Y + 1 + OverlayMeshes.BeaconHeight, top, 3);
        Assert.Equal(block.Y + 1, low.Vertices.ToArray().Min(static v => v.Y), 3);
        var alphaLow = low.Vertices[0].Color >> 24;
        var alphaHigh = high.Vertices[0].Color >> 24;
        Assert.NotEqual(alphaLow, alphaHigh);
        Assert.InRange(alphaLow, 30u, 200u);
        Assert.InRange(alphaHigh, 30u, 200u);
        Assert.All(low.Vertices.ToArray(), static v => Assert.InRange(v.X, 59.0f, 62.0f));
    }

    [Fact]
    public void TheHighlightIsATwelveBarFrameOutsideTheBlock()
    {
        var frame = OverlayMeshes.Highlight(new BlockPos(9, 11, 17));
        Assert.Equal(OverlayMeshes.HighlightBars * 6, frame.FaceCount);
        var v = frame.Vertices.ToArray();
        Assert.True(v.Min(static p => p.X) < 9f && v.Max(static p => p.X) > 10f);
        Assert.True(v.Min(static p => p.Y) < 11f && v.Max(static p => p.Y) > 12f);
        Assert.True(v.Min(static p => p.Z) < 17f && v.Max(static p => p.Z) > 18f);
    }
}
