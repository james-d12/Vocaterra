using Vocaterra.Game.Core.Items.Gear;

namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>What a move came to (I02): <see cref="MoveReason.Done"/>, or the reason it was refused (with the equipment's own reason when a worn slot refused).</summary>
/// <param name="Reason">Done or why not.</param>
/// <param name="Equip">The equipment's reason when <paramref name="Reason"/> is <see cref="MoveReason.SlotRefused"/>, otherwise <see cref="EquipResult.Done"/>.</param>
public readonly record struct MoveResult(MoveReason Reason, EquipResult Equip = EquipResult.Done)
{
    /// <summary>A move that was made.</summary>
    public static MoveResult Done { get; } = new(MoveReason.Done);

    /// <summary>Whether the move was made.</summary>
    public bool IsDone => Reason == MoveReason.Done;

    /// <summary>A refusal with a reason of the move's own.</summary>
    /// <param name="reason">The reason.</param>
    public static MoveResult Refused(MoveReason reason) => new(reason);

    /// <summary>A refusal of the equipment's rules (<see cref="MoveReason.SlotRefused"/>), or <see cref="Done"/> when <paramref name="result"/> is <see cref="EquipResult.Done"/>.</summary>
    /// <param name="result">The equipment's verdict.</param>
    public static MoveResult FromEquip(EquipResult result) => result == EquipResult.Done ? Done : new(MoveReason.SlotRefused, result);
}
