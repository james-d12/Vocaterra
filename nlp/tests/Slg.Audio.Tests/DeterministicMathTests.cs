namespace Slg.Audio.Tests;

/// <summary>Tests for the platform-independent math helpers.</summary>
public class DeterministicMathTests
{
    [Fact]
    public void SinPiHitsExactValuesAtTheCardinalPoints()
    {
        Assert.Equal(0.0, DeterministicMath.SinPi(0.0));
        Assert.Equal(0.0, DeterministicMath.SinPi(1.0));
        Assert.Equal(0.0, DeterministicMath.SinPi(-2.0));
        AssertClose(1.0, DeterministicMath.SinPi(0.5), 1e-15);
        AssertClose(-1.0, DeterministicMath.SinPi(1.5), 1e-15);
        AssertClose(1.0, DeterministicMath.SinPi(-1.5), 1e-15);
        AssertClose(0.5, DeterministicMath.SinPi(1.0 / 6.0), 1e-15);
    }

    [Fact]
    public void SinPiAgreesWithTheRuntimeSine()
    {
        for (var i = -600; i <= 600; i++)
        {
            var x = i * 0.0137;
            AssertClose(Math.Sin(Math.PI * x), DeterministicMath.SinPi(x), 1e-13);
        }
    }

    [Fact]
    public void ExpAgreesWithTheRuntimeExponential()
    {
        for (var i = -300; i <= 300; i++)
        {
            var x = i * 0.0777;
            var expected = Math.Exp(x);
            Assert.InRange(Math.Abs(DeterministicMath.Exp(x) - expected) / expected, 0.0, 1e-13);
        }

        Assert.Equal(1.0, DeterministicMath.Exp(0.0));
    }

    [Fact]
    public void DecibelConversionsMatchTheirDefinitions()
    {
        AssertClose(Math.Pow(10, -3.0 / 20.0), DeterministicMath.DbToAmplitude(-3.0), 1e-13);
        AssertClose(10.0, DeterministicMath.DbToAmplitude(20.0), 1e-13);
        AssertClose(1e-4, DeterministicMath.DbToPower(-40.0), 1e-17);
    }

    [Fact]
    public void BesselI0MatchesKnownValues()
    {
        Assert.Equal(1.0, DeterministicMath.BesselI0(0.0));
        AssertClose(1.2660658777520082, DeterministicMath.BesselI0(1.0), 1e-14);
        AssertClose(427.56411572180474, DeterministicMath.BesselI0(8.0), 1e-9);
    }

    private static void AssertClose(double expected, double actual, double tolerance)
    {
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} but was {actual:R} (tolerance {tolerance:R})");
    }
}
