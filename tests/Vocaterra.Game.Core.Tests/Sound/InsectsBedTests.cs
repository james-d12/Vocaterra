using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>
/// The evening insects' loop (E10): crickets chirping with rests between, never a steady high tone (the owner heard the first version as a constant ringing).
/// </summary>
public sealed class InsectsBedTests
{
    private const int Rate = SoundSynth.SampleRate;

    [Fact]
    public void Given_TheInsectsLoop_When_ItIsMadeTwice_Then_ItIsTheSameAndFullLength()
    {
        var first = AmbientLoops.Render(AmbientBeds.Insects);
        var second = AmbientLoops.Render(AmbientBeds.Insects);

        Assert.Equal((int)(AmbientLoops.LoopSeconds * Rate), first.Length);
        Assert.Equal(first, second);
        Assert.Contains(first, static sample => sample != 0);
    }

    [Fact]
    public void Given_TheInsectsLoop_When_ItIsSplitIntoTwentyMillisecondWindows_Then_AtLeastFortyPercentAreQuiet()
    {
        var loop = AmbientLoops.Render(AmbientBeds.Insects);
        var window = Rate / 50;
        var rms = new List<double>();
        for (var start = 0; start + window <= loop.Length; start += window)
        {
            var sum = 0.0;
            for (var i = start; i < start + window; i++)
            {
                sum += (double)loop[i] * loop[i];
            }

            rms.Add(Math.Sqrt(sum / window));
        }

        var loudest = rms.Max();
        var quiet = rms.Count(r => r < loudest / 10);

        Assert.True(quiet >= 0.4 * rms.Count, $"{quiet} of {rms.Count} windows are quiet: the bed holds a steady tone");
    }

    [Fact]
    public void Given_TheInsectsLoop_When_ItsSpectrumIsRead_Then_NoFiftyHertzBandHoldsAThirdOfTheEnergyBetweenTwoAndSixKilohertz()
    {
        var loop = AmbientLoops.Render(AmbientBeds.Insects);
        const int size = 1 << 18;
        var re = new double[size];
        var im = new double[size];
        for (var i = 0; i < loop.Length; i++)
        {
            var hann = 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (loop.Length - 1)));
            re[i] = loop[i] * hann;
        }

        Fft(re, im);

        double Binhz(int k) => k * (double)Rate / size;
        var first = (int)Math.Ceiling(2000 * size / (double)Rate);
        var last = (int)Math.Floor(6000 * size / (double)Rate);
        var power = new double[last - first + 1];
        var total = 0.0;
        for (var k = first; k <= last; k++)
        {
            power[k - first] = (re[k] * re[k]) + (im[k] * im[k]);
            total += power[k - first];
        }

        var width = (int)Math.Round(50 * size / (double)Rate);
        var best = 0.0;
        var bestAt = 0;
        var run = power.Take(width).Sum();
        for (var k = 0; k + width < power.Length; k++)
        {
            if (run > best)
            {
                best = run;
                bestAt = k;
            }

            run += power[k + width] - power[k];
        }

        Assert.True(best <= total / 3, $"The band at {Binhz(first + bestAt):F0} Hz holds {best / total:P0} of the energy: the bed rings on one pitch");
    }

    private static void Fft(double[] re, double[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wr = Math.Cos(angle);
            var wi = Math.Sin(angle);
            for (var i = 0; i < n; i += length)
            {
                double cr = 1, ci = 0;
                for (var k = 0; k < length / 2; k++)
                {
                    var a = i + k;
                    var b = a + (length / 2);
                    var tr = (re[b] * cr) - (im[b] * ci);
                    var ti = (re[b] * ci) + (im[b] * cr);
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                    (cr, ci) = ((cr * wr) - (ci * wi), (cr * wi) + (ci * wr));
                }
            }
        }
    }
}
