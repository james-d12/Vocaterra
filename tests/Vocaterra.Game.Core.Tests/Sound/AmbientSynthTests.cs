using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>
/// The synthesized ambient sounds (S25): the looping beds are seamless (no click at the seam), the same every time they are made, and in range; the scheduled
/// takes differ from each other; and the scheduled chirps and crackles follow the bed's weight and the microphone rule, and the log shows the beds' places.
/// </summary>
public sealed class AmbientSynthTests
{
    private const int LoopLength = (int)(AmbientLoops.LoopSeconds * SoundSynth.SampleRate);

    [Theory]
    [InlineData("wind")]
    [InlineData("river")]
    [InlineData("waterfall")]
    [InlineData("village")]
    [InlineData("insects")]
    public void EveryBedLoopsSeamlesslyWithoutAClick(string bed)
    {
        var loop = AmbientLoops.Render(bed);

        Assert.Equal(LoopLength, loop.Length);
        var steps = new List<int>(loop.Length);
        for (var i = 1; i < loop.Length; i++)
        {
            steps.Add(Math.Abs(loop[i] - loop[i - 1]));
        }

        // The seam: the last sample is followed by the first. The step across it is what the loop's own steps are (a few times their mean, never a jump of
        // a large part of the sound), so the ear hears no click. A high bed (the insects) has large steps everywhere, so the test is relative to the mean.
        var seam = Math.Abs(loop[0] - loop[^1]);
        var mean = steps.Average();
        Assert.True(seam <= (4 * mean) + 50, $"{bed}: the seam steps {seam}, the loop's mean step is {mean:0}");
    }

    [Theory]
    [InlineData("wind")]
    [InlineData("river")]
    [InlineData("waterfall")]
    [InlineData("village")]
    [InlineData("insects")]
    public void EveryBedIsAudibleAndWithinItsPeak(string bed)
    {
        var loop = AmbientLoops.Render(bed);
        var peak = loop.Max(static s => Math.Abs((int)s));
        var mean = loop.Average(static s => s * (double)s);

        Assert.InRange(peak, 0.65 * short.MaxValue, 0.75 * short.MaxValue);
        Assert.True(Math.Sqrt(mean) > 0.02 * short.MaxValue, $"{bed} is audible");
    }

    [Fact]
    public void TheBedsAreTheSameEveryTimeTheyAreMadeAndTheOnesThatDoNotLoopSayNo()
    {
        foreach (var bed in AmbientLoops.Looping)
        {
            Assert.Equal(AmbientLoops.Render(bed), AmbientLoops.Render(bed));
        }

        Assert.Throws<ArgumentException>(() => AmbientLoops.Render(AmbientBeds.Birds));
        Assert.Throws<ArgumentException>(() => AmbientLoops.Render(AmbientBeds.Fire));
    }

    [Fact]
    public void TheSeamOfAnUnfadedLoopWouldClickAndTheFadeRemovesIt()
    {
        // A ramp that wraps: the raw sound jumps from its top back to its bottom at the seam. Its tail continues the ramp, so the fade must make the seam smooth.
        const int length = 1000;
        const int tail = 200;
        var raw = new float[length + tail];
        for (var i = 0; i < raw.Length; i++)
        {
            raw[i] = (float)(i % 2000) / 2000 * 0.5f; // a slow ramp across the whole sound
        }

        var unfaded = Math.Abs(raw[length - 1] - raw[0]);
        var loop = AmbientLoops.Seamless(raw, length, tail);
        var seam = Math.Abs(loop[0] - loop[^1]);
        Assert.True(unfaded > 0.1, "the unfaded seam jumps");
        Assert.True(seam < 0.02 * short.MaxValue, $"the faded seam steps {seam}");
    }

