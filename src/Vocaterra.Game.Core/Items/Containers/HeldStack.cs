namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// The held stack, the cursor (I02): a one-slot container that takes any stack, and remembers the container and slot the stack was last picked up from, where closing the panel returns it. It is simulation state: the digest covers it
/// when it is not empty (so a run that holds nothing has the digest it had before), and a save is only written with nothing held.
/// </summary>
public sealed class HeldStack : ISlotContainer
{
    private ItemStack _stack = ItemStack.Empty;

    /// <inheritdoc />
    public int Id => ContainerIds.Held;

    /// <inheritdoc />
    public int SlotCount => 1;

    /// <inheritdoc />
    public bool Discards => false;

    /// <summary>The stack held (empty when none).</summary>
    public ItemStack Stack => _stack;

    /// <summary>Whether nothing is held.</summary>
    public bool IsEmpty => _stack.IsEmpty;

    /// <summary>The container the held stack was last picked up from.</summary>
    public int OriginContainer { get; private set; }

    /// <summary>The slot it was last picked up from.</summary>
    public int OriginSlot { get; private set; }

    /// <inheritdoc />
    public ItemStack Get(int slot) => (uint)slot < 1u ? _stack : throw new ArgumentOutOfRangeException(nameof(slot), slot, "The held stack has one slot.");

    /// <inheritdoc />
    public MoveResult CanPut(int slot, ItemStack stack) => slot == 0 ? MoveResult.Done : MoveResult.Refused(MoveReason.NoSuchSlot);

    /// <inheritdoc />
    public void Put(int slot, ItemStack stack)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(slot, 0);
        _stack = stack.IsEmpty ? ItemStack.Empty : stack;
    }

    /// <summary>Notes where the stack now held was picked up from.</summary>
    /// <param name="container">The container.</param>
    /// <param name="slot">The slot.</param>
    public void SetOrigin(int container, int slot)
    {
        OriginContainer = container;
        OriginSlot = slot;
    }

    /// <summary>Drops the stack and its origin (a restore).</summary>
    public void Clear()
    {
        _stack = ItemStack.Empty;
        OriginContainer = 0;
        OriginSlot = 0;
    }
}
