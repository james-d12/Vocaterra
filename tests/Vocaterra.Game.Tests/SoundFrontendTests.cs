using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Startup;
using SoundEventMapper = Vocaterra.Game.Core.Sound.SoundEventMapper;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The sound side of the front-end (S24) without a device: the panels' cues, the frame's folding, the scripted recording window, the push-to-talk hooks that keep the
/// beeps out of the recording, the sound log's format, the launch flags and the null output of a scripted run.
/// </summary>
public sealed class SoundFrontendTests
{
    private static (SoundFrontend Sound, RecordingOutput Output, ListLog Log) Make(ulong seed = 9, bool off = false)
    {
        var output = new RecordingOutput();
        var log = new ListLog();
        var mixer = new SoundMixer(output, new SoundVolumes(1, 1, 1, 1), new PresentationRng(seed), log, off);
        return (new SoundFrontend(mixer, new SoundEventMapper()), output, log);
    }

    private sealed class FakeCapture(int sampleRate, params short[][] chunks) : ISpeechCapture
    {
        private int _next;

        public int SampleRate => sampleRate;

        public void Start()
        {
        }

        public short[] ReadAvailable() => _next < chunks.Length ? chunks[_next++] : [];

        public void Stop()
        {
        }
    }

    [Fact]
    public void PanelsOpeningAndClosingAndTheBooksPageTurnHaveCues()
    {
        var (sound, output, _) = Make();
        void Frame(PanelState state)
        {
            sound.ObservePanels(state);
            sound.Flush(1);
        }

        Frame(default);
        Frame(new PanelState(true, false, false, 0, false));
        Frame(new PanelState(false, false, false, 0, false));
        Frame(new PanelState(false, true, false, 0, false));
        Frame(new PanelState(false, false, false, 0, false));
        Frame(new PanelState(false, false, true, 0, false));
        Frame(new PanelState(false, false, true, 1, false)); // the page turned
        Frame(new PanelState(false, false, false, 0, false));
        Frame(new PanelState(false, false, false, 0, true));
        Frame(new PanelState(false, false, false, 0, false));

        Assert.Equal(
            [SoundCue.InventoryOpen, SoundCue.InventoryClose, SoundCue.CraftingOpen, SoundCue.CraftingClose, SoundCue.BookOpen, SoundCue.BookPage, SoundCue.BookClose, SoundCue.DialogueOpen, SoundCue.DialogueClose],
            output.Played.Select(static r => r.Cue).ToArray());
    }

    [Fact]
    public void ATabClickSoundsOnceNotAlsoAsAPageTurn()
    {
        var (sound, output, _) = Make();
        sound.ObservePanels(new PanelState(false, false, true, 0, false));
        sound.Flush(1);
        output.Played.Clear();

        sound.QueueClick(SoundCue.UiTab);
        sound.ObservePanels(new PanelState(false, false, true, 2, false));
        sound.Flush(2);

        Assert.Equal([SoundCue.UiTab], output.Played.Select(static r => r.Cue).ToArray());
    }

    [Fact]
    public void AFramesCuesAreFoldedBeforeTheyPlay()
    {
        var (sound, output, _) = Make();
        sound.ObserveTurn(Turn(), new WordTurnResult([], [], []));
        sound.Queue(SoundCue.StepDone);
        sound.Queue(SoundCue.StepDone);
        sound.Flush(3);

        Assert.Equal([SoundCue.StepDone], output.Played.Select(static r => r.Cue).ToArray()); // the answer's chime gives way to the step's, and a duplicate plays once
    }

    private static GuideTurn Turn() => new("x", "request_item", true, 1.0, [], null, [], null, "s", [], null, null, null, "h", Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty);

    [Fact]
    public void TheGameEventsOfALoadedGameDoNotSoundAgain()
    {
        var (sound, output, _) = Make();
        var events = new List<GameEvent> { new WorldEvent(1, 1, WorldEvents.FireLit), new WorldEvent(2, 2, WorldEvents.ObjectiveComplete) };
        sound.SkipEvents(events.Count);

        events.Add(new WorldEvent(3, 3, WorldEvents.SessionComplete));
        sound.ObserveGameEvents(events);
        sound.ObserveGameEvents(events); // a second look finds nothing new
        sound.Flush(3);

        Assert.Equal([SoundCue.SessionDone], output.Played.Select(static r => r.Cue).ToArray());
    }

