using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.Tests;

public sealed class FirstPersonCameraTests
{
    private const double Tolerance = 2e-4; // the simulation's table is fixed point with 1 / 65 536 resolution

    private static readonly (int Yaw, int Pitch)[] Poses =
    [
        (0, 0), (1024, 0), (2048, 0), (3072, 0), (Angles.FromDegrees(120), Angles.FromDegrees(-5)), (Angles.FromDegrees(-70), Angles.FromDegrees(40)),
        (500, 1023), (3999, -1023), (17, -300), (2500, 777),
    ];

    [Fact]
    public void TheBasisMatchesTheSimulationsLookVectorForEveryPose()
    {
        foreach (var (yaw, pitch) in Poses)
        {
            var (dx, dy, dz) = Angles.Direction(yaw, pitch);
            var forward = CameraBasis.From(yaw, pitch).Forward;
            Assert.InRange(Math.Abs(forward.X - (dx / (double)Angles.One)), 0, Tolerance);
            Assert.InRange(Math.Abs(forward.Y - (dy / (double)Angles.One)), 0, Tolerance);
            Assert.InRange(Math.Abs(forward.Z - (dz / (double)Angles.One)), 0, Tolerance);
        }
    }

    [Fact]
    public void YawZeroFacesNorthAndTurnsClockwiseSeenFromAbove()
    {
        AssertForward(0, 0, 0, 0, -1); // north is -z
        AssertForward(1024, 0, 1, 0, 0); // east is +x
        AssertForward(2048, 0, 0, 0, 1); // south is +z
        AssertForward(3072, 0, -1, 0, 0); // west is -x
        AssertForward(0, 1023, 0, 1, 0, 0.01); // pitch is positive up
        AssertForward(0, -1023, 0, -1, 0, 0.01);
    }

    private static void AssertForward(int yaw, int pitch, double x, double y, double z, double tolerance = 1e-9)
    {
        var f = CameraBasis.From(yaw, pitch).Forward;
        Assert.InRange(Math.Abs(f.X - x), 0, tolerance);
        Assert.InRange(Math.Abs(f.Y - y), 0, tolerance);
        Assert.InRange(Math.Abs(f.Z - z), 0, tolerance);
    }

    [Fact]
    public void TheViewMatrixSendsTheLookVectorToMinusZAndKeepsRightAndUpOnTheirAxes()
    {
        var camera = new FirstPersonCamera();
        foreach (var (yaw, pitch) in Poses)
        {
            camera.Update(new Vector3(10, 12, 20), yaw, pitch, 1280, 720);
            AssertVector(Vector3.TransformNormal(camera.Forward, camera.View), 0, 0, -1); // MonoGame's camera looks down -z
            AssertVector(Vector3.TransformNormal(camera.Right, camera.View), 1, 0, 0);
            AssertVector(Vector3.TransformNormal(camera.Up, camera.View), 0, 1, 0);
            AssertVector(Vector3.Transform(camera.Eye, camera.View), 0, 0, 0);
            Assert.InRange(Math.Abs(Vector3.Dot(camera.Forward, camera.Right)), 0, 1e-6);
            Assert.InRange(Math.Abs(Vector3.Dot(camera.Forward, camera.Up)), 0, 1e-6);
            Assert.Equal(1.0, camera.Forward.Length(), 5);
        }
    }

    private static void AssertVector(Vector3 v, float x, float y, float z)
    {
        Assert.InRange(Math.Abs(v.X - x), 0, 1e-4);
        Assert.InRange(Math.Abs(v.Y - y), 0, 1e-4);
        Assert.InRange(Math.Abs(v.Z - z), 0, 1e-4);
    }

