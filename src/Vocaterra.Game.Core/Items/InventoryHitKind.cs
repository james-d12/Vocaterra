namespace Vocaterra.Game.Core.Items;

/// <summary>What a point of the inventory screen is on (I02).</summary>
public enum InventoryHitKind
{
    /// <summary>Nothing the screen reacts to.</summary>
    None = 0,

    /// <summary>A bag slot (<see cref="InventoryHit.Slot"/>).</summary>
    Cell = 1,

    /// <summary>The sort button.</summary>
    Sort = 2,

    /// <summary>The trash cell.</summary>
    Trash = 3,
}
