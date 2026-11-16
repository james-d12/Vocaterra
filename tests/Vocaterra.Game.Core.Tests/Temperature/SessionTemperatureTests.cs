using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>What the temperature does in the session (I06): the band's walk factor, the HUD's cold icon, «Tengo frío» once, the needs-off aid, determinism and what the world sets.</summary>
public sealed class SessionTemperatureTests
{
    private static VoxelSession Held(int tenths)
    {
        var session = TestGame.NewSession();
        session.Temperature.HeldTenths = tenths;
        return session;
    }

    private static int Walk(VoxelSession session)
    {
        session.Step([]);
        return session.Simulation.WalkPermille;
    }

    [Theory]
    [InlineData(250, 1000)] // warm
    [InlineData(60, 1000)] // cool
    [InlineData(-20, 850)] // cold
    [InlineData(-80, 650)] // very cold
    public void TheBandsWalkFactorMultipliesTheWalkingSpeed(int held, int permille) => Assert.Equal(permille, Walk(Held(held)));

    [Fact]
    public void AWarmFeelingLeavesTheWalkingSpeedExactlyAsItWasWithNoTemperatureAtAll()
    {
        var warm = Held(250);
        var plain = TestGame.NewSession();
        plain.Temperature.HeldTenths = 250;
        plain.Step([]);

        Assert.Equal(1000, Walk(warm));
        Assert.Equal(warm.Simulation.WalkPermille, plain.Simulation.WalkPermille);
    }

    [Fact]
    public void TheColdFactorMultipliesWithTheNeedsAndTheSprint()
    {
        var session = Held(-20);
        session.SprintHeld = true;
        session.Step([Vocaterra.Game.Core.Simulation.VoxelCommand.Move(0, 1)]);

        var sprint = session.Data.Needs.Stamina.SprintSpeedFactor;
        Assert.Equal((int)Math.Round(1000 * 0.85 * sprint), session.Simulation.WalkPermille);
    }

    [Fact]
    public void WithTheNeedsOffTheColdSlowsNothingAndNoLineOrIconComes()
    {
        var session = Held(-80);
        session.NeedsEnabled = false;
        var hud = new NeedsHud(session);

        Assert.Equal(1000, Walk(session));
        hud.Update(session.Simulation.Tick);
        Assert.Null(hud.ColdIcon);
        Assert.Null(hud.Announcement);
        Assert.Equal(TemperatureRig.Data.BandFor(-80), session.Temperature.Current!.Value.Band); // the reading is still kept, for F3
    }

