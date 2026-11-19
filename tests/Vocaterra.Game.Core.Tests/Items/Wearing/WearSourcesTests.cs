using Vocaterra.Game.Core.Damage;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Items.Wearing;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Items.Wearing;

/// <summary>What wears an item (I07): use for the tool in the hand, time for what is worn, damage for the armour; and that no source wears what another one owns.</summary>
public sealed class WearSourcesTests
{
    private static readonly EquipSlot Head = GearItems.Slot("clothing", "head");
    private static readonly EquipSlot Body = GearItems.Slot("armour", "body"); // the mail is armour: the armour group's body slot
    private static readonly EquipSlot Hand = GearItems.Slot("offhand", "offhand");
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");

    private sealed class Record : IPlayerHealth
    {
        public List<int> Hurts { get; } = [];

        public bool IsDead => false;

        public void Damage(int amount, DamageCause cause) => Hurts.Add(amount);
    }

    private static VoxelSimulation WithHatchetSelected()
    {
        var sim = WearRig.Sim();
        sim.Inventory.SetSlot(0, new ItemStack(GearItems.Hatchet, 1));
        sim.Step([VoxelCommand.SelectSlot(0)]);
        return sim;
    }

    [Fact]
    public void Given_ATrunkAndTheHatchetInHand_When_ABlockIsGathered_Then_TheHatchetWearsOnceEachTime()
    {
        var sim = WithHatchetSelected();

        WearRig.Chop(sim);
        Assert.Equal(1, sim.Inventory.Slots[0].Wear);
        WearRig.Chop(sim);

        Assert.Equal(2, sim.Inventory.Slots[0].Wear);
        Assert.Equal(2, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void Given_TheHatchetInTheBagButNotInHand_When_ABlockIsGathered_Then_ItDoesNotWear()
    {
        var sim = WearRig.Sim();
        sim.Inventory.SetSlot(Inventory.HotbarSize, new ItemStack(GearItems.Hatchet, 1));

        WearRig.Chop(sim);

        Assert.Equal(0, sim.Inventory.Slots[Inventory.HotbarSize].Wear);
    }

    [Fact]
    public void Given_TheHatchetInHandAndABerryGathered_When_TheToolDoesNotWorkOnIt_Then_ItDoesNotWear()
    {
        var sim = WithHatchetSelected();
        sim.Grid.SetBlock(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z, BlockId.BerryBush);

        for (var i = 0; i < BlockDefinition.BerryBushGatherTicks; i++)
        {
            sim.Step([VoxelCommand.Interact(WearRig.Ahead.X, WearRig.Ahead.Y, WearRig.Ahead.Z)]);
        }

        Assert.Equal(1, sim.Inventory.Count(ItemId.Berry));
        Assert.Equal(0, sim.Inventory.Slots[0].Wear);
    }

    [Fact]
    public void Given_WornClothesAndAnHourPerTick_When_TimePasses_Then_TheCapWearsByTheHourAndTheMailDoesNot()
    {
        var sim = WearRig.Sim(hourInOneTick: true);
        WearRig.Wear(sim, GearItems.Cap, Head);
        WearRig.Wear(sim, GearItems.Mail, Body);

        for (var i = 0; i < 3; i++)
        {
            sim.Step([]);
        }

        Assert.Equal(3, sim.Equipment.Get(Head).Wear);
        Assert.Equal(0, sim.Equipment.Get(Body).Wear);
    }

    [Fact]
    public void Given_TheDefaultDay_When_AnHourOfTicksPasses_Then_TheCapHasWornOnceAndNotBefore()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Cap, Head);
        var hour = sim.Clock.CycleTicks / 24;
        Assert.Equal(6000, hour);

        for (var i = 0; i < hour - 1; i++)
        {
            sim.Step([]);
        }

        Assert.Equal(0, sim.Equipment.Get(Head).Wear);
        sim.Step([]);
        Assert.Equal(1, sim.Equipment.Get(Head).Wear);
    }

    [Fact]
    public void Given_ACapInTheBag_When_TimePasses_Then_ItOnlyWearsWhileWorn()
    {
        var sim = WearRig.Sim(hourInOneTick: true);
        sim.Inventory.SetSlot(20, new ItemStack(GearItems.Cap, 1));

        sim.Step([]);
        sim.Step([]);

        Assert.Equal(0, sim.Inventory.Slots[20].Wear);
    }

