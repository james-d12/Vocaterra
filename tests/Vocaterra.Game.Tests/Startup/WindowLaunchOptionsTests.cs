using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>How the window starts: an interactive run in borderless full screen, a <c>--size</c> run or a scripted run in a window of a fixed size.</summary>
public sealed class WindowLaunchOptionsTests
{
    [Fact]
    public void AnInteractiveRunStartsInFullScreen()
    {
        Assert.True(LaunchOptions.TryParse([], out var options, out var error), error);
        Assert.True(options.StartsFullScreen);
    }

    [Fact]
    public void ASizeAsksForAWindow()
    {
        Assert.True(LaunchOptions.TryParse(["--size", "1280x720"], out var options, out var error), error);
        Assert.False(options.StartsFullScreen);
        Assert.Equal(1280, options.WindowWidth);
    }

    [Fact]
    public void AScriptedRunKeepsItsWindow()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png"], out var options, out var error), error);
        Assert.False(options.StartsFullScreen);
    }
}
