namespace Slg.Asr;

/// <summary>Platform-independent softmax built from IEEE-754 basic operations only, so scores are bit-identical everywhere.</summary>
internal static class Softmax
{
    private const double Ln2 = 0.6931471805599453;

    /// <summary>Returns the normalized weights exp(x_i - max) / sum, accumulated left to right; an empty input gives an empty result.</summary>
    public static double[] Normalize(IReadOnlyList<double> logScores)
    {
        var count = logScores.Count;
        var weights = new double[count];
        if (count == 0)
        {
            return weights;
        }

        var max = logScores[0];
        for (var i = 1; i < count; i++)
        {
            if (logScores[i] > max)
            {
                max = logScores[i];
            }
        }

        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
            weights[i] = Exp(logScores[i] - max);
            sum += weights[i];
        }

        for (var i = 0; i < count; i++)
        {
            weights[i] /= sum;
        }

        return weights;
    }

    /// <summary>Returns e^x for x at most zero: ln2 range reduction, a fixed 18-term Taylor series and an exact power-of-two scale.</summary>
    public static double Exp(double x)
    {
        if (x < -700.0)
        {
            return 0.0;
        }

        var k = Math.Round(x / Ln2, MidpointRounding.ToEven);
        var r = x - (k * Ln2);
        var term = 1.0;
        var sum = 1.0;
        for (var n = 1; n <= 18; n++)
        {
            term = term * r / n;
            sum += term;
        }

        return Math.ScaleB(sum, (int)k);
    }
}
