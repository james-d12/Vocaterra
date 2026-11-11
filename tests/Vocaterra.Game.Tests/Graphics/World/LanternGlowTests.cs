using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The glow pass's strength (E02a): the look's, continuous over the whole day with no band in it, and never dimmed by the weather.</summary>
public sealed class LanternGlowTests
{
    private static readonly DayClock Clock = new(DayData.Default);

    private static float Strength(long tick) => SkyLook.At(SkyLookInput.At(Clock, tick)).LanternStrength;

    [Fact]
    public void Given_NoonAndMidnight_When_TheStrengthIsRead_Then_ItIsTheValuesTheBandsUsedToBake()
    {
        Assert.Equal(LightBands.LanternStrength(LightBand.Day), SkyLook.At(new SkyLookInput(13, 1, DayPhase.Day)).LanternStrength);
        Assert.Equal(LightBands.LanternStrength(LightBand.Night), SkyLook.At(new SkyLookInput(0, 0, DayPhase.Night)).LanternStrength);
        Assert.Equal(0.5f, LightBands.LanternStrength(LightBand.Day));
        Assert.Equal(1f, LightBands.LanternStrength(LightBand.Night));
        Assert.Equal(1f, SkyLook.At(new SkyLookInput(21, 0.5, DayPhase.Dusk)).LanternStrength);
    }

    [Fact]
    public void Given_EveryBandEdgeOfTheDay_When_TheStrengthIsReadATickApart_Then_ItDoesNotStep()
    {
        // The four edges of the light bands (about 4.43, 5.51, 20.49 and 21.57 o'clock since E11): the strength has no band in it, so one tick later it differs by the rate of the cycle and no more.
        var edges = new List<double>();
        var last = SkyLook.At(SkyLookInput.At(Clock, -1));
        var lastStrength = last.LanternStrength;
        for (var tick = 0L; tick < Clock.CycleTicks; tick++)
        {
            var look = SkyLook.At(SkyLookInput.At(Clock, tick));
            Assert.True(Math.Abs(look.LanternStrength - lastStrength) < 0.0005f, $"the strength stepped at hour {Clock.HourAt(tick):0.000}");
            if (look.Band != last.Band)
            {
                edges.Add(Clock.HourAt(tick));
            }

            last = look;
            lastStrength = look.LanternStrength;
        }

        Assert.Equal(4, edges.Count);
        edges.Sort(); // the clock starts at hour 8, so the run found the evening's edges first
        double[] expected = [4.43, 5.51, 20.49, 21.57]; // E11: the daylight eases (a smoothstep), so the same daylight thresholds (0.12, 0.85) fall nearer the middle of the dawn and the dusk
        for (var i = 0; i < 4; i++)
        {
            Assert.InRange(edges[i], expected[i] - 0.05, expected[i] + 0.05);
        }
    }

    [Fact]
    public void Given_TheWholeDay_When_TheStrengthIsRead_Then_ItStaysBetweenHalfAndFullAndIsFullFromDuskToDawn()
    {
        for (var tick = 0L; tick < Clock.CycleTicks; tick += 30)
        {
            var look = SkyLook.At(SkyLookInput.At(Clock, tick));
            Assert.InRange(look.LanternStrength, 0.5f, 1f);
            if (Clock.DaylightAt(tick) <= 0.5)
            {
                Assert.Equal(1f, look.LanternStrength);
            }
        }
    }

    [Theory]
    [InlineData(13, 1, DayPhase.Day)]
    [InlineData(21, 0.5, DayPhase.Dusk)]
    [InlineData(0, 0, DayPhase.Night)]
    [InlineData(5.7, 0.85, DayPhase.Dawn)]
    public void Given_AnyWeather_When_TheStrengthIsRead_Then_TheWeatherDoesNotDimIt(double hour, double daylight, DayPhase phase)
    {
        var fair = SkyLook.At(new SkyLookInput(hour, daylight, phase));
        var storm = SkyLook.At(new SkyLookInput(hour, daylight, phase, CloudCover: 1, Sunlight: 0.5, Fog: 0.5));

        Assert.Equal(fair.LanternStrength, storm.LanternStrength);
        Assert.True(storm.SunLight.R <= fair.SunLight.R); // the sun's own light is dimmed: the glow is not
    }

    [Fact]
    public void Given_AStrength_When_ThePassColourIsRead_Then_ItIsTheAmberTimesTheStrengthAndNothingElse()
    {
        var full = LanternGlow.PassColour(1f);
        var half = LanternGlow.PassColour(0.5f);

        Assert.Equal(LanternGlow.Amber, full);
        Assert.Equal(LanternGlow.Amber.R * 0.5f, half.R, 5);
        Assert.Equal(LanternGlow.Amber.G * 0.5f, half.G, 5);
        Assert.Equal(LanternGlow.Amber.B * 0.5f, half.B, 5);
        Assert.Equal((0f, 0f, 0f), LanternGlow.PassColour(-1f));
        Assert.True(LanternGlow.Amber.R > LanternGlow.Amber.G && LanternGlow.Amber.G > LanternGlow.Amber.B, "warm");
    }

    [Fact]
    public void Given_AReach_When_TheVertexColourIsPacked_Then_ItIsAGreyLevelThatIsBlackWhereNoLanternReaches()
    {
        Assert.Equal(LanternGlow.None, LanternGlow.VertexColour(0f));
        Assert.Equal(0xFFFFFFFFu, LanternGlow.VertexColour(1f));
        Assert.Equal(0xFF808080u, LanternGlow.VertexColour(0.5f));
        Assert.Equal(0xFFFFFFFFu, LanternGlow.VertexColour(3f)); // clamped
    }
}
