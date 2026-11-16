namespace Vocaterra.Game.Core.Temperature;

/// <summary>The first <see cref="IWarmthSource"/> (I06): none. A test's source of nothing: the session reads the stat sheet through <see cref="StatWarmth"/> since I03a.</summary>
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
