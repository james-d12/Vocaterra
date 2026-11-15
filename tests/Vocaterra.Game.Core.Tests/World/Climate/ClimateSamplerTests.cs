using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.Tests.World.Climate;

/// <summary>The climate (B01): the six fields, their ranges and continuity, the fill equal to the single sample, and the temperature falling with altitude by the data's lapse.</summary>
public sealed class ClimateSamplerTests
{
    private static WorldGenData World => TestGame.Data.WorldGen;

    [Fact]
    public void Given_ASeed_When_AColumnIsSampledTwiceOrByTwoSamplers_Then_TheClimateIsTheSameAndAnotherSeedGivesAnother()
    {
        var a = new ClimateSampler(World, 42);
        var b = new ClimateSampler(World, 42);
        var other = new ClimateSampler(World, 43);
        var differing = 0;
        for (var i = -3000; i < 3000; i += 97)
        {
            Assert.Equal(a.At(i, 5 - i), b.At(i, 5 - i));
            Assert.Equal(a.At(i, 5 - i), a.At(i, 5 - i));
            differing += a.At(i, 5 - i) != other.At(i, 5 - i) ? 1 : 0;
        }

        Assert.True(differing > 50, "another seed is another climate");
    }

    [Fact]
    public void Given_ManyColumnsNearAndFarAndNegative_When_TheyAreSampled_Then_EveryFieldIsInItsRange()
    {
        var sampler = new ClimateSampler(World, 9);
        foreach (var origin in new[] { 0, -29_000, 29_000, -123_457 })
        {
            for (var i = 0; i < 4000; i += 11)
            {
                var c = sampler.At(origin + i, origin - (3 * i));
                Assert.InRange(c.Temperature, -4096, 4096);
                Assert.InRange(c.Humidity, -4096, 4096);
                Assert.InRange(c.Continentalness, -4096, 4096);
                Assert.InRange(c.Erosion, -4096, 4096);
                Assert.InRange(c.Ridges, 0, 4096);
                Assert.InRange(c.River, -4096, 4096);
            }
        }
    }

    [Fact]
    public void Given_NeighbouringColumns_When_TheyAreSampled_Then_NoFieldJumps()
    {
        // Every field is smooth over at least 640 blocks (the ridges' first octave); the lattice only interpolates linearly, so no block-to-block step is large.
        var sampler = new ClimateSampler(World, 4);
        var biggest = 0;
        foreach (var origin in new[] { -29_000, -2, 0, 29_000 })
        {
            var previous = sampler.At(origin, origin);
            for (var i = 1; i < 1500; i++)
            {
                var c = sampler.At(origin + i, origin + (i / 3));
                biggest = Math.Max(biggest, MaxStep(previous, c));
                previous = c;
            }
        }

        Assert.True(biggest <= 150, $"a field steps {biggest} between neighbouring columns");
    }

    private static int MaxStep(ClimateSample a, ClimateSample b) =>
        new[] { a.Temperature - b.Temperature, a.Humidity - b.Humidity, a.Continentalness - b.Continentalness, a.Erosion - b.Erosion, a.Ridges - b.Ridges, a.River - b.River }.Max(Math.Abs);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-37, 91)]
    [InlineData(-29_005, 29_001)]
    [InlineData(28_990, -28_990)]
    public void Given_ARectangle_When_ItIsFilled_Then_EveryColumnIsWhatAtGives(int x0, int z0)
    {
        var sampler = new ClimateSampler(World, 21);
        var into = new ClimateSample[37 * 21];

        sampler.Fill(x0, z0, 37, 21, into);

        for (var z = 0; z < 21; z++)
        {
            for (var x = 0; x < 37; x++)
            {
                Assert.Equal(sampler.At(x0 + x, z0 + z), into[(z * 37) + x]);
            }
        }
    }

    [Fact]
    public void Given_APointAtOrBelowTheSea_When_TheTemperatureIsAsked_Then_ItIsTheColumnsSeaLevelTemperature()
    {
        var sampler = new ClimateSampler(World, 5);
        var sea = World.Vertical.SeaLevel;

        foreach (var (x, z) in new[] { (0, 0), (-500, 800), (12_345, -6_789) })
        {
            var t = sampler.At(x, z).Temperature;
            Assert.Equal(t, sampler.TemperatureAt(x, sea, z));
            Assert.Equal(t, sampler.TemperatureAt(x, sea - 90, z));
        }
    }

    [Fact]
    public void Given_APointAboveTheSea_When_TheTemperatureIsAsked_Then_ItFallsByTheDataLapse()
    {
        var sampler = new ClimateSampler(World, 5);
        var sea = World.Vertical.SeaLevel;
        var lapse = World.Climate.LapsePer100Blocks;
        var t = sampler.At(300, -200).Temperature;

        Assert.Equal(t - (lapse * IntMath.One / 1000), sampler.TemperatureAt(300, sea + 100, -200), tolerance: 1);
        Assert.Equal(t - (lapse * IntMath.One * 2 / 1000), sampler.TemperatureAt(300, sea + 200, -200), tolerance: 1);
        var previous = int.MaxValue;
        for (var y = sea - 10; y <= 320; y++)
        {
            var now = sampler.TemperatureAt(300, y, -200);
            Assert.True(now <= previous, $"the temperature rises with height at y {y}");
            previous = now;
        }
    }

    [Fact]
    public void Given_ALapseOfZero_When_TheTemperatureIsAskedHigh_Then_ItDoesNotChange()
    {
        var flat = World with { Climate = World.Climate with { LapsePer100Blocks = 0 } };
        var sampler = new ClimateSampler(flat, 5);

        Assert.Equal(sampler.At(1, 2).Temperature, sampler.TemperatureAt(1, 300, 2));
    }

    [Fact]
    public void Given_TheLapseHelper_When_ItIsUsedAsTheSamplerAndTheBiomeTableDo_Then_BothAgree()
    {
        Assert.Equal(1000, ClimateLapse.Apply(1000, 32, 32, 250));
        Assert.Equal(1000, ClimateLapse.Apply(1000, -64, 32, 250));
        Assert.Equal(1000 - 1024, ClimateLapse.Apply(1000, 132, 32, 250)); // 250 thousandths of 4 096 per 100 blocks
    }
}
