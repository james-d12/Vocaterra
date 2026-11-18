using System.Text.Json;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>The weather in the save (E04, <c>systems.atmosphere.weather</c>): not written while the weather is in its first spell, written and restored in the middle of a transition, the old fixtures still loading with no version change.</summary>
public sealed class WeatherSaveTests
{
    private const int Second = DayClock.TicksPerSecond;

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

    private static List<(long, string)> Changes(VoxelSession session, int ticks)
    {
        var found = new List<(long, string)>();
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
            found.AddRange(session.Events.OfType<WorldEvent>().Where(e => e.Name == WorldEvents.WeatherChanged && e.Tick == session.Simulation.Tick).Select(e => (e.Tick, e.Detail!)));
        }

        return found;
    }

    [Fact]
    public void AGameStillInItsFirstSpellWritesNoWeather()
    {
        var session = TestGame.NewSession();
        Tick(session, 120);

        var text = SaveOf(session);

        Assert.DoesNotContain("atmosphere", text, StringComparison.Ordinal);
        Assert.DoesNotContain("weather", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ASaveTakenInTheMiddleOfATransitionRestoresTheSameSnapshotAndContinuesToTheSameKinds()
    {
        var session = TestGame.NewSession();
        Tick(session, (int)session.Simulation.Weather.State.SpellEnd + (20 * Second));
        var text = SaveOf(session);

        var loaded = LoadOf(text, session);

        using (var json = JsonDocument.Parse(text))
        {
            var weather = json.RootElement.GetProperty("systems").GetProperty("atmosphere").GetProperty("weather");
            Assert.Equal(session.Simulation.Weather.State.Current, weather.GetProperty("current").GetString());
        }

        Assert.Equal(session.Simulation.Atmosphere, loaded.Simulation.Atmosphere);
        Assert.Equal(session.Simulation.Weather.State, loaded.Simulation.Weather.State);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        var original = Changes(session, 2000 * Second);
        var continued = Changes(loaded, 2000 * Second);
        Assert.NotEmpty(original);
        Assert.Equal(original, continued);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void ALoadRaisesNoWeatherEvent()
    {
        var session = TestGame.NewSession();
        Tick(session, (int)session.Simulation.Weather.State.SpellEnd + 10);
        var loaded = LoadOf(SaveOf(session), session);

        Tick(loaded, 5);

        Assert.DoesNotContain(loaded.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.WeatherChanged);
    }

    [Fact]
    public void ASleepRollsTheMorningAndTheSavedWeatherRestoresIt()
    {
        var session = SleepTests.EndedFirstSession();
        SaveGame.Sleep(session, SaveGame.Capture(session));
        Tick(session, 90);

        var text = SaveOf(session);
        var loaded = LoadOf(text, session);

        Assert.Equal(1, session.Simulation.Weather.State.Mornings);
        Assert.Equal(1, SaveGame.Parse(text).Systems!.Atmosphere!.Weather!.Mornings);
        Assert.Equal(session.Simulation.Weather.State, loaded.Simulation.Weather.State);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void ASaveNamingAnUnknownKindDoesNotRestore()
    {
        var session = TestGame.NewSession();
        var document = SaveGame.Capture(session);
        var weather = session.Simulation.Weather.State with { Current = "hail" };
        var broken = document with { Systems = document.Systems! with { Atmosphere = new AtmosphereState(0, null, weather) } };

        Assert.Throws<SaveException>(() => SaveGame.Load(SaveGame.Serialize(broken), TestGame.Data, Pristine, credit: session.Credit));
    }

    [Fact]
    public void AnAtmosphereEntryWithOnlyAClockRestoresTheInitialWeather()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(12);
        var loaded = LoadOf(SaveOf(session), session);

        Assert.True(loaded.Simulation.Weather.IsInitial);
        Assert.True(loaded.Simulation.Clock.IsFrozen);
    }

    [Theory]
    [InlineData("save-v10.json")]
    public void TheFixtureStillLoadsWithTheWeatherOfANewGameAndTheVersionIsNine(string fixture)
    {
        var text = File.ReadAllText(Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", fixture));

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine);

        Assert.Null(loaded.Migration);
        Assert.True(loaded.Simulation.Weather.IsInitial);
        Assert.Equal(10, SaveGame.CurrentVersion);
        Assert.DoesNotContain("weather", SaveOf(loaded), StringComparison.Ordinal);
    }
}
