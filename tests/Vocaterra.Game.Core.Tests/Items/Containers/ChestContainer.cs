using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Containers;

namespace Vocaterra.Game.Core.Tests.Items.Containers;

/// <summary>A container of the tests' own (a stand-in for a chest, W24): twelve slots that take anything, but never a bread (a rule of its own).</summary>
internal sealed class ChestContainer(int id, ItemId refused) : ISlotContainer
{
    private readonly ItemStack[] _slots = new ItemStack[12];

    public int Id => id;

    public int SlotCount => _slots.Length;

    public bool Discards => false;

    public ItemStack Get(int slot) => _slots[slot];

    public MoveResult CanPut(int slot, ItemStack stack)
    {
        if ((uint)slot >= (uint)_slots.Length)
        {
            return MoveResult.Refused(MoveReason.NoSuchSlot);
        }

        return !stack.IsEmpty && stack.Item == refused ? MoveResult.Refused(MoveReason.SlotRefused) : MoveResult.Done;
    }

    public void Put(int slot, ItemStack stack) => _slots[slot] = stack.IsEmpty ? ItemStack.Empty : stack;
}
