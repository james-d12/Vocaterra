namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>The trash (I02): one slot that takes any stack and keeps the last one put in until another replaces it (the replaced stack is gone). Taking the stack back out is an ordinary pick-up. It is simulation state (the digest covers it when it holds something) and is not saved.</summary>
public sealed class TrashContainer : ISlotContainer
{
    private ItemStack _stack = ItemStack.Empty;

    /// <inheritdoc />
    public int Id => ContainerIds.Trash;

    /// <inheritdoc />
    public int SlotCount => 1;

    /// <inheritdoc />
    public bool Discards => true;

    /// <summary>The stack in the trash (empty when there is none).</summary>
    public ItemStack Stack => _stack;

    /// <inheritdoc />
    public ItemStack Get(int slot) => (uint)slot < 1u ? _stack : throw new ArgumentOutOfRangeException(nameof(slot), slot, "The trash has one slot.");

    /// <inheritdoc />
    public MoveResult CanPut(int slot, ItemStack stack) => slot == 0 ? MoveResult.Done : MoveResult.Refused(MoveReason.NoSuchSlot);

    /// <inheritdoc />
    public void Put(int slot, ItemStack stack)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(slot, 0);
        _stack = stack.IsEmpty ? ItemStack.Empty : stack;
    }

    /// <summary>Empties the trash (a restore, a test).</summary>
    public void Clear() => _stack = ItemStack.Empty;
}
