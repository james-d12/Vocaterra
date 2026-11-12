using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>The light bands (E02): which band each moment is built in, what each bakes, and the tint that brings a band's baked sun light to the look's.</summary>
public sealed class LightBandsTests
{
    [Theory]
    [InlineData(DayPhase.Day, 1.0, LightBand.Day)]
    [InlineData(DayPhase.Night, 0.0, LightBand.Night)]
    [InlineData(DayPhase.Dawn, 0.5, LightBand.Dusk)]
    [InlineData(DayPhase.Dusk, 0.5, LightBand.Dusk)]
    [InlineData(DayPhase.Dawn, 0.0, LightBand.Night)]
    [InlineData(DayPhase.Dusk, 0.0, LightBand.Night)]
    [InlineData(DayPhase.Dawn, 1.0, LightBand.Day)]
    [InlineData(DayPhase.Dusk, 1.0, LightBand.Day)]
    public void Given_APhaseAndDaylight_When_TheBandIsAsked_Then_DawnAndDuskUseTheDuskBandBetweenTheEnds(DayPhase phase, double daylight, LightBand expected)
    {
        Assert.Equal(expected, LightBands.For(phase, daylight));
    }

    [Fact]
    public void Given_TheEdgesOfTheDuskBand_When_TheBandIsAsked_Then_ItChangesExactlyThere()
    {
        Assert.Equal(LightBand.Dusk, LightBands.For(DayPhase.Dusk, LightBands.DayFrom - 0.001));
        Assert.Equal(LightBand.Day, LightBands.For(DayPhase.Dusk, LightBands.DayFrom));
        Assert.Equal(LightBand.Dusk, LightBands.For(DayPhase.Dawn, LightBands.NightUpTo + 0.001));
        Assert.Equal(LightBand.Night, LightBands.For(DayPhase.Dawn, LightBands.NightUpTo));
    }

    [Fact]
    public void Given_TheBands_When_WhatTheyBakeIsRead_Then_DayIsPlainDuskIsTodaysAndNightIsLowAndBlue()
    {
        Assert.Equal((1f, 1f, 1f), LightBands.BakedSun(LightBand.Day));
        Assert.Equal(ChunkMesher.DuskLight, LightBands.BakedSun(LightBand.Dusk));
        Assert.Equal(ChunkMesher.NightLight, LightBands.BakedSun(LightBand.Night));
        Assert.True(ChunkMesher.NightLight.B > ChunkMesher.NightLight.R);
        Assert.Equal(0.5f, LightBands.LanternStrength(LightBand.Day));
        Assert.Equal(1f, LightBands.LanternStrength(LightBand.Dusk));
        Assert.Equal(1f, LightBands.LanternStrength(LightBand.Night)); // the lanterns at full strength at night
        Assert.Equal(LightBands.Count, Enum.GetValues<LightBand>().Length);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(4.0)]
    [InlineData(4.43)]
    [InlineData(5.0)]
    [InlineData(5.51)]
    [InlineData(6.0)]
    [InlineData(13.0)]
    [InlineData(20.0)]
    [InlineData(20.49)]
    [InlineData(21.0)]
    [InlineData(21.57)]
    [InlineData(22.0)]
    [InlineData(23.5)]
    public void Given_AnyBandAndABoundaryHour_When_TheTintIsTimesTheBakedLight_Then_ItIsTheLooksSunLight(double hour)
    {
        var look = SkyLook.At(HourInput(hour));

        foreach (var band in Enum.GetValues<LightBand>())
        {
            var tint = LightBands.Tint(look.SunLight, band);
            var baked = LightBands.BakedSun(band);

            Assert.Equal(look.SunLight.R, tint.R * baked.R, 5);
            Assert.Equal(look.SunLight.G, tint.G * baked.G, 5);
            Assert.Equal(look.SunLight.B, tint.B * baked.B, 5);
        }
    }

    [Fact]
    public void Given_TheTwoBandsAtAnEdge_When_TheirBakedSunLightsAreTinted_Then_BothShowTheSameLight()
    {
        // The sun's share of a face is the band's baked light times the band's tint: the same for a mesh of either band, so a change of band shows no step in it.
        foreach (var hour in new[] { 4.43, 5.51, 20.49, 21.57 })
        {
            var look = SkyLook.At(HourInput(hour));
            var shown = Enum.GetValues<LightBand>().Select(band =>
            {
                var tint = LightBands.Tint(look.SunLight, band);
                var baked = LightBands.BakedSun(band);
                return (tint.R * baked.R, tint.G * baked.G, tint.B * baked.B);
            }).ToArray();

            Assert.All(shown, s =>
            {
                Assert.Equal(shown[0].Item1, s.Item1, 5);
                Assert.Equal(shown[0].Item2, s.Item2, 5);
                Assert.Equal(shown[0].Item3, s.Item3, 5);
            });
        }
    }

    [Fact]
    public void Given_TheWholeCycle_When_TheBandInForceIsTinted_Then_TheTintStaysNearOne()
    {
        // The tint of the band in force never strays far from 1, so a mesh is never shown much brighter or darker than it was baked.
        var clock = new DayClock(DayData.Default);
        for (var tick = 0L; tick < clock.CycleTicks; tick += 30)
        {
            var look = SkyLook.At(SkyLookInput.At(clock, tick));
            var tint = LightBands.Tint(look.SunLight, look.Band);

            Assert.InRange(tint.R, 0.4f, 1.6f);
            Assert.InRange(tint.G, 0.4f, 1.6f);
            Assert.InRange(tint.B, 0.4f, 1.6f);
        }
    }

    [Fact]
    public void Given_TheWholeCycle_When_TheBandIsFollowed_Then_ItChangesFourTimesInACycle()
    {
        var clock = new DayClock(DayData.Default);
        var changes = new List<(LightBand From, LightBand To)>();
        var last = LightBands.For(clock.PhaseAt(0), clock.DaylightAt(0));
        for (var tick = 1L; tick <= clock.CycleTicks; tick++)
        {
            var band = LightBands.For(clock.PhaseAt(tick), clock.DaylightAt(tick));
            if (band != last)
            {
                changes.Add((last, band));
                last = band;
            }
        }

        Assert.Equal(
            [(LightBand.Day, LightBand.Dusk), (LightBand.Dusk, LightBand.Night), (LightBand.Night, LightBand.Dusk), (LightBand.Dusk, LightBand.Day)],
            changes);
    }

    private static SkyLookInput HourInput(double hour)
    {
        var clock = new DayClock(DayData.Default);
        var tick = (long)Math.Round(hour / 24.0 * clock.CycleTicks) - (long)Math.Round(8 / 24.0 * clock.CycleTicks);
        return SkyLookInput.At(clock, tick);
    }
}
