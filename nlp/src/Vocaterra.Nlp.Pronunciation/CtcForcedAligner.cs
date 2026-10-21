using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>The frames a forced alignment assigned to one target phone.</summary>
/// <param name="PhoneIndex">Index of the phone in the target sequence.</param>
/// <param name="Phone">The target phone.</param>
/// <param name="StartFrame">First frame of the segment.</param>
/// <param name="EndFrame">One past the last frame of the segment; every segment spans at least one frame.</param>
public readonly record struct PhoneSegment(int PhoneIndex, string Phone, int StartFrame, int EndFrame)
{
    /// <summary>Number of frames in the segment.</summary>
    public int FrameCount => EndFrame - StartFrame;
}

/// <summary>The best CTC path of a target sequence: one segment per target phone and the path log-likelihood.</summary>
/// <param name="Segments">One segment per target phone, in order.</param>
/// <param name="LogLikelihood">Sum of the log-probabilities along the best path.</param>
public sealed record CtcAlignment(IReadOnlyList<PhoneSegment> Segments, double LogLikelihood)
{
    /// <summary>Structural equality including the segment list.</summary>
    public bool Equals(CtcAlignment? other)
    {
        if (other is null || !LogLikelihood.Equals(other.LogLikelihood) || Segments.Count != other.Segments.Count)
        {
            return false;
        }

        for (var i = 0; i < Segments.Count; i++)
        {
            if (!Segments[i].Equals(other.Segments[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(CtcAlignment)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(LogLikelihood);
        foreach (var segment in Segments)
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// Viterbi forced alignment over the standard CTC topology: the target is wrapped in optional blanks (blank, phone, blank, ...),
/// a phone may be followed directly by a different phone, and two equal adjacent phones need at least one blank frame between them.
/// </summary>
public static class CtcForcedAligner
{
    /// <summary>The fewest frames that can hold a target: one per phone plus one blank per pair of equal adjacent phones.</summary>
    public static int MinimumFrames(IReadOnlyList<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var frames = targets.Count;
        for (var i = 1; i < targets.Count; i++)
        {
            if (string.Equals(targets[i], targets[i - 1], StringComparison.Ordinal))
            {
                frames++;
            }
        }

        return frames;
    }

    /// <summary>
    /// Finds the most likely frame segmentation of the target. Returns null when the target cannot be aligned: it needs more frames
    /// than the posteriors have, or one of its phones is not in the vocabulary (the blank symbol is not a phone).
    /// </summary>
    /// <remarks>
    /// Ties between equally likely paths are resolved so that blanks absorb them: a phone starts as late and ends as early as the tie allows.
    /// Real posteriors almost never tie; the rule keeps synthetic matrices deterministic.
    /// </remarks>
    public static CtcAlignment? Align(PhonePosteriors posteriors, IReadOnlyList<string> targets)
    {
        ArgumentNullException.ThrowIfNull(posteriors);
        ArgumentNullException.ThrowIfNull(targets);

        var blank = CtcDecoder.BlankIndex(posteriors);
        var labels = new int[targets.Count];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = IndexOf(posteriors.Vocabulary, targets[i]);
            if (labels[i] < 0 || labels[i] == blank)
            {
                return null;
            }
        }

        var frames = posteriors.FrameCount;
        if (frames < MinimumFrames(targets))
        {
            return null;
        }

        var states = (2 * labels.Length) + 1;
        var back = new byte[frames * states];
        var path = Viterbi(posteriors, blank, labels, back, out var logLikelihood);
        if (path is null || !double.IsFinite(logLikelihood))
        {
            return null;
        }

        var segments = new PhoneSegment[labels.Length];
        for (var k = 0; k < labels.Length; k++)
        {
            var start = -1;
            var end = -1;
            for (var t = 0; t < frames; t++)
            {
                if (path[t] == (2 * k) + 1)
                {
                    if (start < 0)
                    {
                        start = t;
                    }

                    end = t + 1;
                }
            }

            segments[k] = new PhoneSegment(k, targets[k], start, end);
        }

        return new CtcAlignment(Array.AsReadOnly(segments), logLikelihood);
    }

    /// <summary>Returns the column of a symbol in the vocabulary (first occurrence, ordinal), or -1.</summary>
    internal static int IndexOf(IReadOnlyList<string> vocabulary, string symbol)
    {
        for (var i = 0; i < vocabulary.Count; i++)
        {
            if (string.Equals(vocabulary[i], symbol, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int[]? Viterbi(PhonePosteriors posteriors, int blank, int[] labels, byte[] back, out double logLikelihood)
    {
        var frames = posteriors.FrameCount;
        var states = (2 * labels.Length) + 1;
        var previous = new double[states];
        var current = new double[states];
        Array.Fill(previous, double.NegativeInfinity);

        if (frames == 0)
        {
            logLikelihood = 0.0;
            return [];
        }

        var first = posteriors.Frame(0);
        previous[0] = first[blank];
        if (labels.Length > 0)
        {
            previous[1] = first[labels[0]];
        }

        for (var t = 1; t < frames; t++)
        {
            var row = posteriors.Frame(t);
            for (var s = 0; s < states; s++)
            {
                double best;
                byte step;
                if ((s & 1) == 1)
                {
                    // A phone state: coming from the blank before it wins ties, then from the previous phone, then staying.
                    best = previous[s - 1];
                    step = 1;
                    if (s >= 3 && labels[s >> 1] != labels[(s >> 1) - 1] && previous[s - 2] > best)
                    {
                        best = previous[s - 2];
                        step = 2;
                    }

                    if (previous[s] > best)
                    {
                        best = previous[s];
                        step = 0;
                    }

                    current[s] = best + row[labels[s >> 1]];
                }
                else
                {
                    // A blank state: staying in the blank wins ties over coming from the phone before it.
                    best = previous[s];
                    step = 0;
                    if (s >= 1 && previous[s - 1] > best)
                    {
                        best = previous[s - 1];
                        step = 1;
                    }

                    current[s] = best + row[blank];
                }

                back[(t * states) + s] = step;
            }

            (previous, current) = (current, previous);
        }

        var end = states - 1;
        logLikelihood = previous[end];
        if (labels.Length > 0 && previous[end - 1] > logLikelihood)
        {
            end -= 1;
            logLikelihood = previous[end];
        }

        if (!double.IsFinite(logLikelihood))
        {
            return null;
        }

        var path = new int[frames];
        var state = end;
        for (var t = frames - 1; t >= 0; t--)
        {
            path[t] = state;
            if (t > 0)
            {
                state -= back[(t * states) + state];
            }
        }

        return path;
    }
}
