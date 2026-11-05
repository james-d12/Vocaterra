using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The needs model (N01, design 9.2 to 9.4): rates, effort, stages and their boundaries, the links, the night and the collapse, food, death. The numbers are the data's.</summary>
public sealed class PlayerNeedsTests
{
    private const int Tps = NeedsData.TicksPerSecond;

    private static NeedsData Data => TestGame.Data.Needs;

    private static PlayerNeeds Fresh() => new(Data);

    private static void Run(PlayerNeeds needs, double seconds, NeedActivity activity = NeedActivity.Idle)
    {
        for (var i = 0; i < (int)Math.Round(seconds * Tps); i++)
        {
            needs.Step(1, activity);
        }
    }

    private static NeedMeter MeterOf(Drive drive) => drive switch { Drive.Hunger => NeedMeter.Hunger, Drive.Thirst => NeedMeter.Thirst, _ => NeedMeter.Sleep };

    public static TheoryData<Drive> Drives() => new() { Drive.Hunger, Drive.Thirst, Drive.Sleep };

    [Fact]
    public void ANewPlayerHasFullNeeds()
    {
        var needs = Fresh();
        foreach (var meter in Enum.GetValues<NeedMeter>())
        {
            Assert.Equal(meter == NeedMeter.Stamina ? Data.Stamina.Maximum : 100, needs.Value(meter));
        }

        Assert.All(Enum.GetValues<Drive>(), d => Assert.Equal(0, needs.StageOf(d)));
        Assert.Equal(0, needs.HealthStage);
        Assert.False(needs.IsDead);
    }

    [Theory]
    [MemberData(nameof(Drives))]
    public void EachDriveFallsAtItsRateInActivePlay(Drive drive)
    {
        var needs = Fresh();
        var seconds = Data.Of(drive).SecondsPerPoint * 4;

        needs.Step((int)(seconds * Tps), NeedActivity.Idle);

        Assert.Equal(96, needs.Value(MeterOf(drive)), 6);
    }

    [Fact]
    public void TheRatesAreThePlansHungerOneEvery36sThirstEvery24sSleepEvery72s()
    {
        Assert.Equal([36, 24, 72], Data.Drives.Select(static d => d.SecondsPerPoint).ToArray());
    }

    [Theory]
    [InlineData(NeedActivity.Sprinting)]
    [InlineData(NeedActivity.Gathering)]
    public void EffortMakesHungerAndThirstFallOneAndAHalfTimesAsFastAndSleepOneAndAQuarter(NeedActivity effort)
    {
        var needs = Fresh();

        needs.Step(12 * Tps, effort);

        Assert.Equal(100 - (12 / 36.0 * 1.5), needs.Value(NeedMeter.Hunger), 6);
        Assert.Equal(100 - (12 / 24.0 * 1.5), needs.Value(NeedMeter.Thirst), 6);
        Assert.Equal(100 - (12 / 72.0 * 1.25), needs.Value(NeedMeter.Sleep), 6);
    }

    [Fact]
    public void WalkingAndRestingFallAtTheBaseRate()
    {
        foreach (var activity in new[] { NeedActivity.Walking, NeedActivity.Resting, NeedActivity.Idle })
        {
            var needs = Fresh();
            needs.Step(36 * Tps, activity);
            Assert.Equal(99, needs.Value(NeedMeter.Hunger), 6);
        }
    }

    [Fact]
    public void SprintingCostsTenAStaminaSecondAndGatheringAMinuteCostsAboutTenSecondsOfSprinting()
    {
        var sprint = Fresh();
        sprint.Step(2 * Tps, NeedActivity.Sprinting);
        Assert.Equal(80, sprint.Value(NeedMeter.Stamina), 6);

        var gather = Fresh();
        gather.Step(60 * Tps, NeedActivity.Gathering);
        Assert.Equal(100 - (10 * Data.Stamina.SprintPerSecond), gather.Value(NeedMeter.Stamina), 0); // 0.0278 a tick: 100.08 a minute, "about" 10 s of sprinting
        Assert.InRange(60 * Tps * Data.Stamina.GatherPerTick, 99.5, 100.5);
    }

