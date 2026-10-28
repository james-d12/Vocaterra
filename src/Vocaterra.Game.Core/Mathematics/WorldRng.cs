namespace Vocaterra.Game.Core.Mathematics;

/// <summary>
/// The simulation's own seeded generator: SplitMix64. Every generation pass derives its generator from the world seed, the pass name and a
/// chunk position with <see cref="For"/>, so passes never consume each other's draws and changing one pass does not reshuffle the others.
/// Nothing in the simulation uses the framework's random classes or the clock.
/// </summary>
public sealed class WorldRng
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const ulong FnvOffset = 0xCBF29CE484222325UL;
    private const ulong FnvPrime = 0x100000001B3UL;
    private ulong _state;

    /// <summary>Creates a generator that starts at <paramref name="state"/>.</summary>
    public WorldRng(ulong state) => _state = state;

    /// <summary>
    /// Derives the generator of one pass in one chunk. Passes that work per column use the chunk column and chunk row 0. The pass name is
    /// hashed over its UTF-16 code units, so it is part of the golden: renaming a pass changes the world.
    /// </summary>
    /// <param name="seed">The world seed.</param>
    /// <param name="passName">The pass name, for example <c>forest</c>.</param>
    /// <param name="chunkX">The chunk column, or 0.</param>
    /// <param name="chunkY">The chunk row, or 0.</param>
    public static WorldRng For(ulong seed, string passName, int chunkX = 0, int chunkY = 0)
    {
        ArgumentNullException.ThrowIfNull(passName);
        var name = FnvOffset;
        foreach (var c in passName)
        {
            name = (name ^ c) * FnvPrime;
        }

        var h = Mix(seed ^ 0xD1B54A32D192ED03UL);
        h = Mix(h ^ name);
        h = Mix(h ^ ((ulong)(uint)chunkX << 32) ^ (uint)chunkY);
        return new WorldRng(h);
    }

    /// <summary>The SplitMix64 output function: a bijective 64-bit mixer, also used for integer hashing.</summary>
    public static ulong Mix(ulong value)
    {
        var z = value;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>The next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        _state += Gamma;
        return Mix(_state);
    }

    /// <summary>The next 32 random bits.</summary>
    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    /// <summary>A number in [0, <paramref name="bound"/>). The multiply-shift reduction has a bias below bound / 2^32, which is negligible here.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bound"/> is not positive.</exception>
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);
        return (int)(((ulong)NextUInt32() * (uint)bound) >> 32);
    }

    /// <summary>A number in [<paramref name="min"/>, <paramref name="maxExclusive"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The range is empty.</exception>
    public int NextInt(int min, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, min);
        return min + NextInt(maxExclusive - min);
    }

    /// <summary>True with probability <paramref name="numerator"/> / <paramref name="denominator"/>.</summary>
    public bool NextChance(int numerator, int denominator) => NextInt(denominator) < numerator;
}
