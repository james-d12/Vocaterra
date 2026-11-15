using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Noise;

/// <summary>The noise recipes (B01): fractal noise, ridged noise and the domain warp.</summary>
public sealed class NoiseStackTests
{
    [Fact]
    public void Given_AField_When_FbmIsSampledAnywhere_Then_ItIsDeterministicAndInRange()
    {
        var a = new NoiseStack(42, "fbm");
        var b = new NoiseStack(42, "fbm");
        foreach (var origin in new[] { 0, -29_000, 29_000, 123_456 })
        {
            for (var i = 0; i < 200; i += 7)
            {
                var v = a.Fbm(origin + i, origin - 2 * i, 512, 4);
                Assert.Equal(v, b.Fbm(origin + i, origin - 2 * i, 512, 4));
                Assert.InRange(v, -4096, 4096);
            }
        }

        Assert.NotEqual(a.Fbm(100, 100, 512, 4), new NoiseStack(42, "other").Fbm(100, 100, 512, 4));
    }

    [Fact]
    public void Given_MoreOctaves_When_FbmIsSampled_Then_TheSurfaceGainsDetailButStaysContinuous()
    {
        var stack = new NoiseStack(1, "octaves");
        long one = 0;
        long four = 0;
        var biggest = 0;
        for (var x = 0; x < 2000; x++)
        {
            one += Math.Abs(stack.Fbm(x + 2, 5, 256, 1) - (2 * stack.Fbm(x + 1, 5, 256, 1)) + stack.Fbm(x, 5, 256, 1));
            four += Math.Abs(stack.Fbm(x + 2, 5, 256, 4) - (2 * stack.Fbm(x + 1, 5, 256, 4)) + stack.Fbm(x, 5, 256, 4));
            biggest = Math.Max(biggest, Math.Abs(stack.Fbm(x + 1, 5, 256, 4) - stack.Fbm(x, 5, 256, 4)));
        }

        Assert.True(four > one, "the finer octaves add curvature at small scales");
        Assert.True(biggest < 14_336 / 32, $"the finest octave (32 blocks) jumps {biggest}");
    }

    [Fact]
    public void Given_RidgedNoise_When_ItIsSampled_Then_ItRunsFromZeroToOneAndReachesTheCrests()
    {
        var stack = new NoiseStack(5, "ridged");
        int low = int.MaxValue, high = 0;
        for (var z = -1000; z < 1000; z += 17)
        {
            for (var x = -1000; x < 1000; x += 17)
            {
                var v = stack.Ridged(x, z, 640, 3);
                Assert.InRange(v, 0, 4096);
                low = Math.Min(low, v);
                high = Math.Max(high, v);
            }
        }

        Assert.True(high > 3600, $"ridged noise has crests ({high})");
        Assert.True(low < 1800, $"and low ground ({low})");
    }

    [Fact]
    public void Given_ADomainWarp_When_APointIsWarped_Then_ItMovesByNoMoreThanTheAmplitudeAndZeroIsTheIdentity()
    {
        var stack = new NoiseStack(8, "warp");
        var moved = 0;
        for (var i = -500; i < 500; i += 11)
        {
            var (x, z) = stack.Warp(i, 3 * i, 256, 48);
            Assert.InRange(x - i, -48, 48);
            Assert.InRange(z - (3 * i), -48, 48);
            moved += (x != i || z != 3 * i) ? 1 : 0;
            Assert.Equal((i, i), stack.Warp(i, i, 256, 0));
        }

        Assert.True(moved > 50, "the warp moves points");
    }
}
