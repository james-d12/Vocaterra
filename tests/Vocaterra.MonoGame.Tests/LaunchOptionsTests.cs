using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

public sealed class LaunchOptionsTests
{
    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static string Fail(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        return error;
    }

    [Fact]
    public void NoArgumentsGiveSeed42AndAnInteractiveRun()
    {
        var options = Parse();
        Assert.Equal(42UL, options.Seed);
        Assert.False(options.IsScreenshotRun);
        Assert.Null(options.ScreenshotPath);
    }

    [Fact]
    public void TheSeedIsAnUnsignedSixtyFourBitInteger()
    {
        Assert.Equal(ulong.MaxValue, Parse("--seed", "18446744073709551615").Seed);
        Assert.Contains("--seed", Fail("--seed", "-1"), StringComparison.Ordinal);
        Assert.Contains("--seed", Fail("--seed", "abc"), StringComparison.Ordinal);
        Assert.Contains("--seed", Fail("--seed", "18446744073709551616"), StringComparison.Ordinal);
        Assert.Contains("needs a value", Fail("--seed"), StringComparison.Ordinal);
    }

    [Fact]
    public void AScreenshotRunTakesFramesAndScriptedWalking()
    {
        var options = Parse("--seed", "7", "--screenshot", "out/p2.png", "--frames", "120", "--walk-right", "60");
        Assert.True(options.IsScreenshotRun);
        Assert.Equal("out/p2.png", options.ScreenshotPath);
        Assert.Equal((7UL, 120, 60), (options.Seed, options.Frames, options.WalkRightTicks));
    }

    [Fact]
    public void AScreenshotRunDefaultsToOneHundredTwentyFramesAndNoWalking()
    {
        var options = Parse("--screenshot", "x.png");
        Assert.Equal((LaunchOptions.DefaultScreenshotFrames, 0), (options.Frames, options.WalkRightTicks));
    }

    [Fact]
    public void FramesWalkingAndSayingNeedAScreenshot()
    {
        Assert.Contains("--screenshot", Fail("--frames", "10"), StringComparison.Ordinal);
        Assert.Contains("--screenshot", Fail("--walk-right", "10"), StringComparison.Ordinal);
        Assert.Contains("--screenshot", Fail("--say", "hola"), StringComparison.Ordinal);
    }

    [Fact]
    public void SayIsRepeatableAndKeepsTheLinesVerbatimInOrder()
    {
        var options = Parse("--seed", "42", "--say", "¿dónde está el árbol?", "--say", "gracias", "--screenshot", "p3.png", "--frames", "300");

        Assert.Equal(["¿dónde está el árbol?", "gracias"], options.Says);
        Assert.Equal(300, options.Frames);
        Assert.True(options.IsScreenshotRun);
    }

    [Fact]
    public void SayingRaisesTheDefaultFrameCountForTheWalkAndTheTyping()
    {
        Assert.Equal(LaunchOptions.DefaultSayFrames, Parse("--say", "hola", "--screenshot", "x.png").Frames);
        Assert.Equal(LaunchOptions.DefaultScreenshotFrames, Parse("--screenshot", "x.png").Frames);
        Assert.Empty(Parse("--screenshot", "x.png").Says);
    }

    [Fact]
    public void ABlankSayAndAnEmptyContentFolderAreRejected()
    {
        Assert.Contains("--say", Fail("--screenshot", "x.png", "--say", "   "), StringComparison.Ordinal);
        Assert.Contains("--content", Fail("--content", string.Empty), StringComparison.Ordinal);
        Assert.Equal("content/game-v1", Parse("--content", "content/game-v1").ContentDirectory);
        Assert.Null(Parse().ContentDirectory);
    }

    [Fact]
    public void BadNumbersAndUnknownArgumentsAreRejected()
    {
        Assert.Contains("--frames", Fail("--screenshot", "x.png", "--frames", "0"), StringComparison.Ordinal);
        Assert.Contains("--frames", Fail("--screenshot", "x.png", "--frames", "-3"), StringComparison.Ordinal);
        Assert.Contains("--walk-right", Fail("--screenshot", "x.png", "--walk-right", "x"), StringComparison.Ordinal);
        Assert.Contains("--bogus", Fail("--bogus"), StringComparison.Ordinal);
        Assert.Contains("file path", Fail("--screenshot", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void HelpIsNotAnErrorButStopsTheRun()
    {
        Assert.False(LaunchOptions.TryParse(["--help"], out _, out var error));
        Assert.Empty(error);
    }
}
