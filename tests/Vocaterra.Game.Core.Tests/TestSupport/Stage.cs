using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>Moves a headless player about the route for the tests of the NPCs: next to an NPC, next to the water, and the fill itself.</summary>
internal static class Stage
{
    /// <summary>The cell the scripted run would stop at to talk to the NPC (about two blocks away, with a clear line to it), from where the player is now.</summary>
    public static BlockPos StandOffOf(VoxelSession session, string npc) =>
        NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc(npc).Feet, session.Simulation.Player.FeetBlock)
        ?? throw new InvalidOperationException($"No stand-off cell around {npc}.");

    /// <summary>Puts the player at the NPC's stand-off cell at once (a debug move, no walking) and turns to face it.</summary>
    public static void TeleportToNpc(VoxelSession session, string npc)
    {
        session.Simulation.TeleportTo(StandOffOf(session, npc));
        Face(session, npc);
    }

    /// <summary>Walks the player to the NPC's stand-off cell along the shortest path and turns to face it. Returns false when there is no path.</summary>
    public static bool WalkToNpc(VoxelSession session, string npc)
    {
        var stand = StandOffOf(session, npc);
        if (!VoxelWalker.WalkTo(session, cell => cell == stand))
        {
            return false;
        }

        Face(session, npc);
        return true;
    }

    /// <summary>Turns the body to the NPC's chest.</summary>
    public static void Face(VoxelSession session, string npc)
    {
        var feet = session.Scene.Npc(npc).Feet;
        var body = session.Simulation.Player;
        const double U = VoxelPhysics.UnitsPerBlock;
        var dx = feet.X + 0.5 - (body.EyeX / U);
        var dy = feet.Y + 1.2 - (body.EyeY / U);
        var dz = feet.Z + 0.5 - (body.EyeZ / U);
        var yaw = Angles.Wrap((int)Math.Round(Math.Atan2(dx, -dz) * Angles.StepsPerTurn / (2 * Math.PI)));
        var pitch = Math.Clamp((int)Math.Round(Math.Atan2(dy, Math.Sqrt((dx * dx) + (dz * dz))) * Angles.StepsPerTurn / (2 * Math.PI)), -1023, 1023);
        session.Step([VoxelCommand.Look(yaw, pitch)]);
    }

    /// <summary>
    /// The nearest river block to the player, as the guide's facts find it: the nearest water of the river's body (the water joined to the scene's <c>river</c>
    /// point), not the village well's (village-plan V04), which is nearer to the villager (a bottle can be filled there too since V10).
    /// </summary>
    public static BlockPos NearestRiver(VoxelSession session) =>
        TestGame.FactsOf(session).NearestOfNounJoinedTo(TestGame.ViewpointOf(session), "río", session.Scene.Point("river"))!.Cell.ToBlock();

    /// <summary>Walks to the river bank next to a water block and returns that water block (the surface block, in reach of the eye).</summary>
    public static BlockPos WalkToWater(VoxelSession session, BlockPos water)
    {
        Assert.True(VoxelWalker.WalkTo(session, cell => CellsApart(cell, water) <= 2.5), "no path to the bank");
        return water;
    }

    /// <summary>Holds the interact command on a water block until the bottle is full or the limit runs out. Returns the ticks it took, or -1.</summary>
    public static int Fill(VoxelSession session, BlockPos water, int maxTicks = 120)
    {
        var (yaw, pitch) = VoxelWalker.AimAt(session, water);
        var before = session.Simulation.Inventory.Count(ItemId.WaterBottle);
        for (var ticks = 1; ticks <= maxTicks; ticks++)
        {
            session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(water.X, water.Y, water.Z)]);
            if (session.Simulation.Inventory.Count(ItemId.WaterBottle) > before)
            {
                return ticks;
            }
        }

        return -1;
    }

    private static double CellsApart(BlockPos a, BlockPos b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
