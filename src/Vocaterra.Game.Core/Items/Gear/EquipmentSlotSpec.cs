namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>One slot of a group of <c>game/equipment.json</c> (I03a).</summary>
/// <param name="Name">The slot's name: a body slot's own (<c>head</c>), a counted slot's wear kind and its number (<c>accessory2</c>). For logs and tests; nothing in code compares it.</param>
/// <param name="Wear">The kind of item the slot takes: an item's <see cref="ItemDefinition.Wear"/> must equal it.</param>
/// <param name="Label">The Spanish noun the panel labels the slot with (<c>cabeza</c>), or <see langword="null"/> when the lexicon has no word yet.</param>
public sealed record EquipmentSlotSpec(string Name, string Wear, string? Label);
