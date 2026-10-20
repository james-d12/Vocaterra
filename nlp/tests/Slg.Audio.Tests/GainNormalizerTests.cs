namespace Slg.Audio.Tests;

/// <summary>Tests for peak normalization.</summary>
public class GainNormalizerTests
{
    [Fact]
    public void KnownPeakIsScaledToTheTarget()
    {
        short[] input = [0, 5000, -10000, 2500];
        var targetPeak = 32767 * Math.Pow(10, -3.0 / 20.0);
        var gain = targetPeak / 10000;

        var output = GainNormalizer.Normalize(input);

        Assert.Equal(input.Length, output.Length);
        for (var i = 0; i < input.Length; i++)
        {
            Assert.InRange((double)output[i], Math.Round(input[i] * gain) - 1, Math.Round(input[i] * gain) + 1);
        }

        Assert.InRange(TestSignals.Peak(output), 23197, 23199);
        Assert.Equal(0, output[0]);
    }

    [Fact]
    public void GainIsCappedAtTheMaximum()
    {
        short[] input = [0, 100, -50, 20];

        Assert.Equal(new short[] { 0, 1000, -500, 200 }, GainNormalizer.Normalize(input));
        Assert.Equal(new short[] { 0, 200, -100, 40 }, GainNormalizer.Normalize(input, -3.0, 6.0206));
    }

    [Fact]
    public void ZeroMaxGainLeavesLevelsAlone()
    {
        short[] input = [0, 100, -50, 20];

        Assert.Equal(input, GainNormalizer.Normalize(input, -3.0, 0.0));
    }

    [Fact]
    public void LoudInputIsAttenuatedToTheTarget()
    {
        var output = GainNormalizer.Normalize([32767, -32768, 100]);

        Assert.InRange(TestSignals.Peak(output), 23197, 23199);
    }

    [Fact]
    public void FullScaleTargetKeepsAFullScaleInputUnchanged()
    {
        short[] input = [32767, -1000, 5];

        Assert.Equal(input, GainNormalizer.Normalize(input, 0.0));
    }

    [Fact]
    public void SilenceAndEmptyInputAreReturnedUnchanged()
    {
        short[] silence = new short[100];

        var output = GainNormalizer.Normalize(silence);

        Assert.Equal(silence, output);
        Assert.NotSame(silence, output);
        Assert.Empty(GainNormalizer.Normalize([]));
    }

    [Fact]
    public void InputIsNeverMutated()
    {
        short[] input = [100, -200, 300];
        var copy = (short[])input.Clone();

        _ = GainNormalizer.Normalize(input);

        Assert.Equal(copy, input);
    }

    [Fact]
    public void RunTwiceGivesIdenticalOutput()
    {
        var input = TestSignals.Sine(300, 16000, 4000, 1234);

        Assert.Equal(GainNormalizer.Normalize(input), GainNormalizer.Normalize(input));
    }

    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GainNormalizer.Normalize([1], 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GainNormalizer.Normalize([1], double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => GainNormalizer.Normalize([1], -3.0, -1.0));
        Assert.Throws<ArgumentNullException>(() => GainNormalizer.Normalize(null!));
    }
}
