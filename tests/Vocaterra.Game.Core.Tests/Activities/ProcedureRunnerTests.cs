using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The procedure runner (W06, activities plan 4.2, design 10.9 to 10.15) on a three-step procedure built in the test: every rung, the three wrong acts, help, folding into a
/// chunk and the goal, the gap, mastery, the atomic exchange and the full bag, leaving, saving, determinism and decision 19's policy.
/// </summary>
public sealed class ProcedureRunnerTests
{
    private const long T0 = 1_000_000;
    private const long Minute = 60;
    private const long Hour = 3600;

    private sealed class Rig
    {
        public Rig(ulong seed = 7, int session = 1, InstructionEvidencePolicy? policy = null)
        {
            Phrases = new PhraseProgress(ProcedureRig.Sheet(), session, ProcedureRig.Rows);
            Runner = new ProcedureRunner(ProcedureRig.Book, ProcedureRig.StationBook, TestGame.Nouns, Phrases, CreditRules.Default, seed, policy);
        }

        public PhraseProgress Phrases { get; }

        public ProcedureRunner Runner { get; }

        public Inventory Bag { get; set; } = ProcedureRig.Bag();

        public StartOutcome Start() => Runner.Start("pan", "horno", true);

        public ActResult Act(string step, long time, InstructionPresentation presented = InstructionPresentation.Heard | InstructionPresentation.Read)
        {
            var (use, on, verb) = ProcedureRig.ActOf(step);
            return Runner.Act(use, on, verb, Bag, time, presented);
        }

        // Does the steps in the drawn order, each right the first time; returns the last result.
        public ActResult Bake(long time, int? session = null, InstructionPresentation presented = InstructionPresentation.Heard | InstructionPresentation.Read)
        {
            if (session is { } s)
            {
                Phrases.Session = s;
            }

            Runner.Abandon();
            Bag = ProcedureRig.Bag();
            Assert.Equal(StartOutcome.Started, Start());
            ActResult? last = null;
            foreach (var step in Runner.Order.ToList())
            {
                last = Act(step, time, presented);
            }

            return last!;
        }

        public ActResult Collect(long time = T0) => Runner.Collect(Bag, time);

        public int Rung(string row) => (int)Phrases.LevelOf(row) + 1;
    }

