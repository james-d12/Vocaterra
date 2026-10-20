using Slg.Core.Determinism;

namespace Slg.Audio;

/// <summary>Computes the stable clip hash used by replay logs.</summary>
public static class ClipHasher
{
    /// <summary>Returns the lowercase hex SHA-256 of the samples as little-endian 16-bit bytes (see <see cref="DeterministicHash.OfSamples"/>).</summary>
    public static string Hash(ReadOnlySpan<short> samples)
    {
        return DeterministicHash.OfSamples(samples);
    }
}
