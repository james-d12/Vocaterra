using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

/// <summary>The command line of the 3D game: the steps in the order given, the screen flags, the refusals, and the accepted-but-ignored <c>--voxel</c>.</summary>
public sealed class VoxelLaunchOptionsTests
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
    public void TheStepsKeepTheOrderTheyWereGiven()
    {
        var options = Parse("--screenshot", "a.png", "--look", "120,-5", "--walk-to", "11,17", "--interact", "9,11,17", "--craft", "antorcha", "--place", "antorcha@21,12,21", "--select", "2");

        Assert.Equal(VoxelStepKind.Look, options.VoxelSteps[0].Kind);
        Assert.Equal(VoxelStepKind.WalkTo, options.VoxelSteps[1].Kind);
        Assert.Equal(VoxelStepKind.Interact, options.VoxelSteps[2].Kind);
        Assert.Equal(VoxelStepKind.Craft, options.VoxelSteps[3].Kind);
        Assert.Equal("antorcha", options.VoxelSteps[3].Item);
        Assert.Equal(VoxelStepKind.Place, options.VoxelSteps[4].Kind);
        Assert.Equal((21, 12, 21), (options.VoxelSteps[4].X, options.VoxelSteps[4].Y, options.VoxelSteps[4].Z));
        Assert.Equal(VoxelStepKind.Select, options.VoxelSteps[5].Kind);
    }

    [Fact]
    public void VoxelIsAcceptedAndIgnoredAndTheSayLinesAreKept()
    {
        var options = Parse("--voxel", "--seed", "7", "--screenshot", "a.png", "--say", "¿dónde está el río?", "--say", "Quiero agua, por favor.");

        Assert.Equal(7UL, options.Seed);
        Assert.Equal(["¿dónde está el río?", "Quiero agua, por favor."], options.Says.ToArray());
        Assert.Empty(options.VoxelSteps);
    }

    [Fact]
    public void TheScreenFlagsAndTheBeaconAreRead()
    {
        var options = Parse("--screenshot", "a.png", "--inventory", "--crafting", "--journal", "--beacon", "river", "--greyscale", "--frames", "300");

        Assert.True(options.ShowInventory);
        Assert.True(options.ShowCrafting);
        Assert.True(options.Journal);
        Assert.Equal("river", options.Beacon);
        Assert.True(options.Greyscale);
        Assert.Equal(300, options.Frames);
        Assert.True(options.IsScreenshotRun);
    }

    [Fact]
    public void SpeechIsLoadedForAnInteractiveRunUnlessTold()
    {
        Assert.True(Parse().NeedsSpeech);
        Assert.False(Parse("--no-speech").NeedsSpeech);
        Assert.False(Parse("--screenshot", "a.png").NeedsSpeech);
    }

    [Fact]
    public void TheStepsNeedAScreenshotAndTheValuesAreChecked()
    {
        Assert.Contains("need --screenshot", Refuse("--look", "120,-5"), StringComparison.Ordinal);
        Assert.Contains("needs a yaw", Refuse("--screenshot", "a.png", "--look", "120"), StringComparison.Ordinal);
        Assert.Contains("hotbar slot", Refuse("--screenshot", "a.png", "--select", "11"), StringComparison.Ordinal);
        Assert.Contains("needs a block", Refuse("--screenshot", "a.png", "--interact", "9,11"), StringComparison.Ordinal);
        Assert.Contains("needs an item", Refuse("--screenshot", "a.png", "--craft", " "), StringComparison.Ordinal);
        Assert.Contains("--frames needs", Refuse("--screenshot", "a.png", "--frames", "0"), StringComparison.Ordinal);
        Assert.Contains("full or half", Refuse("--typed-credit", "some"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTwoDimensionalOptionsAreGone()
    {
        Assert.Contains("Unknown argument '--mine'", Refuse("--screenshot", "a.png", "--mine", "517,182"), StringComparison.Ordinal);
        Assert.Contains("Unknown argument '--teleport'", Refuse("--screenshot", "a.png", "--teleport", "1,2"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownArgumentIsNamed()
    {
        Assert.Equal("Unknown argument '--nope'.", Refuse("--nope"));
    }
}
