namespace Slg.Audio;

/// <summary>
/// Transcendental helpers built only from IEEE-754 basic operations (add, subtract, multiply, divide,
/// floor, round, scale-by-power-of-two), so results are bit-identical on every platform and runtime.
/// <see cref="Math.Sin(double)"/> and <see cref="Math.Exp(double)"/> are not used because their last
/// bit may differ between C runtimes and CPU architectures.
/// </summary>
internal static class DeterministicMath
{
    /// <summary>Natural logarithm of 2.</summary>
    public const double Ln2 = 0.6931471805599453;

    /// <summary>Natural logarithm of 10.</summary>
    public const double Ln10 = 2.302585092994046;

    /// <summary>Returns sin(pi * x) using range reduction and a fixed 13-term Taylor series (error below 1e-16).</summary>
    public static double SinPi(double x)
    {
        var r = x - (2.0 * Math.Floor(x / 2.0));
        var negative = false;
        if (r >= 1.0)
        {
            r -= 1.0;
            negative = true;
        }

        if (r > 0.5)
        {
            r = 1.0 - r;
        }

        var theta = Math.PI * r;
        var theta2 = theta * theta;
        var term = theta;
        var sum = theta;
        for (var k = 1; k <= 12; k++)
        {
            term = term * -theta2 / ((2.0 * k) * ((2.0 * k) + 1.0));
            sum += term;
        }

        return negative ? -sum : sum;
    }

    /// <summary>Returns e^x using ln2 range reduction, a fixed 18-term Taylor series and an exact power-of-two scale.</summary>
    public static double Exp(double x)
    {
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

    /// <summary>Converts decibels of amplitude to a linear amplitude ratio, 10^(db/20).</summary>
    public static double DbToAmplitude(double db)
    {
        return Exp(db * (Ln10 / 20.0));
    }

    /// <summary>Converts decibels of power to a linear power ratio, 10^(db/10).</summary>
    public static double DbToPower(double db)
    {
        return Exp(db * (Ln10 / 10.0));
    }

    /// <summary>Returns the modified Bessel function of the first kind, order zero, using a fixed 40-term power series.</summary>
    public static double BesselI0(double x)
    {
        var quarter = x * x / 4.0;
        var term = 1.0;
        var sum = 1.0;
        for (var k = 1; k <= 40; k++)
        {
            term = term * quarter / ((double)k * k);
            sum += term;
        }

        return sum;
    }
}
