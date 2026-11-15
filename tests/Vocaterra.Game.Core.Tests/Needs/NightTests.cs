using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>A night and a collapse (N03, design 9.5): the needs they move, the day and the sitting, the day's resets, the collapse's timer and where the player wakes.</summary>
public sealed class NightTests
{
    private const int Tps = 60;

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession Rested() => SleepTests.EndedFirstSession();

    private static void Night(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session));

    private static void Collapse(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session), collapse: true);

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static NeedsSave Expected(NeedsSave before, bool collapse)
    {
        var needs = new PlayerNeeds(TestGame.Data.Needs);
        needs.Restore(before);
        if (collapse)
        {
            needs.Collapse();
        }
        else
        {
            needs.Sleep();
        }

        return needs.Capture();
    }

    private static double Of(VoxelSession session, NeedMeter meter) => session.Needs.Value(meter);

    [Theory]
    [InlineData(30, 30, 15, 10)]
    [InlineData(100, 100, 85, 80)]
    [InlineData(12, 12, 10, 10)]
    [InlineData(8, 8, 8, 8)]
    [InlineData(25, 9.5, 10, 9.5)]
    public void ANightLowersHungerAndThirstButNeverBelowTheFloorAndLeavesADriveAlreadyBelowItWhereItIs(double hunger, double thirst, double hungerAfter, double thirstAfter)
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Hunger, hunger);
        session.SetNeed(NeedMeter.Thirst, thirst);
        session.SetNeed(NeedMeter.Sleep, 40);

        Night(session);

        Assert.Equal(hungerAfter, Of(session, NeedMeter.Hunger), 6);
        Assert.Equal(thirstAfter, Of(session, NeedMeter.Thirst), 6);
        Assert.Equal(100, Of(session, NeedMeter.Sleep));
    }

    [Theory]
    [InlineData(50, 70)]
    [InlineData(90, 100)]
    [InlineData(100, 100)]
    public void ANightAddsTwentyHealthUpToAHundred(double before, double after)
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Health, before);

        Night(session);

        Assert.Equal(after, Of(session, NeedMeter.Health), 6);
    }

    [Fact]
    public void TheNeedsAfterANightAreTheRulesAndStaminaIsFilledAfterThePenaltiesAreRecomputed()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 10); // a stage that costs stamina's maximum
        session.SetNeed(NeedMeter.Hunger, 30);
        session.SetNeed(NeedMeter.Thirst, 12);
        session.SetNeed(NeedMeter.Stamina, 3);
        var before = session.Needs.Capture();

        Night(session);

        var after = session.Needs.Capture();
        Assert.Equal(Expected(before, collapse: false), after);
        var effective = session.Needs.Effects().StaminaMaximum;
        Assert.Equal(effective, Of(session, NeedMeter.Stamina));
        Assert.True(effective < 100, "hunger 15 is a stage with a penalty, so the maximum is below 100");
    }

    [Fact]
    public void ACollapseSetsSleepToFiftyFillsStaminaGivesNoHealthAndCostsFifteenHungerAndTwentyThirst()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);
        session.SetNeed(NeedMeter.Hunger, 60);
        session.SetNeed(NeedMeter.Thirst, 55);
        session.SetNeed(NeedMeter.Health, 60);
        session.SetNeed(NeedMeter.Stamina, 0);
        var before = session.Needs.Capture();

        Collapse(session);

        Assert.Equal(Expected(before, collapse: true), session.Needs.Capture());
        Assert.Equal(50, Of(session, NeedMeter.Sleep));
        Assert.Equal(60, Of(session, NeedMeter.Health));
        Assert.Equal(45, Of(session, NeedMeter.Hunger), 6);
        Assert.Equal(35, Of(session, NeedMeter.Thirst), 6);
        Assert.Equal(session.Needs.Effects().StaminaMaximum, Of(session, NeedMeter.Stamina));
        Assert.Equal(0, session.Needs.ExhaustionSeconds);
    }

    [Fact]
    public void ACollapseKeepsTheFloorsToo()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);
        session.SetNeed(NeedMeter.Hunger, 0);
        session.SetNeed(NeedMeter.Thirst, 14);

        Collapse(session);

        Assert.Equal(0, Of(session, NeedMeter.Hunger)); // below the floor: stays
        Assert.Equal(10, Of(session, NeedMeter.Thirst));
    }

    [Fact]
    public void TheCollapseIsDueAfterExactly60ActiveSecondsAtTheLastStageAndNotAt59Point9()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);

        Tick(session, (60 * Tps) - 6); // 59.9 s
        Assert.False(session.CollapseDue);
        Tick(session, 5);
        Assert.False(session.CollapseDue);
        Tick(session, 1);
        Assert.True(session.CollapseDue);
    }

    [Fact]
    public void TheCollapseIsNotDueWhilePlayIsPausedAndItsTimerDoesNotRun()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);
        session.PlayPauses = PlayPause.Conversation;

        Tick(session, 70 * Tps);

        Assert.False(session.CollapseDue);
        Assert.Equal(0, session.Needs.ExhaustionSeconds);
        session.PlayPauses = PlayPause.None;
        Tick(session, 59 * Tps);
        Assert.False(session.CollapseDue);
    }

    [Fact]
    public void TheCollapseIsNeverDueWithTheNeedsOff()
    {
        var session = Rested();
        session.NeedsEnabled = false;
        session.SetNeed(NeedMeter.Sleep, 0);

        Tick(session, 70 * Tps);

        Assert.False(session.CollapseDue);
    }

    [Fact]
    public void ThePlayerWakesWhereTheyFell()
    {
        var session = Rested();
        Tick(session, 30);
        var (feet, tick, digest) = (session.Simulation.Player.FeetBlock, session.Simulation.Tick, session.Simulation.ComputeStateDigest());
        session.SetNeed(NeedMeter.Sleep, 0);

        Collapse(session);

        Assert.Equal(feet, session.Simulation.Player.FeetBlock);
        Assert.Equal(tick, session.Simulation.Tick);
        Assert.NotEqual(digest, session.Simulation.ComputeStateDigest()); // E01: the collapse wakes in the morning, so the clock's offset is in the digest
        session.Simulation.Clock.Restore(null);
        session.Simulation.Weather.Restore(null); // E04: the morning roll is in the digest too
        Assert.Equal(digest, session.Simulation.ComputeStateDigest()); // and nothing else of the state moved
    }

    [Fact]
    public void ACollapseRaisesTheCollapseAndWakingEventsAndANightOnlyTheSleepEvent()
    {
        var collapsed = Rested();
        collapsed.SetNeed(NeedMeter.Sleep, 0);
        Collapse(collapsed);
        var names = collapsed.SessionEvents.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.Contains(WorldEvents.Slept, names);
        Assert.Contains(WorldEvents.PlayerCollapsed, names);
        Assert.Contains(WorldEvents.PlayerWoke, names);
        Assert.Equal("collapse", collapsed.SessionEvents.OfType<WorldEvent>().Single(static e => e.Name == WorldEvents.PlayerWoke).Detail);

        var slept = Rested();
        Night(slept);
        var sleptNames = slept.SessionEvents.OfType<WorldEvent>().Select(static e => e.Name).ToList();
        Assert.DoesNotContain(WorldEvents.PlayerCollapsed, sleptNames);
        Assert.DoesNotContain(WorldEvents.PlayerWoke, sleptNames);
    }

    [Fact]
    public void TheCollapseTipComesUpAfterACollapse()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);

        Collapse(session);
        Tick(session, 20 * 60);

        Assert.True(session.Tips.WasShown("collapse") || session.Tips.Current?.Id == "collapse");
    }

    [Fact]
    public void ANightAndACollapseEachAdvanceTheDayAndTheSittingAndALoadAdvancesTheSittingAlone()
    {
        var session = Rested();
        var (day, sitting) = (session.Day, session.Sitting);

        Night(session);
        Assert.Equal((day + 1, sitting + 1), (session.Day, session.Sitting));

        session.SetNeed(NeedMeter.Sleep, 0);
        Collapse(session);
        Assert.Equal((day + 2, sitting + 2), (session.Day, session.Sitting));

        var loaded = SaveGame.Load(SaveGame.Save(session), TestGame.Data, Pristine, credit: session.Credit);
        Assert.Equal(day + 2, loaded.Day);
        Assert.Equal(sitting + 3, loaded.Sitting);
    }

    [Fact]
    public void TheDaysResetsRunOncePerNightAndOncePerCollapseAndNeverOnALoad()
    {
        var session = Rested();
        Assert.Equal(0, session.DayResetCount);

        Night(session);
        Assert.Equal(1, session.DayResetCount);
        session.SetNeed(NeedMeter.Sleep, 0);
        Collapse(session);
        Assert.Equal(2, session.DayResetCount);

        var loaded = SaveGame.Load(SaveGame.Save(session), TestGame.Data, Pristine, credit: session.Credit);
        Assert.Equal(0, loaded.DayResetCount);
    }

    [Fact]
    public void ANightWithTheNeedsOffLeavesTheMetersAndStillMovesTheDay()
    {
        var session = Rested();
        session.NeedsEnabled = false;
        session.SetNeed(NeedMeter.Sleep, 40);
        session.SetNeed(NeedMeter.Hunger, 30);
        var before = session.Needs.Capture();

        Night(session);

        Assert.Equal(before, session.Needs.Capture());
        Assert.Equal(2, session.Day);
        Assert.Equal(1, session.DayResetCount);
    }

    [Fact]
    public void ANightRaisesNoNeedStageEventAndTheStagesAreTakenAsTheyAre()
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 5);
        Night(session);

        Assert.Equal(0, session.NeedStage(NeedMeter.Sleep));
        Assert.DoesNotContain(session.SessionEvents.OfType<WorldEvent>(), static e => e.Name == WorldEvents.NeedStage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASaveRoundTripsAcrossANightAndACollapse(bool collapse)
    {
        var session = Rested();
        session.SetNeed(NeedMeter.Sleep, 0);
        session.SetNeed(NeedMeter.Hunger, 40);
        if (collapse)
        {
            Collapse(session);
        }
        else
        {
            Night(session);
        }

        var text = SaveGame.Save(session);
        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: session.Credit, bumpSession: false);

        Assert.Equal(session.Needs.Capture(), loaded.Needs.Capture());
        Assert.Equal(text, SaveGame.Save(loaded));
    }

    [Fact]
    public void ANightIsDeterministic()
    {
        string Run(bool collapse)
        {
            var session = Rested();
            session.SetNeed(NeedMeter.Sleep, 20);
            session.SetNeed(NeedMeter.Hunger, 33);
            Tick(session, 100);
            if (collapse)
            {
                Collapse(session);
            }
            else
            {
                Night(session);
            }

            return SaveGame.Save(session);
        }

        Assert.Equal(Run(false), Run(false));
        Assert.Equal(Run(true), Run(true));
    }
}
