using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The one-click bake of a mastered procedure at the station (W07b, design 10.15): absent before mastery, present after it, the guided bake first and selected when the goal row
/// is due again, never forced, the bake through <see cref="GuideSession.BakeMastered"/> once (its uses taken, its makes given), and the keys and the picks.
/// </summary>
public sealed class StationBakeOfferTests
{
    private const long Later = 10 * 24 * 60 * 60; // ten days: every gap has passed

    private static ProceduresSave Mastered() =>
        new(1, ["pan"], [new ProcedureProgressSave("pan", 8, 8, [1, 2, 3], 24, true)], [], null);

    private static int Crafted(StationRig rig) => rig.Session.Events.OfType<WorldEvent>().Count(static e => e.Name == WorldEvents.ItemCrafted);

    [Fact]
    public void ThereIsNoOfferBeforeMastery()
    {
        var rig = new StationRig();
        Assert.True(rig.Panel.Open("horno"));
        Assert.Null(rig.View.Offer);
        rig.Press(GameKey.B); // nothing to bake at once
        Assert.Equal(0, rig.Session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(0, Crafted(rig));
    }

    [Fact]
    public void TheOfferAppearsAfterMasteryAndTheOneClickBakeIsTheDefaultWhenTheGoalIsNotDue()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        var offer = rig.View.Offer;
        Assert.NotNull(offer);
        Assert.Equal("pan", offer.ProcedureId);
        Assert.False(offer.GuidedFirst);
        Assert.True(offer.OneClickSelected);
    }

    [Fact]
    public void TheGuidedBakeComesFirstAndIsSelectedWhenTheGoalRowIsDue()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        // The goal row was credited in an earlier play session: its review is due (the instruction rows are not in DuePhrases, so the station asks by the same rule).
        rig.Session.PhraseProgress.Restore([new PhraseRecord("haz-pan", SupportLevel.CardAndAudio, CreditedUses: 1, LastCreditedAt: 1, LastCreditedSession: 0)]);
        Assert.True(rig.Panel.Open("horno"));
        var offer = rig.View.Offer;
        Assert.NotNull(offer);
        Assert.True(offer.GuidedFirst);
        Assert.False(offer.OneClickSelected);

        rig.Press(GameKey.Enter); // the guided bake is selected: Enter does not bake at once
        Assert.Equal(0, rig.Session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(0, Crafted(rig));
    }

    [Fact]
    public void BakesAtOnceConsumeTheUsesGiveTheMakesOnceAndLogItemCrafted()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        var inventory = rig.Session.Simulation.Inventory;

        rig.Press(GameKey.B);
        Assert.Equal((2, 1, 0, 0), (inventory.Count(TestItems.Bread), inventory.Count(ItemId.Bottle), inventory.Count(ItemId.WaterBottle), inventory.Count(ItemId.Wood)));
        Assert.Equal(1, Crafted(rig));
        Assert.Equal(StationNotice.Done, rig.View.Notice);
        Assert.Equal(ProcedureState.InRun, rig.View.State); // the run stays where it was
        Assert.NotNull(rig.View.Offer); // the guided bake stays on offer

        rig.Press(GameKey.B); // nothing left to use: refused, nothing made
        Assert.Equal(2, inventory.Count(TestItems.Bread));
        Assert.Equal(StationNotice.NotEnough, rig.View.Notice);
        Assert.Equal(1, Crafted(rig));
    }

    [Fact]
    public void APickOfAHeldThingMakesTheGuidedBakeSelected()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        rig.Press(GameKey.D1); // a held thing is picked: the guided bake is the choice
        Assert.False(rig.View.Offer!.OneClickSelected);
        rig.Press(GameKey.Enter); // Enter makes the guided act (right or wrong), never the one-click bake
        Assert.Equal(0, Crafted(rig));
        Assert.Equal(0, rig.Session.Simulation.Inventory.Count(TestItems.Bread));
    }

    [Fact]
    public void TheOneClickChoiceIsSelectedByDefaultSoEnterBakesAtOnce()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        rig.Press(GameKey.Enter);
        Assert.Equal(2, rig.Session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(1, Crafted(rig));
    }

    [Fact]
    public void ClickingTheOneClickChoiceBakesAtOnce()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        var clicked = new StationPanel(rig.Screen, rig.Session, rig.Audio, (_, _) => new StationHit(StationRegion.BakeOneClick, 0), static () => false);
        clicked.Click(new TestInputFrame { LeftDown = true });
        Assert.Equal(2, rig.Session.Simulation.Inventory.Count(TestItems.Bread));
        Assert.Equal(1, Crafted(rig));
    }

    [Fact]
    public void TheOneClickBakeIsRefusedWhenAnIngredientIsGone()
    {
        var rig = new StationRig();
        rig.Session.RestoreProcedures(Mastered());
        Assert.True(rig.Panel.Open("horno"));
        _ = rig.Session.Simulation.Inventory.Remove(ItemId.Wood, 1);
        rig.Press(GameKey.B);
        Assert.Equal(StationNotice.NotEnough, rig.View.Notice);
        Assert.Equal(0, Crafted(rig));
    }
}