    [Fact]
    public void ColdNeverHarmsTheNeedsOrTheHealth()
    {
        var session = Held(-300);
        var health = session.Needs.Value(NeedMeter.Health);
        var warmSession = Held(250);

        for (var i = 0; i < 600; i++)
        {
            session.Step([]);
            warmSession.Step([]);
        }

        Assert.Equal(warmSession.Needs.Value(NeedMeter.Health), session.Needs.Value(NeedMeter.Health));
        Assert.True(session.Needs.Value(NeedMeter.Health) >= health);
        Assert.False(session.Needs.IsDead);
        foreach (var meter in new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep, NeedMeter.Stamina })
        {
            Assert.Equal(warmSession.Needs.Value(meter), session.Needs.Value(meter));
        }
    }

    [Fact]
    public void TheColdIconShowsWhileTheBandIsColdWithItsNotchesAndGoesWhenWarm()
    {
        var session = Held(250);
        var hud = new NeedsHud(session);
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        Assert.Null(hud.ColdIcon);

        session.Temperature.HeldTenths = -20;
        session.Step([]);
        Assert.Equal(new ColdIcon(1, 2), hud.ColdIcon);
        session.Temperature.HeldTenths = -80;
        session.Step([]);
        Assert.Equal(new ColdIcon(2, 2), hud.ColdIcon);
        session.Temperature.HeldTenths = 60; // cool
        session.Step([]);
        Assert.Null(hud.ColdIcon);
    }

    [Fact]
    public void TengoFrioIsSaidOncePerEntryIntoTheColdWithItsEnglishAndAsound()
    {
        var session = Held(250);
        var hud = new NeedsHud(session);
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        Assert.Null(hud.Announcement);
        _ = hud.TakeSounds();

        session.Temperature.HeldTenths = -20;
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        var line = hud.Announcement;
        Assert.NotNull(line);
        Assert.Equal("Tengo frío.", line.Spanish);
        Assert.Equal(session.Locale.Text("temperature.cold"), line.English);
        Assert.Null(line.Meter);
        Assert.Equal(1, hud.TakeSounds());

        // Staying cold, even deeper, says nothing more.
        for (var i = 0; i < NeedsHud.DisplayTicks + 5; i++)
        {
            session.Step([]);
            hud.Update(session.Simulation.Tick);
        }

        session.Temperature.HeldTenths = -90;
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        Assert.Null(hud.Announcement);
        Assert.Equal(0, hud.TakeSounds());

        // Out of the cold and in again says it again.
        session.Temperature.HeldTenths = 100;
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        session.Temperature.HeldTenths = -20;
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        Assert.Equal("Tengo frío.", hud.Announcement?.Spanish);
        Assert.Equal(1, hud.TakeSounds());
    }

    [Fact]
    public void TheNeedsLinesStillComeBesideTheColdLine()
    {
        var session = Held(-20);
        var hud = new NeedsHud(session);
        session.SetNeed(NeedMeter.Thirst, 60);
        session.Step([]);
        hud.Update(session.Simulation.Tick);
        var first = hud.Announcement!.Spanish;
        for (var i = 0; i < NeedsHud.DisplayTicks; i++)
        {
            session.Step([]);
            hud.Update(session.Simulation.Tick);
        }

        Assert.Equal(["Tengo frío.", "Tengo un poco de sed."], new[] { first, hud.Announcement!.Spanish }.Order());
    }

    [Fact]
    public void ANightIsColderThanNoonInTheSessionAndTheSkyIsRead()
    {
        var noon = TestGame.NewSession();
        noon.Simulation.FreezeClock(13);
        noon.Simulation.ForceWeather("sunny");
        noon.Step([]);
        var night = TestGame.NewSession();
        night.Simulation.FreezeClock(0);
        night.Simulation.ForceWeather("sunny");
        night.Step([]);
        var rainyNight = TestGame.NewSession();
        rainyNight.Simulation.FreezeClock(0);
        rainyNight.Simulation.ForceWeather("rain");
        rainyNight.Step([]);

        Assert.True(night.Temperature.Current!.Value.PlaceTenths < noon.Temperature.Current!.Value.PlaceTenths);
        Assert.True(rainyNight.Temperature.Current!.Value.PlaceTenths < night.Temperature.Current!.Value.PlaceTenths);
    }

    [Fact]
    public void ALitFireNearbyWarmsTheReading()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(0);
        session.Simulation.ForceWeather("sunny");
        var middle = session.Fires.Middles.First(session.Fires.IsLit);
        session.Simulation.TeleportTo(new Vocaterra.Game.Core.Voxel.BlockPos(middle.X, middle.Y + 1, middle.Z));
        session.Step([]);
        var atTheFire = session.Temperature.Current!.Value.PlaceTenths;

        session.Simulation.TeleportTo(new Vocaterra.Game.Core.Voxel.BlockPos(middle.X, middle.Y + 10, middle.Z));
        session.Step([]);
        var away = session.Temperature.Current!.Value.PlaceTenths;

        Assert.Equal(session.Data.Temperature.FireBonusTenths, atTheFire - away);
    }

    [Fact]
    public void TwoSessionsGivenTheSameStepsHaveTheSameReadingsAndTheSameDigest()
    {
        var one = TestGame.NewSession();
        var two = TestGame.NewSession();
        for (var i = 0; i < 300; i++)
        {
            one.Step([]);
            two.Step([]);
            Assert.Equal(one.Temperature.Current, two.Temperature.Current);
        }

        Assert.Equal(one.Simulation.ComputeStateDigest(), two.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void TheTemperatureIsNotPartOfTheSavedState()
    {
        var cold = Held(-80);
        var plain = TestGame.NewSession();
        for (var i = 0; i < 5; i++)
        {
            cold.Step([]);
            plain.Step([]);
        }

        Assert.Equal(plain.Simulation.ComputeStateDigest(), cold.Simulation.ComputeStateDigest()); // the held value and the reading reach no part of the state
    }
}
