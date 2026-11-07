using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>The beds under water (H10): the gain and the muffled share follow the view's amount, and the muffled copy is duller.</summary>
public sealed class UnderwaterSoundTests
{
    [Fact]
    public void Given_TheViewIsAboveWater_When_TheGainIsRead_Then_ItIsNoChange()
    {
        Assert.Equal(1.0, UnderwaterSound.Gain(0), 9);
        Assert.Equal(0.0, UnderwaterSound.Muffle(0), 9);
    }

    [Fact]
    public void Given_TheViewIsFullyUnder_When_TheGainIsRead_Then_TheBedsAreAtTheFullUnderwaterLevel()
    {
        Assert.Equal(UnderwaterSound.FullUnderwaterGain, UnderwaterSound.Gain(1), 9);
        Assert.Equal(1.0, UnderwaterSound.Muffle(1), 9);
    }

    [Fact]
    public void Given_AHalfAmount_When_Read_Then_TheGainAndTheMuffleAreHalfway()
    {
        Assert.Equal(0.75, UnderwaterSound.Gain(0.5), 9);
        Assert.Equal(0.5, UnderwaterSound.Muffle(0.5), 9);
    }

    [Fact]
    public void Given_AnAmountOutOfRangeOrNotANumber_When_Read_Then_ItIsClampedOrNone()
    {
        Assert.Equal(1.0, UnderwaterSound.Gain(-3), 9);
        Assert.Equal(UnderwaterSound.FullUnderwaterGain, UnderwaterSound.Gain(7), 9);
        Assert.Equal(1.0, UnderwaterSound.Gain(double.NaN), 9);
        Assert.Equal(0.0, UnderwaterSound.Muffle(double.NaN), 9);
    }

    [Fact]
    public void Given_TheAmountRises_When_Read_Then_TheGainFallsAndTheMuffleRises()
    {
        var gains = Enumerable.Range(0, 11).Select(i => UnderwaterSound.Gain(i / 10.0)).ToList();
        var muffles = Enumerable.Range(0, 11).Select(i => UnderwaterSound.Muffle(i / 10.0)).ToList();

        Assert.True(gains.Zip(gains.Skip(1)).All(pair => pair.Second <= pair.First));
        Assert.True(muffles.Zip(muffles.Skip(1)).All(pair => pair.Second >= pair.First));
    }

    [Fact]
    public void Given_ALowPassCopy_When_AHighToneIsCompared_Then_ItIsMuchQuieterThanALowOne()
    {
        const int rate = SoundSynth.SampleRate;
        var high = Tone(5000, rate);
        var low = Tone(100, rate);

        var highOut = UnderwaterSound.LowPass(high);
        var lowOut = UnderwaterSound.LowPass(low);

        Assert.True(Rms(highOut) < 0.1 * Rms(high), "the high tone is cut");
        Assert.True(Rms(lowOut) > 0.9 * Rms(low), "the low tone passes");
    }

    [Fact]
    public void Given_ALoop_When_LowPassed_Then_TheLengthIsKeptAndTheResultIsTheSameEveryTime()
    {
        var loop = Tone(300, SoundSynth.SampleRate);

        var first = UnderwaterSound.LowPass(loop);

        Assert.Equal(loop.Length, first.Length);
        Assert.Equal(first, UnderwaterSound.LowPass(loop));
    }

    private static short[] Tone(double frequency, int rate)
    {
        var samples = new short[rate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(10000 * Math.Sin(2 * Math.PI * frequency * i / rate));
        }

        return samples;
    }

    private static double Rms(short[] samples) => Math.Sqrt(samples.Average(static s => s * (double)s));
}
