using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The station overlay (W07, design 10.8 and 10.10) over the three-step test procedure: what can be held and where it pages, the verb choice only for a pair of several verbs, the
/// highlight after a slip or on help, the English only at the first rung or after help, the mute fallback, the audio, the labels counted as read once per bake and a whole bake through
/// the keys.
/// </summary>
public sealed class StationPanelTests
{
    private static StationCell Cell(StationView view, string noun) => view.Held.Concat(view.Targets).Append(view.Hand).First(c => c.Noun == noun);

    [Fact]
    public void OpeningStartsTheFirstProcedureOfTheStationAndSpeaksItsFirstRow()
    {
        var rig = new StationRig();
        Assert.True(rig.Panel.Open("oven"));
        Assert.True(rig.Panel.IsOpen);
        Assert.True(rig.Panel.FreesMouse);
        var view = rig.View;
        Assert.Equal("bread", view.ProcedureId);
        Assert.Equal(ProcedureState.InRun, view.State);
        Assert.Equal(3, view.Strip.Dots.Count);
        Assert.Equal(StripDotState.Current, view.Strip.Dots[0].State);
        Assert.Equal([view.Strip.Row!], rig.Audio.Played); // the row is spoken once when it appears
        Assert.False(new StationRig().Panel.Open("no-such-station"));
    }

    [Fact]
    public void ThePanelDoesNotOpenOverAnotherPanel()
    {
        var rig = new StationRig(otherPanelOpen: () => true);
        Assert.False(rig.Panel.Open("oven"));
        Assert.False(rig.Panel.IsOpen);
        Assert.Equal(ProcedureState.NotStarted, rig.Session.Procedures.State);
    }

    [Fact]
    public void WhatCanBeHeldIsTheBagTheToolsAndTheEmptyHandAndPagesByNine()
    {
        var rig = new StationRig();
        _ = rig.Session.Simulation.Inventory.Add(ItemId.Apple, 3);
        _ = rig.Session.Simulation.Inventory.Add(ItemId.Coin, 2);
        _ = rig.Panel.Open("oven");
        var view = rig.View;
        Assert.Contains(view.Held, c => c is { Noun: "madera", Kind: StationCellKind.Bag, Count: 1, Label: "la madera ×1" });
        Assert.Contains(view.Held, c => c is { Noun: "manzana", Count: 3, Label: "la manzana ×3" }); // apples and coins are natural distractors
        Assert.Contains(view.Held, c => c.Noun == "moneda");
        Assert.Equal(StationCellKind.Hand, view.Hand.Kind);
        Assert.Equal(0, view.Hand.Key);
        Assert.Equal(Enumerable.Range(1, view.Held.Count).Select(static i => (int?)i), view.Held.Select(static c => c.Key));
        Assert.Equal((0, 1), (view.Page, view.PageCount));

        // Twenty more kinds of thing would not fit: the list pages by nine, and Page Up and Page Down turn it.
        var crowded = new StationRig();
        foreach (var item in ItemDefinition.All.Where(d => crowded.Session.Nouns.ForItem(d.Id) is not null).Take(14))
        {
            _ = crowded.Session.Simulation.Inventory.Add(item.Id, 1);
        }

        _ = crowded.Panel.Open("oven");
        var first = crowded.View;
        Assert.Equal(StationScreen.PageSize, first.Held.Count);
        Assert.True(first.PageCount >= 2);
        crowded.Press(GameKey.PageDown);
        var second = crowded.View;
        Assert.Equal(1, second.Page);
        Assert.NotEqual(first.Held[0].Noun, second.Held[0].Noun);
        Assert.Equal(1, second.Held[0].Key); // the digits start again on each page
        crowded.Press(GameKey.PageUp);
        crowded.Press(GameKey.PageUp);
        Assert.Equal(0, crowded.View.Page);
    }

