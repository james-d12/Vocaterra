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

    /// <summary>An equipment cell of the paper doll (I04): <see cref="InventoryHit.Slot"/> is the slot's flat index in <c>equipment.json</c> order.</summary>
    Equipment = 4,

    /// <summary>The heading of a doll column (I04b): <see cref="InventoryHit.Slot"/> is the column. It reacts to hover only (the English), never to a click.</summary>
    Heading = 5,
}
