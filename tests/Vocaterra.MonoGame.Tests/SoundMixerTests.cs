using Vocaterra.Game;
using Vocaterra.MonoGame.Sound;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Tests;

/// <summary>Collects what the mixer logs, and what it plays, without a sound device.</summary>
internal sealed class ListLog : ISoundLog
{
    public List<string> Lines { get; } = [];

    public void Write(string line) => Lines.Add(line);
}

/// <summary>An output that records every call: the tests never open an audio device.</summary>
internal sealed class RecordingOutput : ISoundOutput
{
    public List<SoundRequest> Played { get; } = [];

    public int Stops { get; private set; }

    public List<BedLevel> Beds { get; private set; } = [];

    public bool Available => true;

    public void Play(SoundRequest request) => Played.Add(request);

    public void StopAll() => Stops++;

    public void SetBeds(IReadOnlyList<BedLevel> levels) => Beds = [.. levels];

    public void Dispose()
    {
    }
}

/// <summary>The mixer's policy (S24): ducking under a voice, silence while recording, the volumes, the deterministic variation and the beds' cross-fade.</summary>
public sealed class SoundMixerTests
{
    private static (SoundMixer Mixer, RecordingOutput Output, ListLog Log) Make(ulong seed = 7, SoundVolumes? volumes = null, bool off = false)
    {
        var output = new RecordingOutput();
        var log = new ListLog();
        return (new SoundMixer(output, volumes ?? new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(seed), log, off), output, log);
    }

