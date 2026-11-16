namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// A group of slots of <c>game/equipment.json</c> (I03a): the clothes, the armour, the cosmetic layer, the accessories, the off-hand, the backpack. Each is a row of data: code never names a group's
/// <see cref="Id"/>, it follows the flags, so a seventh group is a new row.
/// </summary>
/// <param name="Id">The group's stable key.</param>
/// <param name="Layer">The layer the avatar draws it in (1 under, 2 over, 3 looks only), or <see langword="null"/> for a group that is not drawn as a layer.</param>
/// <param name="Slots">The group's slots, in order.</param>
/// <param name="Accepts">The item tags the group takes: an item with any of them may be put in.</param>
/// <param name="NoDuplicates">Whether one item kind may be worn only once in the group (the accessories).</param>
/// <param name="StatsApply">Whether the worn items give their stats (false for the cosmetic layer).</param>
public sealed record EquipmentGroup(string Id, int? Layer, IReadOnlyList<EquipmentSlotSpec> Slots, IReadOnlyList<string> Accepts, bool NoDuplicates, bool StatsApply)
{
    /// <summary>Whether the group takes an item carrying <paramref name="tag"/>.</summary>
    /// <param name="tag">The tag.</param>
    public bool AcceptsTag(string tag) => Accepts.Contains(tag, StringComparer.Ordinal);
}
