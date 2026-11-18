using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>The command line's names for the streamed world (B07): <c>--world</c> as a named choice (the default streamed world, <c>valley</c> for the bounded valley), the worker threads, the far view's range and the stats line.</summary>
public sealed class StreamedWorldLaunchOptionsTests
{
    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        return error;
    }

    [Fact]
    public void TheStreamedWorldIsTheDefaultAndValleyKeepsTheBoundedValley()
    {
        Assert.Equal(WorldChoice.Streamed, Parse().World);
        Assert.Equal(WorldChoice.Streamed, Parse("--world", "streamed").World);
        Assert.Equal(WorldChoice.Valley, Parse("--world", "valley").World);
        Assert.Contains("--world needs streamed or valley, got 'sample'", Refuse("--world", "sample"), StringComparison.Ordinal);
        Assert.Equal(WorldChoice.Valley, WorldChoices.Parse("valley"));
        Assert.Null(WorldChoices.Parse("Valley"));
    }

    [Fact]
    public void ThePrefetchIsZeroToTwoAndTheDefaultIsLeftToTheGame()
    {
        Assert.Null(Parse().Prefetch);
        Assert.Equal(0, Parse("--prefetch", "0").Prefetch);
        Assert.Equal(2, Parse("--prefetch", "2").Prefetch);
        Assert.Contains("--prefetch needs 0, 1 or 2", Refuse("--prefetch", "3"), StringComparison.Ordinal);
        Assert.Contains("--prefetch needs 0, 1 or 2", Refuse("--prefetch", "-1"), StringComparison.Ordinal);
        Assert.Contains("--prefetch needs 0, 1 or 2", Refuse("--prefetch", "two"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheFarViewIsOffOrARangeInBlocksAndTheDefaultIsThePresets()
    {
        Assert.Null(Parse().FarViewRange);
        Assert.Equal(0, Parse("--far-view", "off").FarViewRange);
        Assert.Equal(16384, Parse("--far-view", "16384").FarViewRange);
        Assert.Contains("--far-view needs off or a range", Refuse("--far-view", "100"), StringComparison.Ordinal);
        Assert.Contains("--far-view needs off or a range", Refuse("--far-view", "far"), StringComparison.Ordinal);
        Assert.Contains("--far-view needs off or a range", Refuse("--far-view", "100000"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatsFlagIsOffByDefault()
    {
        Assert.False(Parse().StreamStats);
        Assert.True(Parse("--stream-stats").StreamStats);
        Assert.False(Parse().NoSettle);
        Assert.True(Parse("--no-settle").NoSettle);
    }

    [Fact]
    public void TheUsageNamesTheNewFlags()
    {
        foreach (var flag in new[] { "--world", "--prefetch", "--far-view", "--stream-stats", "--no-settle" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }
}
