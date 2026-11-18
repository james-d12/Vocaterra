using Vocaterra.Game.Core.World.Noise;

namespace Vocaterra.Game.Core.Tests.World.Noise;

/// <summary>The data spline (B01): through its points, flat beyond its ends, never past the points it runs between.</summary>
public sealed class SplineTests
{
    private static readonly SplinePoint[] Points = [new(-1000, 12), new(-200, 30), new(0, 40), new(300, 40), new(800, 120)];

    [Fact]
    public void Given_ControlPoints_When_TheSplineIsEvaluatedAtThem_Then_ItGivesTheirOutputs()
    {
        var spline = new Spline(Points);

        Assert.All(Points, p => Assert.Equal(p.Y, spline.Evaluate(p.X)));
    }

    [Fact]
    public void Given_AnInputBeyondTheEnds_When_TheSplineIsEvaluated_Then_ItHoldsTheEndOutput()
    {
        var spline = new Spline(Points);

        Assert.Equal(12, spline.Evaluate(-5000));
        Assert.Equal(120, spline.Evaluate(4000));
    }

    [Fact]
    public void Given_TwoPointsOfARisingRun_When_TheInputsBetweenAreEvaluated_Then_TheOutputNeverLeavesTheirRange()
    {
        var spline = new Spline(Points);
        var previous = spline.Evaluate(-1000);
        for (var x = -1000; x <= 800; x++)
        {
            var y = spline.Evaluate(x);
            Assert.InRange(y, 12, 120);
            Assert.True(y >= previous - 0, $"the spline falls at {x} ({previous} to {y}) between rising points");
            previous = y;
        }

        for (var x = 0; x <= 300; x++)
        {
            Assert.Equal(40, spline.Evaluate(x)); // a flat run between two equal points stays flat: no overshoot
        }
    }

    [Fact]
    public void Given_NeighbouringInputs_When_TheSplineIsEvaluated_Then_ItHasNoJump()
    {
        var spline = new Spline(Points);
        var biggest = 0;
        for (var x = -1000; x < 800; x++)
        {
            biggest = Math.Max(biggest, Math.Abs(spline.Evaluate(x + 1) - spline.Evaluate(x)));
        }

        Assert.True(biggest <= 1, $"the spline steps {biggest} between neighbouring inputs");
    }

    [Fact]
    public void Given_PointsThatDoNotRiseOrNone_When_ASplineIsMade_Then_ItIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new Spline([]));
        Assert.Throws<ArgumentException>(() => new Spline([new SplinePoint(1, 1), new SplinePoint(1, 2)]));
        Assert.Throws<ArgumentException>(() => new Spline([new SplinePoint(2, 1), new SplinePoint(1, 2)]));
        Assert.Equal(7, new Spline([new SplinePoint(0, 7)]).Evaluate(3)); // one point is a constant
    }
}
