using Vocaterra.Game.Core.Temperature;

namespace Vocaterra.Game.Core.Tests.Temperature;

/// <summary>The map from the generator's climate temperature to degrees (I06c): straight between its points, flat beyond them, whole-number and the same every time.</summary>
public sealed class ClimateCurveTests
{
    private static readonly ClimateCurve Curve = new([(-1000, -60), (0, 140), (500, 140), (1000, 190)]);

    [Theory]
    [InlineData(-5000, -60)]
    [InlineData(-1000, -60)]
    [InlineData(-500, 40)]
    [InlineData(0, 140)]
    [InlineData(250, 140)]
    [InlineData(500, 140)]
    [InlineData(750, 165)]
    [InlineData(1000, 190)]
    [InlineData(9000, 190)]
    public void Given_AClimateInThousandths_When_ItIsRead_Then_TheCurveGivesTheStraightLineBetweenItsPointsAndFlatBeyond(int thousandths, int expected) =>
        Assert.Equal(expected, Curve.TenthsAt(thousandths * 4096 / 1000));

    [Fact]
    public void Given_TheSameClimate_When_ReadTwice_Then_TheAnswerIsTheSame() => Assert.Equal(Curve.TenthsAt(1234), Curve.TenthsAt(1234));

    [Fact]
    public void Given_FewerThanTwoPoints_When_ACurveIsMade_Then_ItIsRefused() => Assert.Throws<ArgumentException>(() => new ClimateCurve([(0, 0)]));
}
