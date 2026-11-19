namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>The bag as a container (I02): the active slots of the <see cref="Inventory"/>, so a slot past a worn bag's count does not exist.</summary>
/// <param name="bag">The inventory.</param>
public sealed class BagContainer(Inventory bag) : ISlotContainer
{
    /// <inheritdoc />
    public int Id => ContainerIds.Bag;

    /// <inheritdoc />
    public int SlotCount => bag.SlotCount;

    /// <inheritdoc />
    public bool Discards => false;

    /// <inheritdoc />
    public ItemStack Get(int slot) => bag.Slots[slot];

    /// <inheritdoc />
    public MoveResult CanPut(int slot, ItemStack stack)
    {
        if ((uint)slot >= (uint)bag.SlotCount)
        {
            return MoveResult.Refused(MoveReason.NoSuchSlot);
        }

        return !stack.IsEmpty && stack.Count > bag.Items.Get(stack.Item).MaxStack ? MoveResult.Refused(MoveReason.StackTooLarge) : MoveResult.Done;
    }

    /// <inheritdoc />
    public void Put(int slot, ItemStack stack) => bag.SetSlot(slot, stack);
}
