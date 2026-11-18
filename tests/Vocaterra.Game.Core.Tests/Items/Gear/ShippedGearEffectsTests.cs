using Vocaterra.Game.Core.Damage;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Light;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>
/// What the shipped second half of the gear does when worn (I08b): the armour's damage reduction through <see cref="ArmourDamageModifier"/>, each accessory's number on the stat sheet, the light of the lantern, the amulet and the torch through
/// <see cref="PlayerLight"/>, the two bags' slot counts and the bag rule, and the torch that still places from the bag.
/// </summary>
public sealed class ShippedGearEffectsTests
{
    private static readonly DamageCause Fall = new("fall");

    private static ItemId Id(string name) => TestItems.Catalogue.Find(name)!.Id;

    private static int Put(VoxelSimulation sim, string item, EquipSlot slot, int bagSlot = 12)
    {
        Assert.Equal(EquipResult.Done, sim.Equip(GearItems.Bag(sim, Id(item), bagSlot), slot));
        return bagSlot;
    }

    private static void WearArmour(VoxelSimulation sim)
    {
        Put(sim, "helmet", GearItems.Slot("armour", "head"), 12);
        Put(sim, "cuirass", GearItems.Slot("armour", "body"), 13);
        Put(sim, "greaves", GearItems.Slot("armour", "legs"), 14);
        Put(sim, "boots", GearItems.Slot("armour", "feet"), 15);
    }

    private static double Read(VoxelSimulation sim, string key) => StatRules.Read(sim.Stats, key);

    [Fact]
    public void Given_TheFullIronSet_When_ItIsWorn_Then_TheSheetHoldsTwelveAndAHitOfOneHundredIsReducedByFortyEightPercent()
    {
        var sim = GearItems.Sim();

        WearArmour(sim);

        Assert.Equal(12, Read(sim, StatKeys.Armour));
        Assert.Equal(52, new ArmourDamageModifier().Modify(100, Fall, sim.Stats));
    }

    [Fact]
    public void Given_TheFullSetAndTheShield_When_TheyAreWorn_Then_TheSheetHoldsFifteenAndTheReductionIsSixtyPercent()
    {
        var sim = GearItems.Sim();
        WearArmour(sim);

        Put(sim, "shield", GearItems.Slot("offhand", "offhand"), 16);

        Assert.Equal(15, Read(sim, StatKeys.Armour));
        Assert.Equal(40, new ArmourDamageModifier().Modify(100, Fall, sim.Stats));
    }

    [Theory]
    [InlineData("helmet", "head", 2, 92)]
    [InlineData("cuirass", "body", 5, 80)]
    [InlineData("greaves", "legs", 3, 88)]
    [InlineData("boots", "feet", 2, 92)]
    public void Given_OnePieceOfArmour_When_ItIsWorn_Then_ItAddsItsPointsAtFourPercentEach(string item, string slot, int points, int damage)
    {
        var sim = GearItems.Sim();

        Put(sim, item, GearItems.Slot("armour", slot));

        Assert.Equal(points, Read(sim, StatKeys.Armour));
        Assert.Equal(damage, new ArmourDamageModifier().Modify(100, Fall, sim.Stats));
    }

    [Fact]
    public void Given_ArmourWornAndTheShippedDamageRules_When_ThePlayerDrowns_Then_ArmourStillReducesNothing()
    {
        var sim = GearItems.Sim();
        WearArmour(sim);

        Assert.Empty(TestGame.Data.Damage.ModifiersFor(DamageCause.Drowning.Id)); // the owner: armour does not soften drowning, lava or cold
    }

    [Fact]
    public void Given_NothingWorn_When_TheSheetIsRead_Then_NoGearStatMoves()
    {
        var sim = GearItems.Sim();

        Assert.Equal((0.0, 1.0, 1.0, 1.0, 0.0, 0.0), (Read(sim, StatKeys.MaxStamina), Read(sim, StatKeys.WalkSpeed), Read(sim, StatKeys.HungerDrain), Read(sim, StatKeys.ThirstDrain), Read(sim, StatKeys.Armour), Read(sim, StatKeys.Light)));
    }