    private static void Run(SoundMixer mixer, double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds; elapsed += 1.0 / 60)
        {
            mixer.Advance(1.0 / 60);
        }
    }

    [Fact]
    public void ACueIsPlayedAtItsGainTimesTheCategoryVolume()
    {
        var (mixer, output, _) = Make(volumes: new SoundVolumes(1.0, 0.5, 0.25, 0.6));

        var sfx = mixer.Play(SoundCue.PlaceBlock);
        var ui = mixer.Play(SoundCue.UiClick);

        Assert.Equal(SoundCatalog.Of(SoundCue.PlaceBlock).Gain * 0.5, sfx!.Value.Gain, 6);
        Assert.Equal(SoundCatalog.Of(SoundCue.UiClick).Gain * 0.25, ui!.Value.Gain, 6);
        Assert.Equal(SoundCategory.Sfx, sfx.Value.Category);
        Assert.Equal(SoundCategory.Ui, ui.Value.Category);
        Assert.Equal(2, output.Played.Count);
    }

    [Fact]
    public void SfxAndAmbientDuckUnderAVoiceLineAndComeBackAfterIt()
    {
        var (mixer, _, log) = Make();
        var open = mixer.Gain(SoundCategory.Sfx);
        Assert.Equal(1.0, open, 6);

        mixer.SetVoice(true);
        Run(mixer, SoundMixer.DuckAttackSeconds + 0.05);

        Assert.Equal(SoundMixer.DuckLevel, mixer.Duck, 6);
        Assert.Equal(SoundMixer.DuckLevel, mixer.Gain(SoundCategory.Sfx), 6);
        Assert.Equal(SoundMixer.DuckLevel, mixer.Gain(SoundCategory.Ambient), 6);
        Assert.Equal(1.0, mixer.Gain(SoundCategory.Ui), 6); // the short interface and progress sounds are not ducked
        Assert.Equal(1.0, mixer.Gain(SoundCategory.Voice), 6);
        var ducked = mixer.Play(SoundCue.PlaceBlock)!.Value;
        Assert.Equal(SoundCatalog.Of(SoundCue.PlaceBlock).Gain * SoundMixer.DuckLevel, ducked.Gain, 6);

        mixer.SetVoice(false);
        Run(mixer, SoundMixer.DuckReleaseSeconds + 0.05);

        Assert.Equal(1.0, mixer.Duck, 6); // restored after the voice
        Assert.Equal(SoundCatalog.Of(SoundCue.PlaceBlock).Gain, mixer.Play(SoundCue.PlaceBlock)!.Value.Gain, 6);
        Assert.Contains(log.Lines, static l => l.EndsWith("mark    voice-begin", StringComparison.Ordinal));
        Assert.Contains(log.Lines, static l => l.EndsWith("mark    voice-end", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDuckEasesInAndOutWithNoHardCut()
    {
        var (mixer, _, _) = Make();
        mixer.SetVoice(true);
        var previous = mixer.Duck;
        var steps = 0;
        while (mixer.Duck > SoundMixer.DuckLevel)
        {
            mixer.Advance(1.0 / 60);
            Assert.True(previous - mixer.Duck <= ((1 - SoundMixer.DuckLevel) / SoundMixer.DuckAttackSeconds / 60) + 1e-9);
            previous = mixer.Duck;
            steps++;
        }

        Assert.InRange(steps, 4, 6); // about 0.08 s of frames
        mixer.SetVoice(false);
        steps = 0;
        while (mixer.Duck < 1.0)
        {
            mixer.Advance(1.0 / 60);
            steps++;
        }

        Assert.InRange(steps, 30, 40); // about 0.6 s: the sounds come back gently
    }

    [Fact]
    public void WhileRecordingSfxAndUiAreSilentAndTheBeepsPlayOutsideTheWindow()
    {
        var (mixer, output, log) = Make();
        mixer.SetTick(100);

        var lead = mixer.Starting(); // the start beep, before the recording
        mixer.SetTick(101);
        mixer.Started();
        mixer.SetTick(110);
        var sfx = mixer.Play(SoundCue.PlaceBlock);
        var ui = mixer.Play(SoundCue.UiClick);
        var chime = mixer.Play(SoundCue.StepDone);
        Assert.Null(sfx);
        Assert.Null(ui);
        Assert.Null(chime);
        Assert.Equal(0, mixer.Gain(SoundCategory.Sfx));
        Assert.Equal(0, mixer.Gain(SoundCategory.Ui));
        Assert.Equal(0, mixer.Gain(SoundCategory.Ambient));
        Assert.Equal(1, output.Stops); // what was playing is stopped when the recording starts
        mixer.SetTick(150);
        mixer.Stopped(); // the stop beep, after the recording
        var after = mixer.Play(SoundCue.PlaceBlock); // and everything is back

        Assert.True(lead > SoundSynth.StartBeepSeconds);
        Assert.NotNull(after);
        Assert.Equal(3, mixer.DroppedCount("recording"));
        Assert.Equal(
            [SoundCue.PushToTalkStart, SoundCue.PushToTalkStop, SoundCue.PlaceBlock],
            output.Played.Select(static r => r.Cue).ToArray());
        var window = log.Lines.SkipWhile(static l => !l.Contains("recording-begin", StringComparison.Ordinal)).Skip(1).TakeWhile(static l => !l.Contains("recording-end", StringComparison.Ordinal)).ToList();
        Assert.All(window, static l => Assert.StartsWith("# 000110 held back (recording): ", l, StringComparison.Ordinal)); // only comments on what the rule held back, no cue
        Assert.Equal(3, window.Count);
        Assert.Contains("000100 ui      push_to_talk_start", log.Lines[0], StringComparison.Ordinal);
        Assert.Contains("000150 mark    recording-end", log.Lines[1 + 1 + 3], StringComparison.Ordinal);
        Assert.Contains("000150 ui      push_to_talk_stop", log.Lines[1 + 1 + 3 + 1], StringComparison.Ordinal);
    }

    [Fact]
    public void AmbientBedsAreSilentWhileRecordingAndDuckUnderVoices()
    {
        var (mixer, output, _) = Make(volumes: new SoundVolumes(1, 1, 1, 0.8));
        mixer.SetBeds([new BedInput("forest", 1.0)]);
        Run(mixer, SoundMixer.BedFadeSeconds + 0.1);
        Assert.Equal(0.8, output.Beds.Single().Level, 6);

        mixer.SetVoice(true);
        Run(mixer, 0.2);
        Assert.Equal(0.8 * SoundMixer.DuckLevel, output.Beds.Single().Level, 6);

        mixer.Started();
        mixer.Advance(1.0 / 60);
        Assert.Equal(0, output.Beds.Single().Level); // the microphone rule reaches the beds too
        mixer.Stopped();
        mixer.SetVoice(false);
        Run(mixer, SoundMixer.DuckReleaseSeconds + 0.1);
        Assert.Equal(0.8, output.Beds.Single().Level, 6);
    }

    [Fact]
    public void BedsCrossFadeByPositionWeightsWithNoHardCut()
    {
        var (mixer, output, _) = Make();
        mixer.SetBeds([new BedInput("forest", 1.0), new BedInput("river", 0.0)]);
        Run(mixer, SoundMixer.BedFadeSeconds + 0.1);
        Assert.Equal(1.0, Level(mixer, "forest"), 6);

        mixer.SetBeds([new BedInput("forest", 0.25), new BedInput("river", 0.75)]); // the player walks toward the river
        var maxStep = 1.0 / 60 / SoundMixer.BedFadeSeconds;
        var previousForest = 1.0;
        var previousRiver = 0.0;
        for (var i = 0; i < 200; i++)
        {
            mixer.Advance(1.0 / 60);
            Assert.True(Math.Abs(Level(mixer, "forest") - previousForest) <= maxStep + 1e-9);
            Assert.True(Math.Abs(Level(mixer, "river") - previousRiver) <= maxStep + 1e-9);
            previousForest = Level(mixer, "forest");
            previousRiver = Level(mixer, "river");
        }

        Assert.Equal(0.25, Level(mixer, "forest"), 6);
        Assert.Equal(0.75, Level(mixer, "river"), 6);

        mixer.SetBeds([]); // a bed that is not named fades out and is dropped
        Run(mixer, SoundMixer.BedFadeSeconds + 0.1);
        Assert.Empty(mixer.BedLevels);
        Assert.Empty(output.Beds);
    }

    private static double Level(SoundMixer mixer, string bed) => mixer.BedLevels.FirstOrDefault(b => b.Bed == bed).Level;

    [Fact]
    public void VolumeZeroHoldsACategoryBackAndNoSoundsHoldsEverythingBack()
    {
        var (quiet, quietOutput, _) = Make(volumes: new SoundVolumes(1, 0, 1, 1));
        Assert.Null(quiet.Play(SoundCue.PlaceBlock));
        Assert.NotNull(quiet.Play(SoundCue.UiClick));
        Assert.Equal(1, quiet.DroppedCount("volume"));
        Assert.Single(quietOutput.Played);

        var (off, offOutput, _) = Make(off: true);
        Assert.Null(off.Play(SoundCue.PlaceBlock));
        Assert.Null(off.Play(SoundCue.UiClick));
        Assert.Equal(0, off.Starting()); // no start beep, and so nothing to discard from the recording
        Assert.Empty(offOutput.Played);
        Assert.Equal(1.0, off.Gain(SoundCategory.Voice)); // --no-sounds leaves the voices alone
    }

    [Fact]
    public void VolumesAreClampedToZeroToOne()
    {
        var (mixer, _, _) = Make(volumes: new SoundVolumes(2, -1, double.NaN, 0.5));

        Assert.Equal(new SoundVolumes(1, 0, 0, 0.5), mixer.Volumes);
        mixer.SetVolumes(new SoundVolumes(0.3, 9, 0.2, 0.1));
        Assert.Equal(new SoundVolumes(0.3, 1, 0.2, 0.1), mixer.Volumes);
    }

    [Fact]
    public void TheVariationIsTheSameForTheSameSeedAndTheCuesPlayedInTheSameOrder()
    {
        var cues = new[] { SoundCue.GatherWood, SoundCue.GatherWood, SoundCue.GatherStone, SoundCue.BreakWood, SoundCue.PickUp, SoundCue.UiClick, SoundCue.BookPage, SoundCue.BookPage, SoundCue.GatherWood };

        List<SoundRequest> Play(ulong seed)
        {
            var (mixer, output, _) = Make(seed);
            foreach (var cue in cues)
            {
                _ = mixer.Play(cue);
            }

            return output.Played;
        }

        var first = Play(11);
        Assert.Equal(first, Play(11));
        Assert.NotEqual(first, Play(12)); // another world seed varies differently
    }

    [Fact]
    public void ATakeNeverRepeatsAndThePitchStaysWithinTheCuesJitter()
    {
        var (mixer, output, _) = Make(3);
        for (var i = 0; i < 200; i++)
        {
            _ = mixer.Play(SoundCue.GatherWood);
        }

        var takes = output.Played.Select(static r => r.Take).ToList();
        for (var i = 1; i < takes.Count; i++)
        {
            Assert.NotEqual(takes[i - 1], takes[i]);
        }

        Assert.Equal(3, takes.Distinct().Count());
        var jitter = SoundCatalog.Of(SoundCue.GatherWood).PitchJitter;
        Assert.All(output.Played, r => Assert.InRange(r.Pitch, -jitter, jitter));
        Assert.Contains(output.Played, static r => r.Pitch != 0);
        Assert.Equal(0, mixer.Play(SoundCue.StepDone)!.Value.Pitch); // a note keeps its pitch
        Assert.Equal(0, mixer.Play(SoundCue.CraftDone)!.Value.Take); // one take: nothing to choose
    }

    [Fact]
    public void WhatTheMicrophoneHeldBackDoesNotShiftTheVariationOfWhatFollows()
    {
        var (gated, gatedOutput, _) = Make(21);
        var (open, openOutput, _) = Make(21);
        gated.Started();
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(gated.Play(SoundCue.GatherStone));
            _ = open.Play(SoundCue.GatherStone);
        }

        gated.Stopped();
        gatedOutput.Played.Clear();
        openOutput.Played.RemoveRange(0, 5);
        for (var i = 0; i < 5; i++)
        {
            _ = gated.Play(SoundCue.GatherStone);
            _ = open.Play(SoundCue.GatherStone);
        }

        // The stop beep is not in the open run, so compare the stone hits after it.
        Assert.Equal(openOutput.Played.Where(static r => r.Cue == SoundCue.GatherStone).Skip(0).ToList(), gatedOutput.Played.Where(static r => r.Cue == SoundCue.GatherStone).ToList());
    }

    [Fact]
    public void ThePresentationGeneratorIsItsOwnSplitMixStream()
    {
        var a = new PresentationRng(5);
        var b = new PresentationRng(5);
        Assert.Equal(Enumerable.Range(0, 20).Select(_ => a.NextUInt64()).ToArray(), Enumerable.Range(0, 20).Select(_ => b.NextUInt64()).ToArray());
        var c = new PresentationRng(6);
        Assert.NotEqual(new PresentationRng(5).NextUInt64(), c.NextUInt64());
        var d = new PresentationRng(1);
        Assert.All(Enumerable.Range(0, 1000), _ => Assert.InRange(d.NextDouble(), 0.0, 0.9999999999));
        Assert.All(Enumerable.Range(0, 1000), _ => Assert.InRange(d.NextInt(7), 0, 6));
    }
}