    [Theory]
    [InlineData(SoundCue.FootDirt)]
    [InlineData(SoundCue.FootGravel)]
    [InlineData(SoundCue.FootSand)]
    [InlineData(SoundCue.FootWater)]
    [InlineData(SoundCue.BirdChirp)]
    [InlineData(SoundCue.FireCrackle)]
    public void TheSynthesizedTakesDifferFromEachOtherAndAreTheSameEveryTime(SoundCue cue)
    {
        var takes = SoundCatalog.Of(cue).Takes;
        Assert.True(takes > 1, $"{cue} has several takes");
        var rendered = Enumerable.Range(0, takes).Select(t => SoundSynth.Render(cue, t)).ToList();
        for (var a = 0; a < takes; a++)
        {
            Assert.Equal(rendered[a], SoundSynth.Render(cue, a));
            for (var b = a + 1; b < takes; b++)
            {
                Assert.NotEqual(rendered[a], rendered[b]);
            }
        }
    }

    [Fact]
    public void ChirpsAndCrackleFollowTheirBedsWeightAndStayOutOfTheRecording()
    {
        var places = new AmbientPlaces(null, null, null, [], new Point2(10.5, 10.5), [], []);
        var output = new RecordingOutput();
        var log = new ListLog();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(31), log);
        var ambient = new AmbientSound(mixer, places, dusk: false);

        // The forest by day: the birds sing, and the fire (lit, by the listener) crackles.
        var forest = new Listener(10.5, 40, 10.5, 0);
        var tick = 0L;
        for (; tick < 60 * 30; tick++)
        {
            ambient.Observe(tick, forest, fireLit: true);
            mixer.Advance(1.0 / 60);
        }

        var chirps = output.Played.Count(static r => r.Cue == SoundCue.BirdChirp);
        var crackles = output.Played.Count(static r => r.Cue == SoundCue.FireCrackle);
        Assert.True(chirps >= 3, $"birds sang {chirps} chirps in 30 s");
        Assert.True(crackles >= 10, $"the fire crackled {crackles} times in 30 s");
        Assert.Contains(log.Lines, static line => line.Contains("mark    bed at valley", StringComparison.Ordinal));

        // The same route and seed give the same schedule.
        var replay = new RecordingOutput();
        var replayMixer = new SoundMixer(replay, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(31), new ListLog());
        var replayAmbient = new AmbientSound(replayMixer, places, dusk: false);
        for (var t = 0L; t < 60 * 30; t++)
        {
            replayAmbient.Observe(t, forest, fireLit: true);
            replayMixer.Advance(1.0 / 60);
        }

        Assert.Equal(output.Played.Select(static r => (r.Cue, r.Take, r.Gain, r.Pan)).ToList(), replay.Played.Select(static r => (r.Cue, r.Take, r.Gain, r.Pan)).ToList());

        // Recording: nothing scheduled plays, the microphone is silent.
        var before = output.Played.Count;
        mixer.Started();
        for (var t = 0L; t < 60 * 10; t++, tick++)
        {
            ambient.Observe(tick, forest, fireLit: true);
            mixer.Advance(1.0 / 60);
        }

        Assert.Equal(before, output.Played.Count);
        Assert.NotEmpty(output.Beds);
        Assert.All(output.Beds, static b => Assert.Equal(0.0, b.Level));
    }

    [Fact]
    public void TheSoundLogNamesThePlaceAndTheWeightsWhenTheyChange()
    {
        var places = new AmbientPlaces(new Point2(292.5, 218.5), null, null, [], null, [], [new NamedPlace("village", new Point2(292.5, 218.5), 22)]);
        var log = new ListLog();
        var mixer = new SoundMixer(new RecordingOutput(), new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(2), log);
        var ambient = new AmbientSound(mixer, places, dusk: false);

        ambient.Observe(100, new Listener(292.5, 37, 218.5, 0), fireLit: false);
        ambient.Observe(101, new Listener(292.5, 37, 218.5, 0), fireLit: false); // the same place: no new line
        ambient.Observe(200, new Listener(0.5, 37, 0.5, 0), fireLit: false);

        var marks = log.Lines.Where(static l => l.Contains("mark", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, marks.Count);
        Assert.Contains("000100 mark    bed at village", marks[0], StringComparison.Ordinal);
        Assert.Contains("birds=1.0", marks[1], StringComparison.Ordinal);
    }
}
