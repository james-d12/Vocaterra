using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.Core.Simulation;

// The worn equipment and the player's stats (I03a, inventory-plan 3.4 and 3.5). The equipment is simulation state: the equip and unequip commands change it in the tick, a save's state carries it and
// the digest covers it (nothing worn adds nothing to the digest). The stat sheet is built from the registered contributors, the equipment the first, at the end of every tick and whenever
// the equipment changes, so any reader sees the same sheet every machine would.
public sealed partial class VoxelSimulation
{
    private StatPipeline _stats = new();
    private StatSheet _sheet = StatSheet.Empty;

    /// <summary>What the player wears (<c>game/equipment.json</c>): one item in each slot of each group. It changes through <see cref="Equip"/> and <see cref="Unequip"/> or their commands.</summary>
    public Equipment Equipment { get; private set; } = null!;

    /// <summary>The player's stat contributors (the equipment first). Register another with <see cref="StatPipeline.Add"/>: a meal's buff or a place's effect is a new class and its registration, no edit of the sheet or of what reads it.</summary>
    public StatPipeline StatPipeline => _stats;

    /// <summary>The player's stats now (<see cref="StatKeys"/>): built at the end of each tick and after every change of the equipment. <see cref="StatSheet.Empty"/> before the first.</summary>
    public StatSheet Stats => _sheet;

    private void StartEquipment(IItemCatalogue items, EquipmentData data)
    {
        Equipment = new Equipment(data, items);
        _stats = new StatPipeline();
        _stats.Add(new EquipmentStats(Equipment));
        RefreshStats();
    }

    /// <summary>Builds the stat sheet from the contributors now (the tick does it; a host that registers a contributor may call it to see the new sheet at once).</summary>
    public void RefreshStats() => _sheet = _stats.Build();

    /// <summary>Wears the item in a bag slot in <paramref name="slot"/> (the same as a <see cref="VoxelCommandKind.Equip"/> in a tick): raises <see cref="SimulationEventKind.ItemEquipped"/> or <see cref="SimulationEventKind.EquipRefused"/>.</summary>
    /// <param name="bagSlot">The bag slot the item is in.</param>
    /// <param name="slot">The equipment slot.</param>
    public EquipResult Equip(int bagSlot, EquipSlot slot)
    {
        var item = (uint)bagSlot < (uint)Inventory.SlotCount ? Inventory.Slots[bagSlot] : ItemStack.Empty;
        var result = Equipment.Equip(Inventory, bagSlot, slot);
        Note(result, slot, item.Item, SimulationEventKind.ItemEquipped);
        return result;
    }

    /// <summary>Takes off what is worn in <paramref name="slot"/> (the same as a <see cref="VoxelCommandKind.Unequip"/> in a tick): raises <see cref="SimulationEventKind.ItemUnequipped"/> or <see cref="SimulationEventKind.EquipRefused"/>.</summary>
    /// <param name="slot">The equipment slot.</param>
    /// <param name="bagSlot">The bag slot to put it in, or -1 for the first free one.</param>
    public EquipResult Unequip(EquipSlot slot, int bagSlot = -1)
    {
        var item = Equipment.Data.Contains(slot) ? Equipment.Get(slot) : ItemStack.Empty;
        var result = Equipment.Unequip(Inventory, slot, bagSlot);
        Note(result, slot, item.Item, SimulationEventKind.ItemUnequipped);
        return result;
    }

    /// <summary>
    /// A start aid for scripted runs and pictures (<c>--equip</c>): gives the player one of <paramref name="item"/> when the bag has none and wears it in the first slot that takes it (an empty slot before a worn one, groups in the
    /// data's order). Like <see cref="GrantItems"/> it is a host action outside the command stream, and a refusal changes nothing (nothing is given).
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="slot">The slot it was worn in, or the slot of its kind it was refused at (<see langword="null"/> when no slot has its wear kind).</param>
    public EquipResult WearFromStart(ItemId item, out EquipSlot? slot)
    {
        var definition = Inventory.Items.Get(item);
        slot = Equipment.FindSlotFor(Inventory, definition);
        if (slot is null)
        {
            slot = Equipment.Data.AllSlots().Where(s => definition.Wear is not null && Equipment.Data.Spec(s).Wear == definition.Wear).Select(static s => (EquipSlot?)s).FirstOrDefault();
            return slot is { } kind ? Equipment.CanWear(Inventory, definition, kind) : definition.Wear is null ? EquipResult.NotWearable : EquipResult.WrongSlot;
        }

        if (Inventory.Count(item) < 1 && Inventory.Add(item, 1) != 0)
        {
            return EquipResult.BagFull;
        }

        var bagSlot = Enumerable.Range(0, Inventory.SlotCount).First(i => Inventory.Slots[i] is { IsEmpty: false } stack && stack.Item == item);
        return Equip(bagSlot, slot.Value);
    }

    private void Note(EquipResult result, EquipSlot slot, ItemId item, SimulationEventKind done)
    {
        var flat = Equipment.Data.Contains(slot) ? Equipment.Data.FlatIndex(slot) : -1;
        if (result == EquipResult.Done)
        {
            _events.Add(new SimulationEvent(done, Tick, Detail: (int)item, Amount: flat));
            RefreshStats();
        }
        else
        {
            _events.Add(new SimulationEvent(SimulationEventKind.EquipRefused, Tick, Detail: (int)result, Amount: flat));
        }
    }

    // The worn items go into the digest slot by slot (the flat index and the item), and only the worn ones: a run that wears nothing has the digest it had before the equipment existed.
    private void AppendEquipmentDigest(IncrementalHash hash)
    {
        Span<byte> entry = stackalloc byte[8];
        var slots = Equipment.Slots;
        for (var flat = 0; flat < slots.Count; flat++)
        {
            var stack = slots[flat];
            if (stack.IsEmpty)
            {
                continue;
            }

            BinaryPrimitives.WriteInt32LittleEndian(entry, flat);
            BinaryPrimitives.WriteInt32LittleEndian(entry[4..], (int)stack.Item);
            hash.AppendData(entry);
        }
    }
}
