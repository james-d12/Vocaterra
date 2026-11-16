namespace Vocaterra.Game.Core.Items;

/// <summary>What the pointer is on in the inventory screen (I02): found by the screen's layout (<c>InventoryLayout.Hit</c>), read by <see cref="InventoryPanel"/>.</summary>
/// <param name="Kind">The kind of target.</param>
/// <param name="Slot">The bag slot for <see cref="InventoryHitKind.Cell"/>, the equipment's flat slot index for <see cref="InventoryHitKind.Equipment"/>, otherwise 0.</param>
public readonly record struct InventoryHit(InventoryHitKind Kind, int Slot = 0)
{
    /// <summary>No target.</summary>
    public static InventoryHit None { get; } = new(InventoryHitKind.None);
}
