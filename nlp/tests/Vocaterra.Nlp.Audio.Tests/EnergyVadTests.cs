namespace Vocaterra.Nlp.Audio.Tests;

/// <summary>Tests for energy-based voice activity trimming.</summary>
public class EnergyVadTests
{
    private const int Frame = 320;

    [Fact]
    public void SilenceToneSilenceTrimsToTonePlusHangoverExactly()
    {
        var tone = TestSignals.Sine(440, 16000, 8000, 8000);
        var signal = TestSignals.Concat(TestSignals.Silence(8000), tone, TestSignals.Silence(8000));

        var result = new EnergyVad().Trim(signal);

        Assert.True(result.HasSpeech);
        Assert.Equal(75, result.FrameCount);
        Assert.Equal(25, result.SpeechStartFrame);
        Assert.Equal(25 + 25 + 5, result.SpeechEndFrame);
        Assert.Equal(30 * Frame, result.Samples.Length);
        Assert.Equal(tone, result.Samples.Take(8000).ToArray());
        Assert.All(result.Samples.Skip(8000), s => Assert.Equal(0, s));
    }

    [Fact]
    public void AllSilenceYieldsEmptyResultWithoutSpeechFlag()
    {
        var result = new EnergyVad().Trim(TestSignals.Silence(24000));

        Assert.False(result.HasSpeech);
        Assert.Empty(result.Samples);
        Assert.Equal(75, result.FrameCount);
        Assert.Equal(0, result.SpeechStartFrame);
        Assert.Equal(0, result.SpeechEndFrame);
    }

    [Fact]
    public void EmptyInputHasNoFramesAndNoSpeech()
    {
        var result = new EnergyVad().Trim([]);

        Assert.False(result.HasSpeech);
        Assert.Equal(0, result.FrameCount);
    }

    [Fact]
    public void HangoverIsCappedAtTheEndOfTheSignalAndPartialFramesCount()
    {
        var signal = TestSignals.Concat(TestSignals.Silence(8000), TestSignals.Sine(440, 16000, 4000, 8000));

        var result = new EnergyVad().Trim(signal);

        Assert.Equal(38, result.FrameCount);
        Assert.Equal(25, result.SpeechStartFrame);
        Assert.Equal(38, result.SpeechEndFrame);
        Assert.Equal(4000, result.Samples.Length);
    }

    [Fact]
    public void ZeroHangoverEndsAtTheLastSpeechFrame()
    {
        var signal = TestSignals.Concat(TestSignals.Silence(8000), TestSignals.Sine(440, 16000, 8000, 8000), TestSignals.Silence(8000));

        var result = new EnergyVad(hangoverFrames: 0).Trim(signal);

        Assert.Equal(25, result.SpeechStartFrame);
        Assert.Equal(50, result.SpeechEndFrame);
        Assert.Equal(8000, result.Samples.Length);
    }

    [Fact]
    public void InteriorPausesLongerThanTheHangoverAreKept()
    {
        var burst = TestSignals.Sine(440, 16000, Frame, 8000);
        var signal = TestSignals.Concat(TestSignals.Silence(Frame * 3), burst, TestSignals.Silence(Frame * 20), burst, TestSignals.Silence(Frame * 9));

        var result = new EnergyVad().Trim(signal);

        Assert.Equal(3, result.SpeechStartFrame);
        Assert.Equal(3 + 1 + 20 + 1 + 5, result.SpeechEndFrame);
        Assert.Equal(burst, result.Samples.Take(Frame).ToArray());
    }

    [Fact]
    public void ThresholdBoundaryIsExactAroundMinusFortyDbfs()
    {
        // -40 dBFS RMS of a constant signal is 327.68, so 328 is speech and 327 is not.
        Assert.True(new EnergyVad().Trim(Enumerable.Repeat((short)328, Frame).ToArray()).HasSpeech);
        Assert.False(new EnergyVad().Trim(Enumerable.Repeat((short)327, Frame).ToArray()).HasSpeech);
        Assert.True(new EnergyVad().Trim(Enumerable.Repeat((short)-328, Frame).ToArray()).HasSpeech);
    }

    [Fact]
    public void ThresholdIsConfigurable()
    {
        var quiet = TestSignals.Sine(440, 16000, 3200, 100);

        Assert.False(new EnergyVad().Trim(quiet).HasSpeech);
        Assert.True(new EnergyVad(thresholdDbfs: -60.0).Trim(quiet).HasSpeech);
    }

    [Fact]
    public void FrameLengthFollowsTheSampleRate()
    {
        Assert.Equal(320, new EnergyVad().FrameSamples);
        Assert.Equal(160, new EnergyVad(sampleRate: 8000).FrameSamples);
        Assert.Equal(480, new EnergyVad(frameMs: 30).FrameSamples);
    }

    [Fact]
    public void RunTwiceGivesIdenticalResults()
    {
        var signal = TestSignals.Concat(TestSignals.Silence(5000), TestSignals.Sine(300, 16000, 6000, 3000), TestSignals.Silence(5000));

        Assert.Equal(new EnergyVad().Trim(signal), new EnergyVad().Trim(signal));
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyVad(frameMs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyVad(thresholdDbfs: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyVad(hangoverFrames: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyVad(sampleRate: 0));
        Assert.Throws<ArgumentNullException>(() => new EnergyVad().Trim(null!));
    }
}