    [Fact]
    public void AScriptedRecordingRunsTheBeepSilenceBeepSequenceForItsTicksWithoutDelayingAnything()
    {
        var (sound, output, log) = Make();
        sound.Mixer.SetTick(500);
        sound.Scripted.Begin();
        sound.Scripted.Begin(); // already on: nothing more
        for (var tick = 501; tick < 500 + ScriptedRecording.Ticks; tick++)
        {
            sound.Mixer.SetTick(tick);
            Assert.True(sound.Scripted.Active);
            Assert.Null(sound.Mixer.Play(SoundCue.GatherWood)); // the world makes no sound in the window
            sound.Scripted.Tick();
        }

        sound.Mixer.SetTick(500 + ScriptedRecording.Ticks);
        sound.Scripted.Tick();

        Assert.False(sound.Scripted.Active);
        Assert.Equal([SoundCue.PushToTalkStart, SoundCue.PushToTalkStop], output.Played.Select(static r => r.Cue).ToArray());
        Assert.Equal(
            ["000500 ui      push_to_talk_start", "000500 mark    recording-begin", "000524 mark    recording-end", "000524 ui      push_to_talk_stop"],
            log.Lines.Where(static l => !l.StartsWith('#')).Select(static l => l.Contains(" take=", StringComparison.Ordinal) ? l[..l.IndexOf(" take=", StringComparison.Ordinal)] : l).ToArray());
        Assert.Equal(ScriptedRecording.Ticks - 1, log.Lines.Count(static l => l.Contains("held back (recording): sfx gather_wood", StringComparison.Ordinal)));
        Assert.NotNull(sound.Mixer.Play(SoundCue.GatherWood)); // and sound again after it
    }

