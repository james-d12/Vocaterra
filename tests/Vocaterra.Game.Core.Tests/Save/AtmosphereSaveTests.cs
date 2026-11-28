using System.Text.Json;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The day clock in the save (E01, <c>systems.atmosphere</c>): written only when it is not a new game's, read back on a load and on a sleep's restore, with no change of the save version and both fixtures still loading.</summary>
public sealed class AtmosphereSaveTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static string SaveOf(VoxelSession session) => SaveGame.Save(session);

    private static VoxelSession LoadOf(string text, VoxelSession like) => SaveGame.Load(text, TestGame.Data, Pristine, credit: like.Credit);

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    [Fact]
    public void ANewGamesSaveHasNoAtmosphereEntry()
    {
        var session = TestGame.NewSession();
        Tick(session, 120);

        var text = SaveOf(session);

        Assert.DoesNotContain("atmosphere", text, StringComparison.Ordinal);
        Assert.Null(SaveGame.Parse(text).Systems!.Atmosphere);
    }

    [Fact]
    public void TheSaveVersionIsTwelve() => Assert.Equal(12, SaveGame.CurrentVersion);

    [Fact]
    public void ASleepWritesTheOffsetAndALoadRestoresTheSameHourAndContinuesTheSameWay()
    {
        var session = SleepTests.EndedFirstSession();
        SaveGame.Sleep(session, SaveGame.Capture(session));
        Tick(session, 90);
        var text = SaveOf(session);

        var entry = SaveGame.Parse(text).Systems!.Atmosphere;
        var loaded = LoadOf(text, session);

        Assert.NotNull(entry);
        Assert.Null(entry.FrozenHour);
        using (var json = JsonDocument.Parse(text))
        {
            var atmosphere = json.RootElement.GetProperty("systems").GetProperty("atmosphere");
            Assert.True(atmosphere.TryGetProperty("offset", out _));
            Assert.False(atmosphere.TryGetProperty("frozenHour", out _));
        }

        Assert.Equal(session.Simulation.Clock.HourAt(session.Simulation.Tick), loaded.Simulation.Clock.HourAt(loaded.Simulation.Tick));
        Assert.Equal(session.Simulation.Clock.State, loaded.Simulation.Clock.State);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Tick(session, 600);
        Tick(loaded, 600);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(session.Simulation.Clock.HourAt(session.Simulation.Tick), loaded.Simulation.Clock.HourAt(loaded.Simulation.Tick));
    }

    [Fact]
    public void AHeldHourIsSavedAndRestored()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(22.5);
        Tick(session, 30);
        var text = SaveOf(session);

        var loaded = LoadOf(text, session);

        Assert.Equal(22.5, SaveGame.Parse(text).Systems!.Atmosphere!.FrozenHour);
        Assert.True(loaded.Simulation.Clock.IsFrozen);
        Assert.Equal(22.5, loaded.Simulation.Clock.HourAt(loaded.Simulation.Tick), 6);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void ALoadRaisesNoPhaseEvent()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(4.5);
        session.Simulation.UnfreezeClock();
        Tick(session, 10);
        var loaded = LoadOf(SaveOf(session), session);

        Tick(loaded, 5);

        Assert.DoesNotContain(loaded.Events.OfType<Vocaterra.Game.Core.Event.WorldEvent>(), static e => e.Name == Vocaterra.Game.Core.Event.WorldEvents.DayPhase);
    }

    [Fact]
    public void ASaveWhoseHeldHourIsOffTheDialDoesNotRestore()
    {
        var session = TestGame.NewSession();
        var document = SaveGame.Capture(session);
        var broken = document with { Systems = document.Systems! with { Atmosphere = new AtmosphereState(0, 40) } };

        Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(broken), TestGame.Data, Pristine, credit: session.Credit));
    }

    [Theory]
    [InlineData("save-v12.json")]
    public void TheFixtureStillLoadsWithTheClockOfANewGame(string fixture)
    {
        var text = SaveFixture.Read(fixture);

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine);

        Assert.Null(loaded.Migration); // the fixture is read with this bundle's hash (SaveFixture), so the load is strict: its digest (recorded again for the 30-slot bag, I03b) is checked and still the world's
        Assert.Null(SaveGame.Parse(text).Systems?.Atmosphere);
        Assert.True(loaded.Simulation.Clock.State.IsDefault);
        Assert.Equal(12, SaveGame.Parse(SaveOf(loaded)).Version);
    }
}
