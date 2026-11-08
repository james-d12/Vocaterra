using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Water;

/// <summary>Drowning through the player's needs (water-plan H11): ten health per out-of-air report, the death flow of the bread pilot, the <c>player_drowned</c> hint.</summary>
public sealed class DrowningThroughNeedsTests
{
    private const int Tps = NeedsData.TicksPerSecond;

    private static (int X, int Z, int Floor, int Top) DeepColumn(VoxelGrid grid)
    {
        for (var z = 1; z < grid.SizeZ - 1; z++)
        {
            for (var x = 1; x < grid.SizeX - 1; x++)
            {
                var top = grid.HighestBlock(x, z);
                if (top < 0 || grid.GetBlock(x, top, z) != BlockId.Water)
                {
                    continue;
                }

                var floor = top;
                while (floor > 0 && grid.GetBlock(x, floor - 1, z) == BlockId.Water)
                {
                    floor--;
                }

                if (top - floor + 1 >= 4 && grid.IsSolid(x, floor - 1, z))
                {
                    return (x, z, floor, top);
                }
            }
        }

        throw new InvalidOperationException("The route has no deep water.");
    }

    private static (VoxelSession Session, (int X, int Z, int Floor, int Top) Column) Diving()
    {
        var session = TestGame.NewSession();
        var column = DeepColumn(session.Scene.Grid);
        session.Simulation.TeleportTo(new BlockPos(column.X, column.Floor, column.Z));
        return (session, column);
    }

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static double Health(VoxelSession session) => session.Needs.Value(NeedMeter.Health);

    [Fact]
    public void Given_TheBottomOfTheLake_When_TheAirRunsOut_Then_TenHealthComesOffASecond()
    {
        var (session, _) = Diving();

        Tick(session, Breath.MaxAir - 1);
        Assert.Equal(100, Health(session));
        Tick(session, 1); // the last bubble goes: the first report
        Assert.Equal(90, Health(session));
        Tick(session, Tps - 1);
        Assert.Equal(90, Health(session)); // health does not recover between two reports
        Tick(session, 1);
        Assert.Equal(80, Health(session));
        Assert.Equal(0, session.DeathCount);
    }

    [Fact]
    public void Given_NoAir_When_HealthIsSpent_Then_ThePlayerDiesThroughTheDeathFlowWithTheDrownedHint()
    {
        var (session, _) = Diving();

        Tick(session, Breath.MaxAir + (9 * Tps)); // ten reports from full health

        Assert.Equal(1, session.DeathCount);
        var died = Assert.Single(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.PlayerDied);
        Assert.Equal("player_drowned", died.Detail);
        Assert.Equal(session.Locale.Ui("diedDrowned"), session.DeathLine);
        Assert.Contains("Swim up", session.DeathLine!, StringComparison.Ordinal);
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.PlayerWoke);
        Assert.True(Health(session) > 0); // woken at the fire with the waking health
    }

    [Fact]
    public void Given_ARelaxedPlayer_When_TheyDrown_Then_TheyFaintWithTheBagKept()
    {
        var (session, _) = Diving();
        session.RelaxedNeeds = true;

        Tick(session, Breath.MaxAir + (9 * Tps));

        Assert.Equal(1, session.DeathCount);
        Assert.Equal(session.Locale.Ui("faintedDrowned"), session.DeathLine);
        Assert.Null(session.Chest);
    }

    [Fact]
    public void Given_ADamagedPlayer_When_TheyAreAtTheSurface_Then_NoMoreDamageComes()
    {
        var (session, column) = Diving();
        Tick(session, Breath.MaxAir + Tps); // two reports: 80
        Assert.Equal(80, Health(session));

        session.Simulation.TeleportTo(new BlockPos(column.X, column.Top + 1, column.Z));
        for (var i = 0; i < 6 * Tps; i++)
        {
            session.Step([new VoxelCommand(VoxelCommandKind.Jump)]);
        }

        Assert.True(Health(session) >= 80, "surfacing took more health");
        Assert.True(session.Simulation.Breath.Air > 0);
        Assert.Equal(0, session.DeathCount);
    }

    [Fact]
    public void Given_NeedsOff_When_TheAirRunsOut_Then_NoHealthIsLostAndNobodyDies()
    {
        var (session, _) = Diving();
        session.NeedsEnabled = false;

        Tick(session, Breath.MaxAir + (12 * Tps));

        Assert.True(session.Simulation.Breath.IsEmpty);
        Assert.Equal(100, Health(session));
        Assert.Equal(0, session.DeathCount);
    }

    [Fact]
    public void Given_TheAdapter_When_TheNeedsAreNotRunning_Then_ItTakesNothing_And_WhenRunning_ItTakesTheDamage()
    {
        var needs = new PlayerNeeds(TestGame.Data.Needs);
        var running = false;
        var health = new NeedsHealth(needs, () => running);

        health.Damage(10, DamageCause.Drowning);
        Assert.Equal(100, needs.Value(NeedMeter.Health));

        running = true;
        health.Damage(10, DamageCause.Drowning);
        Assert.Equal(90, needs.Value(NeedMeter.Health));
        Assert.False(health.IsDead);
        health.Damage(500, DamageCause.Drowning);
        Assert.Equal(0, needs.Value(NeedMeter.Health));
        Assert.True(health.IsDead);
    }
}
