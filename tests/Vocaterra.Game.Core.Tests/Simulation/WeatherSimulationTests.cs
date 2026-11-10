using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Atmosphere;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The simulation's weather (E04, <c>VoxelSimulation.Atmosphere.cs</c>): the change event once per change, the host actions, the snapshot, the digest in the initial state and after a change, and the state's round trip.</summary>
public sealed class WeatherSimulationTests
{
    private const int Second = DayClock.TicksPerSecond;

    private static WeatherData Quick => WeatherTestData.Quick(WeatherTestData.Shipped);

    private static Scene Route() => VoxelTestSupport.BuildRoute(42);

    private static VoxelSimulation Make(WeatherData? weather = null, ulong seed = 7) => VoxelSimulation.Create(Route(), DayData.Default, weather ?? Quick, seed);

    private static List<SimulationEvent> Changes(VoxelSimulation sim, int ticks)
    {
        var found = new List<SimulationEvent>();
        for (var i = 0; i < ticks; i++)
        {
            sim.Step();
            found.AddRange(sim.Events.Where(static e => e.Kind == SimulationEventKind.WeatherChanged));
        }

        return found;
    }

    [Fact]
    public void ASimulationBuiltWithoutWeatherUsesTheDefaultDataAndSeedZero()
    {
        var sim = VoxelSimulation.Create(Route());

        Assert.Equal(WeatherData.Default, sim.Weather.Data);
        Assert.Equal("clear", sim.Atmosphere.KindId);
    }

    [Fact]
    public void EachSpellEndRaisesOneEventWithTheNewKindsIndexAtTheTickOfTheChange()
    {
        var sim = Make();
        var firstEnd = sim.Weather.State.SpellEnd;

        var events = Changes(sim, (int)firstEnd + (400 * Second));

        Assert.NotEmpty(events);
        Assert.Equal(firstEnd, events[0].Tick);
        Assert.All(events, e => Assert.InRange(e.Detail, 0, Quick.Kinds.Count - 1));
        Assert.Equal(Quick.IndexOf(sim.Weather.State.Current), events[^1].Detail);
        Assert.Equal(events.Count, events.Select(static e => e.Tick).Distinct().Count());
        Assert.Equal(sim.Weather.State.Spells, events.Count); // no kind of the shipped data lists itself, so every spell is a change
    }

    [Fact]
    public void ANewSimulationAndALoadRaiseNoWeatherEvent()
    {
        var sim = Make();
        sim.Step();
        Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.WeatherChanged);

        sim.ForceWeather("rain");
        var state = sim.CaptureState();
        var restored = Make();
        restored.Restore(state);
        restored.Step();

