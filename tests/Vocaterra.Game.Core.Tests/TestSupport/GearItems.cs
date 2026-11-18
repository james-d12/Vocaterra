using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Voxel;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// Test gear for the equipment tests (I03a): the shipped catalogue and some wearable rows of the tests' own (no shipped item can be worn yet), among them a small and a large bag (I03b), the shipped slot groups, and a simulation
/// built with both. The tests name groups and slots by the data's ids, as the data does; no code under test does.
/// </summary>
internal static class GearItems
{
    public static readonly ItemId Shirt = (ItemId)200;
    public static readonly ItemId Hat = (ItemId)201;
    public static readonly ItemId Boots = (ItemId)202;
    public static readonly ItemId Plate = (ItemId)203;
    public static readonly ItemId Helm = (ItemId)204;
    public static readonly ItemId Ring = (ItemId)205;
    public static readonly ItemId Charm = (ItemId)206;
    public static readonly ItemId Shield = (ItemId)207;
    public static readonly ItemId Pack = (ItemId)208;
    public static readonly ItemId Scarf = (ItemId)209;
    public static readonly ItemId Sack = (ItemId)210;
    public static readonly ItemId Rucksack = (ItemId)211;
    public static readonly ItemId Anklet = (ItemId)212;
    public static readonly ItemId Flask = (ItemId)213;
    public static readonly ItemId Gourd = (ItemId)214;

    /// <summary>The slots the small test bag (<see cref="Sack"/>) adds.</summary>
    public const int SackCapacity = 10;

    /// <summary>The slots the large test bag (<see cref="Rucksack"/>) adds.</summary>
    public const int RucksackCapacity = 20;

    private static readonly Lazy<IItemCatalogue> Built = new(() => TestItems.With(
        Gear(Shirt, "test-shirt", ["clothing"], "body", ("warmth", 30)),
        Gear(Hat, "test-hat", ["clothing"], "head", ("warmth", 20)),
        Gear(Boots, "test-boots", ["clothing"], "feet", ("warmth", 10)),
        Gear(Plate, "test-plate", ["armour"], "body", ("armour", 5), ("warmth", 5)),
        Gear(Helm, "test-helm", ["armour"], "head", ("armour", 2)),
        Gear(Ring, "test-ring", ["accessory"], "accessory", ("walkSpeed", 1.1)),
        Gear(Charm, "test-charm", ["accessory"], "accessory", ("maxStamina", 10)),
        Gear(Shield, "test-shield", ["offhand"], "offhand", ("armour", 3)),
        Gear(Pack, "test-pack", ["bag"], "back"),
        Gear(Scarf, "test-scarf", ["clothing", "armour"], "body", ("warmth", 7)),
        Gear(Anklet, "test-anklet", ["accessory"], "accessory", ("walkSpeed", 1.2)),
        Gear(Flask, "test-flask", ["accessory"], "accessory", ("hungerDrain", 0.5)),
        Gear(Gourd, "test-gourd", ["accessory"], "accessory", ("thirstDrain", 0.5)),
        Bag(Sack, "test-sack", SackCapacity),
        Bag(Rucksack, "test-rucksack", RucksackCapacity)));

    /// <summary>The shipped items and the test gear.</summary>
    public static IItemCatalogue Catalogue => Built.Value;

    /// <summary>The shipped slot groups.</summary>
    public static EquipmentData Data => TestGame.Data.Equipment;

    /// <summary>A flat-world simulation with the test gear and the shipped slot groups.</summary>
    public static VoxelSimulation Sim() => VoxelSimulation.Create(VoxelTestSupport.FlatScene(), Catalogue, equipment: Data);

    /// <summary>A session on the route with the test gear in its catalogue (the shipped slot groups), for the tests of death and saves with gear worn.</summary>
    public static VoxelSession Session() => Undress(new VoxelSession(TestGame.Data.WithItems(Catalogue), TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons));

    /// <summary>
    /// Takes off what a new game starts in (the shirt, trousers and shoes of <c>game/session.json</c>, I08a) and leaves the bag as it was, so a test of what is worn starts from a bare player and an unchanged bag.
    /// </summary>
    /// <param name="session">The session.</param>
    public static VoxelSession Undress(VoxelSession session)
    {
        var sim = session.Simulation;
        var bag = sim.Inventory.Slots.ToArray();
        foreach (var (slot, _) in sim.Equipment.Worn().ToList())
        {
            _ = sim.Unequip(slot);
        }

        for (var i = 0; i < bag.Length; i++)
        {
            sim.Inventory.SetSlot(i, bag[i]);
        }

        return session;
    }

    /// <summary>A slot by its group's id and its own name (<c>Slot("clothing", "head")</c>).</summary>
    public static EquipSlot Slot(string group, string name) => Slot(Data, group, name);

    /// <summary>A slot of <paramref name="data"/> by its group's id and its own name.</summary>
    public static EquipSlot Slot(EquipmentData data, string group, string name)
    {
        var g = data.IndexOf(group);
        return new EquipSlot(g, data.Groups[g].Slots.ToList().FindIndex(s => s.Name == name));
    }

    /// <summary>The nth counted slot of a group (<c>Nth("accessories", 1)</c> is the second accessory slot).</summary>
    public static EquipSlot Nth(string group, int index) => new(Data.IndexOf(group), index);

    /// <summary>Puts one of the item in a bag slot and returns the slot.</summary>
    public static int Bag(VoxelSimulation sim, ItemId item, int slot = 12)
    {
        sim.Inventory.SetSlot(slot, new ItemStack(item, 1));
        return slot;
    }

    private static ItemDefinition Bag(ItemId id, string name, int capacity) =>
        new(id, name, 1, name, ItemHeld.Sprite, ["bag"], Wear: "back", Capacity: capacity);

    private static ItemDefinition Gear(ItemId id, string name, string[] tags, string wear, params (string Key, double Value)[] stats) =>
        new(id, name, 1, name, ItemHeld.Sprite, tags, Wear: wear, Stats: stats.Length == 0 ? null : stats.ToDictionary(static s => s.Key, static s => s.Value));
}
