namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The open words the appearance (I09) looks for, kept as data of the caller rather than group ids in code: the <c>wear</c> kind of the slot a bag is worn in and of the off-hand slot (the groups' own ids are never named),
/// and the look part keys that colour the arms. The defaults match <c>game/equipment.json</c> and the item rows.
/// </summary>
/// <param name="BackWear">The <c>wear</c> kind of the slot whose item shows on the back.</param>
/// <param name="OffHandWear">The <c>wear</c> kind of the slot whose item shows in the left hand.</param>
/// <param name="SleeveKey">The look part key that colours the sleeves.</param>
/// <param name="SleeveFallbackKey">The look part key the sleeves take when no worn item gives <paramref name="SleeveKey"/> (a body piece with no sleeve colour of its own).</param>
public sealed record AppearanceRoles(string BackWear = ItemDefinition.BagWear, string OffHandWear = "offhand", string SleeveKey = "sleeve", string SleeveFallbackKey = "body")
{
    /// <summary>The roles of the shipped data.</summary>
    public static AppearanceRoles Default { get; } = new();
}