    [Theory]
    [InlineData("ring", StatKeys.MaxStamina, 20.0)]
    [InlineData("bracelet", StatKeys.WalkSpeed, 1.1)]
    [InlineData("belt", StatKeys.HungerDrain, 0.75)]
    [InlineData("necklace", StatKeys.ThirstDrain, 0.75)]
    [InlineData("amulet", StatKeys.Light, 5.0)]
    public void Given_AnAccessory_When_ItIsWorn_Then_ItsStatIsOnTheSheetAndGoneWhenItComesOff(string item, string key, double expected)
    {
        var sim = GearItems.Sim();
        var slot = GearItems.Nth("accessories", 0);

        Put(sim, item, slot);
        Assert.Equal(expected, Read(sim, key), 6);

        Assert.Equal(EquipResult.Done, sim.Unequip(slot));
        Assert.Equal(key is StatKeys.WalkSpeed or StatKeys.HungerDrain or StatKeys.ThirstDrain ? 1.0 : 0.0, Read(sim, key), 6);
    }

    [Fact]
    public void Given_ThreeAccessories_When_TheyAreWornTogether_Then_EachKeepsItsOwnStat()
    {
        var sim = GearItems.Sim();

        Put(sim, "belt", GearItems.Nth("accessories", 0), 12);
        Put(sim, "necklace", GearItems.Nth("accessories", 1), 13);
        Put(sim, "ring", GearItems.Nth("accessories", 2), 14);

        Assert.Equal((0.75, 0.75, 20.0), (Read(sim, StatKeys.HungerDrain), Read(sim, StatKeys.ThirstDrain), Read(sim, StatKeys.MaxStamina)));
    }

    [Fact]
    public void Given_TheBraceletWorn_When_TheTickPrepares_Then_TheWalkIsTenPercentFaster()
    {
        var session = GearItems.Undress(new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons));
        _ = session.Simulation.Equip(GearItems.Bag(session.Simulation, Id("bracelet"), 12), GearItems.Nth("accessories", 0));

        session.Step([]);

