using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The place query (B06, <see cref="IPlaceInfo"/>): climate, temperature, biome, region, natural surface and underground, read from the pure model for sample places, and from any thread.</summary>
public sealed class WorldQueryTests
{
    private static IPlaceInfo Places => RegionTestWorld.Composition().Places;

    [Fact]
    public void Given_PlacesInsideAndOutsideTheValley_When_TheRegionIsAsked_Then_OnlyTheValleysFootprintHasOne()
    {
        Assert.Equal(WorldFactory.ValleyRegionId, Places.RegionAt(200, 200)!.Id);
        Assert.Equal(WorldFactory.ValleyRegionId, Places.RegionAt(0, 0)!.Id);
        Assert.Equal(WorldFactory.ValleyRegionId, Places.RegionAt(383, 383)!.Id);
        Assert.Null(Places.RegionAt(384, 200));
        Assert.Null(Places.RegionAt(-1, 200));
        Assert.Null(Places.RegionAt(200, 384));
        Assert.Null(Places.RegionAt(5000, -5000));
    }

    [Fact]
    public void Given_ThePlacesOfTheValleyAndTheLand_When_TheNaturalSurfaceIsAsked_Then_ItIsTheRegionsGroundInsideAndTheBlendedModelsOutside()
    {
        var region = RegionTestWorld.Valley();
        foreach (var (x, z) in new[] { (200, 200), (50, 350), (383, 208) })
        {
            Assert.Equal(region.EdgeHeight(x, z), Places.NaturalSurfaceAt(x, z));
        }

        foreach (var (x, z) in new[] { (384, 208), (450, 100), (-40, -40), (3000, -2000) })
        {
            Assert.Equal(RegionTestWorld.Shape(RegionTestWorld.Composition(), x, z).Height, Places.NaturalSurfaceAt(x, z));
        }
    }

    [Fact]
    public void Given_APlace_When_UndergroundIsAsked_Then_ItIsBelowTheNaturalSurfaceOnly()
    {
        foreach (var (x, z) in new[] { (200, 200), (400, 208), (1200, 900) })
        {
            var surface = Places.NaturalSurfaceAt(x, z);
            Assert.True(Places.IsUnderground(x, surface - 1, z));
            Assert.True(Places.IsUnderground(x, -30, z));
            Assert.False(Places.IsUnderground(x, surface, z));
            Assert.False(Places.IsUnderground(x, surface + 1, z));
            Assert.False(Places.IsUnderground(x, surface + 80, z));
        }
    }

    [Fact]
    public void Given_APlace_When_TemperatureIsAskedAtDifferentHeights_Then_ItFallsWithAltitudeAsTheClimateSaysAndTheBiomeIsARowOfTheData()
    {
        var table = TestGame.Data.Biomes;
        foreach (var (x, z) in new[] { (200, 200), (600, 300), (-800, 40) })
        {
            Assert.True(Places.TemperatureAt(x, 150, z) < Places.TemperatureAt(x, 32, z));
            Assert.Equal(Places.TemperatureAt(x, 90, z), Places.TemperatureAt(x, 90, z));
            Assert.Contains(Places.BiomeAt(x, z).Id, table.Rows.Select(static r => r.Id));
            Assert.InRange(Places.ClimateAt(x, z).Continentalness, -4096, 4096);
        }

        Assert.True(Places.ClimateAt(200, 200).Continentalness >= 100 * 4096 / 1000 - 1); // the land bias: the valley is never at sea
    }

    [Fact]
    public void Given_AStreamedWorldWhoseGridIsEdited_When_ThePlaceIsAskedAgain_Then_TheAnswersAreTheSameBecauseTheyAreReadFromTheModelNotTheBlocks()
    {
        var scene = RegionTestWorld.StreamedScene();
        var places = scene.Places!;
        var asked = new[] { (200, 200), (400, 208), (900, 700) };
        var before = asked.Select(p => (places.NaturalSurfaceAt(p.Item1, p.Item2), places.IsUnderground(p.Item1, 40, p.Item2), places.BiomeAt(p.Item1, p.Item2).Id)).ToList();

        foreach (var (x, z) in asked)
        {
            var top = places.NaturalSurfaceAt(x, z);
            scene.Grid.SetBlock(x, top, z, BlockId.Air);
            scene.Grid.SetBlock(x, top - 1, z, BlockId.Air);
            scene.Grid.SetBlock(x, top + 1, z, BlockId.Stone);
        }

        Assert.Equal(before, asked.Select(p => (places.NaturalSurfaceAt(p.Item1, p.Item2), places.IsUnderground(p.Item1, 40, p.Item2), places.BiomeAt(p.Item1, p.Item2).Id)).ToList());
    }

    [Fact]
    public void Given_ManyThreads_When_TheyAskTheSamePlaces_Then_TheAnswersAreTheSameAsOnOneThread()
    {
        var points = Enumerable.Range(0, 600).Select(i => (X: ((i * 37) % 1500) - 400, Z: ((i * 91) % 1500) - 600)).ToList();
        string Answer((int X, int Z) p) =>
            $"{Places.NaturalSurfaceAt(p.X, p.Z)} {Places.ClimateAt(p.X, p.Z)} {Places.BiomeAt(p.X, p.Z).Id} {Places.RegionAt(p.X, p.Z)?.Id} {Places.TemperatureAt(p.X, 70, p.Z)} {Places.IsUnderground(p.X, 50, p.Z)}";
        var alone = points.Select(Answer).ToArray();
        var threaded = new string[points.Count];

        Parallel.For(0, points.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => threaded[i] = Answer(points[i]));

        Assert.Equal(alone, threaded);
    }

    [Fact]
    public void Given_ASceneTheFactoryMade_When_ItsPlacesAreRead_Then_TheyAreThePlaceQueryOfItsWorld()
    {
        var scene = RegionTestWorld.StreamedScene();

        Assert.NotNull(scene.Places);
        Assert.IsType<WorldQuery>(scene.Places);
        Assert.Same(RegionTestWorld.Valley().Id, scene.Places.RegionAt(10, 10)!.Id);
        Assert.IsAssignableFrom<IRegion>(scene.Places.RegionAt(10, 10));
    }
}
