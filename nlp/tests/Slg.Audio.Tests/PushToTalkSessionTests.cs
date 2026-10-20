namespace Slg.Audio.Tests;

/// <summary>Tests for the push-to-talk session and the one-call front-end.</summary>
public class PushToTalkSessionTests
{
    private static readonly short[] Utterance = TestSignals.Concat(
        TestSignals.Silence(4000),
        TestSignals.Sine(440, 16000, 8000, 6000),
        TestSignals.Silence(4000));

    [Fact]
    public void SessionAppliesGainThenVadThenHash()
    {
        var session = new PushToTalkSession();
        session.Begin();
        session.Append(Utterance);

        var clip = session.End();

        var expected = new EnergyVad().Trim(GainNormalizer.Normalize(Utterance));
        Assert.Equal(16000, clip.SampleRate);
        Assert.Equal(expected.Samples, clip.Samples);
        Assert.Equal(ClipHasher.Hash(clip.Samples), clip.ClipHash);
        Assert.Equal(expected, session.LastVadResult);
        Assert.InRange(TestSignals.Peak(clip.Samples), 23197, 23199);
        Assert.Equal(12, expected.SpeechStartFrame);
        Assert.Equal(43, expected.SpeechEndFrame);
    }

    [Fact]
    public void ChunkedAppendsEqualASingleAppend()
    {
        var whole = new PushToTalkSession();
        whole.Begin();
        whole.Append(Utterance);
        var expected = whole.End();

        var chunked = new PushToTalkSession();
        chunked.Begin();
        for (var offset = 0; offset < Utterance.Length; offset += 777)
        {
            chunked.Append(Utterance.AsSpan(offset, Math.Min(777, Utterance.Length - offset)));
        }

        Assert.Equal(expected, chunked.End());
    }

    [Fact]
    public void QuietSpeechIsLiftedAboveTheVadThresholdByTheGainStage()
    {
        var quiet = TestSignals.Concat(TestSignals.Silence(3200), TestSignals.Sine(440, 16000, 3200, 200), TestSignals.Silence(3200));
        Assert.False(new EnergyVad().Trim(quiet).HasSpeech);

        var session = new PushToTalkSession();
        session.Begin();
        session.Append(quiet);
        var clip = session.End();

        Assert.True(session.LastVadResult!.HasSpeech);
        Assert.InRange(TestSignals.Peak(clip.Samples), 1900, 2000);
    }

    [Fact]
    public void RecordingWithoutSpeechYieldsAnEmptyClip()
    {
        var session = new PushToTalkSession();
        session.Begin();
        session.Append(TestSignals.Silence(16000));

        var clip = session.End();

        Assert.Empty(clip.Samples);
        Assert.Equal(ClipHasher.Hash([]), clip.ClipHash);
        Assert.False(session.LastVadResult!.HasSpeech);
    }

    [Fact]
    public void CallsOutOfOrderAreRejected()
    {
        var session = new PushToTalkSession();
        Assert.Throws<InvalidOperationException>(() => session.Append(Utterance));
        Assert.Throws<InvalidOperationException>(() => session.End());

        session.Begin();
        Assert.True(session.IsRecording);
        session.End();
        Assert.False(session.IsRecording);
        Assert.Throws<InvalidOperationException>(() => session.End());
    }

    [Fact]
    public void BeginDiscardsAnUnfinishedRecordingAndSessionsAreReusable()
    {
        var session = new PushToTalkSession();
        session.Begin();
        session.Append(TestSignals.Sine(300, 16000, 8000, 9000));
        session.Begin();
        session.Append(Utterance);
        var first = session.End();

        session.Begin();
        session.Append(Utterance);
        var second = session.End();

        Assert.Equal(first, second);
        Assert.Equal(first.ClipHash, second.ClipHash);
    }

    [Fact]
    public void FrontEndDownmixesResamplesAndProcesses()
    {
        var left = TestSignals.Concat(TestSignals.Silence(24000), TestSignals.Sine(1000, 48000, 24000, 8000), TestSignals.Silence(24000));
        var wav = new WavData(48000, 2, TestSignals.Interleave(left, left));

        var clip = AudioFrontEnd.Process(wav);

        var mono = Downmixer.ToMono(wav);
        var session = new PushToTalkSession();
        session.Begin();
        session.Append(Resampler.To16k(mono, 48000));
        Assert.Equal(session.End(), clip);
        Assert.Equal(16000, clip.SampleRate);
        Assert.NotEmpty(clip.Samples);
    }

    [Fact]
    public void FrontEndOfCancellingChannelsIsEmpty()
    {
        var tone = TestSignals.Sine(1000, 48000, 24000, 8000);
        var wav = new WavData(48000, 2, TestSignals.Interleave(tone, tone.Select(s => (short)-s).ToArray()));

        Assert.Empty(AudioFrontEnd.Process(wav).Samples);
    }

    [Fact]
    public void FrontEndAtSixteenKilohertzSkipsResampling()
    {
        var clip = AudioFrontEnd.Process(new WavData(16000, 1, Utterance));

        var session = new PushToTalkSession();
        session.Begin();
        session.Append(Utterance);
        Assert.Equal(session.End(), clip);
    }

    [Fact]
    public void FrontEndOptionsAreHonoured()
    {
        var options = new AudioFrontEndOptions { TargetPeakDbfs = -12.0, HangoverFrames = 0 };

        var clip = AudioFrontEnd.Process(new WavData(16000, 1, Utterance), options);

        Assert.InRange(TestSignals.Peak(clip.Samples), 8229, 8232);
        Assert.Equal(8320, clip.Samples.Length);
    }
}
