namespace Slg.Audio;

/// <summary>Decoded WAV audio: sample rate, channel count (1 or 2) and interleaved 16-bit samples.</summary>
public sealed record WavData(int SampleRate, int Channels, short[] InterleavedSamples)
{
    /// <summary>Samples per second per channel; always positive.</summary>
    public int SampleRate { get; init; } = SampleRate > 0
        ? SampleRate
        : throw new ArgumentOutOfRangeException(nameof(SampleRate), SampleRate, "Sample rate must be positive.");

    /// <summary>Number of interleaved channels; 1 (mono) or 2 (stereo, left first).</summary>
    public int Channels { get; init; } = Channels is 1 or 2
        ? Channels
        : throw new ArgumentOutOfRangeException(nameof(Channels), Channels, "Only mono and stereo audio is supported.");

    /// <summary>The interleaved sample data (L R L R ... for stereo); callers must not mutate it after construction.</summary>
    public short[] InterleavedSamples { get; init; } = InterleavedSamples ?? throw new ArgumentNullException(nameof(InterleavedSamples));

    /// <summary>Number of sample frames, that is samples per channel.</summary>
    public int FrameCount => InterleavedSamples.Length / Channels;

    /// <summary>Audio length in seconds.</summary>
    public double DurationSeconds => (double)FrameCount / SampleRate;

    /// <summary>Structural equality over sample rate, channel count and sample contents.</summary>
    public bool Equals(WavData? other)
    {
        return other is not null
            && SampleRate == other.SampleRate
            && Channels == other.Channels
            && InterleavedSamples.AsSpan().SequenceEqual(other.InterleavedSamples);
    }

    /// <summary>Hash over sample rate, channel count and length.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(SampleRate, Channels, InterleavedSamples.Length);
    }

    internal void EnsureWholeFrames()
    {
        if (InterleavedSamples.Length % Channels != 0)
        {
            throw new ArgumentException(
                $"Sample count {InterleavedSamples.Length} is not a multiple of the channel count {Channels}.",
                nameof(InterleavedSamples));
        }
    }
}