    [Fact]
    public void Given_TheLampAndTheSatchetWorn_When_HoursPass_Then_BothWearByTime()
    {
        var sim = WearRig.Sim(hourInOneTick: true);
        WearRig.Wear(sim, GearItems.Lamp, Hand);
        WearRig.Wear(sim, GearItems.Satchel, Back);

        sim.Step([]);

        Assert.Equal(1, sim.Equipment.Get(Hand).Wear);
        Assert.Equal(1, sim.Equipment.Get(Back).Wear);
    }

    [Fact]
    public void Given_ArmourWorn_When_TheDamageSourceIsApplied_Then_TheMailAndTheBucklerWearByThePointsAndTheCapDoesNot()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Mail, Body);
        WearRig.Wear(sim, GearItems.Buckler, Hand);
        WearRig.Wear(sim, GearItems.Cap, Head);

        Assert.True(sim.Wear.Apply(DamageWearSource.SourceId, 4));

        Assert.Equal(4, sim.Equipment.Get(Body).Wear);
        Assert.Equal(4, sim.Equipment.Get(Hand).Wear);
        Assert.Equal(0, sim.Equipment.Get(Head).Wear);
    }

    [Fact]
    public void Given_ACauseThatListsTheWearModifier_When_ItHurts_Then_TheArmourWearsByTheHitAndTheHealthTakesTheArmoursShare()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Mail, Body);
        sim.RefreshStats();
        var inner = new Record();
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": [] }, { "id": "test-hit", "modifiers": ["wear", "armour"] } ] }""");
        var health = new ModifiedHealth(inner, data, DamageModifiers.Standard(() => sim.Wear), () => sim.Stats);

        health.Damage(10, new DamageCause("test-hit"));

        Assert.Equal(10, sim.Equipment.Get(Body).Wear); // the whole hit, as the mail's durability is 10: it is worn out by it
        Assert.Equal([8], inner.Hurts); // armour 5 takes 20% off 10
    }

    [Fact]
    public void Given_DrowningWhichListsNoModifier_When_ItHurts_Then_NothingWears()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Mail, Body);
        var health = new ModifiedHealth(new Record(), TestGame.Data.Damage, DamageModifiers.Standard(() => sim.Wear), () => sim.Stats);

        health.Damage(10, DamageCause.Drowning);

        Assert.Equal(0, sim.Equipment.Get(Body).Wear);
    }

    [Fact]
    public void Given_NoWearEngine_When_TheWearModifierRuns_Then_TheDamageIsUnchanged() =>
        Assert.Equal(9, new WearDamageModifier(null).Modify(9, new DamageCause("test-hit"), StatSheet.Empty));

    [Fact]
    public void Given_AnUnknownSourceOrNoQuantity_When_TheEngineIsAsked_Then_NothingWears()
    {
        var sim = WearRig.Sim();
        WearRig.Wear(sim, GearItems.Cap, Head);

        Assert.False(sim.Wear.Apply("magic", 5));
        Assert.False(sim.Wear.Apply(TimeWearSource.SourceId, 0));
        Assert.Equal(0, sim.Equipment.Get(Head).Wear);
    }

    [Fact]
    public void Given_ANewSource_When_ItIsRegistered_Then_ItWearsItsItemsAndNoOtherSourceChanges()
    {
        var sources = WearSources.Standard();
        sources.Add(new EveryHourOnTheBodySource());
        var sim = WearRig.Sim();
        var wear = new ItemWear(sim.Inventory, sim.Equipment, sources, () => 0, _ => { });
        WearRig.Wear(sim, GearItems.Cap, Head);

        Assert.False(wear.Apply("custom", 1)); // no item names this source yet
        Assert.Equal(["use", "time", "damage", "custom"], sources.Ids);
        Assert.Throws<ArgumentException>(() => sources.Add(new EveryHourOnTheBodySource()));
    }

    private sealed class EveryHourOnTheBodySource : IWearSource
    {
        public string Id => "custom";

        public IEnumerable<WearSlot> Targets(ItemWear wear) => wear.WornSlots();
    }
}
