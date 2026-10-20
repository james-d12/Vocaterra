namespace Vocaterra.Nlp.Nlp.Embeddings;

/// <summary>Deterministic vector helpers: sequential summation in ascending index order, no parallel or SIMD reductions.</summary>
internal static class EmbeddingMath
{
    /// <summary>Scales the values to unit L2 norm in double precision; a zero or non-finite vector becomes all zeros.</summary>
    public static double[] ToUnit(ReadOnlySpan<float> values)
    {
        var result = new double[values.Length];
        var sum = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            double v = values[i];
            result[i] = v;
            sum += v * v;
        }

        var norm = Math.Sqrt(sum);
        if (!double.IsFinite(norm) || norm <= 0.0)
        {
            Array.Clear(result);
            return result;
        }

        for (var i = 0; i < result.Length; i++)
        {
            result[i] /= norm;
        }

        return result;
    }

    /// <summary>Scales the vector in place to unit L2 norm accumulated in double precision; a zero or non-finite vector becomes all zeros.</summary>
    public static void NormalizeInPlace(Span<float> values)
    {
        var sum = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            double v = values[i];
            sum += v * v;
        }

        var norm = Math.Sqrt(sum);
        if (!double.IsFinite(norm) || norm <= 0.0)
        {
            values.Clear();
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (float)(values[i] / norm);
        }
    }

    /// <summary>Dot product of two equal-length vectors, summed in ascending index order.</summary>
    public static double Dot(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException($"Vector lengths differ ({left.Length} and {right.Length}).");
        }

        var sum = 0.0;
        for (var i = 0; i < left.Length; i++)
        {
            sum += left[i] * right[i];
        }

        return sum;
    }

    /// <summary>Cosine similarity of two unit vectors: their dot product clamped to [-1, 1].</summary>
    public static double Cosine(ReadOnlySpan<double> unitLeft, ReadOnlySpan<double> unitRight)
    {
        return Math.Clamp(Dot(unitLeft, unitRight), -1.0, 1.0);
    }
}
