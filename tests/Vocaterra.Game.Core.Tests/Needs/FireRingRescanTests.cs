using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>One fire-ring finder serves the whole session (Y01): a ring built while the game runs is seen after <see cref="FireRings.Rescan"/> by the rest rule, the crafting station and the waking ground.</summary>
public sealed class FireRingRescanTests
{
    private const int Tps = 60;

    // The cell the player wakes at after a death with no new ring: where a ring built while the game runs will stand.
    private static BlockPos NormalWakeCell() => Dying(null, rescan: false).Simulation.Player.FeetBlock;

    // Eight stones round the middle, and a torch on the rim, on the side away from the guide's ring so that it lights no ring but the new one; set while the game runs, so the
    // finder knows nothing of it until Rescan.
    private static void BuildLitRing(VoxelSession session, BlockPos middle)
    {
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (dx != 0 || dz != 0)
                {
                    session.Scene.Grid.SetBlock(middle.X + dx, middle.Y, middle.Z + dz, BlockId.FireRing);
                }
            }
        }

        var guide = session.Scene.Point(VoxelSession.CampFireRing);
        var (sx, sz) = Math.Sign(middle.X - guide.X) is var x and not 0 ? (x, 0) : (0, Math.Sign(middle.Z - guide.Z));
        session.Scene.Grid.SetBlock(middle.X + sx, middle.Y + 1, middle.Z + sz, BlockId.Torch);
    }

    // Flat ground well away from every ring: a stone floor with air over it, so a ring can be built there.
    private static BlockPos Meadow(VoxelSession session)
    {
        var guide = session.Scene.Point(VoxelSession.CampFireRing);
        var centre = new BlockPos(guide.X + 14, guide.Y, guide.Z);
        for (var dx = -4; dx <= 4; dx++)
        {
            for (var dz = -4; dz <= 4; dz++)
            {
                session.Scene.Grid.SetBlock(centre.X + dx, centre.Y - 1, centre.Z + dz, BlockId.Stone);
                for (var dy = 0; dy <= 3; dy++)
                {
                    session.Scene.Grid.SetBlock(centre.X + dx, centre.Y + dy, centre.Z + dz, BlockId.Air);
                }
            }
        }

        Assert.False(session.Fires.NearLit(centre, 8), "the meadow is away from every lit ring");
        return centre;
    }

    private static VoxelSession Dying(BlockPos? ring, bool rescan)
    {
        var session = TestGame.NewSession();
        for (var slot = 0; slot < session.Simulation.Inventory.SlotCount; slot++)
        {
            session.Simulation.Inventory.SetSlot(slot, ItemStack.Empty);
        }

        session.SetNeed(NeedMeter.Hunger, 100);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        session.Simulation.Inventory.Add(ItemId.Wood, 12);
        _ = session.Fires.Middles; // the finder has read the world once
        if (ring is { } middle)
        {
            BuildLitRing(session, middle);
            if (rescan)
            {
                session.Fires.Rescan();
            }
        }

        for (var i = 0; i < Tps; i++)
        {
            session.Step([]);
        }

        Assert.Equal(1, session.DeathCount);
        return session;
    }

    [Fact]
    public void GivenOneFinder_When_TheSessionAndTheSimulationAreAsked_Then_TheyShareIt()
    {
        var session = TestGame.NewSession();

        Assert.Same(session.Simulation.Fires, session.Fires);
    }

    [Fact]
    public void GivenARingBuiltInPlay_When_FiresIsRescanned_Then_TheCraftingStationSeesIt()
    {
        var session = TestGame.NewSession();
        _ = session.Fires.Middles;
        var middle = Meadow(session);
        BuildLitRing(session, middle);
        session.Simulation.TeleportTo(middle);
        Assert.False(session.Simulation.StationInReach(BlockId.FireRing), "the finder has not seen the ring yet");

        session.Fires.Rescan();

        Assert.Contains(middle, session.Fires.Middles);
        Assert.True(session.Simulation.StationInReach(BlockId.FireRing));
    }

    [Fact]
    public void GivenARingBuiltInPlay_When_FiresIsRescanned_Then_TheRestRuleSeesIt()
    {
        static double HealthAfterIdling(bool rescan)
        {
            var session = TestGame.NewSession();
            _ = session.Fires.Middles;
            var middle = Meadow(session);
            BuildLitRing(session, middle);
            if (rescan)
            {
                session.Fires.Rescan();
            }

            session.Simulation.TeleportTo(middle);
            session.SetNeed(NeedMeter.Health, 50);
            for (var i = 0; i < 20 * Tps; i++)
            {
                session.Step([]);
            }

            return session.Needs.Value(NeedMeter.Health);
        }

        Assert.True(HealthAfterIdling(rescan: true) > HealthAfterIdling(rescan: false), "health recovers faster at a fire the finder knows");
    }

    [Fact]
    public void GivenARingBuiltAtTheWakeCell_When_FiresIsRescanned_Then_TheWakingGroundAvoidsItsMiddle()
    {
        var wake = NormalWakeCell();

        Assert.Equal(wake, Dying(wake, rescan: false).Simulation.Player.FeetBlock); // an unseen ring's middle is free ground
        var after = Dying(wake, rescan: true).Simulation.Player.FeetBlock;

        Assert.NotEqual(wake, after);
    }
}
