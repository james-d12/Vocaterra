using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The valley's edge wall (B07, D-game-34 items 6 and 15): it stops the body while the pass is closed, lifts all the way round when the pass opens, and stays lifted after a load; B06's exit corridor is walkable once it is.</summary>
public sealed class BodyBarrierTests
{
    private const int U = VoxelPhysics.UnitsPerBlock;

    private static VoxelSession StreamedSession() =>
        new(TestGame.Data, 42, 0, RegionTestWorld.StreamedScene());

    private static void Teleport(VoxelSession session, int x, int z)
    {
        var grid = session.Scene.Grid;
        var y = grid.Limits.EndY - 1;
        while (y > grid.Limits.MinY && !grid.IsSolid(x, y, z))
        {
            y--;
        }

        session.Simulation.TeleportTo(new BlockPos(x, y + 1, z));
    }

    private static double WalkEast(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([VoxelCommand.Move(1, 0)]);
        }

        return session.Simulation.Player.CenterX / (double)U;
    }

    [Fact]
    public void Given_AClosedPass_When_ABodyWalksEastFromTheValleysEdge_Then_TheWallStopsItAtTheEdge()
    {
        var session = StreamedSession();
        Teleport(session, 380, 208);

        var x = WalkEast(session, 400);

        Assert.False(session.Pass.IsOpen);
        Assert.True(session.Scene.Grid.Barrier!.Locked);
        Assert.InRange(x, 380.0, 384.01);
    }

    [Fact]
    public void Given_AnOpenPass_When_ABodyWalksEastFromTheValleysEdge_Then_ItLeavesThroughTheExitCorridor()
    {
        var session = StreamedSession();
        session.PassCheat = true;
        Teleport(session, 380, 208);

        var x = WalkEast(session, 700);

        Assert.True(session.Pass.IsOpen);
        Assert.False(session.Scene.Grid.Barrier!.Locked);
        Assert.True(x > 420, $"the body reached x {x:0.0}");
    }

    [Fact]
    public void Given_TheWallOnAStreamedGrid_When_BlocksAreAsked_Then_OnlyTheBodyAndNothingElseSeesIt()
    {
        var grid = StreamedSession().Scene.Grid;

        Assert.True(grid.BlocksBody(384, 70, 208));
        Assert.True(grid.BlocksBody(-1, 70, 100));
        Assert.True(grid.BlocksBody(100, 70, 384));
        Assert.True(grid.BlocksBody(100, 70, -1));
        Assert.False(grid.BlocksBody(383, 70, 208));
        Assert.False(grid.IsSolid(384, 70, 208), "the world past the edge is as the generator made it: the ray and the renderer see it");
        grid.Barrier!.Locked = false;
        Assert.False(grid.BlocksBody(384, 70, 208));
    }

    [Fact]
    public void Given_AnOpenPassInASave_When_ItIsLoaded_Then_TheWallStaysLifted()
    {
        var session = StreamedSession();
        session.PassCheat = true;
        session.Step([]);
        var save = SaveGame.Save(session);

        var loaded = SaveGame.Load(save, TestGame.Data, _ => RegionTestWorld.StreamedScene(), credit: session.Credit, bumpSession: false);

        Assert.True(loaded.Pass.IsOpen);
        Assert.False(loaded.Scene.Grid.Barrier!.Locked);
    }

    [Fact]
    public void Given_AClosedPassInASave_When_ItIsLoaded_Then_TheWallStillStands()
    {
        var session = StreamedSession();
        session.Step([]);

        var loaded = SaveGame.Load(SaveGame.Save(session), TestGame.Data, _ => RegionTestWorld.StreamedScene(), credit: session.Credit, bumpSession: false);

        Assert.False(loaded.Pass.IsOpen);
        Assert.True(loaded.Scene.Grid.Barrier!.Locked);
    }

    [Fact]
    public void Given_TheBoundedValley_When_BlocksAreAskedAtItsEdge_Then_ThereIsNoBarrierAndTheLimitsAreTheWall()
    {
        var scene = WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded);

        Assert.Null(scene.Grid.Barrier);
        Assert.True(scene.Grid.BlocksBody(384, 70, 208));
        Assert.True(scene.Grid.BlocksBody(-1, 70, 208));
    }

    [Fact]
    public void Given_TheFactorysStreamedWorld_When_ItIsMade_Then_ItHasALockedWallRoundTheValleysFootprintAndTheStreamedParts()
    {
        var scene = WorldFactory.Create(TestGame.Data, 42, WorldMode.Streamed);

        var barrier = Assert.IsType<BodyBarrier>(scene.Grid.Barrier);
        Assert.True(barrier.Locked);
        Assert.Equal((0, 0, 383, 383), (barrier.MinX, barrier.MinZ, barrier.MaxX, barrier.MaxZ));
        Assert.NotNull(scene.Streaming);
    }
}
