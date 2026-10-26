using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Generation;

/// <summary>
/// Smooth value noise in integers, keyed by the world seed and a pass name (<see cref="WorldRng.For"/>): the lattice values are hashes
/// of the cell, and the values between them are blended with a smoothstep. Several octaves added give the hills and the crags; the seed
/// changes them all and nothing else, so the sites never move.
/// </summary>
internal sealed class TerrainNoise
{
    private const ulong Golden = 0x9E3779B97F4A7C15UL;
    private const ulong Second = 0xC2B2AE3D27D4EB4FUL;
    private readonly ulong _key;

    /// <summary>Creates the noise of one pass of one world seed.</summary>
    public TerrainNoise(ulong seed, string pass) => _key = WorldRng.For(seed, pass).NextUInt64();

    /// <summary>The value noise of one octave at a block: 0 to 4 095, smooth over <paramref name="cell"/> blocks.</summary>
    public int Value(int octave, int x, int z, int cell)
    {
        var ix = IntMath.DivDown(x, cell);
        var iz = IntMath.DivDown(z, cell);
        var fx = IntMath.Smooth(IntMath.Mod(x, cell) * IntMath.One / cell);
        var fz = IntMath.Smooth(IntMath.Mod(z, cell) * IntMath.One / cell);
        var a = IntMath.Lerp(Lattice(octave, ix, iz), Lattice(octave, ix + 1, iz), fx);
        var b = IntMath.Lerp(Lattice(octave, ix, iz + 1), Lattice(octave, ix + 1, iz + 1), fx);
        return IntMath.Lerp(a, b, fz);
    }

    /// <summary>
    /// The hills: four octaves of 64, 32, 16 and 8 blocks added with weights 8/16, 4/16, 3/16 and 1/16, as a signed number from -4 095 to 4 095
    /// (a fraction of 4 096 of the amplitude the caller scales it by).
    /// </summary>
    public int Hills(int x, int z)
    {
        var sum = ((Value(0, x, z, 64) - 2048) * 8) + ((Value(1, x, z, 32) - 2048) * 4) + ((Value(2, x, z, 16) - 2048) * 3) + (Value(3, x, z, 8) - 2048);
        return sum / 8; // weights add to 16; the octaves are -2048..2047, so this is -4096..4095
    }

    /// <summary>A hash of a column: 0 to 4 095, no smoothness (the ragged snow line, the choice of a tree).</summary>
    public int Column(int salt, int x, int z) => Lattice(salt, x, z);

    private int Lattice(int octave, int ix, int iz)
    {
        var h = WorldRng.Mix(_key ^ ((ulong)(uint)ix * Golden));
        h = WorldRng.Mix(h ^ ((ulong)(uint)iz * Second) ^ (ulong)(uint)octave);
        return (int)(h >> 52);
    }
}


