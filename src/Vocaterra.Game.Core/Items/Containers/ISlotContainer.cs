namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// Something that holds stacks in numbered slots (I02, inventory-plan 3.9): the one address scheme (a container id and a slot index) the moves of <see cref="InventoryOps"/> work over. The bag, the worn equipment and the trash
/// are the first; a chest (W24) is one more implementation that is registered in <see cref="SlotContainers"/>, and no move changes. A container reads a slot, says whether a slot would take a stack, and puts a stack there; a put
/// replaces what was in the slot (the move decides where the old stack goes).
/// </summary>
public interface ISlotContainer
{
    /// <summary>The container's id in a command (<see cref="ContainerIds"/>).</summary>
    int Id { get; }

    /// <summary>The number of slots now (the bag's active ones).</summary>
    int SlotCount { get; }

    /// <summary>Whether a put in this container replaces what is there and the old stack is gone, with no swap back to the cursor (the trash).</summary>
    bool Discards { get; }

    /// <summary>The stack in a slot (empty when there is none).</summary>
    /// <param name="slot">The slot, from 0 to <see cref="SlotCount"/>.</param>
    ItemStack Get(int slot);

    /// <summary>Whether <paramref name="stack"/> may be put in <paramref name="slot"/>, replacing what is there (an empty stack takes the slot's stack out). Nothing changes.</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="stack">The stack, empty to take the slot's out.</param>
    MoveResult CanPut(int slot, ItemStack stack);

    /// <summary>Puts <paramref name="stack"/> in <paramref name="slot"/>. Call it only after <see cref="CanPut"/> said <see cref="MoveReason.Done"/> (or to undo a put that was made).</summary>
    /// <param name="slot">The slot.</param>
    /// <param name="stack">The stack, empty to empty the slot.</param>
    void Put(int slot, ItemStack stack);
}
