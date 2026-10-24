using Vocaterra.Game;
using Vocaterra.MonoGame.Sound;
using Vocaterra.MonoGame.Utilities;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The ambient beds (S25): the weights at fixed points of the real scene (the camp, the forest, the river, the village, the waterfall and the guide's camp), the
/// pan and the falloff, the cross-fade's continuity, and the beds' silence while recording and their ducking under a voice.
/// </summary>
public sealed class AmbientBedsTests
{
    private static readonly Lazy<AmbientPlaces> Real = new(() => AmbientPlaces.From(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json"))));

    private const int North = 0;
    private const int East = 1024;
    private const int South = 2048;

    // A site's middle, the height of its ground, and the facing (north unless said otherwise).
    private static Listener At(double x, double y, double z, int yaw = North) => new(x, y, z, yaw);

    private static double W(IReadOnlyList<BedInput> inputs, string bed) => inputs.Single(b => b.Bed == bed).Weight;

    private static double P(IReadOnlyList<BedInput> inputs, string bed) => inputs.Single(b => b.Bed == bed).Pan;

    [Fact]
    public void TheCampOnTheCliffHearsTheWindAndNoForestOrVillage()
    {
        // The player's camp: the brink of the ledge at y 72, the fire ring behind it (lit at the end of the session).
        var camp = At(155.5, 72, 115.5);

        var unlit = AmbientBeds.At(Real.Value, camp, dusk: false, fireLit: false);
        Assert.Equal(1.0, W(unlit, AmbientBeds.Wind));
        Assert.Equal(0.0, W(unlit, AmbientBeds.Birds));
        Assert.Equal(0.0, W(unlit, AmbientBeds.Village));
        Assert.Equal(0.0, W(unlit, AmbientBeds.Waterfall));
        Assert.Equal(0.0, W(unlit, AmbientBeds.Fire)); // the ring is cold until the fire is lit

        var lit = AmbientBeds.At(Real.Value, camp, dusk: false, fireLit: true);
        Assert.InRange(W(lit, AmbientBeds.Fire), 0.5, 1.0);
        Assert.Equal("camp.player", AmbientBeds.PlaceOf(Real.Value, camp));
    }

    [Fact]
    public void TheForestByTheGuidesCampHearsTheBirdsByDayAndTheInsectsAtDusk()
    {
        // The guide's camp: in the lowland forest at y 42, far from the village and the river.
        var forest = At(176.5, 42, 186.5);

        var day = AmbientBeds.At(Real.Value, forest, dusk: false, fireLit: false);
        Assert.Equal(1.0, W(day, AmbientBeds.Birds));
        Assert.Equal(0.0, W(day, AmbientBeds.Insects));
        Assert.Equal(0.0, W(day, AmbientBeds.Wind));
        Assert.Equal(0.0, W(day, AmbientBeds.River));
        Assert.Equal(0.0, W(day, AmbientBeds.Village));

        var dusk = AmbientBeds.At(Real.Value, forest, dusk: true, fireLit: false);
        Assert.Equal(0.0, W(dusk, AmbientBeds.Birds));
        Assert.Equal(1.0, W(dusk, AmbientBeds.Insects));

        Assert.Equal("camp.guide", AmbientBeds.PlaceOf(Real.Value, forest));
    }

    [Fact]
    public void TheRiverBedIsFullAtTheRiverAndSilentByTheCampAndPansToTheSideItRuns()
    {
        var river = At(230.5, 37, 222.5);
        var weights = AmbientBeds.At(Real.Value, river, dusk: false, fireLit: false);
        Assert.Equal(1.0, W(weights, AmbientBeds.River));
        Assert.Equal(0.0, W(weights, AmbientBeds.Waterfall));
        Assert.Equal(0.0, W(weights, AmbientBeds.Wind));

        // Facing north, the river lies straight ahead; facing east it lies on the left (the river is to the east at the bridge): the pan follows the bearing.
        var eastOfTheRiver = At(226.5, 37, 222.5, North);
        Assert.True(P(AmbientBeds.At(Real.Value, eastOfTheRiver, false, false), AmbientBeds.River) > 0.5, "the river is to the east: on the right when facing north");
        var facingEast = At(226.5, 37, 222.5, East);
        Assert.True(Math.Abs(P(AmbientBeds.At(Real.Value, facingEast, false, false), AmbientBeds.River)) < 0.2, "the river ahead when facing east: centred");
        var facingSouth = At(226.5, 37, 222.5, South);
        Assert.True(P(AmbientBeds.At(Real.Value, facingSouth, false, false), AmbientBeds.River) < -0.5, "the river is to the east: on the left when facing south");
    }

    [Fact]
    public void TheVillageBedIsFullInTheVillageAndTheForestBirdsFadeOut()
    {
        var village = At(292.5, 37, 218.5);
        var weights = AmbientBeds.At(Real.Value, village, dusk: false, fireLit: false);
        Assert.Equal(1.0, W(weights, AmbientBeds.Village));
        Assert.Equal(0.0, W(weights, AmbientBeds.Birds), 6);
        Assert.Equal(0.0, W(weights, AmbientBeds.River));
        Assert.Equal("village", AmbientBeds.PlaceOf(Real.Value, village));

        // The stall stands on the square's west side, inside the village's reach.
        var stall = At(286.5, 37, 209.5);
        Assert.InRange(W(AmbientBeds.At(Real.Value, stall, false, false), AmbientBeds.Village), 0.9, 1.0);
    }

    [Fact]
    public void TheWaterfallIsLouderTheNearerItIsAndFallsAwayWithDistance()
    {
        var lip = At(258.5, 100, 112.5);
        var nearWaterfall = AmbientBeds.At(Real.Value, lip, false, false);
        Assert.Equal(1.0, W(nearWaterfall, AmbientBeds.Waterfall), 6);
        Assert.Equal("waterfall", AmbientBeds.PlaceOf(Real.Value, lip));

        var previous = 2.0;
        foreach (var distance in new[] { 2.0, 8.0, 16.0, 24.0, 32.0, 40.0 })
        {
            var weight = W(AmbientBeds.At(Real.Value, At(258.5 - distance, 100, 112.5), false, false), AmbientBeds.Waterfall);
            Assert.True(weight <= previous, $"the waterfall weight falls with distance (at {distance})");
            previous = weight;
        }

        Assert.Equal(0.0, W(AmbientBeds.At(Real.Value, At(258.5 - 40, 100, 112.5), false, false), AmbientBeds.Waterfall));
    }

    [Fact]
    public void TheWindRisesOnTheHeightsAndIsSilentInTheValley()
    {
        Assert.Equal(0.0, W(AmbientBeds.At(Real.Value, At(176.5, 42, 186.5), false, false), AmbientBeds.Wind));
        Assert.Equal(0.0, W(AmbientBeds.At(Real.Value, At(292.5, 37, 218.5), false, false), AmbientBeds.Wind));
        var middle = W(AmbientBeds.At(Real.Value, At(0, 63, 0), false, false), AmbientBeds.Wind);
        Assert.InRange(middle, 0.1, 0.9);
        Assert.Equal(1.0, W(AmbientBeds.At(Real.Value, At(0, 90, 0), false, false), AmbientBeds.Wind));
    }

    [Fact]
    public void EveryBedIsListedInTheSameOrderAndWeightsStayInRange()
    {
        var inputs = AmbientBeds.At(Real.Value, At(200.5, 50, 200.5), dusk: false, fireLit: false);
        Assert.Equal(AmbientBeds.All, inputs.Select(static b => b.Bed).ToList());
        foreach (var input in inputs)
        {
            Assert.InRange(input.Weight, 0, 1);
            Assert.InRange(input.Pan, -1, 1);
        }
    }

    [Fact]
    public void PanIsTheBearingFromTheWayThePlayerFacesAndCentredOnTheSpot()
    {
        var listener = At(0.5, 0, 0.5, North);
        Assert.Equal(0.0, AmbientBeds.Pan(listener, new Point2(0.5, 0.5)));
        Assert.Equal(1.0, AmbientBeds.Pan(At(0.5, 0, 0.5, North), new Point2(10.5, 0.5)), 6);
        Assert.Equal(-1.0, AmbientBeds.Pan(At(0.5, 0, 0.5, North), new Point2(-9.5, 0.5)), 6);
        Assert.Equal(0.0, AmbientBeds.Pan(At(0.5, 0, 0.5, North), new Point2(0.5, -10.5)), 6); // straight ahead
        Assert.Equal(-1.0, AmbientBeds.Pan(At(0.5, 0, 0.5, East), new Point2(0.5, -10.5)), 6); // facing east, north is on the left
    }

    [Fact]
    public void TheDistanceToARiverPathIsTheNearestSegment()
    {
        var path = new[] { new Point2(0, 0), new Point2(10, 0), new Point2(10, 10) };
        Assert.Equal(3.0, AmbientBeds.DistanceToPath(new Point2(5, 3), path, out var nearest), 9);
        Assert.Equal(new Point2(5, 0), nearest);
        Assert.Equal(2.0, AmbientBeds.DistanceToPath(new Point2(12, 5), path, out _), 9);
        Assert.Equal(0.0, AmbientBeds.DistanceToPath(new Point2(10, 10), path, out _), 9);
    }

    [Fact]
    public void TheCrossFadeMovesFromOnePlaceToAnotherWithoutAHardCut()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(3), null);
        var camp = AmbientBeds.At(Real.Value, At(155.5, 72, 115.5), false, false);
        var village = AmbientBeds.At(Real.Value, At(292.5, 37, 218.5), false, false);

        mixer.SetBeds(camp);
        for (var i = 0; i < 180; i++)
        {
            mixer.Advance(1.0 / 60); // three seconds at the camp: wind full
        }

        Assert.Equal(1.0, mixer.BedLevels.Single(b => b.Bed == AmbientBeds.Wind).Level, 6);

        mixer.SetBeds(village);
        var previous = mixer.BedLevels.ToDictionary(static b => b.Bed, static b => b.Level, StringComparer.Ordinal);
        var largestStep = 0.0;
        var frames = 0;
        for (; frames < 180; frames++)
        {
            mixer.Advance(1.0 / 60);
            foreach (var level in mixer.BedLevels)
            {
                var before = previous.TryGetValue(level.Bed, out var p) ? p : 0;
                largestStep = Math.Max(largestStep, Math.Abs(level.Level - before));
                previous[level.Bed] = level.Level;
            }
        }

        // The village bed rises over the mixer's 1.5 s, and the wind falls over the same time: no step is larger than the fade allows for one frame.
        var allowed = (1.0 / 60) / SoundMixer.BedFadeSeconds + 1e-9;
        Assert.True(largestStep <= allowed + 1e-9, $"largest step {largestStep:0.0000} > {allowed:0.0000}");
        Assert.Equal(1.0, mixer.BedLevels.Single(b => b.Bed == AmbientBeds.Village).Level, 6);
        Assert.DoesNotContain(mixer.BedLevels, static b => b.Bed == AmbientBeds.Wind && b.Level > 1e-9);
        Assert.Equal(180, frames);
    }

    [Fact]
    public void TheBedsAreSilentWhileTheMicrophoneRecordsAndComeBackAfterwards()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(5), null);
        mixer.SetBeds(AmbientBeds.At(Real.Value, At(155.5, 72, 115.5), false, false));
        Run(mixer, 2);
        Assert.True(output.Beds.Single(static b => b.Bed == AmbientBeds.Wind).Level > 0.3);

        mixer.Started();
        Run(mixer, 0.5);
        Assert.All(output.Beds, static b => Assert.Equal(0.0, b.Level));
        mixer.Stopped();
        Run(mixer, 2);
        Assert.True(output.Beds.Single(static b => b.Bed == AmbientBeds.Wind).Level > 0.3, "the bed comes back when the recording ends");
    }

    [Fact]
    public void TheBedsDuckUnderAVoiceLikeTheOtherSounds()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(9), null);
        mixer.SetBeds(AmbientBeds.At(Real.Value, At(155.5, 72, 115.5), false, false));
        Run(mixer, 2);
        var free = output.Beds.Single(static b => b.Bed == AmbientBeds.Wind).Level;

        mixer.SetVoice(true);
        Run(mixer, 1);
        var ducked = output.Beds.Single(static b => b.Bed == AmbientBeds.Wind).Level;
        Assert.InRange(ducked / free, SoundMixer.DuckLevel - 0.02, SoundMixer.DuckLevel + 0.02);
    }

    private static void Run(SoundMixer mixer, double seconds)
    {
        for (var elapsed = 0.0; elapsed < seconds; elapsed += 1.0 / 60)
        {
            mixer.Advance(1.0 / 60);
        }
    }
}