    [Fact]
    public void ThePushToTalkBeepsFrameTheRecordingAndTheStartBeepIsDiscardedFromIt()
    {
        var (sound, output, log) = Make();
        var rate = 16000;
        var lead = (int)Math.Round((SoundSynth.StartBeepSeconds + 0.05) * rate); // 0.14 s
        var loud = Enumerable.Repeat((short)8000, lead).ToArray(); // the beep, as the microphone hears it
        var speech = Enumerable.Repeat((short)4000, rate).ToArray(); // a second of speech
        var talk = new PushToTalkSpeechCapture(new FakeCapture(rate, loud, speech)) { Sounds = sound.Mixer };

        Assert.True(talk.Begin());
        Assert.True(sound.Mixer.Recording);
        Assert.Equal([SoundCue.PushToTalkStart], output.Played.Select(static r => r.Cue).ToArray()); // before the recording
        talk.Pump();
        talk.Pump();
        Assert.Null(sound.Mixer.Play(SoundCue.PlaceBlock)); // silent while it records
        var clip = talk.End();

        Assert.False(sound.Mixer.Recording);
        Assert.Equal([SoundCue.PushToTalkStart, SoundCue.PushToTalkStop], output.Played.Select(static r => r.Cue).ToArray()); // after the recording
        Assert.NotNull(clip);
        Assert.InRange(clip!.DurationSeconds, 0.9, 1.1); // the second of speech, not the beep (the loud lead-in was dropped)
        Assert.Equal(1, log.Lines.Count(static l => l.Contains("recording-begin", StringComparison.Ordinal)));
        Assert.Equal(1, log.Lines.Count(static l => l.Contains("recording-end", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnUnstartedOrFailedRecordingNeverLeavesTheMixerSilent()
    {
        var (sound, _, _) = Make();
        var noMicrophone = new PushToTalkSpeechCapture(null) { Sounds = sound.Mixer };

        Assert.False(noMicrophone.Begin());
        Assert.False(sound.Mixer.Recording);
        Assert.Null(noMicrophone.End());
        Assert.False(sound.Mixer.Recording);

        var capped = new PushToTalkSpeechCapture(new FakeCapture(100, new short[100 * AudioClips.MaxSeconds + 100])) { Sounds = sound.Mixer };
        Assert.True(capped.Begin());
        capped.Pump(); // the length cap stops the recording by itself: the window closes there, once
        Assert.True(capped.ReachedLimit);
        Assert.False(sound.Mixer.Recording);
        _ = capped.End();
        Assert.Equal(1, sound.Mixer.PlayedCount(SoundCue.PushToTalkStop));
    }

    [Fact]
    public void TheSoundLogHasOneLinePerCueWithTickCategoryCueTakeGainAndPitch()
    {
        var (sound, _, log) = Make(1);
        sound.Mixer.SetTick(42);
        var request = sound.Mixer.Play(SoundCue.GatherStone)!.Value;
        sound.Mixer.SetTick(1234567);
        _ = sound.Mixer.Play(SoundCue.WordLearnt);
        sound.Mixer.WriteSummary();

        Assert.Matches("^000042 sfx     gather_stone      take=[0-2] gain=0\\.\\d\\d pitch=[+-]0\\.\\d\\d$", log.Lines[0]);
        Assert.Equal(SoundMixer.FormatLine(42, request), log.Lines[0]);
        Assert.Matches("^1234567 ui      word_learnt       take=0 gain=0\\.\\d\\d pitch=\\+0\\.00$", log.Lines[1]);
        Assert.Equal("# summary: 2 played (gather_stone=1 word_learnt=1)", log.Lines[2]);
        Assert.Equal("# held back: recording=0 volume=0 off=0", log.Lines[3]);
        Assert.All(log.Lines.Take(2), static l => Assert.Matches(new Regex("^\\d{6,} (sfx|ui|ambient|mark) "), l));
    }

    [Fact]
    public void TheSoundLogWriterMakesTheFolderAndWritesTheLines()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vocaterra-sound-" + Path.GetRandomFileName());
        var path = Path.Combine(folder, "deep", "sounds.log");
        try
        {
            using (var writer = SoundLogWriter.Open(path))
            {
                writer.Write("# header");
                writer.Write("000001 ui      ui_click          take=0 gain=0.48 pitch=+0.01");
            }

            Assert.Equal(["# header", "000001 ui      ui_click          take=0 gain=0.48 pitch=+0.01"], File.ReadAllLines(path));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void TheSoundFlagsAreParsedAndRefuseWhatIsNotAVolume()
    {
        Assert.True(LaunchOptions.TryParse(["--sfx-volume", "0.25", "--ambient-volume", "0", "--no-sounds", "--sound-log", "/tmp/x.log"], out var options, out var error), error);
        Assert.Equal(0.25, options.SfxVolume);
        Assert.Equal(0.0, options.AmbientVolume);
        Assert.True(options.NoSounds);
        Assert.Equal("/tmp/x.log", options.SoundLogPath);

        Assert.True(LaunchOptions.TryParse(["--mute"], out var plain, out _));
        Assert.True(plain.Muted); // --mute keeps its meaning: the voices
        Assert.False(plain.NoSounds);
        Assert.Null(plain.SfxVolume);
        Assert.Null(plain.SoundLogPath);

        Assert.False(LaunchOptions.TryParse(["--sfx-volume", "1.5"], out _, out var tooLoud));
        Assert.Contains("--sfx-volume needs a number from 0 to 1", tooLoud, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--ambient-volume", "loud"], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--sound-log", string.Empty], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--sfx-volume"], out _, out _));
        foreach (var flag in new[] { "--sfx-volume", "--ambient-volume", "--no-sounds", "--sound-log" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScriptedRunAndNoSoundsUseTheNullOutputAndTheCommandLineOverridesTheSavedVolumes()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "x.png"], out var scripted, out _));
        Assert.True(SoundSetup.UsesNullOutput(scripted.Sound)); // silent, and no audio device is opened
        Assert.True(LaunchOptions.TryParse(["--no-sounds"], out var quiet, out _));
        Assert.True(SoundSetup.UsesNullOutput(quiet.Sound));
        Assert.True(LaunchOptions.TryParse([], out var live, out _));
        Assert.False(SoundSetup.UsesNullOutput(live.Sound));
        Assert.False(new NullSoundOutput().Available);

        var saved = new SoundVolumes(0.5, 0.4, 0.3, 0.2);
        Assert.Equal(saved, SoundSetup.VolumesFor(live.Sound, saved));
        Assert.Equal(SoundVolumes.Default, SoundSetup.VolumesFor(live.Sound, null));
        Assert.True(LaunchOptions.TryParse(["--sfx-volume", "1", "--ambient-volume", "0.9"], out var loud, out _));
        Assert.Equal(new SoundVolumes(0.5, 1.0, 0.3, 0.9), SoundSetup.VolumesFor(loud.Sound, saved));
    }

    [Fact]
    public void ASimulationEventsSoundsReadTheEventAndNothingElse()
    {
        var (sound, output, _) = Make();
        var before = new[] { new SimulationEvent(SimulationEventKind.BlockRemoved, 1, 1, 1, 1, (int)BlockId.Trunk, 0) };

        sound.ObserveSimulation(before, static (_, _, _) => BlockId.Air);
        sound.Flush(1);

        Assert.Equal([SoundCue.BreakWood], output.Played.Select(static r => r.Cue).ToArray());
    }
}
