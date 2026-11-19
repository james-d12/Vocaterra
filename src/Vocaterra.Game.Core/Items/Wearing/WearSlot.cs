namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>Where a stack that can wear is: a slot of the bag (the hotbar's included) or a worn slot by its flat index (<see cref="Gear.EquipmentData.FlatIndex"/>).</summary>
/// <param name="IsWorn"><see langword="true"/> for a worn slot, <see langword="false"/> for a bag slot.</param>
/// <param name="Index">The bag slot, or the flat index of the worn slot.</param>
public readonly record struct WearSlot(bool IsWorn, int Index);
