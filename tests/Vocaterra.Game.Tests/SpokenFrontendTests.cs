using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Input;
using Vocaterra.Game.Utilities;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>A fact that needs the speech models: skipped, with the reason in the report, when <c>VOCATERRA_MODELS_DIR</c> or a model is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the fact; <paramref name="needsKokoro"/> is <see langword="false"/> for a test that only recognises a recorded WAV (Vosk alone).</summary>
    public ModelFactAttribute(bool needsKokoro = true)
    {
        var store = SpeechEngine.FindModels(out var problem);
        if (store is null)
        {
            Skip = $"Speech models unavailable: {problem}";
        }
        else if (!Directory.Exists(store.ModelDirectory(SpeechEngine.VoskModelId)) || store.Manifest.Find(SpeechEngine.VoskModelId) is null)
        {
            Skip = $"Speech models unavailable: {SpeechEngine.VoskModelId} is not in {store.RootDirectory}.";
        }
        else if (needsKokoro && (!Directory.Exists(store.ModelDirectory(SpeechEngine.KokoroModelId)) || store.Manifest.Find(SpeechEngine.KokoroModelId) is null))
        {
            Skip = $"Speech models unavailable: {SpeechEngine.KokoroModelId} is not in {store.RootDirectory}.";
        }
    }
}

