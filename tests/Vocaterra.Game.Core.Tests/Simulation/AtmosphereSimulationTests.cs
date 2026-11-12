using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The simulation's day clock (E01, <c>VoxelSimulation.Atmosphere.cs</c>): the phase event at each boundary and never otherwise, determinism, the state's round trip, and the digest of a clock nobody moved.</summary>
public sealed class AtmosphereSimulationTests
{
    // A short day for the tests: 24 s is 1 440 ticks, so an hour is 60 ticks. Starting at 3, dawn is at tick 60, day at 180, dusk at 1 020, night at 1 140 and the next dawn at 1 500.
    private static readonly DayData Short = new(24, 4, 6, 20, 22, 3, 7);

    private static Scene Route(ulong seed = 42) => VoxelTestSupport.BuildRoute(seed);

    private static List<SimulationEvent> PhaseEvents(VoxelSimulation sim, int ticks)
    {
        var found = new List<SimulationEvent>();
        for (var i = 0; i < ticks; i++)
        {
            sim.Step();
            found.AddRange(sim.Events.Where(static e => e.Kind == SimulationEventKind.DayPhaseChanged));
        }

        return found;
    }

    [Fact]
    public void ThePhaseEventIsRaisedOnceAtEachBoundaryAndNotOtherwise()
    {
        var sim = VoxelSimulation.Create(Route(), Short);

        var events = PhaseEvents(sim, 1600);

        Assert.Equal([(60L, DayPhase.Dawn), (180L, DayPhase.Day), (1020L, DayPhase.Dusk), (1140L, DayPhase.Night), (1500L, DayPhase.Dawn)], events.Select(static e => (e.Tick, (DayPhase)e.Detail)).ToArray());
    }

    [Fact]
    public void ANewSimulationRaisesNoEventForTheStartingPhase()
    {
        var sim = VoxelSimulation.Create(Route(), Short);

        Assert.Equal(DayPhase.Night, sim.Phase);
        sim.Step();

        Assert.DoesNotContain(sim.Events, static e => e.Kind == SimulationEventKind.DayPhaseChanged);
    }

    [Fact]
    public void ASimulationBuiltWithoutGameDataUsesTheDefaultValues()
    {
        var sim = VoxelSimulation.Create(Route());

        Assert.Equal(DayData.Default, sim.Clock.Data);
        Assert.Equal(8, sim.Clock.HourAt(sim.Tick), 6);
    }

    [Fact]
    public void AWakeUpAndAHeldHourRaiseNoPhaseEventAndTheClockRunsOnFromThem()
    {
        var sim = VoxelSimulation.Create(Route(), Short);
        VoxelTestSupport.Run(sim, 100); // dawn

        sim.WakeUp();
        Assert.Equal(7, sim.Clock.HourAt(sim.Tick), 6);
        Assert.Empty(PhaseEvents(sim, 30)); // the jump to day is no boundary
        sim.FreezeClock(21.9);
        Assert.Empty(PhaseEvents(sim, 500));
        sim.UnfreezeClock();
        var events = PhaseEvents(sim, 20);

        Assert.Equal([DayPhase.Night], events.Select(static e => (DayPhase)e.Detail).ToArray());
    }

    [Fact]
    public void TwoSimulationsGivenTheSameCommandsHaveTheSameClockAndDigest()
    {
        var a = VoxelSimulation.Create(Route(), Short);
        var b = VoxelSimulation.Create(Route(), Short);
        foreach (var sim in new[] { a, b })
        {
            VoxelTestSupport.Run(sim, 200, VoxelCommand.Move(1, 0), VoxelCommand.Look(Angles.FromDegrees(80), 0));
            sim.WakeUp();
            VoxelTestSupport.Run(sim, 300, VoxelCommand.Move(0, 1));
        }

        Assert.Equal(a.Clock.State, b.Clock.State);
        Assert.Equal(a.Clock.HourAt(a.Tick), b.Clock.HourAt(b.Tick));
        Assert.Equal(a.ComputeStateDigest(), b.ComputeStateDigest());
    }

    [Fact]
    public void ASimulationRestoredAfterAWakeUpHasTheSameHourAndContinuesTheSameWay()
    {
        var sim = VoxelSimulation.Create(Route(), Short);
        VoxelTestSupport.Run(sim, 250, VoxelCommand.Move(1, 0));
        sim.WakeUp();
        VoxelTestSupport.Run(sim, 40, VoxelCommand.Move(1, 0));
        var state = sim.CaptureState();

        var restored = VoxelSimulation.Create(Route(), Short);
        VoxelGridDiff.Apply(restored.Grid, VoxelGridDiff.Capture(sim.Grid, Route().Grid));
        restored.Restore(state);

        Assert.NotNull(state.Atmosphere);
        Assert.Equal(sim.Clock.HourAt(sim.Tick), restored.Clock.HourAt(restored.Tick));
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
        var a = PhaseEvents(sim, 1500);
        var b = PhaseEvents(restored, 1500);
        Assert.Equal(a, b);
        Assert.NotEmpty(a);
        Assert.Equal(sim.ComputeStateDigest(), restored.ComputeStateDigest());
    }

    [Fact]
    public void AStateOfADefaultClockCarriesNoClockAndRestoringItGivesTheDefault()
    {
        var sim = VoxelSimulation.Create(Route());
        VoxelTestSupport.Run(sim, 30);

        Assert.Null(sim.CaptureState().Atmosphere);
        sim.WakeUp();
        var moved = sim.CaptureState();
        Assert.NotNull(moved.Atmosphere);
        sim.Restore(moved with { Atmosphere = null });
        Assert.True(sim.Clock.State.IsDefault);
    }

    [Fact]
    public void TheDigestOfAClockNobodyMovedIsTheDigestBeforeTheClockExisted()
    {
        // The digest is pinned for a fresh route and for the route after 120 ticks of walking: both were taken before the clock was added to the digest.
        var sim = VoxelSimulation.Create(Route());
        var fresh = sim.ComputeStateDigest();
        VoxelTestSupport.Run(sim, 120, VoxelCommand.Move(1, 0));

        Assert.Equal(PinnedFresh, fresh);
        Assert.Equal(PinnedAfterWalking, sim.ComputeStateDigest());
    }

    [Fact]
    public void AMovedOrHeldClockChangesTheDigestAndMovingItBackRestoresIt()
    {
        var sim = VoxelSimulation.Create(Route());
        var before = sim.ComputeStateDigest();

        sim.FreezeClock(12);
        var held = sim.ComputeStateDigest();
        sim.UnfreezeClock();
        sim.WakeUp(); // the tick is 0 and the start hour 8, so the wake at 7 is an offset
        var woken = sim.ComputeStateDigest();

        Assert.NotEqual(before, held);
        Assert.NotEqual(before, woken);
        Assert.NotEqual(held, woken);
        sim.Clock.Restore(null);
        sim.Weather.Restore(null); // E04: the wake rolled the morning's weather too
        Assert.Equal(before, sim.ComputeStateDigest());
    }

    private const string PinnedFresh = "7f60766576336394505b33567e0a62fa05d6da8e3b17904ab5cea4ad0a700cb6"; // Y06m: re-pinned; the route's world now holds the settlement's inn, so the grid digest moved (the clock still adds nothing: the version 8 fixture's digest, taken before the merge, loads strictly)

    private const string PinnedAfterWalking = "77c039bc8d45c08bda377f1a53f0719bdbd32a59225938192eaf227640a5d7b7"; // Y06m: re-pinned with the one above
}
