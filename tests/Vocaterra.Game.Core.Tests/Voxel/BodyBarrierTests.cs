using Vocaterra.Game.Core.Navigation;
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

    [Fact]
    public void Given_LavaOutsideTheLockedWall_When_ABodyWalksIntoIt_Then_TheWallStopsItAndOnceTheWallIsLiftedItSinksIn()
    {
        var grid = RegionTestWorld.StreamedScene().Grid;
        grid.Barrier!.Locked = false; // the ground is found with the wall down, then it goes up again
        var inside = GroundFinder.StandingAt(grid, 382, 208)!.Value;
        var outside = GroundFinder.StandingAt(grid, 384, 208)!.Value;
        var beyond = GroundFinder.StandingAt(grid, 385, 208)!.Value;
        Assert.True(Math.Abs(outside.Y - inside.Y) <= 1 && Math.Abs(beyond.Y - outside.Y) <= 1, "the road is a ramp the body can walk");
        foreach (var cell in new[] { outside, beyond })
        {
            for (var dy = 0; dy < 3; dy++)
            {
                Assert.True(grid.SetBlock(cell.X, cell.Y + dy, 208, BlockId.Lava)); // the first cells outside the valley's footprint, filled with lava over the ground
            }
        }

        grid.Barrier.Locked = true;

        // A body's box inside the lava just outside the wall: free of the world's blocks (lava does not stop a moving body), but the wall stops it, so it may not be there while the wall stands.
        var x = (384 * U) + ((U - VoxelPhysics.PlayerWidth) / 2);
        var z = (208 * U) + ((U - VoxelPhysics.PlayerWidth) / 2);
        Assert.False(VoxelCollider.IsFree(grid, x, outside.Y * U, z), "the wall stops the body in lava outside it");
        Assert.True(grid.IsWalled(384, 208));

        grid.Barrier.Locked = false;

        Assert.False(grid.IsWalled(384, 208));
        Assert.True(VoxelCollider.IsFree(grid, x, outside.Y * U, z), "with the wall lifted the lava is only lava: nothing stops the body, which sinks in");
    }
}
