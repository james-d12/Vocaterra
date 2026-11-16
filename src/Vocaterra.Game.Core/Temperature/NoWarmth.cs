namespace Vocaterra.Game.Core.Temperature;

/// <summary>The first <see cref="IWarmthSource"/> (I06): none. The player wears nothing that warms until the equipment's stats are added (I03a).</summary>
public sealed class NoWarmth : IWarmthSource
{
    /// <summary>The one instance.</summary>
    public static NoWarmth Instance { get; } = new();

    private NoWarmth()
    {
    }

    /// <inheritdoc />
    public int WarmthTenths() => 0;
}
