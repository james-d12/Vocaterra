using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The weather in the session (E04): the <c>weather_changed</c> world event, and a sleep or a collapse rolling the morning.</summary>
public sealed class WeatherSessionTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static IReadOnlyList<WorldEvent> Changes(VoxelSession session) => session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.WeatherChanged).ToList();

    [Fact]
    public void TheEventNameIsWeatherChangedAndIsInTheListTheTriggersCheckAgainst()
    {
        Assert.Equal("weather_changed", WorldEvents.WeatherChanged);
        Assert.Contains(WorldEvents.WeatherChanged, WorldEvents.All);
    }

    [Fact]
    public void ASpellsEndIsAWorldEventWhoseDetailIsTheNewKindsIdAndATriggerMatchesIt()
    {
        var session = TestGame.NewSession();
        Tick(session, (int)session.Simulation.Weather.State.SpellEnd - 1);
        Assert.Empty(Changes(session));

        Tick(session, 1);

        var change = Assert.Single(Changes(session));
        Assert.Equal("cloudy", change.Detail); // sunny is followed by cloudy only
        Assert.Null(change.Npc);
        Assert.True(new Trigger(WorldEvents.WeatherChanged, null, "cloudy").Matches(change));
        Assert.False(new Trigger(WorldEvents.WeatherChanged, null, "rain").Matches(change));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASleepAndACollapseBothRollTheMorningsWeather(bool collapse)
    {
        var session = SleepTests.EndedFirstSession();
        var before = session.Simulation.Weather.State.Mornings;

        SaveGame.Sleep(session, SaveGame.Capture(session), collapse);

        Assert.Equal(before + 1, session.Simulation.Weather.State.Mornings);
        Assert.Equal(session.Simulation.Tick, session.Simulation.Weather.State.SpellStart);
        Assert.Equal(1, session.Simulation.Weather.BlendAt(session.Simulation.Tick));
    }

    [Fact]
    public void ASleepWithTheNeedsOffRollsTheMorningToo()
    {
        var session = SleepTests.EndedFirstSession();
        session.NeedsEnabled = false;

        SaveGame.Sleep(session, SaveGame.Capture(session));

        Assert.Equal(1, session.Simulation.Weather.State.Mornings);
    }
}
