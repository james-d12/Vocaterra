using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Nlp.Content;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;
using Xunit.Abstractions;

namespace Vocaterra.MonoGame.Tests;

/// <summary>A fact that needs the speech models: skipped, with the reason in the report, when <c>VOCATERRA_MODELS_DIR</c> or a model is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Creates the fact.</summary>
    public ModelFactAttribute()
    {
        var store = SpeechEngine.FindModels(out var problem);
        if (store is null)
        {
            Skip = $"Speech models unavailable: {problem}";
        }
        else if (!Directory.Exists(store.ModelDirectory(SpeechEngine.KokoroModelId)) || store.Manifest.Find(SpeechEngine.KokoroModelId) is null)
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
public sealed class SpokenFrontendTests(ITestOutputHelper output)
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
    public void WavAndSpeakAreScriptStepsInTheOrderGivenAndNeedTheSpeechModels()
    {
        var options = Parse("--screenshot", "x.png", "--say", "hola", "--speak", "¿dónde está el árbol?", "--wav", "a.wav", "--skip", "2");

        Assert.Equal([ScriptStepKind.Say, ScriptStepKind.Speak, ScriptStepKind.Wav, ScriptStepKind.Skip], options.Steps.Select(static s => s.Kind));
        Assert.Equal("¿dónde está el árbol?", options.Steps[1].Line);
        Assert.Equal("a.wav", options.Steps[2].Line);
        Assert.Equal(["hola"], options.Says);
        Assert.True(options.NeedsSpeech);
        Assert.Equal(LaunchOptions.DefaultSayFrames, options.Frames);
    }

    [Fact]
    public void ATypedScriptLoadsNoSpeechModelAndAnInteractiveRunLoadsThemUnlessTold()
    {
        Assert.False(Parse("--screenshot", "x.png", "--say", "hola").NeedsSpeech);
        Assert.True(Parse().NeedsSpeech);
        Assert.False(Parse("--no-speech").NeedsSpeech);
        Assert.False(Parse("--screenshot", "x.png", "--speak", "hola", "--no-speech").NeedsSpeech);
    }

    [Fact]
    public void TypedInputEarnsHalfCreditByDefaultAndTheOptionSwitchesItToFull()
    {
        Assert.Equal(CreditRules.Default, Parse().Credit);
        Assert.Equal(WordProgress.UnitsPerUse / 2, Parse().Credit.TypedUnits);
        Assert.Equal(WordProgress.UnitsPerUse, Parse().Credit.SpokenUnits);
        Assert.Equal(CreditRules.FullTyped, Parse("--typed-credit", "full").Credit);
        Assert.Equal(CreditRules.Default, Parse("--typed-credit", "half").Credit);
        Assert.False(LaunchOptions.TryParse(["--typed-credit", "double"], out _, out var error));
        Assert.Contains("full or half", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNewOptionsNeedAValueAndAScreenshotWhereTheyScriptTheRun()
    {
        Assert.False(LaunchOptions.TryParse(["--speak"], out _, out var missing));
        Assert.Contains("needs a value", missing, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--wav", "a.wav"], out _, out var alone));
        Assert.Contains("--screenshot", alone, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "x.png", "--speak", " "], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--screenshot", "x.png", "--status", "sleeping"], out _, out _));
        Assert.Equal("thinking", Parse("--screenshot", "x.png", "--status", "thinking").Status);
        Assert.Contains("Tab", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void HoldingTheKeyRecordsAndLettingGoGivesATrimmed16KilohertzClip()
    {
        var capture = new FakeCapture(44100, new short[44100], Tone(1.0, 44100), new short[44100]);
        var talk = new PushToTalk(capture);

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
        var talk = new PushToTalk(new FakeCapture(16000, new short[32000]));

        talk.Begin();
        talk.Pump();

        Assert.Null(talk.End());
        Assert.Equal(PushToTalk.Silence, talk.Problem);
    }

    [Fact]
    public void RecordingStopsAtTheLengthCapAndTheClipIsAtMostEightSeconds()
    {
        var seconds = Enumerable.Range(0, 12).Select(_ => Tone(1.0, 16000)).ToArray();
        var capture = new FakeCapture(16000, seconds);
        var talk = new PushToTalk(capture);

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
        var talk = new PushToTalk(null);

        Assert.False(talk.Available);
        Assert.False(talk.Begin());
        Assert.Equal(PushToTalk.NoMicrophone, talk.Problem);
        Assert.False(talk.Listening);
        Assert.Null(talk.End());
        talk.Pump(); // harmless
        Assert.Contains("micrófono", PushToTalk.NoMicrophone, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeviceThatCannotOpenIsReportedAndLeavesNoRecording()
    {
        var talk = new PushToTalk(new FakeCapture(16000) { FailToStart = true });

        Assert.False(talk.Begin());
        Assert.False(talk.Listening);
        Assert.StartsWith("No se pudo abrir el micrófono", talk.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePanelShowsThinkingWhileATurnIsOutAndItsLinesArriveInOrderWhenTheyComeBack()
    {
        var session = new GameSession(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value);
        session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var pump = new TurnPump(session, dialogue, () => null);
        dialogue.Open();

        dialogue.Entry.Insert("¿dónde está el árbol?");
        dialogue.RequestSubmit();
        pump.Submit();

        Assert.Equal(1, dialogue.Pending); // "Pensando…"
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
        var session = new GameSession(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value);
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

    [Fact]
    public void AFrameSummaryNamesTheCountTheMedianAndTheLongestFrame()
    {
        var stats = new BusyFrameStats();
        Assert.Equal(0, stats.Longest);
        foreach (var ms in new[] { 4.0, 2.0, 3.0, 40.0, 2.5 })
        {
            stats.Record(ms);
        }

        Assert.Equal(5, stats.Count);
        Assert.Equal(40.0, stats.Longest);
        Assert.Equal(3.0, stats.Median);
        Assert.Equal("5 frames, median 3.0 ms, longest 40.0 ms", stats.Summary());
    }

    // The game's frame as the scripted run drives it: apply what the worker finished, poll and step unless a turn is out (the world waits),
    // then hand over what the panel asked to send.
    private static (GameSession Session, DialogueModel Dialogue, List<AppliedTurn> Turns, int Ticks) RunScript(IReadOnlyList<ScriptStep> steps, bool loadSpeech, CreditRules? credit = null)
    {
        var session = new GameSession(Bundle.Value, 42, GameSession.DefaultStartedAt, Nouns.Value, credit: credit);
        var worker = session.StartWorker(loadSpeech: loadSpeech);
        Assert.True(worker.WaitUntilReady(120000));
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, session.OpeningLine);
        var script = new ScriptedDialogue(session, dialogue, steps);
        var input = new FocusGatedInput(script, focus);
        var pump = new TurnPump(session, dialogue, () => null);
        var commands = new List<PlayerCommand>();
        var turns = new List<AppliedTurn>();
        var ticks = 0;
        var deadline = Environment.TickCount64 + 120000;
        while ((!script.IsDone || session.PendingTurns > 0) && ticks < 3000 && Environment.TickCount64 < deadline)
        {
            turns.AddRange(pump.Apply());
            if (session.PendingTurns == 0)
            {
                CommandMapper.Fill(input.Poll(session.Simulation.Tick), commands);
                session.Step(commands);
                ticks++;
            }
            else
            {
                Thread.Sleep(1);
            }

            pump.Submit();
        }

        turns.AddRange(pump.Apply());
        Assert.True(script.IsDone, $"The script did not finish in {ticks} ticks.");
        return (session, dialogue, turns, ticks);
    }

    [Fact]
    public void AScriptedTypedRunThroughTheWorkerIsDeterministicTickForTick()
    {
        ScriptStep[] steps = [ScriptStep.Say("¿dónde está el árbol?"), ScriptStep.Say("gracias")];

        var first = RunScript(steps, loadSpeech: false);
        var second = RunScript(steps, loadSpeech: false);

        Assert.Equal(first.Ticks, second.Ticks);
        Assert.Equal(first.Session.Simulation.ComputeStateDigest(), second.Session.Simulation.ComputeStateDigest());
        Assert.Equal(2, first.Dialogue.TurnCount);
        Assert.Equal(first.Dialogue.Lines.Select(static l => l.Text.Split(", ")[0]), second.Dialogue.Lines.Select(static l => l.Text.Split(", ")[0]));
        Assert.Equal(first.Turns.Select(static t => t.Turn!.HostStateHash), second.Turns.Select(static t => t.Turn!.HostStateHash));
        first.Session.StopWorker();
        second.Session.StopWorker();
    }

    [Fact]
    public void AScriptedSpeakStepWithoutTheModelsFailsWithAHintAndTheScriptGoesOn()
    {
        var (session, dialogue, turns, _) = RunScript([ScriptStep.Speak("¿dónde está el árbol?"), ScriptStep.Say("hola")], loadSpeech: false);

        Assert.Equal(2, turns.Count);
        Assert.Null(turns[0].Turn);
        Assert.NotNull(turns[0].Outcome.Problem);
        Assert.Equal("greet", turns[1].Turn!.Intent);
        Assert.Contains(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.Hint);
        session.StopWorker();
    }

    [ModelFact]
    public void AScriptedSpeakStepIsSynthesizedRecognizedAndEvaluatedAsSpokenInput()
    {
        var (session, dialogue, turns, _) = RunScript([ScriptStep.Speak("¿cómo hago una mesa?")], loadSpeech: true);

        var turn = turns.Single().Turn!;
        output.WriteLine($"heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Summary}");
        foreach (var line in turn.GuideLines)
        {
            output.WriteLine("guide: " + line);
        }

        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.Equal("cómo hago una mesa", turn.Line);
        Assert.Equal("ask_craft", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Contains(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.PlayerSpoken && l.Text == "cómo hago una mesa");
        Assert.Contains(turn.GuideLines, static l => l.Contains("mesa", StringComparison.Ordinal));
        session.StopWorker();
    }
}
