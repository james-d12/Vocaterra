using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Simulation;

// Moving items by hand (I02, inventory-plan 3.9). The containers (the bag, the worn equipment, the trash), the held stack (the cursor) and the moves over them are simulation state: a SlotMove command changes them in the tick, so
// scripted runs replay the moves and the digest covers them (a held stack and a filled trash are in the digest; with both empty nothing is added). The save keeps neither: it is written with nothing held (see ReturnHeld), and the trash is forgotten.
public sealed partial class VoxelSimulation
{
    // Tags that tell the held stack's and the trash's digest entries apart from each other.
    private const int HeldDigestTag = 0x48454C44;
    private const int TrashDigestTag = 0x54525348;

    /// <summary>The held stack, the cursor: what a pick-up lifted and the panel draws at the pointer. Empty unless the inventory panel is being used; it changes through <see cref="VoxelCommandKind.SlotMove"/>.</summary>
    public HeldStack Held { get; } = new();

    /// <summary>The trash: one slot that keeps its last item until another replaces it. Not saved.</summary>
    public TrashContainer Trash { get; } = new();

    /// <summary>The containers a move can name (the bag, the equipment and the trash first). Add one (a chest, W24) with <see cref="SlotContainers.Add"/>: the moves need no edit.</summary>
    public SlotContainers Containers { get; private set; } = new();

    /// <summary>The moves over <see cref="Containers"/> and <see cref="Held"/>.</summary>
    public InventoryOps Moves { get; private set; } = null!;

    private void StartInteraction(IItemCatalogue items)
    {
        Containers = new SlotContainers();
        Containers.Add(new BagContainer(Inventory));
        Containers.Add(new EquipmentContainer(Equipment, Inventory, NoteWornChange));
        Containers.Add(Trash);
        Moves = new InventoryOps(Containers, Held, items);
    }

    /// <summary>
    /// Does a slot move now (the same as a <see cref="VoxelCommandKind.SlotMove"/> in a tick): raises <see cref="SimulationEventKind.MoveRefused"/> when the move is refused, and changes nothing then.
    /// </summary>
    /// <param name="action">The move.</param>
    /// <param name="container">The container's id (<see cref="ContainerIds"/>).</param>
    /// <param name="slot">The slot.</param>
    public MoveResult Move(MoveAction action, int container, int slot)
    {
        var result = Moves.Apply(action, container, slot);
        if (!result.IsDone)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.MoveRefused, Tick, X: container, Y: slot, Z: (int)action, Detail: (int)result.Reason, Amount: (int)result.Equip));
        }

        return result;
    }

    /// <summary>
    /// Puts the held stack back (what closing the panel does): into the slot it came from, or the bag. When the bag is full what is left stays held until there is room (the panel draws it when it opens again), never lost.
    /// A save is written only when this leaves nothing held. Not a command: use <see cref="VoxelCommandKind.SlotMove"/> with <see cref="MoveAction.ReturnHeld"/> inside a run.
    /// </summary>
    /// <returns><see langword="true"/> when nothing is held now.</returns>
    public bool ReturnHeld() => Moves.ReturnHeld().IsDone;

    private void NoteWornChange(EquipSlot slot, ItemStack old, ItemStack now)
    {
        var flat = Equipment.Data.FlatIndex(slot);
        if (!old.IsEmpty)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.ItemUnequipped, Tick, Detail: (int)old.Item, Amount: flat));
        }

        if (!now.IsEmpty)
        {
            _events.Add(new SimulationEvent(SimulationEventKind.ItemEquipped, Tick, Detail: (int)now.Item, Amount: flat));
        }
    }

    private void ApplySlotMove(in VoxelCommand command)
    {
        _ = Move((MoveAction)command.A, command.B, command.C);
        RefreshStats();
    }

    // The held stack and the trash go into the digest only when they hold something: a run that holds nothing has the digest it had before.
    private void AppendInteractionDigest(IncrementalHash hash)
    {
        Span<byte> entry = stackalloc byte[24];
        if (!Held.IsEmpty)
        {
            BinaryPrimitives.WriteInt32LittleEndian(entry, HeldDigestTag);
            BinaryPrimitives.WriteInt32LittleEndian(entry[4..], (int)Held.Stack.Item);
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], Held.Stack.Count);
            BinaryPrimitives.WriteInt32LittleEndian(entry[12..], Held.OriginContainer);
            BinaryPrimitives.WriteInt32LittleEndian(entry[16..], Held.OriginSlot);
            hash.AppendData(entry[..20]);
        }

        if (!Trash.Stack.IsEmpty)
        {
            BinaryPrimitives.WriteInt32LittleEndian(entry, TrashDigestTag);
            BinaryPrimitives.WriteInt32LittleEndian(entry[4..], (int)Trash.Stack.Item);
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], Trash.Stack.Count);
            hash.AppendData(entry[..12]);
        }
    }
}
