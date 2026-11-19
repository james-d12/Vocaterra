using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// Which line says why a move was refused (I04): one table from the reason a refused move or equip carries to a HUD line key (<c>refuse.noRoom</c>), so the panel needs no switch of its own and a new reason is one row.
/// Each key is a row of <c>hudLines</c> in <c>game/words.json</c> (the Spanish the player reads, text only like the needs' stage lines) and of the English locale under the same id (I04b). A reason the player needs no line for (a click on an empty cell
/// with nothing in hand, a slot that does not exist) maps to <see langword="null"/>.
/// </summary>
public static class MoveRefusalText
{
    /// <summary>Every key the table can return: each needs a Spanish HUD line and its English (<see cref="Content.GameDataValidator.HudLineKeys"/>).</summary>
    public static IReadOnlyList<string> AllKeys { get; } =
    [
        "refuse.different", "refuse.trashDifferent", "refuse.slotFull", "refuse.noRoom", "refuse.stackTooLarge", "refuse.notWearable",
        "refuse.wrongSlot", "refuse.wrongKind", "refuse.duplicate", "refuse.wearOne", "refuse.bagNotEmpty",
    ];

    /// <summary>The key of the line for a refused move.</summary>
    /// <param name="reason">The move's reason (<see cref="MoveReason"/>).</param>
    /// <param name="equip">The equipment's reason when a worn slot refused (<see cref="EquipResult"/>), otherwise <see cref="EquipResult.Done"/>.</param>
    /// <param name="container">The container the move was aimed at (<see cref="ContainerIds"/>).</param>
    public static string? KeyFor(MoveReason reason, EquipResult equip, int container) => reason switch
    {
        MoveReason.SlotRefused => KeyFor(equip),
        MoveReason.DifferentItem => container == ContainerIds.Trash ? "refuse.trashDifferent" : "refuse.different",
        MoveReason.SlotFull => "refuse.slotFull",
        MoveReason.NoRoom => "refuse.noRoom",
        MoveReason.StackTooLarge => "refuse.stackTooLarge",
        _ => null,
    };

    /// <summary>The key of the line for a refused equip or unequip.</summary>
    /// <param name="result">The result (<see cref="EquipResult"/>).</param>
    public static string? KeyFor(EquipResult result) => result switch
    {
        EquipResult.NotWearable => "refuse.notWearable",
        EquipResult.WrongSlot => "refuse.wrongSlot",
        EquipResult.WrongKind => "refuse.wrongKind",
        EquipResult.Duplicate => "refuse.duplicate",
        EquipResult.StackTooLarge => "refuse.wearOne",
        EquipResult.BagSlotOccupied or EquipResult.BagFull => "refuse.noRoom",
        EquipResult.BagNotEmpty => "refuse.bagNotEmpty",
        _ => null,
    };
}