    [Fact]
    public void TheAxeNeedsAQuarterOfTheGatherTicksSoItCostsAQuarter()
    {
        var byHand = Fresh();
        byHand.Step(60, NeedActivity.Gathering);
        var withAxe = Fresh();
        withAxe.Step(15, NeedActivity.Gathering);

        Assert.Equal(4 * (100 - withAxe.Value(NeedMeter.Stamina)), 100 - byHand.Value(NeedMeter.Stamina), 6);
    }

    [Fact]
    public void StaminaRefillsFifteenASecondAfterASecondWithoutEffort()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Stamina, 0);
        needs.Step(Tps, NeedActivity.Gathering); // effort: the delay starts again
        var after = needs.Value(NeedMeter.Stamina);

        Run(needs, 0.5);
        Assert.Equal(after, needs.Value(NeedMeter.Stamina), 6); // half a second after effort: nothing yet
        Run(needs, 1.0);
        Assert.True(needs.Value(NeedMeter.Stamina) > after + 5);
        Run(needs, 10);
        Assert.Equal(100, needs.Value(NeedMeter.Stamina), 6);
    }

    // The boundaries: a stage applies below its point, the last at 0; the meter just below, exactly at and just above 0, 25, 50 and 75.
    public static TheoryData<Drive, double, int> Boundaries()
    {
        var data = new TheoryData<Drive, double, int>();
        foreach (var drive in new[] { Drive.Hunger, Drive.Thirst, Drive.Sleep })
        {
            foreach (var (value, stage) in new (double, int)[]
                     {
                         (100, 0), (75.0001, 0), (75, 0), (74.9999, 1), (50.0001, 1), (50, 1), (49.9999, 2), (25.0001, 2), (25, 2), (24.9999, 3), (0.0001, 3), (0, 4),
                     })
            {
                data.Add(drive, value, stage);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void AStageIsReadFromTheMeterAtItsBoundaries(Drive drive, double value, int stage)
    {
        var needs = Fresh();
        needs.Set(MeterOf(drive), value);

        Assert.Equal(stage, needs.StageOf(drive));
    }

    [Theory]
    [MemberData(nameof(Drives))]
    public void DecayCrossesEachThresholdAndRecoveryToExactlyTheThresholdTakesTheWorseStageOff(Drive drive)
    {
        foreach (var point in new[] { 75.0, 50, 25 })
        {
            // decay: from just above the point, a second of play puts the meter under it
            var down = Fresh();
            down.Set(MeterOf(drive), point + 0.001);
            var before = down.StageOf(drive);
            down.Step(Tps, NeedActivity.Idle);
            Assert.Equal(before + 1, down.StageOf(drive));

            // recovery: exactly the point is the better stage, a hair under it the worse
            var up = Fresh();
            up.Set(MeterOf(drive), point - 1);
            var worse = up.StageOf(drive);
            up.Eat(new NeedFood("x", drive == Drive.Hunger ? 1 : 0, drive == Drive.Thirst ? 1 : 0, 0, null)); // used only to raise a meter by one
            if (drive == Drive.Sleep)
            {
                up.Set(NeedMeter.Sleep, point);
            }

            Assert.Equal(worse - 1, up.StageOf(drive));
        }
    }

    [Fact]
    public void TheStageEffectsAgreeWithTheDataForEveryDriveAndStage()
    {
        foreach (var spec in Data.Drives)
        {
            for (var stage = 1; stage <= 4; stage++)
            {
                var needs = Fresh();
                var point = spec.Stages[stage - 1].Point;
                needs.Set(MeterOf(spec.Drive), stage == 4 ? 0 : point - 0.5);
                var e = spec.Stages[stage - 1].Effects;
                var effects = needs.Effects();

                Assert.Equal(stage, needs.StageOf(spec.Drive));
                Assert.Equal(Math.Max(Data.Stamina.MinimumMaximum, 100 - e.StaminaPenalty), effects.StaminaMaximum, 6);
                Assert.Equal(Math.Max(Data.Stamina.MinimumRefillFactor, e.RefillFactor), effects.RefillFactor, 6);
                Assert.Equal(e.WalkFactor, effects.WalkFactor, 6);
                Assert.Equal(e.SprintAllowed, effects.SprintAllowed);
                Assert.Equal(e.BlocksRecovery, effects.RecoveryBlocked);
                Assert.Equal(e.HealthLoss > 0 ? e.HealthLoss / e.HealthLossEverySeconds : 0, effects.HealthLossPerSecond, 6);
            }
        }
    }

    [Fact]
    public void TheStagesFollowThePlansTable()
    {
        // (drive, stage) -> maximum penalty, refill, walking: design 9.3
        var table = new (Drive, int, double, double, double)[]
        {
            (Drive.Hunger, 2, 15, 0.85, 1), (Drive.Hunger, 3, 30, 0.7, 1), (Drive.Hunger, 4, 45, 0.5, 0.9),
            (Drive.Thirst, 2, 20, 0.8, 1), (Drive.Thirst, 3, 35, 0.6, 1), (Drive.Thirst, 4, 50, 0.4, 0.85),
            (Drive.Sleep, 2, 10, 0.8, 1), (Drive.Sleep, 3, 25, 0.6, 1), (Drive.Sleep, 4, 40, 0.5, 0.85),
        };
        foreach (var (drive, stage, penalty, refill, walk) in table)
        {
            var needs = Fresh();
            needs.Set(MeterOf(drive), stage == 4 ? 0 : Data.Of(drive).Stages[stage - 1].Point - 1);
            var effects = needs.Effects();
            Assert.Equal(100 - penalty, effects.StaminaMaximum, 6);
            Assert.Equal(refill, effects.RefillFactor, 6);
            Assert.Equal(walk, effects.WalkFactor, 6);
        }
    }

    [Fact]
    public void StageOneOnlyShowsAnIconAndChangesNothing()
    {
        foreach (var drive in Enum.GetValues<Drive>())
        {
            var needs = Fresh();
            needs.Set(MeterOf(drive), 60);
            var effects = needs.Effects();
            Assert.Equal(100, effects.StaminaMaximum);
            Assert.Equal(1, effects.RefillFactor);
            Assert.Equal(1, effects.WalkFactor);
            Assert.False(effects.RecoveryBlocked);
        }
    }

    [Fact]
    public void TheWorkedExampleGivesAMaximumOf55AndARefillFactorOf0544()
    {
        // 25 minutes without drinking, 35 without eating, 70 without sleeping, no sprinting or gathering: all three at stage 2.
        var needs = Fresh();
        needs.Set(NeedMeter.Thirst, 100 - (25 * 60 / 24.0));
        needs.Set(NeedMeter.Hunger, 100 - (35 * 60 / 36.0));
        needs.Set(NeedMeter.Sleep, 100 - (70 * 60 / 72.0));

        Assert.All(Enum.GetValues<Drive>(), d => Assert.Equal(2, needs.StageOf(d)));
        var effects = needs.Effects();
        Assert.Equal(55, effects.StaminaMaximum, 6);
        Assert.Equal(0.544, effects.RefillFactor, 6);
        Assert.False(effects.RecoveryBlocked);
    }

    [Fact]
    public void TheWorkedExampleAlsoComesOutOfRealSteps()
    {
        var needs = Fresh();
        needs.Step(25 * 60 * Tps, NeedActivity.Idle); // 25 minutes: thirst 37.5, hunger 58.3, sleep 79.2
        Assert.Equal(2, needs.StageOf(Drive.Thirst));
        Assert.Equal(1, needs.StageOf(Drive.Hunger));
        Assert.Equal(0, needs.StageOf(Drive.Sleep));
    }

    [Fact]
    public void StaminaMaximumNeverFallsBelow20AndTheRefillFactorNeverBelowAQuarter()
    {
        var needs = Fresh();
        foreach (var meter in new[] { NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep })
        {
            needs.Set(meter, 0);
        }

        var effects = needs.Effects();
        Assert.Equal(20, effects.StaminaMaximum);
        Assert.Equal(0.25, effects.RefillFactor);
        Assert.Equal(15 * 0.25, effects.StaminaRefillPerSecond, 6);
        Assert.Equal(0.85, effects.WalkFactor); // the slowest in force, never multiplied
        Assert.Equal(20, needs.Value(NeedMeter.Stamina)); // stamina is held to its maximum
    }

    [Fact]
    public void ThePenaltiesAddTheFactorsMultiplyAndTheSlowestWalkingFactorApplies()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 10); // stage 3: -30, 0.7
        needs.Set(NeedMeter.Thirst, 40); // stage 2: -20, 0.8
        needs.Set(NeedMeter.Sleep, 10); // stage 3: -25, 0.6
        var effects = needs.Effects();

        Assert.Equal(100 - 30 - 20 - 25, effects.StaminaMaximum, 6);
        Assert.Equal(0.7 * 0.8 * 0.6, effects.RefillFactor, 6);
        Assert.Equal(1, effects.WalkFactor);

        needs.Set(NeedMeter.Sleep, 0); // stage 4 walks at 0.85, and the thirst at 0 (0.85) and hunger at 0 (0.9) do not multiply with it
        needs.Set(NeedMeter.Hunger, 0);
        Assert.Equal(0.85, needs.Effects().WalkFactor);
    }

    [Theory]
    [MemberData(nameof(Drives))]
    public void NoSprintAtADrivesLastStage(Drive drive)
    {
        var needs = Fresh();
        Assert.True(needs.CanSprint);
        needs.Set(MeterOf(drive), 0.001);
        Assert.True(needs.CanSprint);
        needs.Set(MeterOf(drive), 0);
        Assert.False(needs.CanSprint);
        Assert.False(needs.Effects().SprintAllowed);
    }

    [Fact]
    public void NoSprintAtVeryWeakHealthAndWalkingSlowsToNineTenths()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Health, 25);
        Assert.Equal(1, needs.HealthStage); // weak (below 50): the heart pulses, nothing else
        Assert.True(needs.CanSprint);
        needs.Set(NeedMeter.Health, 24.9);
        Assert.Equal(2, needs.HealthStage);
        Assert.False(needs.CanSprint);
        Assert.Equal(0.9, needs.Effects().WalkFactor);
        needs.Set(NeedMeter.Health, 50);
        Assert.Equal(0, needs.HealthStage);
        needs.Set(NeedMeter.Health, 49.9);
        Assert.Equal(1, needs.HealthStage);
        Assert.Equal(1, needs.Effects().WalkFactor);
    }

    [Fact]
    public void NoSprintAtZeroStaminaButGatheringIsNeverBlocked()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Stamina, 0);
        Assert.False(needs.CanSprint);
        var hunger = needs.Value(NeedMeter.Hunger);

        needs.Step(Tps, NeedActivity.Gathering); // the model never refuses a gather: it just costs nothing more

        Assert.True(needs.Value(NeedMeter.Hunger) < hunger);
        Assert.Equal(0, needs.Value(NeedMeter.Stamina));
    }

    [Fact]
    public void ASprintTheNeedsDoNotAllowCountsAsWalking()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Thirst, 0);

        needs.Step(36 * Tps, NeedActivity.Sprinting);

        Assert.Equal(99, needs.Value(NeedMeter.Hunger), 6); // not 1.5 times as fast
    }

    [Fact]
    public void HealthLossesAddAtBothLastStages()
    {
        var hunger = Fresh();
        hunger.Set(NeedMeter.Hunger, 0);
        Assert.Equal(1 / 6.0, hunger.Effects().HealthLossPerSecond, 9);

        var thirst = Fresh();
        thirst.Set(NeedMeter.Thirst, 0);
        Assert.Equal(1 / 4.0, thirst.Effects().HealthLossPerSecond, 9);

        var both = Fresh();
        both.Set(NeedMeter.Hunger, 0);
        both.Set(NeedMeter.Thirst, 0);
        Assert.Equal((1 / 6.0) + (1 / 4.0), both.Effects().HealthLossPerSecond, 9);
        both.Step(60 * Tps, NeedActivity.Idle);
        Assert.Equal(100 - (60 * ((1 / 6.0) + (1 / 4.0))), both.Value(NeedMeter.Health), 6);

        var timeToDeath = 100 / ((1 / 6.0) + (1 / 4.0));
        Assert.Equal(240, timeToDeath, 6); // full to nothing in 4 minutes
        Assert.InRange(100 / (1 / 6.0), 599, 601); // hunger alone: 10 minutes
        Assert.True(100 / (1 / 4.0) < 7 * 60); // thirst alone: under 7
    }

    [Fact]
    public void HealthReachingZeroKillsAndADeadPlayerIsNotStepped()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 0);
        needs.Set(NeedMeter.Thirst, 0);

        needs.Step(241 * Tps, NeedActivity.Idle);

        Assert.True(needs.IsDead);
        Assert.Equal(Drive.Thirst, needs.DeathCause);
        var save = needs.Capture();
        needs.Step(Tps, NeedActivity.Idle);
        Assert.Equal(save, needs.Capture());
    }

    [Fact]
    public void ThereIsNoDeathCauseWhenNoDriveIsEmpty() => Assert.Null(Fresh().DeathCause);

    [Fact]
    public void HealthRecoversOnePointEvery12sWhileNoDriveIsAtStage3Or4()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Health, 50);
        needs.Step(12 * Tps, NeedActivity.Idle);
        Assert.Equal(51, needs.Value(NeedMeter.Health), 6);

        needs.Set(NeedMeter.Hunger, 25); // stage 2: recovery stays
        Assert.False(needs.Effects().RecoveryBlocked);
        needs.Set(NeedMeter.Hunger, 24.99); // stage 3: it stops
        Assert.True(needs.Effects().RecoveryBlocked);
        var health = needs.Value(NeedMeter.Health);
        needs.Step(12 * Tps, NeedActivity.Idle);
        Assert.Equal(health, needs.Value(NeedMeter.Health), 6);
    }

    [Theory]
    [MemberData(nameof(Drives))]
    public void EachDriveAtStage3BlocksRecovery(Drive drive)
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Health, 50);
        needs.Set(MeterOf(drive), 10);
        Assert.True(needs.Effects().RecoveryBlocked);
        Assert.Equal(0, needs.Effects().HealthRecoveryPerSecond);
    }

    [Fact]
    public void RestAtALitFireDoublesStaminaRefillAndHealthRecovery()
    {
        var idle = Fresh();
        idle.Set(NeedMeter.Health, 50);
        idle.Set(NeedMeter.Stamina, 0);
        idle.Step(12 * Tps, NeedActivity.Idle);

        var rest = Fresh();
        rest.Set(NeedMeter.Health, 50);
        rest.Set(NeedMeter.Stamina, 0);
        rest.Step(12 * Tps, NeedActivity.Resting);

        Assert.Equal(51, idle.Value(NeedMeter.Health), 6);
        Assert.Equal(52, rest.Value(NeedMeter.Health), 6);
        Assert.Equal(2 * idle.Effects().StaminaRefillPerSecond, rest.Effects(resting: true).StaminaRefillPerSecond, 6);
        Assert.Equal(2 * Fresh().Effects().HealthRecoveryPerSecond, Fresh().Effects(resting: true).HealthRecoveryPerSecond, 6);
        Assert.Equal(100, rest.Value(NeedMeter.Stamina), 6);
        Assert.Equal(100, idle.Value(NeedMeter.Stamina), 6);
        Assert.Equal(100, rest.Value(NeedMeter.Hunger) + (12 / 36.0), 6); // rest does not restore a drive
    }

    [Fact]
    public void WarningTimesComeFromTheDataAboutThirtyAndFortyFiveMinutesWithoutEffort()
    {
        Assert.Equal(30 * 60, SecondsFromFirstWarningToDamage(Drive.Thirst, NeedActivity.Idle), 1);
        Assert.Equal(45 * 60, SecondsFromFirstWarningToDamage(Drive.Hunger, NeedActivity.Idle), 1);
    }

    [Fact]
    public void WarningTimesShortenToAboutTwentyAndThirtyMinutesWithContinuousEffort()
    {
        foreach (var effort in new[] { NeedActivity.Sprinting, NeedActivity.Gathering })
        {
            Assert.Equal(20 * 60, SecondsFromFirstWarningToDamage(Drive.Thirst, effort), 1);
            Assert.Equal(30 * 60, SecondsFromFirstWarningToDamage(Drive.Hunger, effort), 1);
        }
    }

    // From the first warning (stage 1) to stage 4, where health starts to go, by real steps; the data's own points and rates are the expectation's too.
    private static double SecondsFromFirstWarningToDamage(Drive drive, NeedActivity activity)
    {
        var spec = Data.Of(drive);
        var factor = activity == NeedActivity.Idle ? 1 : spec.SprintFactor;
        var computed = spec.Stages[0].Point * spec.SecondsPerPoint / factor;
        var needs = Fresh();
        needs.Set(MeterOf(drive), spec.Stages[0].Point - 0.0001);
        Assert.Equal(1, needs.StageOf(drive));
        var ticks = 0;
        while (needs.StageOf(drive) < 4)
        {
            needs.Step(1, activity == NeedActivity.Sprinting ? NeedActivity.Gathering : activity); // sprinting would stop at stamina 0: gathering is the same effort for the drives
            ticks++;
        }

        Assert.InRange(ticks / (double)Tps, computed - 1, computed + 1);
        return ticks / (double)Tps;
    }

    [Fact]
    public void ANightSetsSleepTo100AddsTwentyHealthAndTakesFifteenHungerAndTwentyThirst()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 60);
        needs.Set(NeedMeter.Thirst, 70);
        needs.Set(NeedMeter.Sleep, 20);
        needs.Set(NeedMeter.Health, 70);

        needs.Sleep();

        Assert.Equal(100, needs.Value(NeedMeter.Sleep));
        Assert.Equal(90, needs.Value(NeedMeter.Health));
        Assert.Equal(45, needs.Value(NeedMeter.Hunger));
        Assert.Equal(50, needs.Value(NeedMeter.Thirst));
        needs.Set(NeedMeter.Health, 95);
        needs.Sleep();
        Assert.Equal(100, needs.Value(NeedMeter.Health)); // up to 100
    }

    [Fact]
    public void ANightNeverLowersADriveBelow10AndLeavesOneAlreadyBelowItWhereItIs()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 20); // 20 - 15 = 5: the floor holds it at 10
        needs.Set(NeedMeter.Thirst, 5); // already under 10: left alone
        needs.Sleep();
        Assert.Equal(10, needs.Value(NeedMeter.Hunger));
        Assert.Equal(5, needs.Value(NeedMeter.Thirst));

        needs.Set(NeedMeter.Thirst, 10);
        needs.Sleep();
        Assert.Equal(10, needs.Value(NeedMeter.Thirst));
    }

    [Fact]
    public void ANightRecomputesThePenaltiesBeforeFillingStaminaToItsEffectiveMaximum()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 40); // after the night 25: stage 2... 40 - 15 = 25 is stage 2
        needs.Set(NeedMeter.Thirst, 30); // after: 10: stage 3, -35
        needs.Set(NeedMeter.Sleep, 0);
        needs.Set(NeedMeter.Stamina, 0);

        needs.Sleep();

        Assert.Equal(2, needs.StageOf(Drive.Hunger));
        Assert.Equal(3, needs.StageOf(Drive.Thirst));
        Assert.Equal(100 - 15 - 35, needs.Value(NeedMeter.Stamina), 6);
        Assert.Equal(needs.Effects().StaminaMaximum, needs.Value(NeedMeter.Stamina));
    }

    [Fact]
    public void ACollapseSetsSleepTo50GivesNoHealthTakesTheSameCostsAndFillsStamina()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Sleep, 0);
        needs.Set(NeedMeter.Hunger, 80);
        needs.Set(NeedMeter.Thirst, 15);
        needs.Set(NeedMeter.Health, 60);
        needs.Set(NeedMeter.Stamina, 0);

        needs.Collapse();

        Assert.Equal(50, needs.Value(NeedMeter.Sleep));
        Assert.Equal(60, needs.Value(NeedMeter.Health));
        Assert.Equal(65, needs.Value(NeedMeter.Hunger));
        Assert.Equal(10, needs.Value(NeedMeter.Thirst));
        Assert.Equal(needs.Effects().StaminaMaximum, needs.Value(NeedMeter.Stamina));
        Assert.Equal(0, needs.ExhaustionSeconds);
    }

    [Fact]
    public void ACollapseIsDueAfter60ActiveSecondsAtTheSleepDrivesLastStage()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Sleep, 0);

        needs.Step((59 * Tps) + 59, NeedActivity.Idle);
        Assert.False(needs.CollapseDue);
        needs.Step(1, NeedActivity.Idle);
        Assert.True(needs.CollapseDue);
        Assert.Equal(60, needs.ExhaustionSeconds, 6);

        needs.Collapse();
        Assert.False(needs.CollapseDue);
    }

    [Fact]
    public void TheExhaustionTimerResetsWhenSleepRecoversAndOnlyRunsAtStage4()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Sleep, 10);
        needs.Step(30 * Tps, NeedActivity.Idle);
        Assert.Equal(0, needs.ExhaustionSeconds);

        needs.Set(NeedMeter.Sleep, 0);
        needs.Step(30 * Tps, NeedActivity.Idle);
        Assert.Equal(30, needs.ExhaustionSeconds, 6);
        needs.Sleep();
        needs.Step(Tps, NeedActivity.Idle);
        Assert.Equal(0, needs.ExhaustionSeconds);
    }

    [Fact]
    public void FoodRestoresAndCapsAtOneHundred()
    {
        var berry = Data.FoodOf("berry")!;
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 50);
        Assert.True(needs.Eat(berry));
        Assert.Equal(58, needs.Value(NeedMeter.Hunger));

        needs.Set(NeedMeter.Hunger, 96);
        Assert.True(needs.Eat(berry));
        Assert.Equal(100, needs.Value(NeedMeter.Hunger));
    }

    [Fact]
    public void AtFullHungerTheFoodIsKeptEvenCookedFood()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Health, 50);

        Assert.False(needs.Eat(Data.FoodOf("bread")!)); // bread heals, but hunger is full: kept
        Assert.False(needs.Eat(Data.FoodOf("berry")!));
        Assert.Equal(50, needs.Value(NeedMeter.Health));
        Assert.Equal(100, needs.Value(NeedMeter.Hunger));
    }

    [Fact]
    public void BreadHealsAtOnceAndTheWaterBottleGivesThirstAndLeavesTheEmptyBottle()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 50);
        needs.Set(NeedMeter.Health, 50);
        Assert.True(needs.Eat(Data.FoodOf("bread")!));
        Assert.Equal(75, needs.Value(NeedMeter.Hunger));
        Assert.Equal(55, needs.Value(NeedMeter.Health));

        needs.Set(NeedMeter.Thirst, 30);
        var bottle = Data.FoodOf("water-bottle")!;
        Assert.True(needs.Eat(bottle));
        Assert.Equal(70, needs.Value(NeedMeter.Thirst));
        Assert.Equal("bottle", bottle.Leaves);
        needs.Set(NeedMeter.Thirst, 100);
        Assert.False(needs.Eat(bottle)); // full thirst: the bottle is kept
    }

    [Fact]
    public void ADrinkByHandGivesTenAndCapsAndFailsAtFullThirst()
    {
        var needs = Fresh();
        Assert.False(needs.Drink());
        needs.Set(NeedMeter.Thirst, 20);
        Assert.True(needs.Drink());
        Assert.Equal(30, needs.Value(NeedMeter.Thirst));
        needs.Set(NeedMeter.Thirst, 95);
        Assert.True(needs.Drink());
        Assert.Equal(100, needs.Value(NeedMeter.Thirst));
    }

    [Fact]
    public void MeetingANeedRemovesItsPenaltyAndStaminaThenRefillsToTheNewMaximum()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Thirst, 40);
        Assert.Equal(80, needs.Value(NeedMeter.Stamina));
        needs.Eat(Data.FoodOf("water-bottle")!);
        Assert.Equal(0, needs.StageOf(Drive.Thirst));
        Run(needs, 3);
        Assert.Equal(100, needs.Value(NeedMeter.Stamina), 6);
    }

    [Fact]
    public void WakingAfterADeathGives50ToHealthAndTheDrivesAndStaminaAtItsMaximum()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 0);
        needs.Set(NeedMeter.Thirst, 0);
        needs.Die();
        Assert.True(needs.IsDead);

        needs.Wake();

        Assert.False(needs.IsDead);
        foreach (var meter in new[] { NeedMeter.Health, NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep })
        {
            Assert.Equal(50, needs.Value(meter));
        }

        Assert.Equal(needs.Effects().StaminaMaximum, needs.Value(NeedMeter.Stamina));
    }

    [Fact]
    public void SetClampsToTheRangeAndRefusesANonNumber()
    {
        var needs = Fresh();
        needs.Set(NeedMeter.Hunger, 150);
        needs.Set(NeedMeter.Thirst, -3);
        Assert.Equal(100, needs.Value(NeedMeter.Hunger));
        Assert.Equal(0, needs.Value(NeedMeter.Thirst));
        Assert.Throws<ArgumentOutOfRangeException>(() => needs.Set(NeedMeter.Sleep, double.NaN));
    }

    [Fact]
    public void TheSameInputsGiveTheSameMetersAndStages()
    {
        NeedsSave Play()
        {
            var needs = Fresh();
            Run(needs, 90, NeedActivity.Walking);
            Run(needs, 45, NeedActivity.Gathering);
            needs.Eat(Data.FoodOf("berry")!);
            Run(needs, 30, NeedActivity.Resting);
            needs.Step(7 * Tps, NeedActivity.Sprinting);
            return needs.Capture();
        }

        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void ARestoredNeedsStepsOnExactlyAsTheOriginalWould()
    {
        var a = Fresh();
        Run(a, 120, NeedActivity.Gathering);
        var b = Fresh();
        b.Restore(a.Capture());

        Run(a, 30);
        Run(b, 30);

        Assert.Equal(a.Capture(), b.Capture());
    }

    [Fact]
    public void RestoreClampsOutOfRangeValuesAndRefusesANonNumber()
    {
        var needs = Fresh();
        needs.Restore(new NeedsSave(150, -5, 50, 1000, 101, -2, -1));

        Assert.Equal(100, needs.Value(NeedMeter.Hunger));
        Assert.Equal(0, needs.Value(NeedMeter.Thirst));
        Assert.Equal(50, needs.Value(NeedMeter.Stamina)); // held to its maximum: thirst at 0 takes 50 off it
        Assert.Equal(100, needs.Value(NeedMeter.Health));
        Assert.Equal(0, needs.ExhaustionSeconds);
        Assert.Throws<ArgumentException>(() => needs.Restore(new NeedsSave(double.NaN, 1, 1, 1, 1)));
    }
}
