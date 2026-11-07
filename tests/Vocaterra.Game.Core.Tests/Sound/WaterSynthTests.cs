using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>The water cues made in code (H10): non-silent, bounded, the same every time they are made, and the takes differ.</summary>
public sealed class WaterSynthTests
{
    private static readonly SoundCue[] WaterCues = [SoundCue.WaterSplash, SoundCue.UnderwaterBubbles];

    [Theory]
    [InlineData(SoundCue.WaterSplash)]
    [InlineData(SoundCue.UnderwaterBubbles)]
    public void Given_AWaterCue_When_Rendered_Then_ItIsSynthesizedNonSilentAndBounded(SoundCue cue)
    {
        Assert.True(SoundSynth.IsSynthesized(cue));
        Assert.Equal(SoundCategory.Sfx, SoundCues.CategoryOf(cue));

        var take = SoundCatalog.Of(cue).Takes - 1;
        var samples = SoundSynth.Render(cue, take);

        Assert.True(samples.Length > SoundSynth.SampleRate / 100, "a cue lasts more than 10 ms");
        var peak = samples.Max(static s => Math.Abs((int)s));
        Assert.True(peak > 0.2 * short.MaxValue, $"{cue} is audible (peak {peak})");
        Assert.True(peak <= 0.81 * short.MaxValue, $"{cue} stays within the peak ({peak})");
        Assert.Equal(0, samples[^1]); // the last 10 ms ramp to zero: no click at the end
    }

    [Theory]
    [InlineData(SoundCue.WaterSplash)]
    [InlineData(SoundCue.UnderwaterBubbles)]
    public void Given_AWaterCue_When_MadeTwice_Then_TheSamplesAreTheSame(SoundCue cue)
    {
        for (var take = 0; take < SoundCatalog.Of(cue).Takes; take++)
        {
            Assert.Equal(SoundSynth.Render(cue, take), SoundSynth.Render(cue, take));
        }
    }

    [Theory]
    [InlineData(SoundCue.WaterSplash)]
    [InlineData(SoundCue.UnderwaterBubbles)]
    public void Given_AWaterCueWithSeveralTakes_When_Rendered_Then_EachTakeDiffers(SoundCue cue)
    {
        var takes = Enumerable.Range(0, SoundCatalog.Of(cue).Takes).Select(t => SoundSynth.Render(cue, t)).ToList();

        for (var a = 0; a < takes.Count; a++)
        {
            for (var b = a + 1; b < takes.Count; b++)
            {
                Assert.NotEqual(takes[a], takes[b]);
            }
        }
    }

    [Fact]
    public void Given_TheWaterCues_When_Named_Then_TheyAreAppendedWithTheirWireNames()
    {
        Assert.Equal("water_splash", SoundCues.NameOf(SoundCue.WaterSplash));
        Assert.Equal("underwater_bubbles", SoundCues.NameOf(SoundCue.UnderwaterBubbles));
        Assert.True(WaterCues.All(cue => (int)cue > (int)SoundCue.FireCrackle));
    }
}
