using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// Which line of the locale bundle says why a move was refused (I04): one table from the reason a refused move or equip carries to a <c>ui.</c> key, so the panel needs no switch of its own and a new reason is one row.
/// The lines are in English, in <c>content/locale/en/ui.json</c>: the game has no Spanish for them yet (they wait for stop 3 of the owner's review). A reason the player needs no line for (a click on an empty cell
/// with nothing in hand, a slot that does not exist) maps to <see langword="null"/>.
/// </summary>
public static class MoveRefusalText
{
    /// <summary>The key of the line for a refused move.</summary>
    /// <param name="reason">The move's reason (<see cref="MoveReason"/>).</param>
    /// <param name="equip">The equipment's reason when a worn slot refused (<see cref="EquipResult"/>), otherwise <see cref="EquipResult.Done"/>.</param>
    /// <param name="container">The container the move was aimed at (<see cref="ContainerIds"/>).</param>
    public static string? KeyFor(MoveReason reason, EquipResult equip, int container) => reason switch
    {
        MoveReason.SlotRefused => KeyFor(equip),
        MoveReason.DifferentItem => container == ContainerIds.Trash ? "refuseTrashDifferent" : "refuseDifferent",
        MoveReason.SlotFull => "refuseSlotFull",
        MoveReason.NoRoom => "refuseNoRoom",
        MoveReason.StackTooLarge => "refuseStackTooLarge",
        _ => null,
    };

    /// <summary>The key of the line for a refused equip or unequip.</summary>
    /// <param name="result">The result (<see cref="EquipResult"/>).</param>
    public static string? KeyFor(EquipResult result) => result switch
    {
        EquipResult.NotWearable => "refuseNotWearable",
        EquipResult.WrongSlot => "refuseWrongSlot",
        EquipResult.WrongKind => "refuseWrongKind",
        EquipResult.Duplicate => "refuseDuplicate",
        EquipResult.StackTooLarge => "refuseWearOne",
        EquipResult.BagSlotOccupied or EquipResult.BagFull => "refuseNoRoom",
        EquipResult.BagNotEmpty => "refuseBagNotEmpty",
        _ => null,
    };
}
