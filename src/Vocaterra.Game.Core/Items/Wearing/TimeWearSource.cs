namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>Wear from time worn: every worn item whose durability names this source wears once for each in-game hour (the simulation counts the hours from its ticks, so a run is deterministic).</summary>
public sealed class TimeWearSource : IWearSource
{
    /// <summary>The id items name it by.</summary>
    public const string SourceId = "time";

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public IEnumerable<WearSlot> Targets(ItemWear wear)
    {
        ArgumentNullException.ThrowIfNull(wear);
        return wear.WornSlots();
    }
}
