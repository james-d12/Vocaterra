using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>The bubbles under water (H10): one sound for each bubble the breath meter loses while the eye is under, and none otherwise.</summary>
public sealed class BreathBubblesTests
{
    // Runs the breath for ticks under water (or above) and counts the bubbles the observer reports.
    private static int CountBubbles(Breath breath, BreathBubbles bubbles, int ticks, bool under)
    {
        var count = 0;
        for (var i = 0; i < ticks; i++)
        {
            _ = breath.Step(under);
            if (bubbles.Observe(breath, under))
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Given_AFullBreath_When_TheAirRunsDownUnderWater_Then_OneBubbleSoundsForEachBubbleLost()
    {
        var breath = new Breath();
        var bubbles = new BreathBubbles();
        _ = bubbles.Observe(breath, eyeUnder: true); // the first reading only sets the count

        var count = CountBubbles(breath, bubbles, Breath.MaxAir, under: true);

        Assert.Equal(Breath.MaxBubbles, count);
        Assert.True(breath.IsEmpty);
    }

    [Fact]
    public void Given_TheEyeIsAboveWater_When_TheBreathRefills_Then_NoBubbleSounds()
    {
        var breath = new Breath();
        breath.Restore(450);
        var bubbles = new BreathBubbles();
        _ = bubbles.Observe(breath, eyeUnder: false);

        var count = CountBubbles(breath, bubbles, 200, under: false);

        Assert.Equal(0, count);
        Assert.True(breath.IsFull);
    }

    [Fact]
    public void Given_TheAirIsFull_When_TheEyeIsUnderButNoBubbleIsLostYet_Then_NoBubbleSounds()
    {
        // Eighty ticks under water take the air to 820 of 900: the meter still shows all ten bubbles, so nothing pops.
        var breath = new Breath();
        var bubbles = new BreathBubbles();
        _ = bubbles.Observe(breath, eyeUnder: true);

        var count = CountBubbles(breath, bubbles, 80, under: true);

        Assert.Equal(0, count);
        Assert.Equal(Breath.MaxBubbles, breath.Bubbles);
    }

    [Fact]
    public void Given_ALoadedBreath_When_FirstRead_Then_TheCountIsSetWithoutABubble()
    {
        var breath = new Breath();
        breath.Restore(450);
        var bubbles = new BreathBubbles();

        Assert.False(bubbles.Observe(breath, eyeUnder: true));
    }

    [Fact]
    public void Given_NoAirLeft_When_TheEyeStaysUnder_Then_NoBubbleSoundsAsNothingIsLost()
    {
        var breath = new Breath();
        breath.Restore(0);
        var bubbles = new BreathBubbles();
        _ = bubbles.Observe(breath, eyeUnder: true);

        var count = CountBubbles(breath, bubbles, 120, under: true);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Given_TheBreathIsPartlyUsed_When_ItRefillsAndRunsDownAgain_Then_BubblesFollowTheBreath()
    {
        var breath = new Breath();
        var bubbles = new BreathBubbles();
        _ = bubbles.Observe(breath, eyeUnder: true);

        var first = CountBubbles(breath, bubbles, 300, under: true);  // 900 down to 600: bubbles at 810, 720, 630
        var refilled = CountBubbles(breath, bubbles, 200, under: false); // back to full: no sound
        var second = CountBubbles(breath, bubbles, 300, under: true);  // 900 down to 600 again

        Assert.Equal(3, first);
        Assert.Equal(0, refilled);
        Assert.Equal(3, second);
    }
}
