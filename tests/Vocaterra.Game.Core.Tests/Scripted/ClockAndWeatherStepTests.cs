using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The scripted <c>--set-time</c> and <c>--set-weather</c> steps (E03): each holds or releases the clock or the weather, is logged, and an unknown value is reported.</summary>
public sealed class ClockAndWeatherStepTests
{
    private static (VoxelSimulation Sim, List<string> Log) Go(params VoxelStep[] steps)
    {
        var sim = FlatSim();
        var log = new List<string>();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 10 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        return (sim, log);
    }

    private static string FirstKind => WeatherData.Default.Kinds[0].Id;

    [Fact]
    public void Given_ASetTimeStep_When_TheScriptRuns_Then_TheClockIsHeldAtTheHourAndLogged()
    {
        var (sim, log) = Go(VoxelStep.SetTime(13));

        Assert.True(sim.Clock.IsFrozen);
        Assert.Equal(13, sim.Clock.HourAt(sim.Tick), 6);
        Assert.Equal("set-time: held at 13", Assert.Single(log));
    }

    [Fact]
    public void Given_AFractionalHour_When_TheScriptRuns_Then_TheClockIsHeldAtThatFraction()
    {
        var (sim, log) = Go(VoxelStep.SetTime(7.5));

        Assert.Equal(7.5, sim.Clock.HourAt(sim.Tick), 6);
        Assert.Equal("set-time: held at 7.5", Assert.Single(log));
    }

    [Fact]
    public void Given_ASetTimeThenRun_When_TheScriptRuns_Then_TheClockIsReleasedAndBothAreLogged()
    {
        var (sim, log) = Go(VoxelStep.SetTime(21), VoxelStep.RunTime());

        Assert.False(sim.Clock.IsFrozen);
        Assert.Equal(["set-time: held at 21", "set-time: running"], log);
    }

    [Fact]
    public void Given_ASetWeatherStep_When_TheScriptRuns_Then_TheKindIsHeldAndLogged()
    {
        var (sim, log) = Go(VoxelStep.SetWeather(FirstKind));

        Assert.True(sim.Weather.IsHeld);
        Assert.Equal(FirstKind, sim.Weather.Current.Id);
        Assert.Equal($"set-weather: held at {FirstKind}", Assert.Single(log));
    }

    [Fact]
    public void Given_ASetWeatherThenRun_When_TheScriptRuns_Then_TheWeatherIsReleasedAndBothAreLogged()
    {
        var (sim, log) = Go(VoxelStep.SetWeather(FirstKind), VoxelStep.RunWeather());

        Assert.False(sim.Weather.IsHeld);
        Assert.Equal([$"set-weather: held at {FirstKind}", "set-weather: running"], log);
    }

    [Fact]
    public void Given_AnUnknownWeatherKind_When_TheScriptRuns_Then_NothingChangesAndTheLogNamesTheKinds()
    {
        var (sim, log) = Go(VoxelStep.SetWeather("nope"));

        Assert.False(sim.Weather.IsHeld);
        Assert.Equal($"set-weather nope: no weather kind called 'nope' (the kinds are {FirstKind})", Assert.Single(log));
    }

    [Fact]
    public void Given_StepsAmongOthers_When_TheScriptRuns_Then_TheyHappenInOrder()
    {
        var (sim, log) = Go(VoxelStep.Look(90, 0), VoxelStep.SetTime(5), VoxelStep.Select(2), VoxelStep.SetWeather(FirstKind));

        Assert.True(sim.Clock.IsFrozen);
        Assert.True(sim.Weather.IsHeld);
        Assert.Equal(4, log.Count);
        Assert.StartsWith("look", log[0], StringComparison.Ordinal);
        Assert.Equal("set-time: held at 5", log[1]);
    }
}
