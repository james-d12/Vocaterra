namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// How full a water cell is: a <see cref="Source"/> (full, what the scene builds), or water flowing at a distance of 1 to
/// <see cref="MaxDistance"/> from its source, thinner the further it is. A cell that is fed from above is <see cref="Falling"/>.
/// Stored as one byte: 0 is a source (so a zeroed or missing layer reads as source), bits 0 to 2 hold the distance and bit 3 the falling flag.
/// </summary>
public readonly struct WaterLevel : IEquatable<WaterLevel>
{
    /// <summary>The furthest distance water flows from its source.</summary>
    public const int MaxDistance = 7;

    private const byte DistanceMask = 0b0111;
    private const byte FallingBit = 0b1000;

    private WaterLevel(byte encoded) => Encoded = encoded;

    /// <summary>The full cell. Its encoding is 0.</summary>
    public static WaterLevel Source => default;

    /// <summary>The byte stored in the chunk layer.</summary>
    public byte Encoded { get; }

    /// <summary>Whether this is a source cell.</summary>
    public bool IsSource => Distance == 0;

    /// <summary>The distance from the source: 0 for a source, 1 to <see cref="MaxDistance"/> for flowing water.</summary>
    public int Distance => Encoded & DistanceMask;

    /// <summary>Whether the cell is fed from above (drawn at full height).</summary>
    public bool Falling => (Encoded & FallingBit) != 0;

    /// <summary>Flowing water at a distance of 1 to <see cref="MaxDistance"/> from its source.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The distance is outside 1 to <see cref="MaxDistance"/>.</exception>
    public static WaterLevel Flowing(int distance, bool falling = false)
    {
        if (distance is < 1 or > MaxDistance)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, $"A flowing distance must lie between 1 and {MaxDistance}.");
        }

        return new WaterLevel((byte)(distance | (falling ? FallingBit : 0)));
    }

    /// <summary>Reads a stored byte back.</summary>
    /// <exception cref="ArgumentException">The byte is not a valid encoding (unused bits set, or a falling source).</exception>
    public static WaterLevel FromEncoded(byte encoded)
    {
        if ((encoded & ~(DistanceMask | FallingBit)) != 0 || ((encoded & DistanceMask) == 0 && encoded != 0))
        {
            throw new ArgumentException($"{encoded} is not a valid water level encoding.", nameof(encoded));
        }

        return new WaterLevel(encoded);
    }

    /// <inheritdoc />
    public bool Equals(WaterLevel other) => Encoded == other.Encoded;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WaterLevel other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Encoded;

    /// <summary>Whether two levels are the same.</summary>
    public static bool operator ==(WaterLevel left, WaterLevel right) => left.Equals(right);

    /// <summary>Whether two levels differ.</summary>
    public static bool operator !=(WaterLevel left, WaterLevel right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => IsSource ? "source" : $"flowing {Distance}{(Falling ? " (falling)" : string.Empty)}";
}
