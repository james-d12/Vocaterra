using Vocaterra.Game.Core.Scripted;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The size gate of a scripted screenshot (S12): the picture waits for the wanted size, then for the patience only.</summary>
public sealed class ScreenshotGateTests
{
    [Fact]
    public void AScreenshotWaitsForTheWantedSizeAndThenForPatienceOnly()
    {
        // The window's first frame at the default 800 x 480 is not the final size; the wanted 1280 x 720 is.
        Assert.False(ScreenshotGate.IsFinalSize(800, 480, 1280, 720, 800, 480));
        Assert.False(ScreenshotGate.IsFinalSize(1280, 720, 1280, 720, 800, 480));
        Assert.True(ScreenshotGate.IsFinalSize(1280, 720, 1280, 720, 1280, 720));

        Assert.False(ScreenshotGate.MayShoot(false, 0));
        Assert.False(ScreenshotGate.MayShoot(false, ScreenshotGate.PatienceFrames - 1));
        Assert.True(ScreenshotGate.MayShoot(false, ScreenshotGate.PatienceFrames));
        Assert.True(ScreenshotGate.MayShoot(true, 0));
    }

    [Fact]
    public void AShotFollowsAFrameDrawnWholeAtTheFinalSize()
    {
        // V23a: the frame in which the window reached its size was drawn before it, in a corner of the new one; the next frame is whole.
        Assert.False(ScreenshotGate.MayShoot(true, 0, 1));
        Assert.True(ScreenshotGate.MayShoot(true, 0, ScreenshotGate.FramesAtSize));
        Assert.False(ScreenshotGate.MayShoot(false, 0, ScreenshotGate.FramesAtSize));
        Assert.True(ScreenshotGate.MayShoot(false, ScreenshotGate.PatienceFrames, 0)); // a window never resized still gets its picture
    }
}