        Assert.Equal(1100, session.Simulation.WalkPermille);
    }

    [Theory]
    [InlineData("lantern", "offhand", 12, 9.2, 0.96)]
    [InlineData("torch", "offhand", 9, 7.4, 0.72)]
    [InlineData("amulet", "accessory", 5, 5.0, 0.4)]
    public void Given_AWornLightItem_When_TheLightIsMadeAtNight_Then_ReachAndStrengthComeFromTheShippedNumbers(string item, string kind, int stat, double radius, double strength)
    {
        var sim = GearItems.Sim();

        Put(sim, item, kind == "offhand" ? GearItems.Slot("offhand", "offhand") : GearItems.Nth("accessories", 0));
        var light = PlayerLight.Of(TestGame.Data.Light, Read(sim, StatKeys.Light), 0.3);

        Assert.Equal(stat, Read(sim, StatKeys.Light));
        Assert.True(light.IsOn);
        Assert.Equal(radius, light.Radius, 3);
        Assert.Equal(strength, light.Strength, 3);
    }

    [Fact]
    public void Given_TheLanternAndTheAmulet_When_TheyAreWornTogether_Then_TheLightsAddUpToSeventeenAndTheLightIsAtItsTop()
    {
        var sim = GearItems.Sim();
        Put(sim, "lantern", GearItems.Slot("offhand", "offhand"), 12);
        Put(sim, "amulet", GearItems.Nth("accessories", 0), 13);

        var light = PlayerLight.Of(TestGame.Data.Light, Read(sim, StatKeys.Light), 0.3);

        Assert.Equal(PlayerLight.Of(TestGame.Data.Light, 16, 0.3), light);
    }

    [Fact]
    public void Given_TheLightItemsAndDaylight_When_TheLightIsMade_Then_ItIsOff()
    {
        Assert.Equal(PlayerLight.Off, PlayerLight.Of(TestGame.Data.Light, 12, 1.0));
    }

    [Theory]
    [InlineData("sack", 40, 10)]
    [InlineData("backpack", 50, 20)]
    public void Given_ABagWorn_When_TheInventoryIsCounted_Then_TheBaseOfThirtyGrowsByItsCapacity(string bag, int slots, int capacity)
    {
        var sim = GearItems.Sim();
        var back = GearItems.Slot("backpack", "back");

        Put(sim, bag, back);

        Assert.Equal(30, TestGame.Data.Equipment.BagSlots);
        Assert.Equal(slots, sim.Inventory.SlotCount);
        Assert.Equal(capacity, sim.Equipment.Capacity);
    }

    [Theory]
    [InlineData("sack", 39)]
    [InlineData("backpack", 49)]
    public void Given_AWornBagWithAnItemInItsLastSlot_When_ItIsTakenOff_Then_ItIsRefusedAndTheSlotCountStays(string bag, int last)
    {
        var sim = GearItems.Sim();
        var back = GearItems.Slot("backpack", "back");
        Put(sim, bag, back);
        sim.Inventory.SetSlot(last, new ItemStack(TestItems.Apple, 1));
        var slots = sim.Inventory.SlotCount;

        var result = sim.Unequip(back);

        Assert.Equal(EquipResult.BagNotEmpty, result);
        Assert.Equal(slots, sim.Inventory.SlotCount);
        Assert.Equal(Id(bag), sim.Equipment.Get(back).Item);
        Assert.Equal(TestItems.Apple, sim.Inventory.Slots[last].Item);
    }

    [Fact]
    public void Given_ABagWornAndItsExtraSlotsEmpty_When_ItIsTakenOff_Then_ItComesOffAndTheBagReturnsToThirty()
    {
        var sim = GearItems.Sim();
        var back = GearItems.Slot("backpack", "back");
        Put(sim, "backpack", back);

        Assert.Equal(EquipResult.Done, sim.Unequip(back));

        Assert.Equal(30, sim.Inventory.SlotCount);
    }

    [Fact]
    public void Given_ASackWorn_When_TheBackpackIsSwappedInWithItemsEverywhere_Then_ItIsAllowedAndTheBagGrowsToFifty()
    {
        var sim = GearItems.Sim();
        var back = GearItems.Slot("backpack", "back");
        Put(sim, "sack", back, 12);
        for (var i = 13; i < sim.Inventory.SlotCount; i++)
        {
            sim.Inventory.SetSlot(i, new ItemStack(TestItems.Apple, 1));
        }

        sim.Inventory.SetSlot(5, new ItemStack(Id("backpack"), 1));
        var result = sim.Equip(5, back);

        Assert.Equal(EquipResult.Done, result);
        Assert.Equal(50, sim.Inventory.SlotCount);
        Assert.Equal(Id("sack"), sim.Inventory.Slots[5].Item);
    }

    [Fact]
    public void Given_TheBackpackWornAndItsSlotsInUse_When_TheSackIsSwappedIn_Then_ItIsRefused()
    {
        var sim = GearItems.Sim();
        var back = GearItems.Slot("backpack", "back");
        Put(sim, "backpack", back, 12);
        sim.Inventory.SetSlot(45, new ItemStack(TestItems.Apple, 1)); // a slot only the backpack gives
        sim.Inventory.SetSlot(5, new ItemStack(Id("sack"), 1));

        Assert.Equal(EquipResult.BagNotEmpty, sim.Equip(5, back));
        Assert.Equal(50, sim.Inventory.SlotCount);
    }

    [Fact]
    public void Given_ATorchWornInTheOffHand_When_AnotherTorchIsPlacedFromTheBag_Then_TheWornOneStaysAndOnlyTheBagLoses()
    {
        var sim = GearItems.Sim();
        var offHand = GearItems.Slot("offhand", "offhand");
        Put(sim, "torch", offHand, 12);
        _ = sim.GrantItems(ItemId.Torch, 2);
        var cell = new BlockPos(32, GroundTop + 1, 30);

        sim.Step([VoxelCommand.Place(ItemId.Torch, cell.X, cell.Y, cell.Z)]);

        Assert.Equal(BlockId.Torch, sim.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Torch));
        Assert.Equal(ItemId.Torch, sim.Equipment.Get(offHand).Item);
        Assert.Equal(9, Read(sim, StatKeys.Light));
    }

    [Fact]
    public void Given_AStackOfTorches_When_ItIsEquippedWhole_Then_ItIsRefusedAsOneAtATime()
    {
        var sim = GearItems.Sim();
        sim.Inventory.SetSlot(7, new ItemStack(ItemId.Torch, 5));

        var result = sim.Equip(7, GearItems.Slot("offhand", "offhand"));

        Assert.Equal(EquipResult.StackTooLarge, result);
        Assert.Equal(5, sim.Inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void Given_TheCatalogueDrownedInGear_When_TheContentIsValidated_Then_NoProblemIsReported()
    {
        Assert.Empty(Vocaterra.Game.Core.Content.GameDataValidator.Validate(TestGame.Data));
    }
}
