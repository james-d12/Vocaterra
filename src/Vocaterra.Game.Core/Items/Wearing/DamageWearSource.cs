namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>Wear from damage taken: every worn item whose durability names this source wears by the points of a hit (the damage pipeline's <see cref="Damage.WearDamageModifier"/> raises it for the causes <c>game/damage.json</c> lists it for).</summary>
public sealed class DamageWearSource : IWearSource
{
    /// <summary>The id items name it by.</summary>
    public const string SourceId = "damage";

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public IEnumerable<WearSlot> Targets(ItemWear wear)
    {
        ArgumentNullException.ThrowIfNull(wear);
        return wear.WornSlots();
    }
}
