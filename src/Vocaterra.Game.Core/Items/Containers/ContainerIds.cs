namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>The ids of the first containers (I02). A command names a container by its id and a slot index; a later container (a chest, W24) takes the next free id and is registered in <see cref="SlotContainers"/>, with no edit of <see cref="InventoryOps"/>.</summary>
public static class ContainerIds
{
    /// <summary>The bag: the active slots of the <see cref="Inventory"/> (the first ten are the hotbar).</summary>
    public const int Bag = 0;

    /// <summary>The worn equipment: every slot of every group, in <c>equipment.json</c> order (<see cref="Gear.EquipmentData.FlatIndex"/>).</summary>
    public const int Equipment = 1;

    /// <summary>The trash: one slot that keeps its last item until another replaces it.</summary>
    public const int Trash = 2;

    /// <summary>The held stack (the cursor). Not addressable by a command: the moves use it, a command never names it.</summary>
    public const int Held = 3;
}
