using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Core.Tests;

/// <summary>The autosave policy (X05, R04): which events make a save due, the tick throttle, and that a save waits for a safe frame.</summary>
public sealed class AutosavePolicyTests
{
    private const long Start = 1000;

    private static WorldEvent World(string name, long tick = Start + 1) => new(tick, 0, name);

    private static LessonEvent Lesson(LessonEventKind kind, StepOutcome? outcome = null, long tick = Start + 1) =>
        new(tick, 0, kind, "first-words", 2, "listen-hola", "listen", outcome);

    [Theory]
    [InlineData(LessonEventKind.StepDone, "lesson first-words step 2 done")]
    [InlineData(LessonEventKind.LessonCompleted, "lesson first-words completed")]
    public void ALessonStepEndAndTheLessonsEndMakeASaveDue(LessonEventKind kind, string reason)
    {
        var events = new List<GameEvent> { Lesson(kind, kind == LessonEventKind.StepDone ? StepOutcome.Done : null) };
        var policy = new AutosavePolicy(Start, 0);

        var decision = policy.Decide(Start + 5, events, atRest: true);

        Assert.True(decision.Save);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void ASkippedOrMovedOnLessonStepIsAStepEndToo()
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent> { Lesson(LessonEventKind.StepDone, StepOutcome.Skipped) };

        Assert.True(policy.Decide(Start + 1, events, atRest: true).Save);

        events.Add(Lesson(LessonEventKind.StepDone, StepOutcome.MovedOn));
        Assert.True(policy.Decide(Start + 2, events, atRest: true).Save);
    }

    [Theory]
    [InlineData(WorldEvents.ObjectiveStep, "objective step done")]
    [InlineData(WorldEvents.SessionComplete, "session complete")]
    [InlineData(WorldEvents.TalkClosed, "dialogue closed")]
    public void AnObjectiveStepTheSessionsEndAndAClosedDialogueMakeASaveDue(string name, string reason)
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent> { World(name) };

        var decision = policy.Decide(Start + 1, events, atRest: true);

        Assert.True(decision.Save);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void OtherEventsMakeNoSaveDue()
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent>
        {
            World(WorldEvents.TalkOpened),
            World(WorldEvents.ItemPickedUp),
            World(WorldEvents.ObjectiveComplete),
            Lesson(LessonEventKind.StepStarted),
            Lesson(LessonEventKind.Help),
            Lesson(LessonEventKind.Retry),
            Lesson(LessonEventKind.LessonStarted),
        };

        Assert.False(policy.Decide(Start + 1, events, atRest: true).Save);
        Assert.Null(policy.Due);
    }

    [Fact]
    public void ASaveIsNeverTakenWhileTheGameIsNotAtRest()
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent> { World(WorldEvents.TalkClosed) };

        for (var tick = Start + 1; tick < Start + 100; tick++)
        {
            Assert.False(policy.Decide(tick, events, atRest: false).Save);
        }

        Assert.Equal("dialogue closed", policy.Due); // still due, and kept

        var decision = policy.Decide(Start + 100, events, atRest: true);
        Assert.True(decision.Save);
        Assert.Equal("dialogue closed", decision.Reason);
    }

    [Fact]
    public void ADueSaveNamesTheNewestEventAndTheEventsAreReadOnce()
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent> { World(WorldEvents.ObjectiveStep) };
        Assert.False(policy.Decide(Start + 1, events, atRest: false).Save);

        events.Add(World(WorldEvents.SessionComplete));
        var decision = policy.Decide(Start + 2, events, atRest: true);

        Assert.Equal("session complete", decision.Reason);
        Assert.False(policy.Decide(Start + 3, events, atRest: true).Save); // nothing new: no second save
    }

    [Fact]
    public void TheIntervalIsFiveMinutesOfTicksAndTheSaveIsThrottledToIt()
    {
        Assert.Equal(5 * 60, AutosavePolicy.IntervalSeconds);
        Assert.Equal(5L * 60 * VoxelSimulation.TicksPerSecond, AutosavePolicy.IntervalTicks);

        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent>();

        Assert.False(policy.Decide(Start + AutosavePolicy.IntervalTicks - 1, events, atRest: true).Save);

        var first = policy.Decide(Start + AutosavePolicy.IntervalTicks, events, atRest: true);
        Assert.True(first.Save);
        Assert.Equal(AutosavePolicy.IntervalReason, first.Reason);
        Assert.Equal(Start + AutosavePolicy.IntervalTicks, policy.LastSavedTick);

        Assert.False(policy.Decide(Start + AutosavePolicy.IntervalTicks + 1, events, atRest: true).Save);
        Assert.True(policy.Decide(Start + 2 * AutosavePolicy.IntervalTicks, events, atRest: true).Save);
    }

    [Fact]
    public void AnEventSaveResetsTheIntervalSoItIsNotDoubled()
    {
        var policy = new AutosavePolicy(Start, 0);
        var events = new List<GameEvent> { World(WorldEvents.TalkClosed, Start + 10) };

        Assert.True(policy.Decide(Start + 10, events, atRest: true).Save);

        // The next throttled save is a full interval after the event save, not after the start.
        Assert.False(policy.Decide(Start + AutosavePolicy.IntervalTicks, events, atRest: true).Save);
        Assert.True(policy.Decide(Start + 10 + AutosavePolicy.IntervalTicks, events, atRest: true).Save);
    }

    [Fact]
    public void TheEventsOfALoadedSessionAreNotNew()
    {
        var old = new List<GameEvent> { World(WorldEvents.SessionComplete), World(WorldEvents.ObjectiveStep) };
        var policy = new AutosavePolicy(Start, old.Count);

        Assert.False(policy.Decide(Start + 1, old, atRest: true).Save);
        Assert.Null(policy.Due);
    }

    [Fact]
    public void TheSameRunAsksForTheSameSavesAtTheSameTicks()
    {
        List<(long Tick, bool Save)> Run()
        {
            var policy = new AutosavePolicy(Start, 0);
            var events = new List<GameEvent>();
            var result = new List<(long, bool)>();
            for (var tick = Start + 1; tick <= Start + 2 * AutosavePolicy.IntervalTicks; tick += 997)
            {
                if (tick % 5000 < 997)
                {
                    events.Add(World(WorldEvents.TalkClosed, tick));
                }

                result.Add((tick, policy.Decide(tick, events, atRest: tick % 3 != 0).Save));
            }

            return result;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ANegativeEventCountIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutosavePolicy(Start, -1));
    }

    [Fact]
    public void TheFileNamesAreTheAutosaveAndTheCrashSave()
    {
        Assert.Equal("autosave.save", AutosaveFiles.FileName);
        Assert.Equal("crash.save", AutosaveFiles.CrashFileName);
    }
}
