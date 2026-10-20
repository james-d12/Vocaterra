using System.Text.Json.Serialization;
using Slg.Core.Determinism;

namespace Slg.Core.Audio;

/// <summary>A mono 16-bit PCM clip (16 000 Hz by convention) together with its stable content hash.</summary>
public sealed record PcmClip(short[] Samples, int SampleRate, string ClipHash)
{
    /// <summary>Default sample rate of the pipeline in hertz.</summary>
    public const int DefaultSampleRate = 16000;

    /// <summary>The mono sample data; callers must not mutate it after construction.</summary>
    public short[] Samples { get; init; } = Samples ?? throw new ArgumentNullException(nameof(Samples));

    /// <summary>Samples per second; always positive.</summary>
    public int SampleRate { get; init; } = SampleRate > 0
        ? SampleRate
        : throw new ArgumentOutOfRangeException(nameof(SampleRate), SampleRate, "Sample rate must be positive.");

    /// <summary>Hash of the samples (<see cref="DeterministicHash.OfSamples"/>) used by replay logs.</summary>
    public string ClipHash { get; init; } = ClipHash ?? throw new ArgumentNullException(nameof(ClipHash));

    /// <summary>Clip length in seconds.</summary>
    [JsonIgnore]
    public double DurationSeconds => (double)Samples.Length / SampleRate;

    /// <summary>Creates a clip and computes its <see cref="ClipHash"/> from the samples.</summary>
    public static PcmClip FromSamples(short[] samples, int sampleRate = DefaultSampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        return new PcmClip(samples, sampleRate, DeterministicHash.OfSamples(samples));
    }

    /// <summary>Structural equality over sample rate, hash and sample contents.</summary>
    public bool Equals(PcmClip? other)
    {
        return other is not null
            && SampleRate == other.SampleRate
            && string.Equals(ClipHash, other.ClipHash, StringComparison.Ordinal)
            && ValueEquality.ArrayEquals(Samples, other.Samples);
    }

    /// <summary>Hash over sample rate, clip hash and length.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(SampleRate, ClipHash, Samples.Length);
    }
}