        Assert.DoesNotContain(restored.Events, static e => e.Kind == SimulationEventKind.WeatherChanged);
    }

    [Fact]
    public void AForcedKindAndAWakeUpRaiseOneEventAtTheNextStepWhenTheKindChanges()
    {
        var sim = Make();

        sim.ForceWeather("rain");
        sim.ForceWeather("rain"); // no change, no event
        var forced = Changes(sim, 3);
        sim.ReleaseWeather();
        var released = Changes(sim, 3);

        var change = Assert.Single(forced);
        Assert.Equal(Quick.IndexOf("rain"), change.Detail);
        Assert.Empty(released);
    }

    [Fact]
    public void AWakeUpRollsTheMorningsWeatherAndTellsAChangeOnce()
    {
        var changedAtLeastOnce = false;
        for (ulong seed = 0; seed < 20; seed++)
        {
            var sim = Make(seed: seed);
            sim.WakeUp();
            var events = Changes(sim, 5);
            Assert.Equal(1, sim.Weather.State.Mornings);
            Assert.True(events.Count <= 1);
            changedAtLeastOnce |= events.Count == 1;
            Assert.Equal(1, sim.Weather.BlendAt(sim.Tick)); // a cut
        }

        Assert.True(changedAtLeastOnce);
    }

    [Fact]
    public void TheSnapshotFollowsTheTickAndTheForcedKind()
    {
        var sim = Make();
        sim.ForceWeather("rain");
        sim.Step();

        var snapshot = sim.Atmosphere;

        Assert.Equal("rain", snapshot.KindId);
        Assert.Equal(0.8, snapshot.Channels.PrecipitationIntensity);
        Assert.Equal(sim.Clock.HourAt(sim.Tick), snapshot.Hour);
    }

    [Fact]
    public void TheDigestInTheInitialStateIsTheDigestOfAnySimulationWithTheSameThingsAndChangesWithTheFirstChange()
    {
        var a = Make(seed: 7);
        var b = Make(seed: 7);
        var defaults = VoxelSimulation.Create(Route());
        Assert.Equal(defaults.ComputeStateDigest(), a.ComputeStateDigest()); // the weather adds nothing in its first spell
        Assert.True(a.Weather.IsInitial);
        VoxelTestSupport.Run(a, 100);
        VoxelTestSupport.Run(defaults, 100);
        Assert.Equal(defaults.ComputeStateDigest(), a.ComputeStateDigest());

        var beforeChange = a.ComputeStateDigest();
        var firstEnd = a.Weather.State.SpellEnd;
        VoxelTestSupport.Run(a, (int)(firstEnd - a.Tick));
        VoxelTestSupport.Run(b, (int)firstEnd);

        Assert.False(a.Weather.IsInitial);
        Assert.NotEqual(defaults.ComputeStateDigest(), a.ComputeStateDigest());
        Assert.NotEqual(beforeChange, a.ComputeStateDigest());
        Assert.Equal(b.ComputeStateDigest(), a.ComputeStateDigest()); // the same seed gives the same digest
    }

    [Fact]
    public void ADifferentSeedGivesADifferentDigestAfterTheChanges()
    {
        var a = Make(seed: 1);
        var b = Make(seed: 2);
        var ticks = (int)Math.Max(a.Weather.State.SpellEnd, b.Weather.State.SpellEnd) + (900 * Second);

        VoxelTestSupport.Run(a, ticks);
        VoxelTestSupport.Run(b, ticks);

        Assert.NotEqual(a.Weather.State, b.Weather.State);
        Assert.NotEqual(a.ComputeStateDigest(), b.ComputeStateDigest());
    }

    [Fact]
    public void ACapturedStateInTheInitialWeatherCarriesNoAtmosphereAndOneInTheMiddleOfATransitionRestoresTheSameSnapshot()
    {
        var sim = Make();
        Assert.Null(sim.CaptureState().Atmosphere);
        VoxelTestSupport.Run(sim, (int)sim.Weather.State.SpellEnd + (20 * Second)); // in the middle of the first transition
        var state = sim.CaptureState();
        var restored = Make();

        restored.Restore(state);

        Assert.NotNull(state.Atmosphere?.Weather);
        Assert.Equal(sim.Atmosphere, restored.Atmosphere);
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
        var next = new List<(long, string)>();
        var nextRestored = new List<(long, string)>();
        for (var i = 0; i < 2000 * Second; i++)
        {
            sim.Step();
            restored.Step();
            if (sim.Events.Any(static e => e.Kind == SimulationEventKind.WeatherChanged))
            {
                next.Add((sim.Tick, sim.Weather.State.Current));
            }

            if (restored.Events.Any(static e => e.Kind == SimulationEventKind.WeatherChanged))
            {
                nextRestored.Add((restored.Tick, restored.Weather.State.Current));
            }
        }

        Assert.NotEmpty(next);
        Assert.Equal(next, nextRestored);
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
    }

    [Fact]
    public void RestoringWithNoAtmosphereGivesTheInitialWeatherAndDropsPendingEvents()
    {
        var sim = Make();
        sim.ForceWeather("rain");

        sim.Restore(sim.CaptureState() with { Atmosphere = null });
        sim.Step();

        Assert.True(sim.Weather.IsInitial);
        Assert.Equal("sunny", sim.Weather.Current.Id);
        Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.WeatherChanged);
    }
}
