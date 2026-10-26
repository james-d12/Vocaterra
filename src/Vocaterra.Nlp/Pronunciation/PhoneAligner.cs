namespace Vocaterra.Nlp.Pronunciation;

/// <summary>One edit operation in a phone alignment.</summary>
public enum AlignmentOp
{
    /// <summary>The hypothesis phone equals the reference phone.</summary>
    Match,

    /// <summary>The hypothesis phone replaces a different reference phone.</summary>
    Substitute,

    /// <summary>A reference phone is missing from the hypothesis.</summary>
    Delete,

    /// <summary>The hypothesis has an extra phone with no reference counterpart.</summary>
    Insert,
}

/// <summary>One step of an alignment; the index on the side that does not take part is -1.</summary>
/// <param name="Op">The edit operation.</param>
/// <param name="HypIndex">Index into the hypothesis, or -1 for <see cref="AlignmentOp.Delete"/>.</param>
/// <param name="RefIndex">Index into the reference, or -1 for <see cref="AlignmentOp.Insert"/>.</param>
public readonly record struct AlignmentStep(AlignmentOp Op, int HypIndex, int RefIndex);

/// <summary>A minimal-edit alignment of a hypothesis phone sequence to a reference sequence, in reading order.</summary>
/// <param name="Steps">The steps from the start of both sequences to their ends.</param>
/// <param name="Distance">The Levenshtein distance (number of non-match steps).</param>
public sealed record PhoneAlignment(IReadOnlyList<AlignmentStep> Steps, int Distance)
{
    /// <summary>Structural equality including the step list.</summary>
    public bool Equals(PhoneAlignment? other)
    {
        if (other is null || Distance != other.Distance || Steps.Count != other.Steps.Count)
        {
            return false;
        }

        for (var i = 0; i < Steps.Count; i++)
        {
            if (!Steps[i].Equals(other.Steps[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Hash consistent with <see cref="Equals(PhoneAlignment)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Distance);
        foreach (var step in Steps)
        {
            hash.Add(step);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Levenshtein alignment of phone sequences with a deterministic backtrace.</summary>
public static class PhoneAligner
{
    /// <summary>
    /// Aligns a hypothesis to a reference with unit-cost substitution, deletion and insertion. Ties between equally cheap
    /// alignments are broken in reading order, preferring Match, then Substitute, then Delete, then Insert at every step, so the
    /// result is unique for a given pair of sequences. Phones compare with ordinal string equality.
    /// </summary>
    public static PhoneAlignment Align(IReadOnlyList<string> hyp, IReadOnlyList<string> reference)
    {
        ArgumentNullException.ThrowIfNull(hyp);
        ArgumentNullException.ThrowIfNull(reference);

        var n = hyp.Count;
        var m = reference.Count;

        // cost[i, j] is the distance between the suffixes hyp[i..] and reference[j..], so the trace can walk forward.
        var cost = new int[n + 1, m + 1];
        for (var i = n; i >= 0; i--)
        {
            for (var j = m; j >= 0; j--)
            {
                if (i == n)
                {
                    cost[i, j] = m - j;
                }
                else if (j == m)
                {
                    cost[i, j] = n - i;
                }
                else
                {
                    var diagonal = cost[i + 1, j + 1] + (string.Equals(hyp[i], reference[j], StringComparison.Ordinal) ? 0 : 1);
                    cost[i, j] = Math.Min(diagonal, Math.Min(cost[i, j + 1], cost[i + 1, j]) + 1);
                }
            }
        }

        var steps = new List<AlignmentStep>(Math.Max(n, m));
        var a = 0;
        var b = 0;
        while (a < n || b < m)
        {
            if (a < n && b < m && string.Equals(hyp[a], reference[b], StringComparison.Ordinal) && cost[a, b] == cost[a + 1, b + 1])
            {
                steps.Add(new AlignmentStep(AlignmentOp.Match, a, b));
                a++;
                b++;
            }
            else if (a < n && b < m && cost[a, b] == cost[a + 1, b + 1] + 1)
            {
                steps.Add(new AlignmentStep(AlignmentOp.Substitute, a, b));
                a++;
                b++;
            }
            else if (b < m && cost[a, b] == cost[a, b + 1] + 1)
            {
                steps.Add(new AlignmentStep(AlignmentOp.Delete, -1, b));
                b++;
            }
            else
            {
                steps.Add(new AlignmentStep(AlignmentOp.Insert, a, -1));
                a++;
            }
        }

        return new PhoneAlignment(steps.AsReadOnly(), cost[0, 0]);
    }
}
