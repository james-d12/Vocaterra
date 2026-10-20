using System.Text.Json.Serialization;
using Slg.Core.Determinism;

namespace Slg.Core.Audio;

/// <summary>A block of mono 16-bit PCM audio, such as synthesized speech.</summary>
public sealed record AudioBuffer(short[] Samples, int SampleRate)
{
    /// <summary>The mono sample data; callers must not mutate it after construction.</summary>
    public short[] Samples { get; init; } = Samples ?? throw new ArgumentNullException(nameof(Samples));

    /// <summary>Samples per second; always positive.</summary>
    public int SampleRate { get; init; } = SampleRate > 0
        ? SampleRate
        : throw new ArgumentOutOfRangeException(nameof(SampleRate), SampleRate, "Sample rate must be positive.");

    /// <summary>Buffer length in seconds.</summary>
    [JsonIgnore]
    public double DurationSeconds => (double)Samples.Length / SampleRate;

    /// <summary>Structural equality over sample rate and sample contents.</summary>
    public bool Equals(AudioBuffer? other)
    {
        return other is not null && SampleRate == other.SampleRate && ValueEquality.ArrayEquals(Samples, other.Samples);
    }

    /// <summary>Hash over sample rate and length.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(SampleRate, Samples.Length);
    }
}
