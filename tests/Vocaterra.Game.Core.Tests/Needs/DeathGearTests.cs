using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>Death with gear on (I03b, inventory-plan 3.14): worn items and the worn bag stay worn, stacks of one stay in the bag, and the stackables go to the belongings chest; <c>death.keepInBag</c> is read from the data.</summary>
public sealed class DeathGearTests
{
    private const int Tps = 60;
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");
    private static readonly EquipSlot Body = GearItems.Slot("clothing", "body");

    // A session on the test gear whose player is about to die of thirst, with an empty bag.
    private static VoxelSession Dying()
    {
        var session = GearItems.Session();
        for (var slot = 0; slot < session.Simulation.Inventory.SlotCount; slot++)
        {
            session.Simulation.Inventory.SetSlot(slot, ItemStack.Empty);
        }

        session.SetNeed(NeedMeter.Hunger, 100);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        return session;
    }

    private static void Tick(VoxelSession session)
    {
        for (var i = 0; i < Tps; i++)
        {
            session.Step([]);
        }
    }

    [Fact]
    public void Given_AWornShirtAndAWornBag_When_ThePlayerDies_Then_BothStayWornAndTheBagKeepsItsSize()
    {
        var session = Dying();
        var sim = session.Simulation;
        Assert.Equal(EquipResult.Done, sim.Equip(GearItems.Bag(sim, GearItems.Shirt, 2), Body));
        Assert.Equal(EquipResult.Done, sim.Equip(GearItems.Bag(sim, GearItems.Rucksack, 3), Back));
        _ = sim.Inventory.Add(ItemId.Wood, 12);

        Tick(session);

        Assert.Equal(1, session.DeathCount);
        Assert.Equal(GearItems.Shirt, sim.Equipment.Get(Body).Item);
        Assert.Equal(GearItems.Rucksack, sim.Equipment.Get(Back).Item);
        Assert.Equal(50, sim.Inventory.SlotCount);
    }

    [Fact]
    public void Given_StacksOfOneInTheWornBagsSlots_When_ThePlayerDies_Then_TheyStayInTheirSlotsAndTheStackablesGoToTheChest()
    {
        var session = Dying();
        var sim = session.Simulation;
        _ = sim.Equip(GearItems.Bag(sim, GearItems.Rucksack, 3), Back);
        sim.Inventory.SetSlot(45, new ItemStack(TestItems.Axe, 1)); // a stack of one in a slot only the worn bag gives
        sim.Inventory.SetSlot(46, new ItemStack(ItemId.Berry, 9));
        sim.Inventory.SetSlot(5, new ItemStack(ItemId.Book, 1));
        sim.Inventory.SetSlot(6, new ItemStack(ItemId.Wood, 40));

        Tick(session);

        Assert.Equal(new ItemStack(TestItems.Axe, 1), sim.Inventory.Slots[45]);
        Assert.Equal(new ItemStack(ItemId.Book, 1), sim.Inventory.Slots[5]);
        Assert.True(sim.Inventory.Slots[46].IsEmpty);
        Assert.True(sim.Inventory.Slots[6].IsEmpty);
        Assert.Equal([(ItemId.Wood, 40), (ItemId.Berry, 9)], session.Chest!.Items);
    }

    [Fact]
    public void Given_ABagFullOfStacksOfOne_When_ThePlayerDies_Then_EveryStackStaysAndNoChestIsLeft()
    {
        var session = Dying();
        var sim = session.Simulation;
        for (var slot = 0; slot < sim.Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(TestItems.Axe, 1)); // they are never moved, so they always fit the count
        }

        Tick(session);

        Assert.Equal(sim.Inventory.SlotCount, sim.Inventory.Count(TestItems.Axe));
        Assert.Null(session.Chest);
    }

    [Fact]
    public void Given_TheDeathRuleOfTheData_When_ItsKeepRuleIsAsked_Then_ItKeepsWhatStacksToOne()
    {
        var rule = TestGame.Data.Needs.Death;

        Assert.Equal("stacksOfOne", rule.KeepInBag);
        Assert.True(rule.Keeps(GearItems.Catalogue.Get(GearItems.Rucksack)));
        Assert.True(rule.Keeps(GearItems.Catalogue.Get(TestItems.Axe)));
        Assert.False(rule.Keeps(GearItems.Catalogue.Get(ItemId.Wood)));
    }

    [Fact]
    public void Given_ADeathRuleTheCodeDoesNotKnow_When_ItIsAsked_Then_ItThrowsAndTheLoaderRefusesIt()
    {
        var unknown = TestGame.Data.Needs.Death with { KeepInBag = "everything" };

        _ = Assert.Throws<InvalidOperationException>(() => unknown.Keeps(GearItems.Catalogue.Get(ItemId.Wood)));
        var text = File.ReadAllText(Repo.Path("content", "game-v1", "game", "needs.json")).Replace("\"keepInBag\": \"stacksOfOne\"", "\"keepInBag\": \"everything\"", StringComparison.Ordinal);
        var error = Assert.Throws<InvalidDataException>(() => NeedsData.Parse(text));
        Assert.Contains("keepInBag", error.Message, StringComparison.Ordinal);
        Assert.Contains("everything", error.Message, StringComparison.Ordinal);
    }
}
