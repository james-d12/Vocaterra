using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Underground;

/// <summary>
/// The unbreakable floor (<see cref="FloorSpec"/>): from the world's bottom up, a ragged layer whose thickness in each column is drawn from a hash of the column, between the spec's thinnest and
/// thickest. Every column has at least the thinnest, so the world has no hole in its floor. Pure and immutable.
/// </summary>
internal sealed class BedrockFloor
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly FloorSpec _spec;
    private readonly ulong _key;

    /// <summary>Creates the floor of one world seed.</summary>
    /// <param name="spec">The floor.</param>
    /// <param name="seed">The world seed.</param>
    public BedrockFloor(FloorSpec spec, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _spec = spec;
        _key = WorldRng.For(seed, "gen/floor").NextUInt64();
    }

    /// <summary>The y of the floor's lowest block.</summary>
    public int BottomY => _spec.BottomY;

    /// <summary>The y of the highest block the floor can have in any column.</summary>
    public int HighestPossibleY => _spec.BottomY + _spec.MaxThickness - 1;

    /// <summary>How many blocks of bedrock a column has.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int ThicknessAt(int x, int z)
    {
        var hash = WorldRng.Mix(_key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)z * GoldenZ));
        return _spec.MinThickness + (int)(hash % (ulong)(_spec.MaxThickness - _spec.MinThickness + 1));
    }

    /// <summary>The y of the highest bedrock block of a column.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public int TopAt(int x, int z) => _spec.BottomY + ThicknessAt(x, z) - 1;
}
