using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>A simulation with the test gear and its moves, and a snapshot of everything a move may change (the bag, the worn items, the trash and the held stack), for the tests of the moves by hand (I02).</summary>
internal sealed class MoveRig
{
    public static readonly ItemId Apple = TestItems.Apple;
    public static readonly ItemId Bread = TestItems.Bread;
    public static readonly ItemId Flour = TestItems.Flour;
    public static readonly EquipSlot Body = GearItems.Slot("clothing", "body");
    public static readonly EquipSlot Back = GearItems.Slot("backpack", "back");

    public MoveRig()
    {
        Sim = GearItems.Sim();
    }

    public VoxelSimulation Sim { get; }

    public InventoryOps Ops => Sim.Moves;

    public Inventory Bag => Sim.Inventory;

    public int Flat(EquipSlot slot) => Sim.Equipment.Data.FlatIndex(slot);

    public int Limit(ItemId item) => Bag.Items.Get(item).MaxStack;

    public MoveRig With(int slot, ItemId item, int count)
    {
        Bag.SetSlot(slot, new ItemStack(item, count));
        return this;
    }

    public string Snapshot() =>
        string.Join(',', Bag.Slots.Select(Describe)) + "|" + string.Join(',', Sim.Equipment.Slots.Select(Describe)) + "|" + Describe(Sim.Trash.Stack) + "|" + Describe(Sim.Held.Stack) + $"|{Sim.Held.OriginContainer}.{Sim.Held.OriginSlot}";

    private static string Describe(ItemStack s) => s.IsEmpty ? "-" : $"{(int)s.Item}x{s.Count}";
}
