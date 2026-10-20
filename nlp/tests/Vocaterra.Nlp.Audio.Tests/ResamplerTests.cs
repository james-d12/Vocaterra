namespace Vocaterra.Nlp.Audio.Tests;

/// <summary>Tests for the fixed-kernel 16 kHz resampler.</summary>
public class ResamplerTests
{
    /// <summary>First 64 output samples of a 0.5 s, 1 kHz, amplitude 16384 sine resampled from 48 kHz (self-recorded).</summary>
    private static readonly short[] Golden48kSineFirst64 =
    [
        676, 6114, 11686, 15059, 16447, 15089, 11619, 6248, 11, -6274, -11588, -15131, -16392, -15130, -11592, -6265,
        -3, 6272, 11585, 15137, 16385, 15137, 11586, 6270, 0, -6270, -11585, -15137, -16385, -15137, -11585, -6270,
        0, 6270, 11585, 15137, 16385, 15137, 11585, 6270, 0, -6270, -11585, -15137, -16385, -15137, -11585, -6270,
        0, 6270, 11585, 15137, 16385, 15137, 11585, 6270, 0, -6270, -11585, -15137, -16385, -15137, -11585, -6270,
    ];

    private const string Golden48kSineHash = "2a66231dbd90a88be63cdce16c2884f876a58f4a95696bb55226840e56385ab5";

    [Fact]
    public void FortyEightKilohertzSineMatchesGoldenOutput()
    {
        var input = TestSignals.Sine(1000, 48000, 24000, 16384);

        var output = Resampler.To16k(input, 48000);

        Assert.Equal(8000, output.Length);
        Assert.Equal(Golden48kSineFirst64, output.Take(64).ToArray());
        Assert.Equal(Golden48kSineHash, ClipHasher.Hash(output));
        Assert.Equal(output, Resampler.To16k(input, 48000));
    }

    [Fact]
    public void FortyEightKilohertzSineHasPeriodOfSixteenSamples()
    {
        var output = Resampler.To16k(TestSignals.Sine(1000, 48000, 24000, 16384), 48000);

        Assert.Equal(16, DominantPeriod(output));
    }

    [Theory]
    [InlineData(8000)]
    [InlineData(11025)]
    [InlineData(22050)]
    [InlineData(32000)]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void CommonRatesProduceExactLengthAndPreserveATone(int sourceRate)
    {
        var input = TestSignals.Sine(1000, sourceRate, sourceRate, 10000);

        var output = Resampler.To16k(input, sourceRate);

        Assert.Equal(16000, output.Length);
        Assert.Equal(16, DominantPeriod(output));
        var interior = output.Skip(400).Take(15200).Select(s => Math.Abs((int)s)).Max();
        Assert.InRange(interior, 9950, 10050);
        Assert.Equal(output, Resampler.To16k(input, sourceRate));
    }

    [Fact]
    public void LengthIsFlooredForNonIntegerRatios()
    {
        Assert.Equal(2, Resampler.To16k(new short[7], 44100).Length);
        Assert.Equal(16, Resampler.To16k(new short[45], 44100).Length);
        Assert.Equal(20, Resampler.To16k(new short[10], 8000).Length);
        Assert.Empty(Resampler.To16k([], 48000));
    }

    [Fact]
    public void SixteenKilohertzInputIsReturnedAsACopy()
    {
        short[] input = [1, -2, 3, 32767, -32768];

        var output = Resampler.To16k(input, 16000);

        Assert.Equal(input, output);
        Assert.NotSame(input, output);
    }

    [Fact]
    public void ConstantInputIsPreservedAwayFromTheEdges()
    {
        foreach (var rate in new[] { 8000, 11025, 44100, 48000 })
        {
            var output = Resampler.To16k(Enumerable.Repeat((short)1000, rate / 2).ToArray(), rate);

            Assert.All(output.Skip(150).Take(output.Length - 300), s => Assert.Equal(1000, s));
        }
    }

    [Fact]
    public void ToneAboveTheTargetNyquistIsRejected()
    {
        var output = Resampler.To16k(TestSignals.Sine(10000, 48000, 48000, 16384), 48000);

        var interior = output.Skip(400).Take(15200).Select(s => Math.Abs((int)s)).Max();
        Assert.True(interior <= 8, $"aliased energy too high: {interior}");
    }

    [Fact]
    public void OutputIsClampedToSixteenBits()
    {
        var output = Resampler.To16k(Enumerable.Repeat(short.MaxValue, 4800).ToArray(), 48000);

        Assert.All(output, s => Assert.InRange(s, (short)0, short.MaxValue));
    }

    [Fact]
    public void InvalidRatesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Resampler.To16k([1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Resampler.To16k([1], -48000));
        Assert.Throws<ArgumentNullException>(() => Resampler.To16k(null!, 48000));
    }

    private static int DominantPeriod(short[] samples)
    {
        const int start = 200;
        const int window = 4000;
        var best = 0;
        var bestScore = long.MinValue;
        for (var lag = 2; lag <= 24; lag++)
        {
            long score = 0;
            for (var i = start; i < start + window; i++)
            {
                score += (long)samples[i] * samples[i + lag];
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = lag;
            }
        }

        return best;
    }
}
