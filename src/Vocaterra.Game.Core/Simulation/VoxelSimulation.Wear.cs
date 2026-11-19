using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Wearing;

namespace Vocaterra.Game.Core.Simulation;

// Durability and wear (I07, inventory-plan 3.12). The wear lives in the ItemStacks of the bag and the worn slots, so every move, the state and the save carry it; this part holds the engine, the hour that wears what is worn,
// the events of an item wearing out and the digest of the wear. Nothing here reads the clock: the hour is counted from the tick and the day cycle's length.
public sealed partial class VoxelSimulation
{
    /// <summary>The wear engine (I07): what a gather, an hour worn and a hit of damage do to the stacks, and what a repair does. The damage pipeline's <see cref="Damage.WearDamageModifier"/> and the session use it.</summary>
    public ItemWear Wear { get; private set; } = null!;

    private void StartWear()
    {
        Wear = new ItemWear(Inventory, Equipment, WearSources.Standard(), () => _selectedSlot, NoteWornOut);
    }

    /// <summary>
    /// A start aid for scripted runs and pictures (<c>--wear</c>): sets the wear of the first stack of <paramref name="item"/> in the bag or worn (the item is given first when neither holds one), clamped to the item's durability. Like
    /// <see cref="GrantItems"/> it is a host action outside the command stream; it raises no event, even when it wears the item out.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="amount">The wear to set (0 or more).</param>
    /// <returns>The wear set, or -1 when the item has no durability or the bag had no room to give one.</returns>
    public int SetWear(ItemId item, int amount)
    {
        if (Inventory.Items.Get(item) is not { Durability: { } durability })
        {
            return -1;
        }

        var wear = Math.Clamp(amount, 0, durability.Max);
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            if (Inventory.Slots[i] is { IsEmpty: false } stack && stack.Item == item)
            {
                Inventory.SetSlot(i, stack.WithWear(wear));
                RefreshStats();
                return wear;
            }
        }

        foreach (var (slot, definition) in Equipment.Worn())
        {
            if (definition.Id == item)
            {
                Equipment.SetWear(slot, wear);
                RefreshStats();
                return wear;
            }
        }

        if (Inventory.Add(item, 1) != 0)
        {
            return -1;
        }

        return SetWear(item, amount);
    }

    // The ticks of one in-game hour: a twenty-fourth of the day cycle (the data's cycle seconds at 60 ticks a second).
    private long TicksPerHour => Math.Max(1, _clock.CycleTicks / 24);

    // Once an in-game hour passes every worn item whose durability says "time" wears once.
    private void StepWear()
    {
        if (Tick % TicksPerHour == 0)
        {
            Wear.Apply(TimeWearSource.SourceId, 1);
        }
    }

    private void NoteWornOut(WornOutNotice notice)
    {
        _events.Add(new SimulationEvent(notice.IsTool ? SimulationEventKind.ToolWorn : SimulationEventKind.GearWornOut, Tick, Detail: (int)notice.Item));
        RefreshStats(); // a worn-out piece stops giving its stats at once
    }

    // The wear of every bag slot and worn slot that has any goes into the digest (the slot's index and the wear), and nothing when nothing is worn: a run that never wore anything has the digest it had before.
    private void AppendWearDigest(IncrementalHash hash)
    {
        Span<byte> entry = stackalloc byte[9];
        for (var i = 0; i < Inventory.SlotCount; i++)
        {
            Append(hash, entry, 0, i, Inventory.Slots[i].Wear);
        }

        var worn = Equipment.Slots;
        for (var flat = 0; flat < worn.Count; flat++)
        {
            Append(hash, entry, 1, flat, worn[flat].Wear);
        }

        static void Append(IncrementalHash hash, Span<byte> entry, byte container, int index, int wear)
        {
            if (wear <= 0)
            {
                return;
            }

            entry[0] = container;
            BinaryPrimitives.WriteInt32LittleEndian(entry[1..], index);
            BinaryPrimitives.WriteInt32LittleEndian(entry[5..], wear);
            hash.AppendData(entry);
        }
    }
}
