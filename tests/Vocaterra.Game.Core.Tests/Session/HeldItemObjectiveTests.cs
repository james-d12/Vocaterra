using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// An objective step that asks for items counts the items in the bag, not the pickups made (issue #32): five wood picked, two crafted into a torch, leave the camp's wood
/// at three of five. Once the objective is done its steps stand, whatever the bag holds after.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class HeldItemObjectiveTests
{
    [Fact]
    public void TheCampsWoodIsTheWoodInTheBagSoCraftingATorchTakesTheStepBack()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 6));
        session.Step([]);
        Assert.Equal("madera 5/5", session.Objective.Step("wood")!.Text);
        Assert.True(session.Objective.Step("wood")!.Done);

        Assert.True(session.Simulation.TakeItems(ItemId.Wood, 2)); // a torch crafted from two wood: four are left
        session.Step([]);

        Assert.Equal("madera 4/5", session.Objective.Step("wood")!.Text);
        Assert.False(session.Objective.Step("wood")!.Done);
        Assert.False(session.Objective.Complete);

        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 1)); // one more cut: five again, and the step is done again
        session.Step([]);
        Assert.True(session.Objective.Step("wood")!.Done);
        Assert.Equal(["madera 5/5", "madera 5/5"], session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.ObjectiveStep).Select(static e => e.Detail));
    }

    [Fact]
    public void APickupEventCountsNoMoreThanTheBagHolds()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        _ = session.HostEvent(WorldEvents.ItemPickedUp, "fruta"); // an event with no fruit in the bag

        Assert.Equal(0, session.Objective.Step("berry")!.Count);
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Berry, 2));
        _ = session.HostEvent(WorldEvents.ItemPickedUp, "fruta");
        Assert.Equal(2, session.Objective.Step("berry")!.Count);
    }

    [Fact]
    public void ADoneChecklistStandsWhenTheBagEmptiesAfterIt()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Wood, 5));
        Assert.Equal(0, session.Simulation.GrantItems(ItemId.Berry, 3));
        _ = session.HostEvent(WorldEvents.TorchAtRing, "firering");
        _ = session.HostEvent(WorldEvents.BottleFilled);
        Assert.True(session.Objective.Complete);

        Assert.True(session.Simulation.TakeItems(ItemId.Wood, 5)); // the wood goes into torches and the fruit is eaten after the checklist is done
        Assert.True(session.Simulation.TakeItems(ItemId.Berry, 3));
        session.Step([]);

        Assert.True(session.Objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", session.Objective.Summary);
    }

    [Fact]
    public void OnlyAStepThatCountsAPickupOfANounCountsTheBag()
    {
        var camp = TestGame.Camp();
        Assert.Equal(["madera", "fruta", null, null], camp.Spec.Steps.Select(static s => s.HeldNoun));

        var completed = camp.Hold(static noun => noun == "madera" ? 7 : 0);
        Assert.Equal("madera 5/5", Assert.Single(completed).Text);
        Assert.Equal(7, camp.Step("wood")!.Count);
        Assert.Empty(camp.Hold(static _ => 0));
        Assert.Equal(0, camp.Step("wood")!.Count);
    }
}
