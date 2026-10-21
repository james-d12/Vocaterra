namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// Per-class temperature that turns a goodness-of-pronunciation value (a log-probability gap, at most 0) into a score in (0, 1]:
/// <c>score = exp(gop / temperature)</c>. A larger temperature forgives a larger gap.
/// </summary>
/// <remarks>
/// Default temperatures: vowel 2.0, tap_trill 3.0, fricative 2.5, stop 2.0, nasal 2.0, lateral 2.5, glide 2.5. A gap of -0.69
/// (the intended phone half as likely as the winner) scores about .71 for a vowel and .79 for a tap; a gap of -2.3 (ten times less
/// likely) scores about .32 and .46.
/// </remarks>
public sealed class GopCalibration
{
    private static readonly double[] DefaultTemperatures = [2.0, 3.0, 2.5, 2.0, 2.0, 2.5, 2.5];

    private readonly double[] temperatures;

    private GopCalibration(double[] temperatures)
    {
        this.temperatures = temperatures;
    }

    /// <summary>The shipped calibration.</summary>
    public static GopCalibration Default { get; } = new(DefaultTemperatures);

    /// <summary>Returns the temperature of a phone class.</summary>
    public double Temperature(PhoneClass phoneClass)
    {
        var index = (int)phoneClass;
        if (index < 0 || index >= temperatures.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(phoneClass), phoneClass, "Unknown phone class.");
        }

        return temperatures[index];
    }

    /// <summary>Returns a copy with one temperature replaced; the temperature must be finite and positive.</summary>
    public GopCalibration With(PhoneClass phoneClass, double temperature)
    {
        if (!double.IsFinite(temperature) || temperature <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "Temperature must be finite and positive.");
        }

        var copy = (double[])temperatures.Clone();
        copy[(int)phoneClass] = temperature;
        return new GopCalibration(copy);
    }

    /// <summary>Maps a goodness-of-pronunciation value (at most 0) to a score in (0, 1] for a phone token.</summary>
    public double ScoreOf(string phone, double gop)
    {
        var temperature = Temperature(PhoneClasses.Of(phone));
        return Math.Exp(Math.Min(gop, 0.0) / temperature);
    }
}
