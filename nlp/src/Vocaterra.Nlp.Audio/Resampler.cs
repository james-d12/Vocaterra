namespace Vocaterra.Nlp.Audio;

/// <summary>
/// Converts mono 16-bit audio to 16 kHz with a fixed windowed-sinc polyphase rational resampler.
/// <para>
/// Design (identical for every source rate, including the integer-ratio ones, so there is a single code path):
/// the rate ratio is reduced to <c>up/down</c> by the greatest common divisor (for example 48 000 gives 1/3,
/// 44 100 gives 160/441, 11 025 gives 640/441, 8 000 gives 2/1). Output sample <c>n</c> is evaluated at input
/// time <c>n * down / up</c> as a weighted sum of the surrounding input samples (zero-padded at both ends, so
/// the filter is zero-phase and adds no delay). The kernel is <c>h(t) = 2fc * sinc(2fc * t) * kaiser(t / H)</c>
/// with <c>t</c> in input samples, a low-pass cutoff of <see cref="CutoffFraction"/> times the lower of the two
/// sample rates (7 200 Hz when going to 16 kHz from anything above it), and a Kaiser window of
/// <see cref="KaiserBeta"/> over <c>H = ceil(ZeroCrossings / (2 fc))</c> input samples per side, giving
/// <c>2H</c> taps per output sample (for example 160 taps at 48 kHz) and roughly 80 dB of stopband attenuation.
/// Each polyphase row is normalized to sum to exactly one so constant (DC) input is preserved.
/// </para>
/// <para>
/// Determinism: all arithmetic is <see cref="double"/>; sine and Bessel terms come from
/// <c>DeterministicMath</c> (basic IEEE operations only); sums run in ascending tap order; the result is rounded
/// with <see cref="MidpointRounding.ToEven"/> and clamped to the 16-bit range. The output length is
/// <c>floor(input length * up / down)</c>.
/// </para>
/// </summary>
public static class Resampler
{
    /// <summary>The pipeline sample rate in hertz.</summary>
    public const int TargetRate = 16000;

    /// <summary>Number of sinc zero crossings kept on each side of the kernel centre.</summary>
    public const int ZeroCrossings = 24;

    /// <summary>Kaiser window shape parameter.</summary>
    public const double KaiserBeta = 8.0;

    /// <summary>Low-pass cutoff as a fraction of the lower of the source and target sample rates.</summary>
    public const double CutoffFraction = 0.45;

    /// <summary>Resamples mono audio recorded at <paramref name="sourceRate"/> hertz to 16 kHz; a 16 kHz input is returned as a copy.</summary>
    public static short[] To16k(short[] mono, int sourceRate)
    {
        ArgumentNullException.ThrowIfNull(mono);
        if (sourceRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRate), sourceRate, "Sample rate must be positive.");
        }

        if (sourceRate == TargetRate)
        {
            return (short[])mono.Clone();
        }

        var divisor = Gcd(sourceRate, TargetRate);
        var up = TargetRate / divisor;
        var down = sourceRate / divisor;
        var cutoff = CutoffFraction * Math.Min(sourceRate, TargetRate) / sourceRate;
        var half = (int)Math.Ceiling(ZeroCrossings / (2.0 * cutoff));
        var outputLength = checked((int)((long)mono.Length * up / down));

        var output = new short[outputLength];
        var bank = new double[up][];
        var windowNorm = DeterministicMath.BesselI0(KaiserBeta);
        for (var n = 0; n < outputLength; n++)
        {
            var position = (long)n * down;
            var index = position / up;
            var phase = (int)(position % up);
            var taps = bank[phase] ??= BuildTaps(phase, up, half, cutoff, windowNorm);
            var first = index - half + 1;
            var accumulator = 0.0;
            for (var k = 0; k < taps.Length; k++)
            {
                var source = first + k;
                if (source < 0)
                {
                    continue;
                }

                if (source >= mono.Length)
                {
                    break;
                }

                accumulator += mono[source] * taps[k];
            }

            output[n] = ToInt16(accumulator);
        }

        return output;
    }

    internal static short ToInt16(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.ToEven);
        return rounded >= short.MaxValue ? short.MaxValue : rounded <= short.MinValue ? short.MinValue : (short)rounded;
    }

    private static double[] BuildTaps(int phase, int up, int half, double cutoff, double windowNorm)
    {
        var fraction = (double)phase / up;
        var taps = new double[2 * half];
        var sum = 0.0;
        for (var k = 0; k < taps.Length; k++)
        {
            var t = (k - half + 1) - fraction;
            var u = 2.0 * cutoff * t;
            var sinc = u == 0.0 ? 2.0 * cutoff : 2.0 * cutoff * DeterministicMath.SinPi(u) / (Math.PI * u);
            var ratio = t / half;
            var shape = Math.Max(0.0, 1.0 - (ratio * ratio));
            var window = DeterministicMath.BesselI0(KaiserBeta * Math.Sqrt(shape)) / windowNorm;
            taps[k] = sinc * window;
            sum += taps[k];
        }

        for (var k = 0; k < taps.Length; k++)
        {
            taps[k] /= sum;
        }

        return taps;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
