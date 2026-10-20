using Slg.Core.Stages;

namespace Slg.Pronunciation;

/// <summary>Greedy (best-path) CTC decoding of phone posteriors.</summary>
public static class CtcDecoder
{
    /// <summary>The symbol that marks the CTC blank in <see cref="PhonePosteriors.Vocabulary"/>; adapters rename their padding token to it.</summary>
    public const string BlankSymbol = "<blank>";

    /// <summary>
    /// Takes the argmax symbol of every frame (the lowest index wins a tie), collapses runs of the same symbol and drops blanks.
    /// A repeated phone therefore needs at least one blank frame between its two occurrences.
    /// </summary>
    public static IReadOnlyList<string> Greedy(PhonePosteriors posteriors)
    {
        ArgumentNullException.ThrowIfNull(posteriors);
        var blank = BlankIndex(posteriors);
        var decoded = new List<string>();
        var previous = -1;
        for (var frame = 0; frame < posteriors.FrameCount; frame++)
        {
            var best = ArgMax(posteriors.Frame(frame));
            if (best != previous && best != blank)
            {
                decoded.Add(posteriors.Vocabulary[best]);
            }

            previous = best;
        }

        return decoded.AsReadOnly();
    }

    /// <summary>Returns the column of <see cref="BlankSymbol"/>; throws when the vocabulary has no blank.</summary>
    internal static int BlankIndex(PhonePosteriors posteriors)
    {
        for (var i = 0; i < posteriors.Vocabulary.Count; i++)
        {
            if (string.Equals(posteriors.Vocabulary[i], BlankSymbol, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentException($"The posterior vocabulary has no '{BlankSymbol}' symbol.", nameof(posteriors));
    }

    /// <summary>Index of the largest value; the lowest index wins a tie and NaN is never chosen unless every value is NaN.</summary>
    internal static int ArgMax(ReadOnlySpan<float> values)
    {
        var best = 0;
        var bestValue = float.NegativeInfinity;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > bestValue)
            {
                bestValue = values[i];
                best = i;
            }
        }

        return best;
    }
}
