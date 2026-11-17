using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>B17c: the sun of the faces' shade follows the hour (a function of the sky's look, so the shader needs no band), is the afternoon's fixed one at midday so the midday look is unchanged, and with the setting off stays there with the older, lighter dusk.</summary>
public sealed class SunModelTests
{
    private static readonly DayClock Clock = new(DayData.Default);

    private static long TickAt(double hour) => (long)Math.Round(hour / 24.0 * Clock.CycleTicks) - (long)Math.Round(8 / 24.0 * Clock.CycleTicks);

    private static SkyLook At(double hour) => SkyLook.At(SkyLookInput.At(Clock, TickAt(hour)));

    private static SkyLook At(double hour, double sunlight, double cloud) => SkyLook.At(SkyLookInput.At(Clock, TickAt(hour)) with { Sunlight = sunlight, CloudCover = cloud });

    private static double Angle((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
        Math.Acos(Math.Clamp((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z), -1f, 1f)) * 180 / Math.PI;

    [Fact]
    public void Given_TheMiddleOfTheDay_When_TheSunIsRead_Then_ItIsTheFixedAfternoonSunExactlyAtAnyHighHour()
    {
        foreach (var hour in new[] { 10.5, 12, 13, 14.5, 16 })
        {
            Assert.Equal(ChunkMesher.SunDirection, SunModel.ShadingDirection(At(hour), followsHour: true));
        }
    }

    [Fact]
    public void Given_TheSettingOff_When_AnyHourIsRead_Then_TheSunIsTheFixedOneAndTheColourIsTheLooks()
    {
        foreach (var hour in new[] { 0.0, 5.0, 6.5, 13.0, 19.5, 21.0, 23.0 })
        {
            var look = At(hour);
            Assert.Equal(ChunkMesher.SunDirection, SunModel.ShadingDirection(look, followsHour: false));
        }

        var noon = At(13);
        Assert.Equal(noon.SunLight, SunModel.Colour(noon, followsHour: false));
        Assert.Equal(noon, SunModel.Adjusted(noon, followsHour: false));
    }

    [Fact]
    public void Given_TheDawnAndTheDusk_When_TheSunIsRead_Then_ItStandsLowInTheEastAndInTheWestAsB22BakedIt()
    {
        var dawn = SunModel.ShadingDirection(At(5.8), true);
        var dusk = SunModel.ShadingDirection(At(20.2), true);
        Assert.True(dawn.X > 0.9f && dawn.Y is > 0.05f and < 0.3f, $"dawn {dawn}");
        Assert.True(dusk.X < -0.9f && dusk.Y is > 0.05f and < 0.3f, $"dusk {dusk}");
        Assert.True(Angle(dusk, LightBands.SunDirectionOf(LightBand.Dusk)) < 4, "within a few degrees of B22's baked dusk sun");
        Assert.True(Angle(dawn, LightBands.SunDirectionOf(LightBand.Dawn)) < 4);
    }

    [Fact]
    public void Given_TheNight_When_TheSunIsRead_Then_ItIsTheMoonsDirectionAboveTheHorizon()
    {
        var midnight = SunModel.ShadingDirection(At(0), true);
        Assert.True(Angle(midnight, LightBands.SunDirectionOf(LightBand.Night)) < 5, "the moon at midnight, as B22's Night band");
        foreach (var hour in new[] { 22.0, 23.5, 1.0, 3.0, 4.5 })
        {
            Assert.True(SunModel.ShadingDirection(At(hour), true).Y > 0.1f, $"hour {hour}: the shading sun is never under the horizon");
        }
    }

    [Fact]
    public void Given_TheWholeDay_When_TheSunIsReadEveryMinute_Then_ItMovesInSmallSteps()
    {
        var previous = SunModel.ShadingDirection(At(0), true);
        var largest = 0.0;
        for (var minute = 1; minute <= 24 * 60; minute++)
        {
            var now = SunModel.ShadingDirection(At(minute / 60.0), true);
            var step = Angle(previous, now);
            largest = Math.Max(largest, step);
            previous = now;
        }

        Assert.True(largest < 3.0, $"the largest step in a minute of game time is {largest} degrees (no jump at the band edges that B22's bands had)");
    }

    [Fact]
    public void Given_ADusk_When_TheLightIsRead_Then_TheDeeperDuskIsTheLooksAndTheOlderOneIsTheSettingOffs()
    {
        var dusk = At(21);
        Assert.True(dusk.TwilightLight > 0.9f, "the middle of the dusk is the twilight keyframe");
        Assert.Equal(dusk.SunLight, SunModel.Colour(dusk, followsHour: true));
        var older = SunModel.Colour(dusk, followsHour: false);
        Assert.Equal(SunModel.LegacyDuskLight.G, older.G, 1);
        Assert.True(older.G > dusk.SunLight.G && older.B > dusk.SunLight.B, "lighter and less red than B22's");
        Assert.Equal(older, SunModel.Adjusted(dusk, followsHour: false).SunLight);
        Assert.Equal(dusk.SunLight, SunModel.Adjusted(dusk, followsHour: true).SunLight);
        Assert.Equal(0f, At(13).TwilightLight, 3);
        Assert.Equal(At(13).SunLight, SunModel.Colour(At(13), followsHour: false)); // the day's light has none of the dusk in it
    }

    [Fact]
    public void Given_TheWeather_When_TheSunIsRead_Then_TheDirectionIsTheGeometrysAndTheCoverDimsTheLight()
    {
        var clear = At(13);
        var storm = At(13, 0.5, 1.0);
        Assert.Equal(SunModel.ShadingDirection(clear, true), SunModel.ShadingDirection(storm, true));
        Assert.True(SunModel.Colour(storm, true).R < SunModel.Colour(clear, true).R);
    }
}
