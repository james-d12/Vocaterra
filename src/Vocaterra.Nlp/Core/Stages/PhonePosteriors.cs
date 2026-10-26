using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Frame-by-frame phone log-probabilities (row-major [frame, phone]) from a phoneme recognizer.</summary>
public sealed record PhonePosteriors(IReadOnlyList<string> Vocabulary, float[] LogProbs, int FrameCount, double FrameSeconds)
{
    /// <summary>The phone vocabulary; its order defines the column index.</summary>
    public IReadOnlyList<string> Vocabulary { get; init; } = Vocabulary ?? throw new ArgumentNullException(nameof(Vocabulary));

    /// <summary>Row-major log-probabilities with length FrameCount times the vocabulary size.</summary>
    public float[] LogProbs { get; init; } = ValidateLogProbs(Vocabulary, LogProbs, FrameCount);

    /// <summary>Number of frames (rows).</summary>
    public int FrameCount { get; init; } = FrameCount >= 0
        ? FrameCount
        : throw new ArgumentOutOfRangeException(nameof(FrameCount), FrameCount, "Frame count must not be negative.");

    /// <summary>Number of phones (columns).</summary>
    [JsonIgnore]
    public int PhoneCount => Vocabulary.Count;

    /// <summary>Log-probability of a phone at a frame.</summary>
    public float this[int frame, int phone]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(frame);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, FrameCount);
            ArgumentOutOfRangeException.ThrowIfNegative(phone);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(phone, PhoneCount);
            return LogProbs[(frame * PhoneCount) + phone];
        }
    }

    /// <summary>Returns the log-probabilities of every phone at one frame.</summary>
    public ReadOnlySpan<float> Frame(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frame, FrameCount);
        return LogProbs.AsSpan(frame * PhoneCount, PhoneCount);
    }

    /// <summary>Structural equality including vocabulary and probability contents.</summary>
    public bool Equals(PhonePosteriors? other)
    {
        return other is not null
            && FrameCount == other.FrameCount
            && FrameSeconds.Equals(other.FrameSeconds)
            && ValueEquality.ListEquals(Vocabulary, other.Vocabulary)
            && ValueEquality.ArrayEquals(LogProbs, other.LogProbs);
    }

    /// <summary>Hash over frame count, frame length and vocabulary.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(FrameCount);
        hash.Add(FrameSeconds);
        ValueEquality.AddList(ref hash, Vocabulary);
        return hash.ToHashCode();
    }

    private static float[] ValidateLogProbs(IReadOnlyList<string> vocabulary, float[] logProbs, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(vocabulary, nameof(Vocabulary));
        ArgumentNullException.ThrowIfNull(logProbs, nameof(LogProbs));
        var expected = (long)frameCount * vocabulary.Count;
        if (logProbs.LongLength != expected)
        {
            throw new ArgumentException(
                $"LogProbs length {logProbs.LongLength} does not equal FrameCount * Vocabulary.Count ({frameCount} * {vocabulary.Count} = {expected}).",
                nameof(LogProbs));
        }

        return logProbs;
    }
}
