using System.Globalization;
using System.Numerics;

namespace Slg.Core.Determinism;

/// <summary>Deterministic xoshiro256** generator seeded through SplitMix64; the only permitted source of randomness.</summary>
public sealed class SeededRng
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>Creates a generator whose four state words are four consecutive SplitMix64 outputs of the seed.</summary>
    public SeededRng(ulong seed)
    {
        var mix = new SplitMix64(seed);
        _s0 = mix.Next();
        _s1 = mix.Next();
        _s2 = mix.Next();
        _s3 = mix.Next();
    }

    /// <summary>Creates the generator for one NPC interaction from the world seed, NPC id and interaction counter.</summary>
    public static SeededRng ForInteraction(ulong worldSeed, string npcId, long counter)
    {
        ArgumentNullException.ThrowIfNull(npcId);
        return new SeededRng(DeterministicHash.Seed64(
            worldSeed.ToString(CultureInfo.InvariantCulture),
            npcId,
            counter.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>Returns the next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5UL, 7) * 9UL;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Returns an unbiased integer in [0, exclusiveMax) using rejection sampling.</summary>
    public int NextInt(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(exclusiveMax, 0);
        var bound = (ulong)exclusiveMax;
        var threshold = (0UL - bound) % bound;
        while (true)
        {
            var draw = NextUInt64();
            if (draw >= threshold)
            {
                return (int)(draw % bound);
            }
        }
    }

    /// <summary>Returns a double in [0, 1) with 53 bits of precision.</summary>
    public double NextDouble()
    {
        return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>Picks one element uniformly; throws if the list is empty.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[NextInt(items.Count)];
    }

    /// <summary>Shuffles the list in place with Fisher-Yates, walking from the last index down to 1.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