    [Fact]
    public void TheToolsAreTargetsOrHeldByWhatTheStepsDoWithThem()
    {
        // The test procedure acts on the table and the oven and with nothing from the station: both fixtures are targets and no tool is held.
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        Assert.Equal(["mesa", "horno"], rig.View.Targets.Select(static t => t.Noun));
        Assert.Equal(["mesa", "horno"], rig.View.Fixtures.Select(static t => t.Noun));
        Assert.DoesNotContain(rig.View.Held, static c => c.Kind == StationCellKind.Tool);
    }

    [Fact]
    public void TheDigitsSelectWhatIsHeldAndZeroTheEmptyHandAndTabMovesBetweenTargets()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        Assert.Equal("mesa", rig.View.Targets.First(static t => t.Selected).Noun); // the first target is taken until another is chosen
        rig.Hold("agua");
        Assert.True(Cell(rig.View, "agua").Selected);
        Assert.Equal("el agua ×1", rig.View.HeldLabel); // the selected item stays visible
        rig.Press(GameKey.D0);
        Assert.True(rig.View.Hand.Selected);
        Assert.False(Cell(rig.View, "agua").Selected);
        rig.Press(GameKey.Tab);
        Assert.Equal("horno", rig.View.Targets.First(static t => t.Selected).Noun);
        rig.Panel.HandleInput(new TestInputFrame { Keys = new HashSet<GameKey> { GameKey.Tab, GameKey.LeftShift } });
        Assert.Equal("mesa", rig.View.Targets.First(static t => t.Selected).Noun);
    }

    [Fact]
    public void TheVerbChoiceAppearsOnlyForAPairWithSeveralVerbs()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var order = rig.Session.Procedures.Order.ToList();

        // One verb for the pair: Enter acts at once, no choice.
        rig.DoStep(order[0]);
        Assert.Null(rig.View.Verbs);
        rig.DoStep(order[1]);
        Assert.Equal(2, rig.View.Strip.Dots.Count(static d => d.State == StripDotState.Done));

        // The hand on the oven has two verbs (sacar, abrir): a choice opens; Esc closes it and not the station.
        rig.Press(GameKey.D0);
        rig.Aim("horno");
        rig.Press(GameKey.Enter);
        Assert.Equal(["sacar", "abrir"], rig.View.Verbs!.Verbs);
        rig.Press(GameKey.Escape);
        Assert.Null(rig.View.Verbs);
        Assert.True(rig.Panel.IsOpen);

        // The verb picked is the act: abrir is the right thing and target with the wrong verb, a slip of the step.
        rig.Press(GameKey.D0);
        rig.Press(GameKey.Enter);
        rig.Press(GameKey.Down); // the cursor on abrir
        rig.Press(GameKey.Enter);
        Assert.Equal(StationNotice.WrongVerb, rig.View.Notice);
        Assert.Equal(2, rig.Session.Procedures.View().StepsDone);
    }

    [Fact]
    public void ThePickedVerbIsTheActAndTheRightOneMovesOn()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        rig.DoStep(rig.Session.Procedures.Order[0]);
        rig.DoStep(rig.Session.Procedures.Order[1]);
        rig.Press(GameKey.D0);
        rig.Aim("horno");
        rig.Press(GameKey.Enter);
        rig.Press(GameKey.D1); // sacar
        Assert.Equal(ProcedureState.Done, rig.View.State);
        Assert.Equal(StationNotice.Done, rig.View.Notice);
        Assert.Equal(3, rig.Session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void AWrongActHighlightsTheThingAndTheTargetAtTheFirstRungButNotBefore()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var step = rig.Session.Procedures.Current!.Steps.First(s => s.Id == rig.Session.Procedures.Order[0]);
        Assert.DoesNotContain(rig.View.Held.Concat(rig.View.Targets), static c => c.Highlight); // nothing shows before a slip at the English rung (design 10.2)

        rig.Press(GameKey.D0); // the empty hand on the table: wrong thing
        rig.Press(GameKey.Enter);
        var view = rig.View;
        Assert.Equal(StationNotice.WrongThing, view.Notice);
        Assert.True(Cell(view, step.ActUse).Highlight);
        Assert.True(Cell(view, step.ActOn).Highlight);
        Assert.Single(view.Held, static c => c.Highlight);
        Assert.Single(view.Targets, static c => c.Highlight);
    }

    [Fact]
    public void AtTheLaterRungsTheFirstSlipRepeatsTheSecondSlowsAndTheThirdHighlights()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        rig.Bake(); // a first bake, right: the rows stand at the card-and-audio rung
        rig.NextBake();
        Assert.Equal(ProcedureState.InRun, rig.View.State);
        Assert.Equal(2, rig.View.Strip.Rung);
        rig.Audio.Played.Clear();

        rig.Press(GameKey.D0);
        rig.Press(GameKey.Enter); // slip 1: the instruction again
        Assert.Single(rig.Audio.Played);
        Assert.DoesNotContain(rig.View.Held.Concat(rig.View.Targets), static c => c.Highlight);
        rig.Press(GameKey.D0);
        rig.Press(GameKey.Enter); // slip 2: slowly
        Assert.EndsWith("@slow", rig.Audio.Played[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(rig.View.Held.Concat(rig.View.Targets), static c => c.Highlight);
        rig.Press(GameKey.D0);
        rig.Press(GameKey.Enter); // slip 3: the highlight (counted as help)
        Assert.Contains(rig.View.Held, static c => c.Highlight);
        Assert.Contains(rig.View.Targets, static c => c.Highlight);
    }

    [Fact]
    public void HelpShowsTheEnglishAndTheHighlightAtOnceAndStepsTheRowUp()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        rig.Bake();
        rig.NextBake();
        var before = rig.View.Strip;
        Assert.False(before.ShowsEnglish); // rung 2: the card and the audio, the English on help
        Assert.True(before.ShowsText);
        Assert.DoesNotContain(rig.View.Held.Concat(rig.View.Targets), static c => c.Highlight);

        rig.Press(GameKey.H);
        var after = rig.View;
        Assert.True(after.Strip.ShowsEnglish);
        Assert.Contains(after.Held, static c => c.Highlight);
        Assert.Contains(after.Targets, static c => c.Highlight);
        Assert.Equal(before.Rung - 1, after.Strip.Rung); // the row steps up toward the English
    }

    [Fact]
    public void HelpSelectsTheHighlightedThingAndTargetSoEnterMakesTheShownAct()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var step = rig.Session.Procedures.Current!.Steps.First(s => s.Id == rig.Session.Procedures.Order[0]);

        rig.Press(GameKey.H);
        Assert.Equal(step.ActUse, rig.View.Held.Single(static c => c.Selected).Noun); // the highlighted thing is the selection
        Assert.Equal(step.ActOn, rig.View.Targets.Single(static c => c.Selected).Noun); // and its target
        Assert.True(Cell(rig.View, step.ActUse).Highlight);

        rig.Press(GameKey.Enter); // the shown act, made by Enter
        Assert.Equal(StationNotice.Right, rig.View.Notice);
    }

    [Fact]
    public void TheThirdSlipsHighlightIsTheSelectionToo()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        rig.Bake();
        rig.NextBake(); // the rows at rung 2: the first and second slips repeat, the third highlights
        var step = rig.Session.Procedures.Current!.Steps.First(s => s.Id == rig.Session.Procedures.Order[0]);
        for (var slip = 0; slip < 3; slip++)
        {
            rig.Press(GameKey.D0);
            rig.Press(GameKey.Enter);
        }

        Assert.Equal(step.ActOn, rig.View.Targets.Single(static c => c.Selected).Noun); // the target of the shown act
        Assert.Equal(step.ActUse, rig.View.Held.Single(static c => c.Selected).Noun); // the thing to hold
    }

    [Fact]
    public void TheEnglishShowsOnlyAtTheFirstRungOrAfterHelp()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        Assert.Equal(1, rig.View.Strip.Rung);
        Assert.True(rig.View.Strip.ShowsEnglish);
        Assert.True(rig.View.Strip.ShowsText);

        rig.Bake();
        rig.NextBake(11 * 60); // a right act at rung 1 moves every row to rung 2; the next gap passes, so a right act at rung 2 steps it to rung 3
        Assert.Equal(2, rig.View.Strip.Rung);
        Assert.False(rig.View.Strip.ShowsEnglish);
        Assert.True(rig.View.Strip.ShowsText);

        rig.Bake();
        rig.NextBake(11 * 60);
        Assert.Equal(3, rig.View.Strip.Rung);
        Assert.False(rig.View.Strip.ShowsEnglish);
        Assert.False(rig.View.Strip.ShowsText); // audio only: heard, not read
        rig.Press(GameKey.H);
        Assert.True(rig.View.Strip.ShowsEnglish); // after help
        Assert.True(rig.View.Strip.ShowsText);
    }

    [Fact]
    public void WhenTheBakeIsDoneNothingIsTaught()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        foreach (var id in rig.Session.Procedures.Order.ToList())
        {
            rig.DoStep(id);
        }

        Assert.Equal(ProcedureState.Done, rig.View.State);
        Assert.False(rig.View.Strip.ShowsEnglish);
        Assert.False(rig.View.Strip.ShowsText);
        Assert.Null(rig.View.Strip.Row);
        Assert.All(rig.View.Strip.Actions, static a => Assert.False(a.Enabled));
    }

    [Fact]
    public void WithNoAudioTheSpanishCardShowsAtTheAudioOnlyRungAndTheActIsReadingEvidence()
    {
        var rig = new StationRig(playable: false);
        _ = rig.Panel.Open("oven");
        rig.Bake();
        rig.NextBake(11 * 60);
        rig.Bake();
        rig.NextBake(11 * 60);
        var strip = rig.View.Strip;
        Assert.Equal(3, strip.Rung); // the rung is the same: muted play is not held back
        Assert.True(strip.ShowsText);
        Assert.True(strip.CardForced);
        Assert.Empty(rig.Audio.Played);
        Assert.Contains(strip.Actions, static a => a.Kind == StripActionKind.Repeat && !a.Enabled); // nothing to replay
        var row = strip.Row!;
        var before = rig.Session.Procedures.Progress.EvidenceOf(row);
        rig.DoStep(rig.Session.Procedures.Order[0]);
        var evidence = rig.Session.Procedures.Progress.EvidenceOf(row);
        Assert.Equal(before.HeardActs, evidence.HeardActs);
        Assert.Equal(before.ReadActs + 1, evidence.ReadActs);
    }

    [Fact]
    public void AnActIsHeardWhenTheAudioPlayedAndReadWhenTheTextWasOnScreen()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var row = rig.View.Strip.Row!;
        rig.DoStep(rig.Session.Procedures.Order[0]);
        var evidence = rig.Session.Procedures.Progress.EvidenceOf(row);
        Assert.Equal(1, evidence.HeardActs);
        Assert.Equal(1, evidence.ReadActs);
    }

    [Fact]
    public void RepeatAndSlowerPlayTheRowAndAreListeningAidsNeverHelp()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        rig.Audio.Played.Clear();
        var row = rig.View.Strip.Row!;
        rig.Press(GameKey.R);
        rig.Press(GameKey.S);
        Assert.Equal([row, row + "@slow"], rig.Audio.Played);
        Assert.Equal(1, rig.Session.Procedures.Repeats);
        Assert.Equal(1, rig.Session.Procedures.Slowers);
        Assert.Equal(1, rig.View.Strip.Rung); // no help: the rung did not move
        Assert.False(rig.Session.Procedures.Progress.RecordOf("bread").Mastered);
    }

    [Fact]
    public void ABakeThroughTheKeysMakesTheBreadKeepsTheBottleAndEscLeavesWithTheRunSaved()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var order = rig.Session.Procedures.Order.ToList();
        rig.DoStep(order[0]);
        Assert.Equal(StationNotice.Right, rig.View.Notice);
        Assert.Equal(1, rig.View.Strip.Dots.Count(static d => d.State == StripDotState.Done));

        rig.Press(GameKey.Escape); // leave in the middle
        Assert.False(rig.Panel.IsOpen);
        Assert.Equal(ProcedureState.InRun, rig.Session.Procedures.State);
        Assert.True(rig.Panel.Open("oven")); // the run is resumed where it was
        Assert.Equal(1, rig.View.Strip.Dots.Count(static d => d.State == StripDotState.Done));

        rig.DoStep(order[1]);
        rig.DoStep(order[2]);
        Assert.Equal(StationNotice.Done, rig.View.Notice);
        Assert.Equal(3, rig.Session.Simulation.Inventory.Count(ItemId.Bread)); // two, and one more: all three steps were right the first time
        Assert.Equal(1, rig.Session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal(0, rig.Session.Simulation.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void AFullBagLeavesTheBakeToCollectAndEnterTriesAgain()
    {
        var rig = new StationRig();
        var bag = rig.Session.Simulation.Inventory;
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            bag.SetSlot(slot, default);
        }

        for (var slot = 0; slot < Inventory.SlotCount - 3; slot++)
        {
            bag.SetSlot(slot, new ItemStack(ItemId.Torch, 1));
        }

        _ = bag.Add(ItemId.Wood, 2); // using one leaves the slot taken
        _ = bag.Add(ItemId.WaterBottle, 1); // its slot goes to the bottle given back
        _ = bag.Add(ItemId.Coin, 1); // the last slot
        _ = rig.Panel.Open("oven");
        foreach (var id in rig.Session.Procedures.Order.ToList())
        {
            rig.DoStep(id);
        }

        Assert.Equal(ProcedureState.ReadyToCollect, rig.View.State);
        Assert.Equal(StationNotice.BagFull, rig.View.Notice);
        Assert.Equal(0, bag.Count(ItemId.Bread)); // nothing was used up
        rig.Press(GameKey.Enter);
        Assert.Equal(ProcedureState.ReadyToCollect, rig.View.State); // still no room
        Assert.True(bag.RemoveFromSlot(0, 1));
        Assert.True(bag.RemoveFromSlot(1, 1));
        rig.Press(GameKey.Enter);
        Assert.Equal(ProcedureState.Done, rig.View.State);
        Assert.Equal(StationNotice.Done, rig.View.Notice);
        Assert.Equal(3, bag.Count(ItemId.Bread));
    }

    [Fact]
    public void EveryShownNounIsReadOnceInABakeAndAgainInTheNext()
    {
        var rig = new StationRig();
        var before = rig.Session.Words.RecordOf("madera").Read;
        _ = rig.Panel.Open("oven");
        var afterOpen = rig.Session.Words.RecordOf("madera").Read;
        Assert.Equal(before + 1, afterOpen);
        rig.Press(GameKey.Escape);
        _ = rig.Panel.Open("oven"); // the same bake, opened again
        Assert.Equal(afterOpen, rig.Session.Words.RecordOf("madera").Read);
        rig.Press(GameKey.PageDown);
        Assert.Equal(afterOpen, rig.Session.Words.RecordOf("madera").Read);
        Assert.Equal(0, rig.Session.Words.RecordOf("madera").Spoken); // a read is never a spoken credit
        Assert.False(rig.Session.Words.RecordOf("madera").Known);
    }

    [Fact]
    public void TheEnglishOfANounShowsOnHoverUntilTheNounIsKnown()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        Assert.False(string.IsNullOrEmpty(Cell(rig.View, "madera").Hover));
        Assert.Equal(rig.Session.Locale.NounSupport("madera"), Cell(rig.View, "madera").Hover);
    }

    [Fact]
    public void TalkingKeysCallTheHookW08Fills()
    {
        var rig = new StationRig();
        _ = rig.Panel.Open("oven");
        var asked = new List<StationTalk>();
        rig.Press(GameKey.V); // no hook yet: nothing happens
        rig.Panel.OnTalk = asked.Add;
        rig.Press(GameKey.V);
        rig.Press(GameKey.Space);
        rig.Press(GameKey.W);
        Assert.Equal([StationTalk.Speak, StationTalk.Speak, StationTalk.Type], asked);
    }
}
