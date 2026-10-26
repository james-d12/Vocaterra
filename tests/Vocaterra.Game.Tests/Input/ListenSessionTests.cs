using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Input;
using Vocaterra.Nlp.Core.Audio;

namespace Vocaterra.Game.Tests.Input;

/// <summary>Push-to-talk: a recording for one target, ended by its key going up or by the length cap, the level while it runs, and a cancel that sends nothing.</summary>
public sealed class ListenSessionTests
{
    private const int Rate = 16000;

    [Fact]
    public void TheClipGoesToTheTargetWhenTheKeyGoesUpAndNotWhileItIsHeld()
    {
        var listen = new ListenSession(new PushToTalkSpeechCapture(new FakeCapture(Tone(1.0), Tone(0.5))));
        var heard = new List<PcmClip?>();
        var levels = new List<double>();
        Assert.True(listen.Begin(new ListenTarget(heard.Add, (level, _) => levels.Add(level)), Keys.V));
        Assert.True(listen.Listening);

        listen.Pump(1 / 60.0);
        listen.ReleaseIfUp(Held(Keys.V));
        Assert.Empty(heard);
        Assert.Single(levels);
        Assert.True(levels[0] > 0);

        listen.ReleaseIfUp(Held());
        Assert.False(listen.Listening);
        Assert.NotNull(Assert.Single(heard));
        listen.Pump(1 / 60.0);
        Assert.Single(levels); // the recording is over: nothing more is pumped
    }

    [Fact]
    public void TheRecordingEndsAtItsLengthCapAndTheClipGoesToTheTarget()
    {
        var seconds = Enumerable.Range(0, 12).Select(_ => Tone(1.0)).ToArray();
        var listen = new ListenSession(new PushToTalkSpeechCapture(new FakeCapture(seconds)));
        var heard = new List<PcmClip?>();
        Assert.True(listen.Begin(new ListenTarget(heard.Add), Keys.Space));
        for (var frame = 0; frame < 12 && heard.Count == 0; frame++)
        {
            listen.Pump(1 / 60.0);
        }

        Assert.False(listen.Listening);
        Assert.NotNull(Assert.Single(heard));
    }

    [Fact]
    public void ACancelledRecordingIsDroppedAndTheTargetHearsNothing()
    {
        var listen = new ListenSession(new PushToTalkSpeechCapture(new FakeCapture(Tone(1.0))));
        var heard = new List<PcmClip?>();
        Assert.True(listen.Begin(new ListenTarget(heard.Add), Keys.V));
        listen.Pump(1 / 60.0);
        listen.Cancel();
        Assert.False(listen.Listening);
        listen.ReleaseIfUp(Held());
        listen.Pump(1 / 60.0);
        Assert.Empty(heard);
    }

    [Fact]
    public void WithoutAMicrophoneNothingStartsAndTheProblemIsSaid()
    {
        var listen = new ListenSession(new PushToTalkSpeechCapture(null));
        var heard = new List<PcmClip?>();
        Assert.False(listen.Begin(new ListenTarget(heard.Add), Keys.V));
        Assert.Equal(PushToTalkSpeechCapture.NoMicrophone, listen.Problem);
        listen.Pump(1 / 60.0);
        Assert.Empty(heard);
    }

    private static InputSnapshot Held(params Keys[] keys) => new(new KeyboardState(keys), default, new KeyboardState(keys), false, false, 0);

    private static short[] Tone(double seconds)
    {
        var samples = new short[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(8000 * Math.Sin(2 * Math.PI * 300 * i / Rate));
        }

        return samples;
    }

    private sealed class FakeCapture(params short[][] chunks) : ISpeechCapture
    {
        private int _next;

        public int SampleRate => Rate;

        public void Start()
        {
        }

        public short[] ReadAvailable() => _next < chunks.Length ? chunks[_next++] : [];

        public void Stop()
        {
        }
    }
}