    [Fact]
    public void APointInTheCompassDirectionLandsInFrontOfTheCameraWhenTheYawFacesIt()
    {
        var camera = new FirstPersonCamera();
        var eye = new Vector3(50, 12, 50);
        var targets = new (int Yaw, Vector3 Offset)[]
        {
            (0, new Vector3(0, 0, -10)), // north
            (1024, new Vector3(10, 0, 0)), // east
            (2048, new Vector3(0, 0, 10)), // south
            (3072, new Vector3(-10, 0, 0)), // west
        };
        foreach (var (yaw, offset) in targets)
        {
            camera.Update(eye, yaw, 0, 1280, 720);
            AssertVector(Vector3.Transform(eye + offset, camera.View), 0, 0, -10);
        }

        // Facing north, a point to the east is on the right (+x in the view) and one to the west on the left; the sign must not flip.
        camera.Update(eye, 0, 0, 1280, 720);
        Assert.True(Vector3.Transform(eye + new Vector3(5, 0, -10), camera.View).X > 0);
        Assert.True(Vector3.Transform(eye + new Vector3(-5, 0, -10), camera.View).X < 0);
        // Looking up, a point above is up in the view.
        camera.Update(eye, 0, 200, 1280, 720);
        Assert.True(Vector3.Transform(eye + new Vector3(0, 3, -10), camera.View).Y > Vector3.Transform(eye + new Vector3(0, -3, -10), camera.View).Y);
    }

    [Fact]
    public void ThePlainProjectionAgreesWithTheEnginesMatrices()
    {
        var camera = new FirstPersonCamera();
        var eye = new Vector3(33.5f, 12.6f, 41.25f);
        foreach (var (yaw, pitch) in Poses)
        {
            camera.Update(eye, yaw, pitch, 1280, 720);
            foreach (var (r, u, d) in new[] { (0f, 0f, 8f), (1.5f, -0.8f, 6f), (-3f, 2f, 12f), (4f, 4f, 20f) })
            {
                var world = eye + (camera.Forward * d) + (camera.Right * r) + (camera.Up * u);
                var clip = Vector4.Transform(new Vector4(world, 1f), camera.View * camera.Projection);
                var px = ((clip.X / clip.W) + 1f) / 2f * 1280f;
                var py = (1f - (clip.Y / clip.W)) / 2f * 720f;
                var point = camera.Screen.Project(world.X, world.Y, world.Z);
                Assert.True(point.InFront);
                Assert.InRange(Math.Abs(point.X - px), 0, 0.6);
                Assert.InRange(Math.Abs(point.Y - py), 0, 0.6);
                Assert.InRange(Math.Abs(point.Depth - d), 0, 1e-3);
            }
        }
    }

    [Fact]
    public void APointStraightAheadProjectsToTheCentreOfTheScreen()
    {
        var camera = new FirstPersonCamera();
        camera.Update(new Vector3(5, 6, 7), 700, -150, 1000, 500);
        var ahead = camera.Eye + (camera.Forward * 9f);
        var p = camera.Screen.Project(ahead.X, ahead.Y, ahead.Z);
        Assert.InRange(Math.Abs(p.X - 500), 0, 1e-3);
        Assert.InRange(Math.Abs(p.Y - 250), 0, 1e-3);
        Assert.True(camera.Frustum.Contains(ahead) == ContainmentType.Contains);
        var behind = camera.Eye - (camera.Forward * 9f);
        Assert.False(camera.Screen.Project(behind.X, behind.Y, behind.Z).InFront);
        Assert.True(camera.Frustum.Contains(behind) == ContainmentType.Disjoint);
    }

    [Fact]
    public void ThePlayersEyeIsWhereTheSimulationPutsIt()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var sim = VoxelSimulation.Create(scene);
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(90), (int)Math.Round(LookControl.DegreesToSteps(-30)))]);
        var body = sim.Player;
        var camera = new FirstPersonCamera();
        camera.Update(new Vector3(body.EyeX / 4096f, body.EyeY / 4096f, body.EyeZ / 4096f), body.Yaw, body.Pitch, 1280, 720);
        // The camera looks along the ray the simulation picks with: a block on that ray is straight ahead on screen.
        var hit = BlockRay.Cast(sim.Grid, body);
        Assert.NotNull(hit);
        var block = hit.Value.Block;
        // The ray passes through the block it hit, so the block's nearest point to the ray is ahead, not behind or to a side.
        var centre = camera.Screen.Project(block.X + 0.5, block.Y + 0.5, block.Z + 0.5);
        Assert.True(centre.InFront);
        Assert.InRange(Math.Abs(centre.Right), 0, 1.0);
        Assert.InRange(Math.Abs(centre.Up), 0, 1.0);
    }
}
