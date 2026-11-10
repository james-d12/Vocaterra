using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Sky;

/// <summary>The sky's look over the cycle (E02): a pure function of the hour, the daylight and the phase, whose keyframes join without a jump.</summary>
public sealed class SkyLookTests
{
    private static readonly DayClock Clock = new(DayData.Default);

    private static SkyLook Look(double hour) => SkyLook.At(SkyLookInput.At(Clock, TickAt(hour)));

    // A tick of the default cycle (2400 s, the clock starting at hour 8) that reads the given hour.
    private static long TickAt(double hour) => (long)Math.Round(hour / 24.0 * Clock.CycleTicks) - (long)Math.Round(8 / 24.0 * Clock.CycleTicks);

    private static double Luminance((byte R, byte G, byte B) c) => (0.3 * c.R) + (0.59 * c.G) + (0.11 * c.B);

    private static int Distance((byte R, byte G, byte B) a, (byte R, byte G, byte B) b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    [Fact]
    public void Given_Noon_When_TheLookIsRead_Then_ItIsTodaysDayPalette()
    {
        var look = SkyLook.At(new SkyLookInput(13, 1, DayPhase.Day));

        Assert.Equal(SkyPalette.Horizon, look.DomeHorizon);
        Assert.Equal(SkyPalette.Middle, look.DomeMiddle);
        Assert.Equal(SkyPalette.Zenith, look.DomeZenith);
        Assert.Equal(SkyPalette.CloudTop, look.CloudTop);
        Assert.Equal(SkyPalette.CloudSide, look.CloudSide);
        Assert.Equal(SkyPalette.CloudBottom, look.CloudBottom);
        Assert.Equal((1f, 1f, 1f), look.SunLight);
        Assert.Equal(0.5f, look.LanternStrength);
        Assert.Equal(LightBand.Day, look.Band);
        Assert.Equal(SkyLook.Noon, look);
    }

    [Fact]
    public void Given_TheDuskHour_When_TheLookIsRead_Then_ItIsTodaysDuskPalette()
    {
        var look = Look(SkyLook.DuskHour);

        Assert.Equal(SkyPalette.DuskHorizon, look.DomeHorizon);
        Assert.Equal(SkyPalette.DuskMiddle, look.DomeMiddle);
        Assert.Equal(SkyPalette.DuskZenith, look.DomeZenith);
        Assert.Equal(SkyPalette.DuskCloudTop, look.CloudTop);
        Assert.Equal(SkyPalette.DuskCloudSide, look.CloudSide);
        Assert.Equal(SkyPalette.DuskCloudBottom, look.CloudBottom);
        Assert.Equal(ChunkMesher.DuskLight, look.SunLight);
        Assert.Equal(1f, look.LanternStrength);
        Assert.Equal(LightBand.Dusk, look.Band);
    }

    [Fact]
    public void Given_TheMiddleOfADawn_When_TheLookIsRead_Then_ItMirrorsTheMiddleOfADusk()
    {
        var dawn = Look(5);
        var dusk = Look(21);

        Assert.Equal(dusk.DomeHorizon, dawn.DomeHorizon);
        Assert.Equal(dusk.DomeZenith, dawn.DomeZenith);
        Assert.Equal(dusk.SunLight, dawn.SunLight);
        Assert.Equal(dusk.Band, dawn.Band);
    }

    [Fact]
    public void Given_Midnight_When_TheLookIsRead_Then_ItIsDarkerThanTheDayButNotBlack()
    {
        var night = Look(0);
        var day = Look(13);

        Assert.True(Luminance(night.DomeHorizon) < Luminance(day.DomeHorizon) / 3);
        Assert.True(Luminance(night.DomeZenith) < Luminance(day.DomeZenith) / 3);
        Assert.True(Luminance(night.CloudTop) < Luminance(day.CloudTop) / 2);
        Assert.True(night.SunLight.R < 0.5f && night.SunLight.G < 0.5f && night.SunLight.B < 0.7f);
        Assert.True(night.SunLight.B > night.SunLight.R, "the night's light is blue");
        Assert.True(night.SunLight.R >= 0.2f && night.SunLight.G >= 0.25f, "readable: the ground and the figures can be seen without a torch");
        Assert.True(Luminance(night.DomeHorizon) > 20, "the sky is dark blue and not black");
        Assert.True(night.DomeHorizon.B > night.DomeHorizon.R + 20, "blue");
        Assert.Equal(LightBand.Night, night.Band);
        Assert.Equal(1f, night.LanternStrength);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    [InlineData(8.0)]
    [InlineData(13.0)]
    [InlineData(19.0)]
    [InlineData(20.5)]
    [InlineData(21.0)]
    [InlineData(23.0)]
    public void Given_AnyHour_When_TheLookIsRead_Then_TheFogIsTheHorizonsColour(double hour)
    {
        var look = Look(hour);

        Assert.Equal(look.DomeHorizon, look.FogColour);
    }

    [Fact]
    public void Given_TheWholeCycle_When_TheLookIsReadASecondApart_Then_NothingJumpsIncludingAtTheKeyframesAndTheWrap()
    {
        // One tick is a sixtieth of a second: read every 60 ticks (a second of play) over two cycles, so the wrap from 24 to 0 is in it. A dusk is two hours, 200 seconds of play, so the
        // fastest colour moves about two and a half steps of 255 a second; a jump would be a step of ten or more.
        var last = SkyLook.At(SkyLookInput.At(Clock, -60));
        for (var tick = 0L; tick <= 2 * Clock.CycleTicks; tick += 60)
        {
            var now = SkyLook.At(SkyLookInput.At(Clock, tick));
            var at = $"tick {tick} (hour {Clock.HourAt(tick):0.000})";
            Assert.True(Distance(last.DomeHorizon, now.DomeHorizon) <= 3, "horizon jumped at " + at);
            Assert.True(Distance(last.DomeMiddle, now.DomeMiddle) <= 3, "middle jumped at " + at);
            Assert.True(Distance(last.DomeZenith, now.DomeZenith) <= 3, "zenith jumped at " + at);
            Assert.True(Distance(last.CloudTop, now.CloudTop) <= 3, "cloud jumped at " + at);
            Assert.True(Distance(last.SunColour, now.SunColour) <= 3, "sun colour jumped at " + at);
            Assert.True(Math.Abs(last.SunLight.R - now.SunLight.R) < 0.01f && Math.Abs(last.SunLight.G - now.SunLight.G) < 0.01f && Math.Abs(last.SunLight.B - now.SunLight.B) < 0.01f, "sun light jumped at " + at);
            Assert.True(Math.Abs(last.LanternStrength - now.LanternStrength) < 0.01f, "lanterns jumped at " + at);
            Assert.True(Math.Abs(last.Stars - now.Stars) < 0.03f, "stars jumped at " + at);
            Assert.True(Math.Abs(last.SunStrength - now.SunStrength) < 0.05f, "sun strength jumped at " + at);
            Assert.True(Math.Abs(last.MoonStrength - now.MoonStrength) < 0.05f, "moon strength jumped at " + at);
            last = now;
        }
    }

    [Theory]
    [InlineData(DayPhase.Dawn)]
    [InlineData(DayPhase.Dusk)]
    public void Given_ADawnOrADusk_When_TheWeightsAreRead_Then_TheyAddUpToOneAndTwilightPeaksAtHalfDaylight(DayPhase phase)
    {
        for (var d = 0.0; d <= 1.0001; d += 0.05)
        {
            var (day, twilight, night) = SkyLook.Weights(phase, Math.Min(d, 1));
            Assert.Equal(1f, day + twilight + night, 4);
        }

        Assert.Equal((0f, 1f, 0f), SkyLook.Weights(phase, 0.5));
        Assert.Equal((1f, 0f, 0f), SkyLook.Weights(phase, 1));
        Assert.Equal((0f, 0f, 1f), SkyLook.Weights(phase, 0));
    }

    [Fact]
    public void Given_TheNightHours_When_TheSunIsRead_Then_ItIsBelowTheHorizonAndNotDrawn()
    {
        foreach (var hour in new[] { 22.0, 23.0, 0.0, 2.0, 3.9 })
        {
            var look = Look(hour);
            Assert.True(look.SunDirection.Y < 0f, $"hour {hour}");
            Assert.Equal(0f, look.SunStrength);
        }

        foreach (var hour in new[] { 7.0, 10.0, 13.0, 18.0 })
        {
            Assert.Equal(1f, Look(hour).SunStrength);
        }
    }

    [Fact]
    public void Given_TheNightAndTheDay_When_TheMoonAndTheStarsAreRead_Then_TheyShowByNightOnly()
    {
        var night = Look(0);
        var day = Look(13);

        Assert.Equal(1f, night.Stars, 3);
        Assert.True(night.MoonStrength > 0.9f);
        Assert.Equal(0f, day.Stars);
        Assert.Equal(0f, day.MoonStrength);
        Assert.True(Look(21).Stars < 0.01f, "the stars wait for the dark");
        Assert.True(Look(22).Stars > 0f);
    }

    [Fact]
    public void Given_TheMoonsHours_When_TheLookIsRead_Then_TheMoonIsAboveTheHorizonWhenItShows()
    {
        for (var h = 0.0; h < 24; h += 0.25)
        {
            var look = Look(h);
            if (look.MoonStrength > 0.01f)
            {
                Assert.True(look.MoonDirection.Y > 0f, $"hour {h}");
            }
        }
    }

    [Fact]
    public void Given_TheSameInput_When_TheLookIsReadTwice_Then_TheResultsAreEqual()
    {
        var input = new SkyLookInput(19.5, 0.75, DayPhase.Day);

        Assert.Equal(SkyLook.At(input), SkyLook.At(input));
    }

    [Fact]
    public void Given_AnInputOutOfRange_When_TheLookIsRead_Then_TheDaylightIsHeldToTheDial()
    {
        Assert.Equal(SkyLook.At(new SkyLookInput(13, 1, DayPhase.Dusk)), SkyLook.At(new SkyLookInput(13, 7, DayPhase.Dusk)));
        Assert.Equal(SkyLook.At(new SkyLookInput(0, 0, DayPhase.Dawn)), SkyLook.At(new SkyLookInput(0, -3, DayPhase.Dawn)));
    }
}
