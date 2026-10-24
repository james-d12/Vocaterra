namespace Vocaterra.Game.Utilities;

/// <summary>
/// The presentation's random numbers (S24): which take of a sound plays, and its small pitch change. It is seeded from the world seed and is a
/// generator of its own (SplitMix64), so it never shares a draw with the simulation's <c>WorldRng</c> and nothing it does can change a turn, a state hash or the
/// replay. Nothing here reads the clock or the framework's random classes: the same seed and the same cues give the same variation.
/// </summary>
public sealed class PresentationRng
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;
    private const ulong StreamSalt = 0x53E1D5A0C0FFEE24UL; // keeps this stream apart from every pass of the world's own generator
    private ulong _state;

    /// <summary>Creates the generator for a world seed.</summary>
    /// <param name="worldSeed">The world seed.</param>
    public PresentationRng(ulong worldSeed) => _state = worldSeed ^ StreamSalt;

    /// <summary>The next 64 random bits.</summary>
    public ulong NextUInt64()
    {
        _state += Gamma;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A number from 0 (included) to 1 (excluded).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A whole number from 0 (included) to <paramref name="limit"/> (excluded).</summary>
    /// <param name="limit">The exclusive upper bound, at least 1.</param>
    public int NextInt(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        return (int)(NextUInt64() % (ulong)limit);
    }

    /// <summary>A number from <paramref name="minimum"/> to <paramref name="maximum"/>.</summary>
    /// <param name="minimum">The lowest value.</param>
    /// <param name="maximum">The highest value.</param>
    public double NextBetween(double minimum, double maximum) => minimum + ((maximum - minimum) * NextDouble());
}
