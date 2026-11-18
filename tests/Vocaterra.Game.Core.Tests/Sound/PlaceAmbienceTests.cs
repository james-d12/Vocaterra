using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>
/// The ambient beds by place (B14): the bed a place in each biome of <c>game/biomes.json</c> hears is the one its row names, the cave bed takes over under the ground (quiet, and the surface beds go), the
/// hand-crafted regions keep the beds they had, a border between two biomes is a slope the mixer then cross-fades, and no biome id is named in the source.
/// </summary>
public sealed class PlaceAmbienceTests
{
    private static readonly AmbientPlaces None = new(null, null, null, [], null, [], []);

    private static AtmosphereSnapshot Sky(double daylight) => new(daylight * 12, DayPhase.Day, daylight, default, string.Empty, null);

    private static readonly AtmosphereSnapshot Noon = Sky(1);
    private static readonly AtmosphereSnapshot Night = Sky(0);

    private static double W(IReadOnlyList<BedInput> inputs, string bed) => inputs.Single(b => b.Bed == bed).Weight;

    private static IReadOnlyList<BedInput> At(IPlaceInfo places, double x, double y, double z, AtmosphereSnapshot sky) =>
        AmbientBeds.At(None, new Listener(x, y, z, 0), sky, fireLit: false, place: PlaceAmbience.At(places, x, y, z));

    /// <summary>A world of one biome everywhere (or of two, split at x = 0), with a flat surface at y 70 and a roof of stone over the heights below it.</summary>
    private sealed class Fake(BiomeRow west, BiomeRow? east = null, int surface = 70, bool region = false) : IPlaceInfo
    {
        public ClimateSample ClimateAt(int x, int z) => default;

        public int SnowBelowTemperature => int.MinValue;

        public int TemperatureAt(int x, int y, int z) => 0;

        public BiomeRow BiomeAt(int x, int z) => east is not null && x >= 0 ? east : west;

        public IRegion? RegionAt(int x, int z) => region ? RegionTestWorld.Valley() : null;

        public int NaturalSurfaceAt(int x, int z) => surface;

        public bool IsUnderground(int x, int y, int z) => y < surface;
    }

    private static BiomeRow Row(string id) => TestGame.Data.Biomes.Row(id);

