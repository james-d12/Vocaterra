using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>
/// The rain's sound (E06): the rain bed follows the precipitation channel's intensity and is lower under cover (read from the grid's highest block, the query the drops use); the wind bed
/// rises with the wind channel on top of its rise with height; the birds are quieter in rain; every other bed is what it was; and the rain loop is seamless, deterministic and small.
/// </summary>
public sealed class RainBedTests
{
    private static readonly Lazy<AmbientPlaces> Real = new(() => AmbientPlaces.From(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json"))));

    // The precipitation kind of the bundle's data: the tests never write its id.
    private static readonly string Kind = TestGame.Data.Weather.Kinds.Select(static k => k.Channels.PrecipitationKind).First(static p => p.Length > 0);

    private static Listener At(double x, double y, double z) => new(x, y, z, 0);

    private static double W(IReadOnlyList<BedInput> inputs, string bed) => inputs.Single(b => b.Bed == bed).Weight;

    private static AtmosphereSnapshot Sky(double daylight, double intensity = 0, double wind = 0, string? kind = null) =>
        new(daylight * 12, DayPhase.Day, daylight, new WeatherChannels(0.5, 1, 0, wind, intensity > 0 ? kind ?? Kind : string.Empty, intensity), string.Empty, null);

    // The guide's camp: the lowland forest at y 42. The player's camp on the cliff is at y 72.
    private static readonly Listener Forest = At(176.5, 42, 186.5);
    private static readonly Listener Camp = At(155.5, 72, 115.5);

    private static Func<int, int, int> Ground(int top) => (_, _) => top;

    private static Func<int, int, int> RoofOver(int x0, int x1, int z0, int z1, int roof, int ground) => (x, z) => x >= x0 && x <= x1 && z >= z0 && z <= z1 ? roof : ground;

    [Fact]
    public void Given_NoRain_When_TheBedsAreRead_Then_TheRainBedIsSilent()
    {
        Assert.Equal(0.0, W(AmbientBeds.At(Real.Value, Forest, Sky(1), false), AmbientBeds.Rain));
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void Given_RainInTheOpen_When_TheBedsAreRead_Then_TheRainBedsWeightIsTheIntensity(double intensity)
    {
        var weights = AmbientBeds.At(Real.Value, Forest, Sky(1, intensity), false, Ground(41));

        Assert.Equal(intensity, W(weights, AmbientBeds.Rain), 9);
    }

    [Fact]
    public void Given_RisingIntensity_When_TheBedsAreRead_Then_TheRainBedNeverFalls()
    {
        var previous = -1.0;
        for (var step = 0; step <= 100; step++)
        {
            var weight = W(AmbientBeds.At(Real.Value, Forest, Sky(1, step / 100.0), false, Ground(41)), AmbientBeds.Rain);
            Assert.True(weight >= previous);
            previous = weight;
        }
    }

    [Fact]
    public void Given_AKindWithNoPrecipitationNamed_When_TheIntensityIsSet_Then_NothingFalls()
    {
        var snapshot = new AtmosphereSnapshot(12, DayPhase.Day, 1, new WeatherChannels(0.5, 1, 0, 0.3, string.Empty, 0.9), string.Empty, null);

        Assert.Equal(0.0, W(AmbientBeds.At(Real.Value, Forest, snapshot, false), AmbientBeds.Rain));
    }

    [Fact]
    public void Given_ARoofOverTheListener_When_TheBedsAreRead_Then_TheRainIsLowerByTheCoverCut()
    {
        var open = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0.8), false, Ground(41)), AmbientBeds.Rain);
        var under = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0.8), false, Ground(48)), AmbientBeds.Rain); // a canopy 7 blocks up over every column round the feet

        Assert.Equal(0.8, open, 9);
        Assert.Equal(0.8 * (1 - AmbientBeds.RainCoverCut), under, 9);
        Assert.True(under > 0, "the rain on a roof is still heard");
    }

    [Fact]
    public void Given_PlantsAtTheFeet_When_TheBedsAreRead_Then_TheyAreNotCover()
    {
        // Tall grass or a flower on the ground the listener stands on: its top is at the feet (42), not over the head.
        var weights = AmbientBeds.At(Real.Value, Forest, Sky(1, 0.8), false, Ground(42));

        Assert.Equal(0.8, W(weights, AmbientBeds.Rain), 9);
    }

    [Fact]
    public void Given_WalkingOutFromUnderAnEave_When_TheBedsAreRead_Then_TheRainRisesInSteps_AndNotInOne()
    {
        // A roof over x 100..110; the listener walks east out from under it along z 50.5.
        var roof = RoofOver(100, 110, 40, 60, 48, 41);
        var weights = Enumerable.Range(0, 9).Select(i => W(AmbientBeds.At(Real.Value, At(108.5 + i, 42, 50.5), Sky(1, 0.8), false, roof), AmbientBeds.Rain)).ToList();

        Assert.True(weights.Zip(weights.Skip(1)).All(static p => p.Second >= p.First - 1e-12), "walking out never lowers the rain");
        Assert.True(weights[0] < weights[^1]);
        Assert.Contains(weights, w => w > weights[0] + 0.01 && w < 0.8 - 0.01); // a step in between: the edge is gradual
        Assert.All(weights.Zip(weights.Skip(1)), p => Assert.True(p.Second - p.First < 0.8 * AmbientBeds.RainCoverCut / 2, "no step is more than half the cut"));
    }

    [Fact]
    public void Given_ACoverQuery_When_TheBedsAreRead_Then_ItIsNotAskedWhenItDoesNotRain()
    {
        var asked = 0;
        _ = AmbientBeds.At(Real.Value, Forest, Sky(1), false, (_, _) => { asked++; return 41; });
        Assert.Equal(0, asked);

        _ = AmbientBeds.At(Real.Value, Forest, Sky(1, 0.5), false, (_, _) => { asked++; return 41; });
        Assert.True(asked > 0 && asked <= 9, "the nine columns round the feet");
    }

    [Fact]
    public void Given_TheWindChannel_When_TheBedsAreRead_Then_TheWindBedRisesWithItAboveTheCalm()
    {
        var calm = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0, 0.2), false), AmbientBeds.Wind);
        var none = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0, 0), false), AmbientBeds.Wind);
        var breezy = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0, 0.6), false), AmbientBeds.Wind);
        var gale = W(AmbientBeds.At(Real.Value, Forest, Sky(1, 0, 1), false), AmbientBeds.Wind);

        Assert.Equal(0.0, none); // the valley has none, as before
        Assert.Equal(0.0, calm); // the first data's breeze (0.2) is below the calm: the lowland is as it was
        Assert.True(breezy > 0 && breezy < gale);
        Assert.Equal(AmbientBeds.WindChannelReach, gale, 9);
    }

    [Fact]
    public void Given_TheHeightAndTheWind_When_TheBedsAreRead_Then_TheWindAddsToTheHeightsAndStopsAtOne()
    {
        var middle = At(0, 63, 0); // half way up the wind's ramp
        var byHeight = W(AmbientBeds.At(Real.Value, middle, Sky(1, 0, 0), false), AmbientBeds.Wind);
        var withWind = W(AmbientBeds.At(Real.Value, middle, Sky(1, 0, 0.8), false), AmbientBeds.Wind);

        Assert.True(withWind > byHeight);
        Assert.Equal(1.0, W(AmbientBeds.At(Real.Value, Camp, Sky(1, 0, 1), false), AmbientBeds.Wind)); // the weights stay in range
        Assert.Equal(1.0, W(AmbientBeds.At(Real.Value, Camp, Sky(1, 0, 0.2), false), AmbientBeds.Wind)); // and the first data's camp is as it was
    }

    [Fact]
    public void Given_RainByDay_When_TheBedsAreRead_Then_TheBirdsAreQuieterByOneMinusTheIntensity()
    {
        var dry = W(AmbientBeds.At(Real.Value, Forest, Sky(1), false), AmbientBeds.Birds);
        Assert.Equal(1.0, dry);

        foreach (var intensity in new[] { 0.25, 0.5, 0.8, 1.0 })
        {
            var wet = W(AmbientBeds.At(Real.Value, Forest, Sky(1, intensity), false, Ground(41)), AmbientBeds.Birds);
            Assert.Equal(dry * (1 - intensity), wet, 9);
        }
    }

    [Fact]
    public void Given_RainAtSeveralPlaces_When_TheBedsAreRead_Then_EveryOtherBedIsWhatItWasWithoutIt()
    {
        var places = new[] { Forest, Camp, At(292.5, 37, 218.5), At(258.5 - 5, 100, 112.5), At(222, 34, 222) };
        foreach (var listener in places)
        {
            // The same wind channel with and without rain: only the rain and the birds differ.
            var dry = AmbientBeds.At(Real.Value, listener, Sky(0.4, 0, 0.6), true);
            var wet = AmbientBeds.At(Real.Value, listener, Sky(0.4, 0.8, 0.6), true, Ground((int)listener.Y - 1));

            foreach (var bed in AmbientBeds.All.Where(static b => b is not AmbientBeds.Rain and not AmbientBeds.Birds))
            {
                Assert.Equal(W(dry, bed), W(wet, bed), 12);
            }

            Assert.Equal(AmbientBeds.All, wet.Select(static b => b.Bed).ToList());
            Assert.All(wet, static b => Assert.InRange(b.Weight, 0, 1));
        }
    }

    [Fact]
    public void Given_TheFirstDatasKinds_When_TheBedsAreRead_Then_ThePlayersCampSoundsAsItDidInSunnyWeather()
    {
        foreach (var kind in TestGame.Data.Weather.Kinds.Where(static k => k.Channels.PrecipitationIntensity <= 0 && k.Channels.Wind <= AmbientBeds.WindCalm))
        {
            var snapshot = new AtmosphereSnapshot(13, DayPhase.Day, 1, kind.Channels, kind.Id, null);
            var weights = AmbientBeds.At(Real.Value, Forest, snapshot, false);

            Assert.Equal(0.0, W(weights, AmbientBeds.Rain));
            Assert.Equal(0.0, W(weights, AmbientBeds.Wind));
            Assert.Equal(1.0, W(weights, AmbientBeds.Birds));
        }
    }

    [Fact]
    public void Given_TheRainBed_When_TheMixerPlaysIt_Then_ItFadesInOverTheBedFade_DucksUnderAVoice_AndIsMutedWhileRecording()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0, 1.0), new PresentationRng(11), null);
        var rain = AmbientBeds.At(Real.Value, Forest, Sky(1, 0.8), false, Ground(41));
        mixer.SetBeds(rain);

        var steps = new List<double>();
        for (var i = 0; i < 120; i++)
        {
            mixer.Advance(1.0 / 60);
            steps.Add(mixer.BedLevels.Single(static b => b.Bed == AmbientBeds.Rain).Level);
        }

        Assert.Equal(0.8, steps[^1], 6); // the bed's level is its weight; the output's is the weight times the bed's gain
        Assert.True(steps.Zip(steps.Skip(1)).All(static p => p.Second >= p.First && p.Second - p.First < 0.02), "a smooth cross-fade, no step");

        mixer.SetVoice(true);
        Run(mixer, 1);
        Assert.Equal(0.8 * AmbientBeds.Gain(AmbientBeds.Rain) * SoundMixer.DuckLevel, output.Beds.Single(static b => b.Bed == AmbientBeds.Rain).Level, 2);
        mixer.SetVoice(false);
        Run(mixer, 2);

        mixer.Started();
        Run(mixer, 0.5);
        Assert.Equal(0.0, output.Beds.Single(static b => b.Bed == AmbientBeds.Rain).Level);
        mixer.Stopped();
        Run(mixer, 2.5);
        Assert.True(output.Beds.Single(static b => b.Bed == AmbientBeds.Rain).Level > 0.1, "the rain comes back when the recording ends");
    }

    [Fact]
    public void Given_TheBeds_When_TheWeatherBedsAreAsked_Then_OnlyTheRainIsOne()
    {
        Assert.True(AmbientBeds.IsWeather(AmbientBeds.Rain));
        Assert.All(AmbientBeds.All.Where(static b => b != AmbientBeds.Rain), static b => Assert.False(AmbientBeds.IsWeather(b), b));
    }

    [Fact]
    public void Given_TheRainBedAtTheDefaults_When_Played_Then_ItsLevelIsWhatItWasBeforeE09()
    {
        // The defaults: Ambient 0.6 and Weather 0.6, so the rain's output is its weight times its gain times 0.6, as it was when the rain shared the ambient volume.
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, SoundVolumes.Default, new PresentationRng(11), null);
        mixer.SetBeds([new BedInput(AmbientBeds.Rain, 1.0)]);
        Run(mixer, 3);
        Assert.Equal(0.2 * 0.6, output.Beds.Single(static b => b.Bed == AmbientBeds.Rain).Level, 6);
    }

    [Fact]
    public void Given_TheViewUnderWater_When_TheRainBedPlays_Then_ItIsDulledLikeTheOtherBeds()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(11), null);
        mixer.SetBeds(AmbientBeds.At(Real.Value, Forest, Sky(1, 1.0), false, Ground(41)));
        Run(mixer, 2);
        var dry = output.Beds.Single(static b => b.Bed == AmbientBeds.Rain);
        Assert.Equal(0.0, dry.Muffle, 6);

        mixer.SetUnderwater(1);
        Run(mixer, 0.1);
        var wet = output.Beds.Single(static b => b.Bed == AmbientBeds.Rain);

        Assert.Equal(1.0, wet.Muffle, 6);
        Assert.Equal(dry.Level * UnderwaterSound.FullUnderwaterGain, wet.Level, 6);
    }

    [Fact]
    public void Given_RainAtTheFirstDatasIntensity_When_ItsLevelIsCompared_Then_ItStaysBelowEveryOtherBedAtItsOwnPlace()
    {
        // The gain is set so the rain does not mask a voice before the duck: at the data's intensity its level is lower than the village murmur's, the river's and the wind's at their places.
        var intensity = TestGame.Data.Weather.Kinds.Max(static k => k.Channels.PrecipitationIntensity);
        var rain = intensity * AmbientBeds.Gain(AmbientBeds.Rain);

        Assert.True(rain < AmbientBeds.Gain(AmbientBeds.Village));
        Assert.True(rain < AmbientBeds.Gain(AmbientBeds.River));
        Assert.True(rain < AmbientBeds.Gain(AmbientBeds.Wind));
    }

    [Fact]
    public void Given_TheRainLoop_When_ItIsMade_Then_ItIsSeamlessAudibleDeterministicAndSmall()
    {
        var loop = AmbientLoops.Render(AmbientBeds.Rain);

        Assert.Contains(AmbientBeds.Rain, AmbientLoops.Looping);
        Assert.Equal((int)(AmbientLoops.LoopSeconds * SoundSynth.SampleRate), loop.Length);
        Assert.Equal(loop, AmbientLoops.Render(AmbientBeds.Rain));

        var steps = new List<int>();
        for (var i = 1; i < loop.Length; i++)
        {
            steps.Add(Math.Abs(loop[i] - loop[i - 1]));
        }

        var seam = Math.Abs(loop[0] - loop[^1]);
        Assert.True(seam <= (4 * steps.Average()) + 50, $"the seam steps {seam}, the mean step is {steps.Average():0}");
        Assert.InRange(loop.Max(static s => Math.Abs((int)s)), 0.65 * short.MaxValue, 0.75 * short.MaxValue);
        Assert.True(Math.Sqrt(loop.Average(static s => s * (double)s)) > 0.02 * short.MaxValue, "audible");

        // The size: 16-bit mono, like every loop (about 430 KiB), and all the loops together, with the pre-filtered copy under water, far below the audio budget of 15 MB.
        var bytes = loop.Length * 2L;
        Assert.True(bytes <= 450_000, $"the rain loop is {bytes} bytes");
        var all = AmbientLoops.Looping.Sum(static bed => AmbientLoops.Render(bed).Length * 2L * 2);
        Assert.True(all < 15L * 1024 * 1024, $"the loops take {all / 1024.0 / 1024:0.0} MB with their muffled copies");
    }

    [Fact]
    public void Given_TheRainLoop_When_ItsSpectrumIsRead_Then_ItIsAHissAboveTheWindsAndWithoutATone()
    {
        var rain = AmbientLoops.Render(AmbientBeds.Rain);
        var wind = AmbientLoops.Render(AmbientBeds.Wind);

        // The share of the energy in the first difference (a crude high-pass): the rain's is far higher than the wind's, which is low-passed.
        static double Brightness(short[] x)
        {
            double total = 0;
            double diff = 0;
            for (var i = 1; i < x.Length; i++)
            {
                total += (double)x[i] * x[i];
                diff += (double)(x[i] - x[i - 1]) * (x[i] - x[i - 1]);
            }

            return diff / total;
        }

        Assert.True(Brightness(rain) > 3 * Brightness(wind), $"rain {Brightness(rain):0.00}, wind {Brightness(wind):0.00}");
    }

    private static void Run(SoundMixer mixer, double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds; elapsed += 1.0 / 60)
        {
            mixer.Advance(1.0 / 60);
        }
    }
}
