using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>Where the sun and the moon are over the day (E02): the sun rises in the east, sets in the west and passes near today's face-shading direction.</summary>
public sealed class SkyArcTests
{
    private static double Degrees((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
        Math.Acos(Math.Clamp((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z), -1f, 1f)) * 180 / Math.PI;

    [Fact]
    public void Given_Sunrise_When_TheSunIsRead_Then_ItIsOnTheEasternHorizon()
    {
        var sun = SkyArc.SunAt(SkyArc.Sunrise);

        Assert.Equal(1f, sun.X, 3);
        Assert.Equal(0f, sun.Y, 3);
    }

    [Fact]
    public void Given_Sunset_When_TheSunIsRead_Then_ItIsOnTheWesternHorizon()
    {
        var sun = SkyArc.SunAt(SkyArc.Sunset);

        Assert.Equal(-1f, sun.X, 3);
        Assert.Equal(0f, sun.Y, 3);
    }

    [Fact]
    public void Given_Noon_When_TheSunIsRead_Then_ItIsHighInTheNorthernSky()
    {
        var sun = SkyArc.SunAt(13);

        Assert.True(sun.Y > 0.8f, "high");
        Assert.True(sun.Z < -0.4f, "towards the north, where today's face shading puts it");
        Assert.Equal(0f, sun.X, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4.9)]
    [InlineData(21.1)]
    [InlineData(23)]
    public void Given_ANightHour_When_TheSunIsRead_Then_ItIsBelowTheHorizon(double hour)
    {
        Assert.True(SkyArc.SunAt(hour).Y < 0f);
    }

    [Fact]
    public void Given_TheHoursOfADay_When_TheSunIsRead_Then_ItMovesWithoutAJumpIncludingMidnight()
    {
        var last = SkyArc.SunAt(0);
        for (var minute = 1; minute <= 24 * 60; minute++)
        {
            var now = SkyArc.SunAt(minute / 60.0 % 24.0);
            Assert.True(Degrees(last, now) < 1.5, $"a jump at minute {minute}");
            last = now;
        }
    }

    [Fact]
    public void Given_TheMorning_When_TheSunIsRead_Then_ItPassesNearTheMirrorImageOfTodaysFaceShadingDirectionAndTheDirectionItselfInTheAfternoon()
    {
        var shade = ChunkMesher.SunDirection;
        var mirrored = (-shade.X, shade.Y, shade.Z);
        var nearest = Enumerable.Range(0, 24 * 60).Select(static m => m / 60.0).Min(h => Degrees(SkyArc.SunAt(h), mirrored));
        var nearestAfternoon = Enumerable.Range(0, 24 * 60).Select(static m => m / 60.0).Min(h => Degrees(SkyArc.SunAt(h), shade));

        Assert.True(nearest < 2.0, "the morning passes through the mirror image of the face-shading direction");
        Assert.True(nearestAfternoon < 2.0, "and the afternoon through the direction itself");
    }

    [Fact]
    public void Given_TheNight_When_TheMoonIsRead_Then_ItRisesInTheEveningIsHighAtMidnightAndSetsInTheMorning()
    {
        Assert.Equal(0f, SkyArc.MoonAt(SkyArc.Moonrise).Y, 3);
        Assert.True(SkyArc.MoonAt(0).Y > 0.7f);
        Assert.Equal(0f, SkyArc.MoonAt(SkyArc.Moonset).Y, 3);
        Assert.True(SkyArc.MoonAt(12).Y < 0f, "below the horizon at noon");
        Assert.True(SkyArc.MoonAt(21).X < SkyArc.MoonAt(0).X + 1f);
    }

    [Fact]
    public void Given_TheDirections_When_Measured_Then_TheyAreUnitVectors()
    {
        for (var hour = 0.0; hour < 24; hour += 0.25)
        {
            foreach (var d in new[] { SkyArc.SunAt(hour), SkyArc.MoonAt(hour) })
            {
                Assert.Equal(1f, MathF.Sqrt((d.X * d.X) + (d.Y * d.Y) + (d.Z * d.Z)), 3);
            }
        }
    }
}
