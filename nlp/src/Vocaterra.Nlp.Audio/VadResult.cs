namespace Vocaterra.Nlp.Audio;

/// <summary>Outcome of energy-based voice activity trimming; frame indices describe the half-open range [SpeechStartFrame, SpeechEndFrame).</summary>
public sealed record VadResult(short[] Samples, bool HasSpeech, int SpeechStartFrame, int SpeechEndFrame, int FrameCount)
{
    /// <summary>The trimmed samples; empty when <see cref="HasSpeech"/> is false.</summary>
    public short[] Samples { get; init; } = Samples ?? throw new ArgumentNullException(nameof(Samples));

    /// <summary>Structural equality over all fields, including sample contents.</summary>
    public bool Equals(VadResult? other)
    {
        return other is not null
            && HasSpeech == other.HasSpeech
            && SpeechStartFrame == other.SpeechStartFrame
            && SpeechEndFrame == other.SpeechEndFrame
            && FrameCount == other.FrameCount
            && Samples.AsSpan().SequenceEqual(other.Samples);
    }

    /// <summary>Hash over the scalar fields and sample count.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(HasSpeech, SpeechStartFrame, SpeechEndFrame, FrameCount, Samples.Length);
    }
}
