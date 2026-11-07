using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Sound;

namespace Vocaterra.Game.Core.Tests.Sound;

/// <summary>The splash of a fast entry into water (H10): a fall into the water splashes, a wade in and a swim do not.</summary>
public sealed class WaterEntryTests
{
    // The feet's state for a tick: the height in blocks and whether the feet are in water. The other fields do not matter to the splash.
    private static FootState Feet(double feetY, bool inWater) => new(0, 0, Grounded: false, Surface.Water, inWater, feetY);

    // Feeds the ticks in turn; returns the splash of each (true only where the feet went into the water fast).
    private static bool[] Run(params FootState[] ticks)
    {
        var entry = new WaterEntry();
        return ticks.Select(entry.Observe).ToArray();
    }

    [Fact]
    public void Given_AFallIntoWaterFasterThanTheThreshold_When_TheFeetEnter_Then_ItSplashes()
    {
        // 0.2 blocks in one tick is 12 blocks a second: a fall from above the surface.
        var splashes = Run(Feet(5.0, inWater: false), Feet(4.8, inWater: true));

        Assert.False(splashes[0]);
        Assert.True(splashes[1]);
    }

    [Fact]
    public void Given_AWadeIn_When_TheFeetEnterWithoutFalling_Then_NoSplash()
    {
        var splashes = Run(Feet(5.0, inWater: false), Feet(5.0, inWater: true));

        Assert.False(splashes[1]);
    }

    [Fact]
    public void Given_ASlowFallIntoWater_When_TheFeetEnter_Then_NoSplash()
    {
        // 0.1 blocks a tick is 6 blocks a second: slower than the threshold (a one-block step off a ledge is about 8 blocks a second, and makes none either).
        var splashes = Run(Feet(5.0, inWater: false), Feet(4.9, inWater: true));

        Assert.False(splashes[1]);
    }

    [Fact]
    public void Given_AStepOffAOneBlockLedge_When_TheFeetEnterTheWater_Then_NoSplash()
    {
        // A fall of one block from rest is about 8 blocks a second with the game's gravity: below the threshold.
        var perTick = Math.Sqrt(2 * VoxelPhysics.Gravity * VoxelPhysics.UnitsPerBlock) / VoxelPhysics.UnitsPerBlock;
        var speed = perTick * VoxelPhysics.TicksPerSecond;
        var splashes = Run(Feet(5.0, inWater: false), Feet(5.0 - perTick, inWater: true));

        Assert.True(speed < WaterEntry.SplashBlocksPerSecond, $"a one-block drop is {speed:0.0} blocks a second");
        Assert.False(splashes[1]);
    }

    [Fact]
    public void Given_AFallFromAHardLandingHeight_When_TheFeetEnter_Then_ItSplashes()
    {
        // The speed of a body falling HardFallBlocks from rest: the threshold is set below it, so the landing rule's fall splashes.
        var h = Footsteps.HardFallBlocks * VoxelPhysics.UnitsPerBlock;
        var speed = Math.Sqrt(2 * VoxelPhysics.Gravity * h) * VoxelPhysics.TicksPerSecond / VoxelPhysics.UnitsPerBlock;
        var perTick = speed / VoxelPhysics.TicksPerSecond;

        var splashes = Run(Feet(5.0, inWater: false), Feet(5.0 - perTick, inWater: true));

        Assert.True(speed >= WaterEntry.SplashBlocksPerSecond, $"a hard landing falls at {speed:0.0} blocks a second");
        Assert.True(splashes[1]);
    }

    [Fact]
    public void Given_TheFeetAreAlreadyInTheWater_When_TheyFallFast_Then_NoSplashAsItIsNotAnEntry()
    {
        var splashes = Run(Feet(5.0, inWater: true), Feet(4.5, inWater: true));

        Assert.False(splashes[1]);
    }

    [Fact]
    public void Given_TheFeetLeaveTheWaterAndReenterFast_When_TheyEnterAgain_Then_ItSplashesAgain()
    {
        var splashes = Run(Feet(5.0, inWater: false), Feet(4.8, inWater: true), Feet(4.8, inWater: false), Feet(4.5, inWater: true));

        Assert.True(splashes[1]);
        Assert.False(splashes[2]);
        Assert.True(splashes[3]);
    }

    [Fact]
    public void Given_TheFirstReadingIsInTheWater_When_Read_Then_NoSplash()
    {
        // A loaded game or the first tick: there is no earlier height to fall from.
        var splashes = Run(Feet(4.0, inWater: true));

        Assert.False(splashes[0]);
    }
}
