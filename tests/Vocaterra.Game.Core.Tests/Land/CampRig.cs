using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>The set-up of the camp tests (Y12): a new world on seed 42 whose player owns the north plot and holds the camp kit, and the first spot of the plot a camp fits.</summary>
internal static class CampRig
{
    /// <summary>A session that owns the plot at <see cref="LandTestScene.NorthPlot"/>, holds the camp kit, and stands west of the plot.</summary>
    public static VoxelSession Owner(bool kit = true)
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        session.Day = 2; // «Mi terreno» counts from the second play session
        var (x, z) = LandTestScene.NorthPlot;
        Assert.True(session.SetPendingPlot(x, z).Ok);
        Assert.True(session.OwnPlot());
        if (kit)
        {
            Assert.Equal(0, session.Simulation.GrantItems(ItemId.CampKit, 1));
        }

        StandWest(session);
        return session;
    }

    /// <summary>Puts the player on the ground three blocks west of the plot, in the middle of its depth.</summary>
    public static void StandWest(VoxelSession session)
    {
        var plot = session.OwnedPlot!.Value;
        var x = plot.X - 3;
        var z = plot.Z + (plot.Depth / 2);
        session.Simulation.TeleportTo(new BlockPos(x, CampPlan.GroundOf(session.Scene.Grid, x, z) + 1, z));
    }

    /// <summary>The camp stamp of the bundle.</summary>
    public static StampSpec Shape(VoxelSession session) => session.Data.Scene.Stamps[CampPlan.StampName];

    /// <summary>The plan for a column and a side on the ground of the world as it is.</summary>
    public static CampPlan PlanAt(VoxelSession session, int x, int z, Direction facing) =>
        CampPlan.Around(Shape(session), x, z, CampPlan.GroundOf(session.Scene.Grid, x, z), facing);

    /// <summary>The first column of the owned plot (by row, then by column) where the camp facing the side fits: the plot's checks and a cell to wake on.</summary>
    public static (int X, int Z) Spot(VoxelSession session, Direction facing)
    {
        var plot = session.OwnedPlot!.Value;
        for (var z = plot.Z; z < plot.Z + plot.Depth; z++)
        {
            for (var x = plot.X; x < plot.X + plot.Width; x++)
            {
                var plan = PlanAt(session, x, z, facing);
                if (plan.Check(session.Scene.Grid, session.IsOwned) == CampFault.None && plan.HasWake(session.Scene.Grid))
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException($"No spot for the camp facing {facing} on the plot.");
    }

    /// <summary>Sets the camp up on the first spot of the plot facing the side; the player stands west of the plot.</summary>
    public static CampPlan Place(VoxelSession session, Direction facing = Direction.North)
    {
        var (x, z) = Spot(session, facing);
        StandWest(session);
        var ground = new BlockPos(x, CampPlan.GroundOf(session.Scene.Grid, x, z), z);
        Assert.Equal(VoxelSession.CampResult.Placed, session.UseCampKit(ground, facing));
        return session.Camp!;
    }

    /// <summary>The cell on the rim of the ring (on top of its first stone), where a torch lights the ring.</summary>
    public static BlockPos Rim(CampPlan camp)
    {
        var stone = camp.RingStones[0];
        return new BlockPos(stone.X, stone.Y + 1, stone.Z);
    }

    /// <summary>Lights the camp's ring with a torch set on the rim by the player (the torch is given and the player is put in reach).</summary>
    public static void Light(VoxelSession session)
    {
        var camp = session.Camp!;
        _ = session.Simulation.GrantItems(ItemId.Torch, 1);
        var rim = Rim(camp);
        session.Simulation.TeleportTo(new BlockPos(camp.Wake.X, camp.Wake.Y, camp.Wake.Z));
        Assert.True(VoxelWalker.Place(session, ItemId.Torch, rim));
    }

    /// <summary>The block at a cell.</summary>
    public static BlockId BlockAt(VoxelSession session, BlockPos cell) => session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z);
}