    public static TheoryData<string> Biomes
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var row in TestGame.Data.Biomes.Rows)
            {
                data.Add(row.Id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Biomes))]
    public void Given_APlaceInEachBiome_When_TheBedsAreChosen_Then_ItsRowsBedsAreHeardAndNoOtherPlaceBed(string id)
    {
        var row = Row(id);
        var world = new Fake(row);
        var day = At(world, 10.5, 71, 10.5, Noon);
        var night = At(world, 10.5, 71, 10.5, Night);

        Assert.NotEmpty(row.Ambient ?? []);
        foreach (var bed in AmbientBeds.ByPlace)
        {
            var share = (row.Ambient ?? []).Where(s => s.Bed == bed).Sum(static s => s.Weight) / 1000.0;
            var byDay = bed == AmbientBeds.Insects ? 0 : share; // the insects are the night's, the birds the day's
            var byNight = bed == AmbientBeds.Birds ? 0 : share;
            Assert.Equal(byDay, W(day, bed), 6);
            Assert.Equal(byNight, W(night, bed), 6);
        }

        Assert.Equal(0.0, W(day, AmbientBeds.Cave));
        Assert.Equal(id, PlaceAmbience.At(world, 10.5, 71, 10.5).Name);
    }

    [Fact]
    public void Given_TheShippedBiomes_When_TheirAmbientIsRead_Then_EveryRowNamesBedsThatExistAndTheLoudestIsWhatTheBiomeIsKnownFor()
    {
        foreach (var row in TestGame.Data.Biomes.Rows)
        {
            Assert.All(row.Ambient!, share => Assert.Contains(share.Bed, AmbientBeds.ByPlace));
            Assert.All(row.Ambient!, share => Assert.InRange(share.Weight, 0, 1000));
        }

        string Loudest(string id) => Row(id).Ambient!.MaxBy(static s => s.Weight)!.Bed;
        Assert.Equal(AmbientBeds.Surf, Loudest("ocean"));
        Assert.Equal(AmbientBeds.River, Loudest("river-channel"));
        Assert.Equal(AmbientBeds.Wind, Loudest("jagged-peaks"));
        Assert.Equal(AmbientBeds.Birds, Loudest("dense-woodland"));
    }

    [Theory]
    [InlineData(70, 71, 0)] // standing on the ground: nothing overhead
    [InlineData(70, 52, 1)] // eighteen blocks down: the cave and nothing of the surface
    public void Given_ThePlayerUnderTheGround_When_TheBedsAreChosen_Then_TheCaveBedReplacesTheSurfaceOnes(int surface, int feet, int cave)
    {
        var world = new Fake(Row("dense-woodland"), surface: surface);
        var inputs = At(world, 10.5, feet, 10.5, Noon);

        Assert.Equal(cave, W(inputs, AmbientBeds.Cave));
        if (cave == 1)
        {
            Assert.All(inputs.Where(static b => b.Bed != AmbientBeds.Cave), static b => Assert.Equal(0.0, b.Weight));
            Assert.Equal("underground", PlaceAmbience.At(world, 10.5, feet, 10.5).Name);
        }
    }

    [Fact]
    public void Given_TheDepth_When_ItGrows_Then_TheCaveBedRisesSmoothlyAsTheSurfaceBedsFall()
    {
        var world = new Fake(Row("dense-woodland"));
        var last = (Cave: -1.0, Birds: 2.0);
        for (var depth = 0.0; depth <= 16; depth += 0.5)
        {
            var feet = 70 - 1.6 - depth;
            var inputs = At(world, 10.5, feet, 10.5, Noon);
            var now = (Cave: W(inputs, AmbientBeds.Cave), Birds: W(inputs, AmbientBeds.Birds));
            Assert.True(now.Cave >= last.Cave && now.Birds <= last.Birds, $"at {depth} the cave bed is {now.Cave} and the birds {now.Birds}");
            Assert.True(now.Cave - last.Cave < 0.2 || last.Cave < 0, "a half block deeper moves the bed by less than 0.2");
            last = now;
        }

        Assert.Equal(1.0, last.Cave);
        Assert.Equal(0.0, last.Birds);
    }

    [Fact]
    public void Given_TheCaveBed_When_ItIsRendered_Then_ItIsTheQuietestOfTheBedsAndRepeatsAsALoop()
    {
        static double Rms(short[] loop) => Math.Sqrt(loop.Sum(static s => (double)s * s) / loop.Length) / short.MaxValue;
        var cave = AmbientLoops.Render(AmbientBeds.Cave);
        var surf = AmbientLoops.Render(AmbientBeds.Surf);

        Assert.Equal((int)(AmbientLoops.LoopSeconds * SoundSynth.SampleRate), cave.Length);
        Assert.Equal(cave, AmbientLoops.Render(AmbientBeds.Cave));
        Assert.NotEqual(cave, surf);
        Assert.InRange(Math.Abs((int)cave[^1] - cave[0]), 0, 2000); // the end meets the start: no click at the loop's seam
        var level = Rms(cave) * AmbientBeds.Gain(AmbientBeds.Cave);
        foreach (var bed in AmbientLoops.Looping.Where(static b => b != AmbientBeds.Cave))
        {
            Assert.True(level < Rms(AmbientLoops.Render(bed)) * AmbientBeds.Gain(bed), $"the cave bed is quieter than the {bed} bed");
        }
    }

    [Fact]
    public void Given_ARegion_When_TheBedsAreChosen_Then_TheScenesBedsStayAsTheyWere()
    {
        var composition = RegionTestWorld.Composition();
        var camp = new Listener(155.5, 72, 115.5, 0); // the mountain ledge
        var places = AmbientPlaces.From(Vocaterra.Game.Core.World.Layout.SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")));

        var before = AmbientBeds.At(places, camp, Noon, fireLit: false);
        var after = AmbientBeds.At(places, camp, Noon, fireLit: false, place: PlaceAmbience.At(composition.Places, camp.X, camp.Y, camp.Z));

        Assert.False(PlaceAmbience.At(composition.Places, camp.X, camp.Y, camp.Z).Procedural);
        Assert.Equal(before.Select(static b => (b.Bed, b.Weight, b.Pan)).Where(static b => b.Bed != AmbientBeds.Surf && b.Bed != AmbientBeds.Cave), after.Select(static b => (b.Bed, b.Weight, b.Pan)).Where(static b => b.Bed != AmbientBeds.Surf && b.Bed != AmbientBeds.Cave));
        Assert.Equal("camp.player", AmbientBeds.PlaceOf(places, camp, PlaceAmbience.At(composition.Places, camp.X, camp.Y, camp.Z)));
    }

    [Fact]
    public void Given_ARegionWithGroundOverhead_When_TheBedsAreChosen_Then_TheCaveBedIsHeardThereToo()
    {
        var world = new Fake(Row("plains"), surface: 90, region: true);

        var inputs = At(world, 10.5, 60, 10.5, Noon);

        Assert.Equal(1.0, W(inputs, AmbientBeds.Cave));
        Assert.Equal(0.0, W(inputs, AmbientBeds.Wind));
        Assert.Equal("underground", AmbientBeds.PlaceOf(None, new Listener(10.5, 60, 10.5, 0), PlaceAmbience.At(world, 10.5, 60, 10.5)));
    }

    [Fact]
    public void Given_ABorderBetweenTwoBiomes_When_TheListenerWalksAcross_Then_TheBedsSlideAndTheMixerCrossFades()
    {
        var world = new Fake(Row("ocean"), Row("dense-woodland")); // west of x 0 the sea, east of it the forest
        var steps = Enumerable.Range(-12, 25).Select(i => At(world, i + 0.5, 71, 0.5, Noon)).ToList();

        // A block's step moves no bed by more than the columns' share of it (a fifth of the biome's own weight at most).
        for (var i = 1; i < steps.Count; i++)
        {
            foreach (var bed in AmbientBeds.ByPlace)
            {
                Assert.True(Math.Abs(W(steps[i], bed) - W(steps[i - 1], bed)) <= 0.2 + 1e-9, $"{bed} jumps between {i - 1} and {i}");
            }
        }

        Assert.Equal(1.0, W(steps[0], AmbientBeds.Surf), 6);
        Assert.Equal(0.0, W(steps[^1], AmbientBeds.Surf), 6);
        Assert.Equal(1.0, W(steps[^1], AmbientBeds.Birds), 6);

        // And the mixer, given the targets of the two sides, moves a bed by no more than its fade allows in a tick.
        var mixer = new SoundMixer(new RecordingOutput(), new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(1), new ListLog());
        mixer.SetBeds(steps[0]);
        for (var t = 0; t < 120; t++)
        {
            mixer.Advance(1.0 / 60);
        }

        var surf = mixer.BedLevels.Single(static b => b.Bed == AmbientBeds.Surf).Level;
        mixer.SetBeds(steps[^1]);
        mixer.Advance(1.0 / 60);
        var next = mixer.BedLevels.Single(static b => b.Bed == AmbientBeds.Surf).Level;
        Assert.InRange(surf - next, 0, (1.0 / 60 / SoundMixer.BedFadeSeconds) + 1e-9);
        Assert.True(surf > next && next > 0, "the surf is still there a tick after the listener crosses");
    }

    [Fact]
    public void Given_TheAmbientSound_When_ItObservesAListenerInAPlace_Then_ItsLogNamesTheBiomeAndTheCave()
    {
        var log = new ListLog();
        var mixer = new SoundMixer(new RecordingOutput(), new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(3), log);
        var ambient = new AmbientSound(mixer, None, new Fake(Row("plains")));

        ambient.Observe(10, new Listener(10.5, 71, 10.5, 0), Noon, fireLit: false);
        ambient.Observe(20, new Listener(10.5, 50, 10.5, 0), Noon, fireLit: false);

        var marks = log.Lines.Where(static l => l.Contains("mark", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, marks.Count);
        Assert.Contains("bed at plains", marks[0], StringComparison.Ordinal);
        Assert.Contains("bed at underground: cave=1.0", marks[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(2026UL)]
    [Trait(TestSpeed.Trait, TestSpeed.Slow)]
    public void Given_TheRealWorldOfASeed_When_AColumnOfEachBiomeIsFound_Then_ItHearsTheBedsOfItsRow(ulong seed)
    {
        var places = RegionTestWorld.Composition(seed).Places;
        var found = new Dictionary<string, (int X, int Z)>(StringComparer.Ordinal);
        for (var z = -4096; z <= 4096 && found.Count < TestGame.Data.Biomes.Rows.Count; z += 64)
        {
            for (var x = -4096; x <= 4096; x += 64)
            {
                if (places.RegionAt(x, z) is not null)
                {
                    continue;
                }

                var row = places.BiomeAt(x, z);
                var steady = new[] { (6, 2), (-6, -2), (2, -6), (-2, 6) }.All(d => places.RegionAt(x + d.Item1, z + d.Item2) is null && places.BiomeAt(x + d.Item1, z + d.Item2).Id == row.Id);
                if (steady && !found.ContainsKey(row.Id))
                {
                    found[row.Id] = (x, z);
                }
            }
        }

        Assert.True(found.Count >= 10, $"seed {seed}: columns of {found.Count} biomes found ({string.Join(", ", found.Keys)})");
        foreach (var (id, (x, z)) in found)
        {
            var y = places.NaturalSurfaceAt(x, z) + 1;
            var place = PlaceAmbience.At(places, x + 0.5, y, z + 0.5);
            Assert.Equal(id, place.Name);
            foreach (var share in Row(id).Ambient!)
            {
                Assert.Equal(share.Weight / 1000.0, place.Share(share.Bed), 6);
            }
        }
    }
}