    [Fact]
    public void AThreeStepProcedureGoesThroughEveryRung()
    {
        var rig = new Rig();
        var first = new Rig();
        Assert.Equal(StartOutcome.Started, first.Start());
        var view = first.Runner.View();
        Assert.Equal((ProcedureState.InRun, 1, true, true), (view.State, view.Rung, view.ShowEnglish, view.ShowText));
        Assert.Null(view.HighlightUse); // the highlight comes only after a slip or on help

        // Sitting 1, first bake: every row moves from rung 1 to rung 2 at once, with no comprehension credit.
        var done = rig.Bake(T0);
        Assert.Equal((ProcedureState.Done, 3), (done.State, done.Made));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(2, rig.Rung(row)));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(0, rig.Phrases.RecordOf(row).Answered));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(1, rig.Phrases.RecordOf(row).Heard)); // the instruction shown, once per row per bake

        // Second bake, ten minutes later: rows right first time move to rung 3 (audio only).
        _ = rig.Bake(T0 + (11 * Minute));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(3, rig.Rung(row)));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(1, rig.Phrases.RecordOf(row).Answered));

        // Another day: rung 4.
        _ = rig.Bake(T0 + (24 * Hour), session: 2);
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(4, rig.Rung(row)));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(2, rig.Phrases.RecordOf(row).Answered));
        Assert.All(ProcedureRig.Rows, row => Assert.Equal(0, rig.Phrases.RecordOf(row).Supported + rig.Phrases.RecordOf(row).Unaided + rig.Phrases.RecordOf(row).Transfer)); // never production
    }

    [Fact]
    public void TheRungsShowWhatTheyShow()
    {
        var rig = new Rig();
        _ = rig.Bake(T0);
        _ = rig.Bake(T0 + (11 * Minute));
        _ = rig.Runner.Start("pan", "horno", false);
        var view = rig.Runner.View();
        Assert.Equal((3, false, false, true, false), (view.Rung, view.ShowEnglish, view.ShowText, view.PlayAudio, view.TeacherPresent));
        _ = rig.Runner.Help(T0 + (12 * Minute));
        view = rig.Runner.View();
        Assert.Equal((2, true, true), (view.Rung, view.ShowEnglish, view.ShowText)); // help: English and card, one rung up
        Assert.NotNull(view.HighlightUse);
    }

    [Fact]
    public void ThreeKindsOfWrongActChangeNothingAndAreNotProductionSlips()
    {
        var rig = new Rig();
        _ = rig.Start();
        var first = rig.Runner.View().StepId!;
        var (use, on, verb) = ProcedureRig.ActOf(first);
        var before = rig.Bag.Slots.Select(static s => s).ToList();

        var thing = rig.Runner.Act("mano", "mesa", "poner", rig.Bag, T0);
        var wrongVerb = rig.Runner.Act(use, on, "echar" == verb ? "poner" : "echar", rig.Bag, T0);
        var late = rig.Runner.Act("mano", "horno", "sacar", rig.Bag, T0); // the last step's act
        Assert.Equal(ActOutcome.WrongThing, thing.Outcome);
        Assert.Equal(ActOutcome.WrongVerb, wrongVerb.Outcome);
        Assert.Equal(ActOutcome.OutOfOrder, late.Outcome);
        Assert.Equal(before, rig.Bag.Slots.ToList());
        Assert.Equal(first, rig.Runner.View().StepId);
        Assert.All(ProcedureRig.Rows, row => Assert.Equal(0, rig.Phrases.RecordOf(row).Slips));
        Assert.DoesNotContain(rig.Runner.DrainEvents(), static e => e.Name == WorldEvents.ProcedureStep);
    }

    [Fact]
    public void ASlipAnswersByTheLadderOfTheRung()
    {
        var rig = new Rig();
        _ = rig.Start();
        Assert.Equal(SlipResponse.Highlight, rig.Runner.Act("mano", "mesa", "poner", rig.Bag, T0).Response); // rung 1: the first slip shows the highlight
        Assert.NotNull(rig.Runner.View().HighlightUse);
        _ = rig.Bake(T0);
        Assert.Equal(StartOutcome.Started, rig.Start()); // rung 2 now
        Assert.Equal(SlipResponse.Repeat, rig.Runner.Act("mano", "mesa", "poner", rig.Bag, T0).Response);
        Assert.Null(rig.Runner.View().HighlightUse);
        Assert.Equal(SlipResponse.Slower, rig.Runner.Act("mano", "mesa", "poner", rig.Bag, T0).Response);
        Assert.Equal(SlipResponse.Highlight, rig.Runner.Act("mano", "mesa", "poner", rig.Bag, T0).Response);
        Assert.NotNull(rig.Runner.View().HighlightUse);

        // The highlight of the third slip counts as help: the right act earns no comprehension.
        _ = rig.Act(rig.Runner.View().StepId!, T0 + (20 * Minute));
        var step = rig.Runner.Order[0];
        Assert.Equal(0, rig.Phrases.RecordOf(Row(step)).Answered);
        Assert.Equal(2, rig.Rung(Row(step)));
    }

    [Fact]
    public void RepeatAndSlowerAreListeningAidsNeverHelp()
    {
        var rig = new Rig();
        _ = rig.Start();
        Assert.True(rig.Runner.Repeat());
        Assert.True(rig.Runner.Slower());
        Assert.Equal((1, 1), (rig.Runner.Repeats, rig.Runner.Slowers));
        Assert.All(ProcedureRig.Rows, row => Assert.Equal(0, rig.Phrases.RecordOf(row).DontKnow));
        Assert.Null(rig.Runner.View().HighlightUse);
        _ = rig.Bake(T0 + Hour);
        Assert.False(rig.Runner.Repeat()); // no run at a step now
    }

    [Fact]
    public void HelpStepsTheRowUpAndTheRightActAfterItEarnsNothing()
    {
        var rig = new Rig();
        _ = rig.Bake(T0);
        _ = rig.Bake(T0 + (11 * Minute)); // rung 3
        Assert.Equal(StartOutcome.Started, rig.Start());
        var step = rig.Runner.View().StepId!;
        var view = rig.Runner.Help(T0 + (30 * Minute));
        Assert.Equal((2, true), (view.Rung, view.ShowEnglish));
        Assert.Equal(1, rig.Phrases.RecordOf(Row(step)).DontKnow);
        _ = rig.Act(step, T0 + (30 * Minute));
        Assert.Equal(2, rig.Rung(Row(step))); // no step down after help
        Assert.Equal(1, rig.Phrases.RecordOf(Row(step)).Answered); // only the earlier bake
    }

    [Fact]
    public void TwoRunsWithinTheGapCreditOnce()
    {
        var rig = new Rig();
        _ = rig.Bake(T0); // rung 2; the gap clock starts
        _ = rig.Bake(T0 + (5 * Minute)); // within ten minutes: comprehension, but the row does not step down
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(2, rig.Rung(row)));
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(1, rig.Phrases.RecordOf(row).Answered));
        _ = rig.Bake(T0 + (11 * Minute)); // past the gap since the first credit
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(3, rig.Rung(row)));
    }

    [Fact]
    public void RowsAtTheLastRungFoldIntoAChunkThenTheGoalAndHelpUnfoldsThem()
    {
        var rig = new Rig();
        long t = T0;
        for (var session = 1; session <= 3; session++, t += 24 * Hour)
        {
            _ = rig.Bake(t, session);
        }

        // Rows were 1 -> 2 -> 3 -> 4 over three sittings. The next bake introduces the chunks.
        Assert.All(ProcedureRig.Rows.Take(3), row => Assert.Equal(4, rig.Rung(row)));
        rig.Phrases.Session = 4;
        Assert.Equal(StartOutcome.Started, rig.Start());
        var view = rig.Runner.View();
        Assert.Equal((UnitKind.Chunk, "haz-la-masa", 2), (view.Unit, view.Row, view.Rung)); // a chunk row starts at rung 2
        rig.Runner.Abandon();

        var answered = rig.Phrases.RecordOf("pon-la-madera").Answered;
        _ = rig.Bake(t, 4); // the chunks are credited, the hidden rows are not
        t += 24 * Hour;
        Assert.Equal(3, rig.Rung("haz-la-masa"));
        Assert.Equal(3, rig.Rung("haz-el-pan"));
        Assert.Equal(answered, rig.Phrases.RecordOf("pon-la-madera").Answered);
        Assert.Equal(1, rig.Phrases.RecordOf("haz-la-masa").Answered);

        _ = rig.Bake(t, 5);
        t += 24 * Hour;
        Assert.Equal((4, 4), (rig.Rung("haz-la-masa"), rig.Rung("haz-el-pan")));

        // Both chunks at rung 4: the goal alone replaces them.
        rig.Phrases.Session = 6;
        Assert.Equal(StartOutcome.Started, rig.Start());
        view = rig.Runner.View();
        Assert.Equal((UnitKind.Goal, "haz-pan", 2), (view.Unit, view.Row, view.Rung));

        // Help unfolds the goal for this bake into its chunk, and the goal row steps up (to rung 1).
        view = rig.Runner.Help(t);
        Assert.Equal(UnitKind.Chunk, view.Unit);
        Assert.Equal(1, rig.Rung("haz-pan"));
        Assert.Equal(1, rig.Phrases.RecordOf("haz-pan").DontKnow);

        // Help again unfolds the chunk into its steps, and the chunk row steps up.
        view = rig.Runner.Help(t);
        Assert.Equal(UnitKind.Step, view.Unit);
        Assert.Equal(3, rig.Rung("haz-la-masa"));
        Assert.Equal(1, rig.Phrases.RecordOf("haz-la-masa").DontKnow);
    }

    [Fact]
    public void TheGoalAtTheLastRungAndTwoCleanSittingsMasterTheProcedure()
    {
        var rig = new Rig();
        long t = T0;
        var raised = new List<string>();
        for (var session = 1; session <= 8; session++, t += 24 * Hour)
        {
            _ = rig.Bake(t, session);
            raised.AddRange(rig.Runner.DrainEvents().Select(static e => e.Name));
        }

        Assert.Equal(4, rig.Rung("haz-pan"));
        Assert.True(rig.Runner.Progress.IsMastered("pan"));
        Assert.Equal(1, raised.Count(static n => n == WorldEvents.ProcedureMastered));
        Assert.Equal(8, raised.Count(static n => n == WorldEvents.ProcedureDone));
        Assert.Equal(8, rig.Runner.Progress.RecordOf("pan").Completed);
        Assert.Equal(Enumerable.Range(1, 8), rig.Runner.Progress.RecordOf("pan").Sittings);
    }

    [Fact]
    public void ABakeWithHelpIsNotACleanSittingAndNeverMasters()
    {
        var rig = new Rig();
        long t = T0;
        for (var session = 1; session <= 9; session++, t += 24 * Hour)
        {
            rig.Phrases.Session = session;
            rig.Bag = ProcedureRig.Bag();
            Assert.Equal(StartOutcome.Started, rig.Start());
            _ = rig.Runner.Help(t); // asked for in every bake
            foreach (var step in rig.Runner.Order.ToList())
            {
                _ = rig.Act(step, t);
            }
        }

        Assert.Empty(rig.Runner.Progress.RecordOf("pan").Sittings);
        Assert.False(rig.Runner.Progress.IsMastered("pan"));
    }

    [Fact]
    public void LeavingHalfWayTakesNothingAndTheRunGoesOn()
    {
        var rig = new Rig();
        _ = rig.Start();
        var first = rig.Runner.View().StepId!;
        _ = rig.Act(first, T0);
        var bag = rig.Bag.Slots.ToList();
        rig.Runner.Leave();

        Assert.Equal(bag, rig.Bag.Slots.ToList());
        Assert.Equal(ProcedureState.InRun, rig.Runner.State);
        Assert.Equal(StartOutcome.Resumed, rig.Start());
        Assert.Equal(1, rig.Runner.View().StepsDone);
        Assert.Equal(1, rig.Runner.Progress.RecordOf("pan").Runs); // a resumed run is not counted again
        Assert.Equal(1, rig.Phrases.RecordOf(Row(first)).Heard);
    }

    [Fact]
    public void AGroupOfStepsIsDrawnPerBakeAndTheSameSeedDrawsTheSame()
    {
        var orders = new HashSet<string>();
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var rig = new Rig((ulong)seed);
            _ = rig.Start();
            var order = rig.Runner.Order;
            Assert.Equal("sacar", order[2]); // the group is two places, the last step stays
            _ = orders.Add(string.Join(",", order));
            var again = new Rig((ulong)seed);
            _ = again.Start();
            Assert.Equal(order, again.Runner.Order);
        }

        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public void TheSameActsGiveTheSameEvents()
    {
        List<ProcedureEvent> Play()
        {
            var rig = new Rig(3);
            var events = new List<ProcedureEvent>();
            for (var i = 1; i <= 3; i++)
            {
                _ = rig.Bake(T0 + (i * Hour), i);
                events.AddRange(rig.Runner.DrainEvents());
            }

            return events;
        }

        var one = Play();
        Assert.Equal(one, Play());
        Assert.Equal([WorldEvents.ProcedureStarted, WorldEvents.ProcedureStep, WorldEvents.ProcedureStep, WorldEvents.ProcedureStep, WorldEvents.ProcedureDone], one.Take(5).Select(static e => e.Name));
        Assert.All(one.Where(static e => e.Name == WorldEvents.ProcedureStep), e => Assert.Contains(e.Detail, ProcedureRig.Definition.Steps.Select(static s => s.Id)));
        Assert.All([WorldEvents.ProcedureStarted, WorldEvents.ProcedureStep, WorldEvents.ProcedureDone, WorldEvents.ProcedureMastered], name => Assert.Contains(name, WorldEvents.All));
    }

    [Fact]
    public void TheYieldHasTheBonusOnlyWhenEnoughStepsAreRightFirstTime()
    {
        var clean = new Rig();
        Assert.Equal(3, clean.Bake(T0).Made); // two loaves and one for three first-time steps
        Assert.Equal(3, clean.Bag.Count(TestItems.Bread));
        Assert.Equal((1, 0, 0), (clean.Bag.Count(ItemId.Bottle), clean.Bag.Count(ItemId.WaterBottle), clean.Bag.Count(ItemId.Wood)));

        var slipped = new Rig();
        _ = slipped.Start();
        _ = slipped.Runner.Act("mano", "mesa", "poner", slipped.Bag, T0);
        ActResult? last = null;
        foreach (var step in slipped.Runner.Order.ToList())
        {
            last = slipped.Act(step, T0);
        }

        Assert.Equal(2, last!.Made);

        var helped = new Rig(); // help is allowed for the practical reward
        _ = helped.Start();
        _ = helped.Runner.Help(T0);
        foreach (var step in helped.Runner.Order.ToList())
        {
            last = helped.Act(step, T0);
        }

        Assert.Equal(3, last!.Made);
    }

    [Fact]
    public void TheExchangeIsAtomicAndAMissingIngredientReopensItsStep()
    {
        var rig = new Rig();
        _ = rig.Start();
        var order = rig.Runner.Order.ToList();
        _ = rig.Act(order[0], T0);
        _ = rig.Act(order[1], T0);
        _ = rig.Bag.Remove(ItemId.Wood, 1); // sold meanwhile
        var result = rig.Act(order[2], T0);

        Assert.Equal(ProcedureState.InRun, result.State);
        Assert.Equal(["madera"], result.Missing);
        Assert.Equal(0, rig.Bag.Count(TestItems.Bread));
        Assert.Equal(1, rig.Bag.Count(ItemId.WaterBottle)); // nothing was used up
        Assert.Equal("madera", rig.Runner.View().StepId);
        _ = rig.Bag.Add(ItemId.Wood, 1);
        var done = rig.Act("madera", T0); // the step is done again, and the last one was done already
        Assert.Equal((ProcedureState.Done, 3), (done.State, done.Made));
    }

    [Fact]
    public void AFullBagLeavesTheBakeReadyToCollectAndItGrantsOnce()
    {
        var rig = new Rig { Bag = new Inventory(TestItems.Catalogue) };
        for (var slot = 0; slot < Inventory.SlotCount - 3; slot++)
        {
            rig.Bag.SetSlot(slot, new ItemStack(ItemId.Torch, 1));
        }

        _ = rig.Bag.Add(ItemId.Wood, 2); // using one leaves the slot taken
        _ = rig.Bag.Add(ItemId.WaterBottle, 1); // its slot goes to the bottle given back
        _ = rig.Bag.Add(ItemId.Coin, 1); // the last slot
        Assert.Equal(StartOutcome.Started, rig.Start());
        ActResult? last = null;
        foreach (var step in rig.Runner.Order.ToList())
        {
            last = rig.Act(step, T0);
        }

        Assert.Equal(ProcedureState.ReadyToCollect, last!.State);
        Assert.Equal((0, 2, 1), (rig.Bag.Count(TestItems.Bread), rig.Bag.Count(ItemId.Wood), rig.Bag.Count(ItemId.WaterBottle))); // nothing was used up
        Assert.Equal(ProcedureState.ReadyToCollect, rig.Collect().State); // still no room
        Assert.DoesNotContain(rig.Runner.DrainEvents(), static e => e.Name == WorldEvents.ProcedureDone);

        Assert.True(rig.Bag.RemoveFromSlot(0, 1)); // a torch dropped
        var collected = rig.Collect();
        Assert.Equal((ProcedureState.Done, 3), (collected.State, collected.Made));
        Assert.Equal((3, 1, 0, 1), (rig.Bag.Count(TestItems.Bread), rig.Bag.Count(ItemId.Wood), rig.Bag.Count(ItemId.WaterBottle), rig.Bag.Count(ItemId.Bottle)));
        Assert.Equal(ActOutcome.NotRunning, rig.Collect().Outcome); // collecting again grants nothing
        Assert.Equal(3, rig.Bag.Count(TestItems.Bread));
        Assert.Single(rig.Runner.DrainEvents(), static e => e.Name == WorldEvents.ProcedureDone);
    }

    [Fact]
    public void PresentationPolicyMovesRungsTheSameWhicheverWayAndKeepsHeardAndReadApart()
    {
        var heard = new Rig();
        var read = new Rig();
        for (var i = 0; i < 3; i++)
        {
            _ = heard.Bake(T0 + (i * 24 * Hour), i + 1, InstructionPresentation.Heard);
            _ = read.Bake(T0 + (i * 24 * Hour), i + 1, InstructionPresentation.Read);
        }

        Assert.Equal(ProcedureRig.Rows.Select(heard.Rung), ProcedureRig.Rows.Select(read.Rung));
        Assert.Equal((3, 0), (heard.Runner.Progress.EvidenceOf("pon-la-madera").HeardActs, heard.Runner.Progress.EvidenceOf("pon-la-madera").ReadActs));
        Assert.Equal((0, 3), (read.Runner.Progress.EvidenceOf("pon-la-madera").HeardActs, read.Runner.Progress.EvidenceOf("pon-la-madera").ReadActs));

        var strict = new Rig(policy: new InstructionEvidencePolicy(RequirePresentation: true)); // the owner's alternative: an unpresented act earns nothing
        _ = strict.Bake(T0, presented: InstructionPresentation.None);
        Assert.Equal(1, strict.Rung("pon-la-madera"));
    }

    [Fact]
    public void ARunSurvivesCaptureAndRestoreAndCannotGrantTwice()
    {
        var rig = new Rig();
        _ = rig.Bake(T0);
        rig.Bag = ProcedureRig.Bag();
        _ = rig.Start();
        var order = rig.Runner.Order.ToList();
        _ = rig.Act(order[0], T0 + Hour);
        var saved = rig.Runner.Capture();

        var other = new Rig();
        other.Phrases.Restore(rig.Phrases.Records);
        other.Bag = rig.Bag;
        other.Runner.Restore(saved);
        Assert.Equal(rig.Runner.View(), other.Runner.View());
        Assert.Equal(order, other.Runner.Order);
        var (was, now) = (rig.Runner.Progress.RecordOf("pan"), other.Runner.Progress.RecordOf("pan"));
        Assert.Equal((was.Runs, was.Completed, was.FirstTimeSteps, was.Mastered), (now.Runs, now.Completed, now.FirstTimeSteps, now.Mastered));
        Assert.Equal(was.Sittings, now.Sittings);
        Assert.Equal(StartOutcome.Resumed, other.Start());

        _ = other.Act(order[1], T0 + Hour);
        var done = other.Act(order[2], T0 + Hour);
        Assert.Equal(ProcedureState.Done, done.State);
        var breads = other.Bag.Count(TestItems.Bread);

        var replay = new Rig();
        replay.Bag = other.Bag;
        replay.Runner.Restore(other.Runner.Capture());
        Assert.Equal(ProcedureState.Done, replay.Runner.State);
        Assert.Equal(ActOutcome.NotRunning, replay.Act(order[0], T0).Outcome);
        Assert.Equal(ActOutcome.NotRunning, replay.Collect().Outcome);
        Assert.Equal(breads, replay.Bag.Count(TestItems.Bread));
        Assert.Null(new Rig().Runner.Capture()); // nothing to save yet
    }

    [Fact]
    public void ASavedRunOfAProcedureTheBundleNoLongerHasIsDropped()
    {
        var rig = new Rig();
        _ = rig.Start();
        var saved = rig.Runner.Capture()! with { Run = rig.Runner.Capture()!.Run! with { Procedure = "gone" } };
        var other = new Rig();
        other.Runner.Restore(saved);
        Assert.Equal(ProcedureState.NotStarted, other.Runner.State);
        Assert.Equal(1, other.Runner.Progress.RecordOf("pan").Runs);
    }

    [Fact]
    public void ARunNeedsItsStationAndItsItems()
    {
        var rig = new Rig();
        Assert.Equal(StartOutcome.Unknown, rig.Runner.Start("pan", "mesa", true));
        Assert.Equal(StartOutcome.Unknown, rig.Runner.Start("nope", "horno", true));
        _ = rig.Start();
        Assert.Equal(ActOutcome.NotRunning, new Rig().Act("sacar", T0).Outcome);
    }

    private static string Row(string step) => ProcedureRig.Definition.Steps.First(s => s.Id == step).Row;
}
