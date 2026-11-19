namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>Wear from use (W13): a gather with the tool in the selected hotbar slot wears that tool, one occurrence a gather.</summary>
public sealed class UseWearSource : IWearSource
{
    /// <summary>The id items name it by.</summary>
    public const string SourceId = "use";

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public IEnumerable<WearSlot> Targets(ItemWear wear)
    {
        ArgumentNullException.ThrowIfNull(wear);
        yield return new WearSlot(false, wear.SelectedSlot);
    }
}
