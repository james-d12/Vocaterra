using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>The warmth the player's stat sheet gives (I03a): the sheet's <see cref="StatKeys.Warmth"/>, in tenths of a degree, so clothes, a meal's buff or any later contributor warm the player with no change here. It replaces <see cref="NoWarmth"/>.</summary>
public sealed class StatWarmth : IWarmthSource
{
    private readonly Func<StatSheet> _sheet;

    /// <summary>A warmth source over the stat sheet.</summary>
    /// <param name="sheet">The current sheet (the simulation's, read each time).</param>
    public StatWarmth(Func<StatSheet> sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        _sheet = sheet;
    }

    /// <inheritdoc />
    public int WarmthTenths() => _sheet().GetWhole(StatKeys.Warmth);
}
