using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Climate;

/// <summary>The continents (B02b redo): the continentalness as jittered cells with a channel of sea along every border, so the land cells are continents and the sea is one ocean whatever the seed.</summary>
public sealed class ContinentFieldTests
{
    private static WorldGenData World => TestGame.Data.WorldGen;

    private static ClimateSampler Sampler(ulong seed, ContinentSpec? spec = null) => new(World with { Climate = World.Climate with { Continents = spec ?? World.Climate.Continents } }, seed);

    [Fact]
    public void Given_ASeedAndAColumn_When_TheContinentalnessIsReadTwiceOrFar_Then_ItIsTheSameAndAnotherSeedGivesAnother()
    {
        var (a, b, other) = (Sampler(42), Sampler(42), Sampler(43));
        var differing = 0;
        foreach (var origin in new[] { 0, -29_000, 29_000 })
        {
            for (var i = 0; i < 100; i++)
            {
                var (x, z) = (origin + (i * 37), origin - (i * 53));
                Assert.Equal(a.At(x, z).Continentalness, b.At(x, z).Continentalness);
                Assert.InRange(a.At(x, z).Continentalness, -4096, 4096);
                differing += a.At(x, z).Continentalness != other.At(x, z).Continentalness ? 1 : 0;
            }
        }

        Assert.True(differing > 100, "the seed keys the cells");
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void Given_AnySeed_When_AWideSquareIsSampled_Then_TheSeaIsAboutFortyPercentAndOneConnectedOcean(ulong seed)
    {
        const int Half = 16384;
        const int Step = 128;
        var sampler = Sampler(seed);
        var n = (2 * Half) / Step;
        var sea = new bool[n * n];
        for (var j = 0; j < n; j++)
        {
            for (var i = 0; i < n; i++)
            {
                sea[(j * n) + i] = sampler.At(-Half + (i * Step), -Half + (j * Step)).Continentalness < 0;
            }
        }

        var share = sea.Count(static s => s) * 100.0 / sea.Length;
        var seen = new bool[sea.Length];
        var largest = 0;
        for (var start = 0; start < sea.Length; start++)
        {
            if (!sea[start] || seen[start])
            {
                continue;
            }

            var (size, stack) = (0, new Stack<int>());
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                size++;
                foreach (var m in new[] { k % n > 0 ? k - 1 : -1, k % n < n - 1 ? k + 1 : -1, k / n > 0 ? k - n : -1, k / n < n - 1 ? k + n : -1 })
                {
                    if (m >= 0 && sea[m] && !seen[m])
                    {
                        seen[m] = true;
                        stack.Push(m);
                    }
                }
            }

            largest = Math.Max(largest, size);
        }

        var connected = largest * 100.0 / sea.Count(static s => s);
        Assert.InRange(share, 33, 48); // every block of cells holds the same number of sea cells, so the share is steady
        Assert.True(connected >= 97, $"the largest sea is {connected:0.0}% of the sea: the channels join every sea");
    }

    [Fact]
    public void Given_NoSeaCells_When_TheSeaIsCounted_Then_OnlyTheChannelsAreSeaAndTheyStillJoin()
    {
        var spec = World.Climate.Continents! with { SeaPerBlock = 0 };
        var withSea = Sampler(42);
        var without = Sampler(42, spec);
        int Share(ClimateSampler s) => Enumerable.Range(0, 128 * 128).Count(k => s.At(-16384 + ((k % 128) * 256), -16384 + ((k / 128) * 256)).Continentalness < 0);

        Assert.True(Share(without) < Share(withSea), "the open sea cells are most of the sea");
        Assert.True(Share(without) > 0, "the channels along the cell borders are sea");
    }

    [Fact]
    public void Given_TheRidgedFieldWithARoundedCrest_When_ItIsSampledAlongALine_Then_ItsCrestIsSmootherThanTheSharpOne()
    {
        var stack = new NoiseStack(42, "climate/ridges");
        int Sharpest(int round)
        {
            var values = Enumerable.Range(0, 800).Select(i => stack.Ridged(i * 8, 0, 2048, 1, round)).ToArray();
            return Enumerable.Range(1, values.Length - 2).Max(i => Math.Abs(values[i - 1] - (2 * values[i]) + values[i + 1]));
        }

        var (sharp, rounded) = (Sharpest(0), Sharpest(150));
        Assert.True(rounded < sharp, $"the rounded crest bends by {rounded} at most and the sharp one by {sharp}");
        Assert.InRange(stack.Ridged(0, 0, 2048, 1, 150), 0, IntMath.One);
    }
}
