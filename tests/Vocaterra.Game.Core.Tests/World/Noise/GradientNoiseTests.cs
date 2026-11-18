using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Noise;

/// <summary>The integer gradient noise (B01): determinism, range, continuity, negative and far coordinates, both dimensions.</summary>
public sealed class GradientNoiseTests
{
    private static readonly int[] Origins = [0, -1, -29_000, 29_000, 1_000_003, -1_000_003];

    [Fact]
    public void Given_ASeedAndAPass_When_ASamplesAreDrawnTwice_Then_TheyAreEqualAndAnotherSeedOrPassGivesOtherValues()
    {
        var a = new GradientNoise(42, "t");
        var b = new GradientNoise(42, "t");
        var otherSeed = new GradientNoise(43, "t");
        var otherPass = new GradientNoise(42, "u");
        var seedDiffers = 0;
        var passDiffers = 0;
        for (var i = -100; i < 100; i += 3)
        {
            Assert.Equal(a.Sample(0, i, 7 - i, 64), b.Sample(0, i, 7 - i, 64));
            Assert.Equal(a.Sample(2, i, 5, 7 - i, 32, 16), b.Sample(2, i, 5, 7 - i, 32, 16));
            seedDiffers += a.Sample(0, i, 7 - i, 64) != otherSeed.Sample(0, i, 7 - i, 64) ? 1 : 0;
            passDiffers += a.Sample(0, i, 7 - i, 64) != otherPass.Sample(0, i, 7 - i, 64) ? 1 : 0;
        }

        Assert.True(seedDiffers > 20, "another seed is another noise");
        Assert.True(passDiffers > 20, "another pass name is another noise");
        Assert.NotEqual(a.Sample(0, 33, 17, 64), a.Sample(1, 33, 17, 64)); // another octave is another lattice
    }

    [Fact]
    public void Given_ManySamplesOfBothDimensions_When_TheyAreRead_Then_TheyStayInRangeAndUseMostOfIt()
    {
        var noise = new GradientNoise(7, "range");
        int low2 = 0, high2 = 0, low3 = 0, high3 = 0;
        for (var z = -300; z < 300; z += 5)
        {
            for (var x = -300; x < 300; x += 5)
            {
                var v2 = noise.Sample(0, x, z, 48);
                var v3 = noise.Sample(0, x, (x ^ z) & 63, z, 48, 24);
                Assert.InRange(v2, -4096, 4096);
                Assert.InRange(v3, -4096, 4096);
                low2 = Math.Min(low2, v2);
                high2 = Math.Max(high2, v2);
                low3 = Math.Min(low3, v3);
                high3 = Math.Max(high3, v3);
            }
        }

        Assert.True(low2 < -2800 && high2 > 2800, $"2D noise uses its range ({low2} to {high2})");
        Assert.True(low3 < -1500 && high3 > 1500, $"3D noise uses its range ({low3} to {high3})");
    }

    [Fact]
    public void Given_TheLatticePoints_When_TheNoiseIsSampledThere_Then_ItIsZero()
    {
        var noise = new GradientNoise(5, "lattice");
        for (var i = -4; i <= 4; i++)
        {
            Assert.Equal(0, noise.Sample(0, i * 64, -i * 128, 64));
            Assert.Equal(0, noise.Sample(0, i * 32, i * 16, -i * 32, 32, 16));
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(512)]
    public void Given_NeighbouringBlocks_When_The2DNoiseIsSampled_Then_TheStepIsBoundedByTheCell(int cell)
    {
        var noise = new GradientNoise(11, "smooth");
        var biggest = 0;
        foreach (var origin in Origins)
        {
            for (var i = 0; i < 600; i++)
            {
                biggest = Math.Max(biggest, Math.Abs(noise.Sample(0, origin + i + 1, origin - 3, cell) - noise.Sample(0, origin + i, origin - 3, cell)));
                biggest = Math.Max(biggest, Math.Abs(noise.Sample(0, origin + 5, origin + i + 1, cell) - noise.Sample(0, origin + 5, origin + i, cell)));
            }
        }

        // Unit gradients rising and falling over a cell: no more than about 3.5 of the range per cell, so per block at most 14 336 / cell (plus rounding).
        Assert.True(biggest <= (14_336 / cell) + 4, $"a {cell}-block noise jumps {biggest} between neighbours");
    }

    [Fact]
    public void Given_NeighbouringBlocks_When_The3DNoiseIsSampled_Then_TheStepIsBounded()
    {
        var noise = new GradientNoise(11, "smooth3");
        var biggest = 0;
        foreach (var origin in Origins)
        {
            for (var i = 0; i < 400; i++)
            {
                biggest = Math.Max(biggest, Math.Abs(noise.Sample(0, origin + i + 1, 30, origin, 64, 32) - noise.Sample(0, origin + i, 30, origin, 64, 32)));
                biggest = Math.Max(biggest, Math.Abs(noise.Sample(0, origin, i + 1, origin + 9, 64, 32) - noise.Sample(0, origin, i, origin + 9, 64, 32)));
                biggest = Math.Max(biggest, Math.Abs(noise.Sample(0, origin + 9, 30, origin + i + 1, 64, 32) - noise.Sample(0, origin + 9, 30, origin + i, 64, 32)));
            }
        }

        Assert.True(biggest <= 14_336 / 32 + 4, $"the 3D noise jumps {biggest} between neighbours");
    }

    [Fact]
    public void Given_NegativeCoordinates_When_TheNoiseIsSampled_Then_ItIsContinuousAcrossZeroAndIsNotAMirror()
    {
        var noise = new GradientNoise(3, "negative");
        var mirrored = 0;
        for (var x = -200; x < 200; x++)
        {
            Assert.True(Math.Abs(noise.Sample(0, x + 1, 9, 64) - noise.Sample(0, x, 9, 64)) <= 14_336 / 64 + 4);
            mirrored += noise.Sample(0, -x, 9, 64) == noise.Sample(0, x, 9, 64) ? 1 : 0;
        }

        Assert.True(mirrored < 40, "the negative side is its own noise, not the positive side reflected");
    }

    [Fact]
    public void Given_CoordinatesNearPlusAndMinus29000_When_TheNoiseIsSampled_Then_ItIsDeterministicInRangeAndNotConstant()
    {
        var noise = new GradientNoise(99, "far");
        foreach (var origin in new[] { 29_000, -29_000 })
        {
            var seen = new HashSet<int>();
            for (var i = 0; i < 300; i++)
            {
                var v = noise.Sample(0, origin + i, origin - i, 128);
                Assert.Equal(v, noise.Sample(0, origin + i, origin - i, 128));
                Assert.InRange(v, -4096, 4096);
                seen.Add(v);
                var w = noise.Sample(0, origin + i, 40, origin + 7, 128, 32);
                Assert.InRange(w, -4096, 4096);
            }

            Assert.True(seen.Count > 100, "the far noise varies");
        }
    }
}
