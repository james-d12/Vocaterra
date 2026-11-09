using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// The procedure runner in the session and the save (W06): its events are world events, a run survives a save and a load with the needs kept, a version-6 save with no
/// procedure entry and every older version load with none, and the mastered recipe's one-click bake goes through the crafting rule.
/// </summary>
public sealed class ProcedureSessionTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession NewSession()
    {
        var session = new VoxelSession(ProcedureRig.Data(), TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.NoLessons);
        _ = session.Simulation.Inventory.Add(ItemId.Wood, 1);
        _ = session.Simulation.Inventory.Add(ItemId.WaterBottle, 1);
        return session;
    }

    private static VoxelSession Reload(SaveDocument document) =>
        SaveGame.Load(SaveGame.Serialize(document), ProcedureRig.Data(), Pristine, credit: CreditRules.FullTyped, bumpSession: false);

    private static ActResult Act(GuideSession session, string step)
    {
        var (use, on, verb) = ProcedureRig.ActOf(step);
        return session.ActInProcedure(use, on, verb);
    }

    private static IEnumerable<WorldEvent> Raised(GuideSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name);

    private static ProceduresSave Mastered() =>
        new(1, ["bread"], [new ProcedureProgressSave("bread", 8, 8, [1, 2, 3], 24, true)], [], null);

    [Fact]
    public void TheRunnersEventsAreWorldEventsInTheLog()
    {
        var session = NewSession();
        Assert.Equal(StartOutcome.Started, session.StartProcedure("bread", "oven", true));
        foreach (var step in session.Procedures.Order.ToList())
        {
            _ = Act(session, step);
        }

        Assert.Equal("bread", Assert.Single(Raised(session, WorldEvents.ProcedureStarted)).Detail);
        Assert.Equal(3, Raised(session, WorldEvents.ProcedureStep).Count());
        Assert.Equal("baker", Assert.Single(Raised(session, WorldEvents.ProcedureDone)).Npc);
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void ARunInTheMiddleOfABakeSurvivesASaveAndALoadAndTheNeedsAreKept()
    {
        var session = NewSession();
        session.Needs.Set(NeedMeter.Hunger, 41);
        _ = session.StartProcedure("bread", "oven", true);
        var order = session.Procedures.Order.ToList();
        _ = Act(session, order[0]);
        var document = SaveGame.Capture(session, Pristine());
        Assert.NotNull(document.Systems!.Procedures);
        Assert.Contains("\"procedures\"", SaveGame.Serialize(document), StringComparison.Ordinal);

        var loaded = Reload(document);
        Assert.Equal(session.Procedures.View(), loaded.Procedures.View());
        Assert.Equal(order, loaded.Procedures.Order);
        Assert.Equal(41, loaded.Needs.Value(NeedMeter.Hunger), 3);
        Assert.Equal(1, loaded.Procedures.View().StepsDone);

        _ = Act(loaded, order[1]);
        var done = Act(loaded, order[2]);
        Assert.Equal((ProcedureState.Done, 3), (done.State, done.Made));
        Assert.Equal(3, loaded.Simulation.Inventory.Count(ItemId.Bread));

        // Saved after the completion and loaded: the Done run grants nothing more.
        var again = Reload(SaveGame.Capture(loaded, Pristine()));
        Assert.Equal(ProcedureState.Done, again.Procedures.State);
        Assert.Equal(ActOutcome.NotRunning, Act(again, order[0]).Outcome);
        Assert.Equal(3, again.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void ASavedGameWithNoProcedureEntryLoadsWithNoneAndKeepsItsNeeds()
    {
        var session = NewSession();
        session.Needs.Set(NeedMeter.Thirst, 55);
        var document = SaveGame.Capture(session, Pristine());
        Assert.Null(document.Systems!.Procedures); // nothing to save: the entry is absent, as in a save made before W06
        Assert.DoesNotContain("procedures", SaveGame.Serialize(document), StringComparison.Ordinal);

        var loaded = Reload(document);
        Assert.Equal(ProcedureState.NotStarted, loaded.Procedures.State);
        Assert.True(loaded.Procedures.Progress.IsEmpty);
        Assert.Equal(55, loaded.Needs.Value(NeedMeter.Thirst), 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ASaveWithoutTheLaterSectionsLoadsWithNoProcedures(int version)
    {
        var session = NewSession();
        _ = session.StartProcedure("bread", "oven", true);
        var current = SaveGame.Capture(session, Pristine());
        var older = version switch
        {
            1 => current with { Lessons = null, Tips = null, Learner = null, BookGranted = false, Settings = null, World = null, Systems = null },
            2 => current with { Settings = null, World = null, BookGranted = false, Systems = null },
            3 => current with { Settings = null, World = null, Systems = null },
            4 => current with { Systems = null },
            5 => current with { Systems = null }, // no systems at all: full needs, empty procedure state
            _ => current with { Systems = current.Systems! with { Procedures = null } }, // a save made before W06
        };

        var loaded = SaveGame.Load(SaveGame.Serialize(older), ProcedureRig.Data(), Pristine, credit: CreditRules.FullTyped, bumpSession: false, migrate: version < 6);

        Assert.Equal(ProcedureState.NotStarted, loaded.Procedures.State);
        Assert.True(loaded.Procedures.Progress.IsEmpty);
        Assert.Equal(PlayerNeeds.Full, loaded.Needs.Value(NeedMeter.Hunger));
        Assert.Equal(SaveGame.CurrentVersion, SaveGame.Capture(loaded, Pristine()).Version);
    }

    [Fact]
    public void TheMasteredRecipesOneClickBakeGoesThroughTheCraftingRule()
    {
        var session = NewSession();
        Assert.Equal(CraftResult.NoSuchRecipe, session.BakeMastered("bread")); // not mastered yet
        session.RestoreProcedures(Mastered());

        Assert.Equal(CraftResult.Done, session.BakeMastered("bread"));
        var inventory = session.Simulation.Inventory;
        Assert.Equal((2, 1, 0, 0), (inventory.Count(ItemId.Bread), inventory.Count(ItemId.Bottle), inventory.Count(ItemId.WaterBottle), inventory.Count(ItemId.Wood))); // one run's yield, no bonus
        Assert.Equal("pan", Assert.Single(Raised(session, WorldEvents.ItemCrafted)).Detail);
        Assert.Equal(CraftResult.MissingIngredients, session.BakeMastered("bread"));
        Assert.Equal(2, inventory.Count(ItemId.Bread)); // nothing changed

        // The guided bake stays on offer: the run starts as ever.
        Assert.Equal(StartOutcome.Started, session.StartProcedure("bread", "oven", true));
    }

    [Fact]
    public void TheOneClickBakeIsAllOrNothingWithAFullBag()
    {
        var session = NewSession();
        session.RestoreProcedures(Mastered());
        var inventory = session.Simulation.Inventory;
        _ = inventory.Add(ItemId.Wood, 1); // two: using one leaves the slot taken
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            if (inventory.Slots[slot].IsEmpty)
            {
                inventory.SetSlot(slot, new ItemStack(ItemId.Torch, 1));
            }
        }

        var before = inventory.Slots.ToList();
        Assert.Equal(CraftResult.InventoryFull, session.BakeMastered("bread"));
        Assert.Equal(before, inventory.Slots.ToList());
        Assert.Empty(Raised(session, WorldEvents.ItemCrafted));
    }
}
