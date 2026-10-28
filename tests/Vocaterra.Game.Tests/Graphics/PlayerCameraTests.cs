using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Graphics.Camera;

namespace Vocaterra.Game.Tests.Graphics;

/// <summary>The camera that follows the eye: the eye of the last two ticks, the point between them a frame draws, and the view over the shoulder.</summary>
public sealed class PlayerCameraTests
{
    [Fact]
    public void TheCameraStartsAtTheBodysEyeAndAFrameDrawsThePointBetweenTheLastTwoTicks()
    {
        var sim = VoxelSimulation.Create(RouteScene.Clearing());
        var camera = new PlayerCamera(sim, new LookControl(), new ViewModeState(), sim.Grid.IsSolid);
        var start = camera.CurrentEye;
        Assert.Equal(sim.Player.EyeX / (float)VoxelPhysics.UnitsPerBlock, start.X, 4);
        Assert.Equal(sim.Player.EyeY / (float)VoxelPhysics.UnitsPerBlock, start.Y, 4);

        for (var i = 0; i < 10; i++)
        {
            sim.Step([VoxelCommand.MoveRelative(0, 1)]);
            camera.AfterTick();
        }

        var last = camera.CurrentEye;
        sim.Step([VoxelCommand.MoveRelative(0, 1)]);
        camera.AfterTick();
        Assert.NotEqual(last, camera.CurrentEye);
        AssertNear(last, camera.Place(0f, 1280, 720, 400f, 1280, 720));
        AssertNear(camera.CurrentEye, camera.Place(1f, 1280, 720, 400f, 1280, 720));
        AssertNear(Vector3.Lerp(last, camera.CurrentEye, 0.5f), camera.Place(0.5f, 1280, 720, 400f, 1280, 720));
    }

    [Fact]
    public void OverTheShoulderTheCameraStandsBehindTheEyeAndTheFrameStillReturnsTheEye()
    {
        var sim = VoxelSimulation.Create(RouteScene.Clearing());
        var viewMode = new ViewModeState();
        var camera = new PlayerCamera(sim, new LookControl(), viewMode, sim.Grid.IsSolid);
        AssertNear(camera.Place(1f, 1280, 720, 400f, 1280, 720), camera.Camera.Eye); // first person: the camera is at the eye

        viewMode.Set(CameraMode.ThirdPerson);
        Assert.True(camera.Shoulder);
        var eye = camera.Place(1f, 1280, 720, 400f, 1280, 720);
        AssertNear(camera.CurrentEye, eye);
        Assert.InRange(Vector3.Distance(eye, camera.Camera.Eye), 0.5f, 4f);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}
