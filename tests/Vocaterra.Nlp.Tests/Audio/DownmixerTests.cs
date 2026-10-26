namespace Vocaterra.Nlp.Audio.Tests;

/// <summary>Tests for the fixed stereo-to-mono downmix.</summary>
public class DownmixerTests
{
    [Fact]
    public void StereoUsesArithmeticShiftOfTheSum()
    {
        var wav = new WavData(16000, 2, [10, 20, 3, 4, -3, -4, -3, -2, 32767, 32767, -32768, -32768, 32767, -32768, 1, 0]);

        var mono = Downmixer.ToMono(wav);

        // (3 + 4) >> 1 = 3; (-3 + -4) >> 1 = -4 (floor); (-3 + -2) >> 1 = -3; (32767 + -32768) >> 1 = -1; (1 + 0) >> 1 = 0.
        Assert.Equal(new short[] { 15, 3, -4, -3, 32767, -32768, -1, 0 }, mono);
    }

    [Fact]
    public void MonoIsCopiedUnchanged()
    {
        short[] samples = [1, -2, 3];
        var wav = new WavData(16000, 1, samples);

        var mono = Downmixer.ToMono(wav);

        Assert.Equal(samples, mono);
        Assert.NotSame(samples, mono);
    }

    [Fact]
    public void OppositeChannelsCancelExactly()
    {
        var tone = TestSignals.Sine(440, 16000, 1600, 9000);
        var inverted = tone.Select(s => (short)-s).ToArray();

        var mono = Downmixer.ToMono(new WavData(16000, 2, TestSignals.Interleave(tone, inverted)));

        Assert.All(mono, s => Assert.Equal(0, s));
    }

    [Fact]
    public void RaggedStereoDataIsRejected()
    {
        Assert.Throws<ArgumentException>(() => Downmixer.ToMono(new WavData(16000, 2, [1, 2, 3])));
    }
}
