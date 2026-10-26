using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Tests.Mathematics;

/// <summary>The integer angle table: it must agree with the library sine (checked here, never in the simulation) and be exact where it matters.</summary>
public sealed class AnglesTests
{
    [Fact]
    public void TheTableMatchesTheLibrarySineWithinOneUnit()
    {
        for (var angle = 0; angle < Angles.StepsPerTurn; angle++)
        {
            var expected = Math.Sin(angle * 2 * Math.PI / Angles.StepsPerTurn) * Angles.One;
            Assert.True(Math.Abs(Angles.Sin(angle) - expected) <= 1.0, $"sin({angle}) = {Angles.Sin(angle)}, expected {expected:F2}");
            var expectedCos = Math.Cos(angle * 2 * Math.PI / Angles.StepsPerTurn) * Angles.One;
            Assert.True(Math.Abs(Angles.Cos(angle) - expectedCos) <= 1.0, $"cos({angle}) = {Angles.Cos(angle)}, expected {expectedCos:F2}");
        }
    }

    [Fact]
    public void TheCardinalAnglesAreExact()
    {
        Assert.Equal(0, Angles.Sin(0));
        Assert.Equal(Angles.One, Angles.Sin(1024));
        Assert.Equal(0, Angles.Sin(2048));
        Assert.Equal(-Angles.One, Angles.Sin(3072));
        Assert.Equal(Angles.One, Angles.Cos(0));
        Assert.Equal(0, Angles.Cos(1024));
        Assert.Equal(-Angles.One, Angles.Cos(2048));
        Assert.Equal(46341, Angles.Sin(512));
    }

    [Fact]
    public void AnglesWrapAndDegreesConvert()
    {
        Assert.Equal(Angles.Sin(100), Angles.Sin(100 + Angles.StepsPerTurn));
        Assert.Equal(Angles.Sin(-100), -Angles.Sin(100));
        Assert.Equal(0, Angles.FromDegrees(0));
        Assert.Equal(1024, Angles.FromDegrees(90));
        Assert.Equal(2048, Angles.FromDegrees(180));
        Assert.Equal(3072, Angles.FromDegrees(270));
        Assert.Equal(3072, Angles.FromDegrees(-90));
        Assert.Equal(0, Angles.FromDegrees(360));
        Assert.Equal(Angles.MaxPitch, Angles.ClampPitch(5000));
        Assert.Equal(-Angles.MaxPitch, Angles.ClampPitch(-5000));
    }

    [Fact]
    public void TheLookDirectionIsAUnitVectorOnTheCompass()
    {
        Assert.Equal((0, 0, -Angles.One), Angles.Direction(0, 0)); // north
        Assert.Equal((Angles.One, 0, 0), Angles.Direction(1024, 0)); // east
        Assert.Equal((0, 0, Angles.One), Angles.Direction(2048, 0)); // south
        Assert.Equal((-Angles.One, 0, 0), Angles.Direction(3072, 0)); // west
        Assert.Equal(Angles.One, Angles.Direction(0, 1024).Y); // straight up (outside the clamp, still defined)
        for (var yaw = 0; yaw < Angles.StepsPerTurn; yaw += 37)
        {
            for (var pitch = -Angles.MaxPitch; pitch <= Angles.MaxPitch; pitch += 53)
            {
                var (x, y, z) = Angles.Direction(yaw, pitch);
                var length = Math.Sqrt(((double)x * x) + ((double)y * y) + ((double)z * z)) / Angles.One;
                Assert.InRange(length, 0.9999, 1.0001);
            }
        }
    }

    [Fact]
    public void TheTableIsMonotonicOverTheFirstQuarter()
    {
        var previous = -1;
        for (var angle = 0; angle <= Angles.QuarterTurn; angle++)
        {
            var value = Angles.Sin(angle);
            Assert.True(value >= previous);
            previous = value;
        }
    }
}