/// <summary>
/// Spoken input in the front-end without a window or a microphone: the push-to-talk state with a fake device, the panel's thinking and
/// listening states, the new launch options, and the scripted run's <c>--wav</c> and <c>--speak</c> steps driven the way the game drives them.
/// </summary>
[Collection("speech models")]
public sealed class SpokenFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private sealed class FakeCapture(int sampleRate, params short[][] chunks) : ISpeechCapture
    {
        private int _next;

        public int SampleRate => sampleRate;

        public bool Running { get; private set; }

        public bool FailToStart { get; set; }

        public int Starts { get; private set; }

        public void Start()
        {
            if (FailToStart)
            {
                throw new InvalidOperationException("device busy");
            }

            Running = true;
            Starts++;
        }

        public short[] ReadAvailable() => _next < chunks.Length ? chunks[_next++] : [];

        public void Stop() => Running = false;
    }

    private static short[] Tone(double seconds, int rate)
    {
        var samples = new short[(int)(seconds * rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(8000 * Math.Sin(2 * Math.PI * 300 * i / rate));
        }

        return samples;
    }

    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    [Fact]
    public void LibVoskIsCopiedNextToTheFrontEndTestsByTheDirectPackageReference()
    {
        if (OperatingSystem.IsLinux())
        {
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "libvosk.so")), "libvosk.so is not in the output: add the direct Vosk reference.");
        }
    }

    [Fact]
    public void HoldingTheKeyRecordsAndLettingGoGivesATrimmed16KilohertzClip()
    {
        var capture = new FakeCapture(44100, new short[44100], Tone(1.0, 44100), new short[44100]);
        var talk = new PushToTalkSpeechCapture(capture);

        Assert.True(talk.Begin());
        Assert.True(talk.Listening);
        Assert.True(capture.Running);
        talk.Pump();
        talk.Pump();
        talk.Pump();
        var clip = talk.End();

        Assert.False(talk.Listening);
        Assert.False(capture.Running);
        Assert.NotNull(clip);
        Assert.Equal(16000, clip.SampleRate);
        Assert.InRange(clip.DurationSeconds, 1.2, 1.4); // a second of tone and 120 ms of padding either side; the seconds of silence are cut
        Assert.Null(talk.Problem);
    }

    [Fact]
    public void AClipOfSilenceGivesNoClipAndAMessage()
    {
        var talk = new PushToTalkSpeechCapture(new FakeCapture(16000, new short[32000]));

        talk.Begin();
        talk.Pump();

        Assert.Null(talk.End());
        Assert.Equal(PushToTalkSpeechCapture.Silence, talk.Problem);
    }

    [Fact]
    public void RecordingStopsAtTheLengthCapAndTheClipIsAtMostEightSeconds()
    {
        var seconds = Enumerable.Range(0, 12).Select(_ => Tone(1.0, 16000)).ToArray();
        var capture = new FakeCapture(16000, seconds);
        var talk = new PushToTalkSpeechCapture(capture);

        talk.Begin();
        var pumps = 0;
        while (talk.Listening && pumps < 12)
        {
            talk.Pump();
            pumps++;
        }

        Assert.True(talk.ReachedLimit);
        Assert.False(talk.Listening);
        Assert.False(capture.Running);
        Assert.Equal(AudioClips.MaxSeconds, pumps);
        var clip = talk.End();
        Assert.NotNull(clip);
        Assert.Equal(AudioClips.MaxSeconds * AudioClips.SampleRate, clip.Samples.Length);
    }

    [Fact]
    public void WithoutAMicrophoneThePanelGetsASpanishMessageAndNothingBreaks()
    {
        var talk = new PushToTalkSpeechCapture(null);

        Assert.False(talk.Available);
        Assert.False(talk.Begin());
        Assert.Equal(PushToTalkSpeechCapture.NoMicrophone, talk.Problem);
        Assert.False(talk.Listening);
        Assert.Null(talk.End());
        talk.Pump(); // harmless
        Assert.Contains("microphone", PushToTalkSpeechCapture.NoMicrophone, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeviceThatCannotOpenIsReportedAndLeavesNoRecording()
    {
        var talk = new PushToTalkSpeechCapture(new FakeCapture(16000) { FailToStart = true });

        Assert.False(talk.Begin());
        Assert.False(talk.Listening);
        Assert.StartsWith("Could not open the microphone", talk.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePanelShowsThinkingWhileATurnIsOutAndItsLinesArriveInOrderWhenTheyComeBack()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var pump = new TurnPump(session, dialogue, () => null);
        dialogue.Open();

        dialogue.Entry.Insert("¿dónde está el árbol?");
        dialogue.RequestSubmit();
        pump.Submit();

        Assert.Equal(1, dialogue.Pending); // "Thinking…"
        Assert.False(dialogue.SubmitRequested);
        Assert.Equal(DialogueSpeaker.Player, dialogue.Lines[^1].Speaker); // what the player typed is shown at once
        Assert.Equal(1, session.PendingTurns);

        var deadline = Environment.TickCount64 + 30000;
        IReadOnlyList<AppliedTurn> applied = [];
        while (applied.Count == 0 && Environment.TickCount64 < deadline)
        {
            applied = pump.Apply();
            Thread.Sleep(2);
        }

        Assert.Single(applied);
        Assert.Equal(0, dialogue.Pending);
        Assert.Equal(1, dialogue.TurnCount);
        Assert.Contains(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.World);
        session.StopWorker();
    }

    [Fact]
    public void AFailedSpokenRequestShowsItsProblemAsAHintAndClearsThePendingState()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value);
        session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine);
        var pump = new TurnPump(session, dialogue, () => null);
        dialogue.Open();

        dialogue.RequestSpeech(TurnSource.Wav, "/nonexistent.wav");
        pump.Submit();
        Assert.Equal(1, dialogue.Pending);

        var deadline = Environment.TickCount64 + 30000;
        while (dialogue.Pending > 0 && Environment.TickCount64 < deadline)
        {
            pump.Apply();
            Thread.Sleep(2);
        }

        Assert.Equal(0, dialogue.Pending);
        Assert.Equal(0, dialogue.TurnCount);
        Assert.Equal(DialogueSpeaker.Hint, dialogue.Lines[^1].Speaker);
        session.StopWorker();
    }

    [Fact]
    public void ClosingThePanelDropsTheListeningStateAndAScriptedSpeechRequest()
    {
        var dialogue = new DialogueModel(new InputFocus(), "hola");
        dialogue.Open();
        dialogue.Listening = true;
        dialogue.RequestSpeech(TurnSource.Speak, "hola");

        dialogue.Close();

        Assert.False(dialogue.Listening);
        Assert.Null(dialogue.PendingSpeech);
        dialogue.RequestSpeech(TurnSource.Speak, "hola"); // closed: ignored
        Assert.Null(dialogue.TakeSpeechRequest());
    }

    // The game's frame as the scripted run drives it: apply what the worker finished, poll and step unless a turn is out (the world waits),
    // then hand over what the panel asked to send.
}
