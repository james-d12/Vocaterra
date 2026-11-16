using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// The worn equipment as a container (I02): the flat slots of <see cref="Equipment"/> (each group's slots in <c>equipment.json</c> order). Its accept check is the equip rules, so a refused put returns the
/// <see cref="EquipResult"/> as the reason; it holds one of an item (a larger stack is <see cref="EquipResult.StackTooLarge"/>). A put keeps the bag's active count at its base plus the worn capacity, as
/// <see cref="Equipment.Equip"/> does. It claims for a quick move what the equipment data says can be worn.
/// </summary>
public sealed class EquipmentContainer : IQuickTarget
{
    private readonly Equipment _equipment;
    private readonly Inventory _bag;
    private readonly Action<EquipSlot, ItemStack, ItemStack>? _changed;

    /// <summary>Creates the container over the worn items and the bag the rules read.</summary>
    /// <param name="equipment">The worn items.</param>
    /// <param name="bag">The inventory.</param>
    /// <param name="changed">Called after a slot changes, with the slot, what was there and what is there now (the simulation raises its events and rebuilds the stats from it), or <see langword="null"/>.</param>
    public EquipmentContainer(Equipment equipment, Inventory bag, Action<EquipSlot, ItemStack, ItemStack>? changed = null)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(bag);
        _equipment = equipment;
        _bag = bag;
        _changed = changed;
    }

    /// <inheritdoc />
    public int Id => ContainerIds.Equipment;

    /// <inheritdoc />
    public int SlotCount => _equipment.Data.SlotCount;

    /// <inheritdoc />
    public bool Discards => false;

    /// <inheritdoc />
    public ItemStack Get(int slot) => _equipment.Slots[slot];

    /// <inheritdoc />
    public MoveResult CanPut(int slot, ItemStack stack)
    {
        if ((uint)slot >= (uint)SlotCount)
        {
            return MoveResult.Refused(MoveReason.NoSuchSlot);
        }

        var at = _equipment.Data.SlotAt(slot);
        if (stack.IsEmpty)
        {
            return MoveResult.FromEquip(_equipment.CanTakeOff(_bag, at));
        }

        if (stack.Count != 1)
        {
            return MoveResult.FromEquip(EquipResult.StackTooLarge);
        }

        return MoveResult.FromEquip(_equipment.CanWear(_bag, _equipment.Items.Get(stack.Item), at));
    }

    /// <inheritdoc />
    public void Put(int slot, ItemStack stack)
    {
        var at = _equipment.Data.SlotAt(slot);
        var old = _equipment.Get(at);
        _equipment.Set(at, stack);
        _equipment.SyncBag(_bag);
        _changed?.Invoke(at, old, stack.IsEmpty ? ItemStack.Empty : stack);
    }

    /// <inheritdoc />
    public bool Claims(ItemStack stack, out int slot, out MoveResult refusal)
    {
        slot = -1;
        refusal = MoveResult.Done;
        var definition = _equipment.Items.Get(stack.Item);
        if (definition.Wear is null)
        {
            return false;
        }

        if (_equipment.FindSlotFor(_bag, definition) is { } found)
        {
            slot = _equipment.Data.FlatIndex(found);
            return true;
        }

        var sameKind = _equipment.Data.AllSlots().Where(s => _equipment.Data.Spec(s).Wear == definition.Wear).Select(static s => (EquipSlot?)s).FirstOrDefault();
        refusal = sameKind is { } kind ? MoveResult.FromEquip(_equipment.CanWear(_bag, definition, kind)) : MoveResult.FromEquip(EquipResult.WrongSlot);
        return true;
    }
}
