using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame.Tests;

public sealed class VoxelLaunchOptionsTests
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
    public void TheTwoDimensionalSliceStaysTheDefault()
    {
        var options = Parse();
        Assert.False(options.Voxel);
        Assert.Empty(options.VoxelSteps);
        Assert.Null(options.Beacon);
        Assert.Null(options.SceneFile);
        Assert.True(Parse("--voxel").Voxel);
        Assert.Equal(42UL, Parse("--voxel").Seed);
        Assert.Equal(7UL, Parse("--voxel", "--seed", "7").Seed);
    }

    [Fact]
    public void LookTakesYawAndPitchInDegrees()
    {
        var options = Parse("--voxel", "--look", "120,-5", "--screenshot", "a.png");
        var step = Assert.Single(options.VoxelSteps);
        Assert.Equal(VoxelStepKind.Look, step.Kind);
        Assert.Equal(120.0, step.Yaw);
        Assert.Equal(-5.0, step.Pitch);
        Assert.Equal(22.5, Parse("--voxel", "--look", "22.5,0.5", "--screenshot", "a.png").VoxelSteps[0].Yaw);
        Assert.Equal(-90.0, Parse("--voxel", "--look", "-90,0", "--screenshot", "a.png").VoxelSteps[0].Yaw);
        foreach (var bad in new[] { "120", "a,b", "1,2,3", "120,95", "120,-90", ",", "nan,0" })
        {
            Assert.Contains("--look", Fail("--voxel", "--look", bad, "--screenshot", "a.png"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WalkToTakesAColumnAndARow()
    {
        var step = Assert.Single(Parse("--voxel", "--walk-to", "60,52", "--screenshot", "a.png").VoxelSteps);
        Assert.Equal(VoxelStepKind.WalkTo, step.Kind);
        Assert.Equal((60, 52), (step.X, step.Z));
        foreach (var bad in new[] { "60", "60,52,1", "x,y", "-1,4", "60;52" })
        {
            Assert.Contains("--walk-to", Fail("--voxel", "--walk-to", bad, "--screenshot", "a.png"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void InteractTakesABlock()
    {
        var step = Assert.Single(Parse("--voxel", "--interact", "9,11,17", "--screenshot", "a.png").VoxelSteps);
        Assert.Equal(VoxelStepKind.Interact, step.Kind);
        Assert.Equal((9, 11, 17), (step.X, step.Y, step.Z));
        foreach (var bad in new[] { "9,11", "9,11,17,2", "a,b,c", "-1,2,3" })
        {
            Assert.Contains("--interact", Fail("--voxel", "--interact", bad, "--screenshot", "a.png"), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PlaceWithThreeNumbersIsTheBlockFormAndWithTwoStaysTheTileForm()
    {
        var step = Assert.Single(Parse("--voxel", "--place", "madera@18,11,17", "--screenshot", "a.png").VoxelSteps);
        Assert.Equal(VoxelStepKind.Place, step.Kind);
        Assert.Equal("madera", step.Item);
        Assert.Equal((18, 11, 17), (step.X, step.Y, step.Z));

        // The 2D form is unchanged: a noun and a tile, a step of the 2D script, no 3D step.
        var tile = Parse("--place", "madera@517,182", "--screenshot", "a.png");
        Assert.Empty(tile.VoxelSteps);
        var two = Assert.Single(tile.Steps);
        Assert.Equal(ScriptStepKind.Place, two.Kind);
        Assert.Equal("madera", two.Item);

        Assert.Contains("--place", Fail("--voxel", "--place", "madera", "--screenshot", "a.png"), StringComparison.Ordinal);
        Assert.Contains("--place", Fail("--voxel", "--place", "@1,2,3", "--screenshot", "a.png"), StringComparison.Ordinal);
        Assert.Contains("--place", Fail("--place", "madera@1,2,3,4", "--screenshot", "a.png"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheStepsKeepTheirOrder()
    {
        var options = Parse(
            "--voxel", "--look", "10,0", "--walk-to", "11,17", "--interact", "9,11,17", "--select", "3", "--place", "madera@12,11,17", "--look", "270,-45", "--screenshot", "a.png");
        Assert.Equal(
            [VoxelStepKind.Look, VoxelStepKind.WalkTo, VoxelStepKind.Interact, VoxelStepKind.Select, VoxelStepKind.Place, VoxelStepKind.Look],
            options.VoxelSteps.Select(static s => s.Kind).ToList());
        Assert.Equal(3, options.VoxelSteps[3].Slot);
        Assert.Empty(options.Steps); // the 2D script is not used in 3D
    }

    [Fact]
    public void BeaconNamesAPointAndSceneNamesAFile()
    {
        var options = Parse("--voxel", "--beacon", "river", "--scene", "route.json");
        Assert.Equal("river", options.Beacon);
        Assert.Equal("route.json", options.SceneFile);
        Assert.Contains("--beacon", Fail("--voxel", "--beacon", " "), StringComparison.Ordinal);
        Assert.Contains("needs a value", Fail("--voxel", "--beacon"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheThreeDimensionalOptionsNeedVoxelAndTheScriptedOnesNeedAScreenshot()
    {
        Assert.Contains("--voxel", Fail("--look", "1,2", "--screenshot", "a.png"), StringComparison.Ordinal);
        Assert.Contains("--voxel", Fail("--beacon", "river"), StringComparison.Ordinal);
        Assert.Contains("--voxel", Fail("--scene", "a.json"), StringComparison.Ordinal);
        Assert.Contains("--voxel", Fail("--walk-to", "1,2", "--screenshot", "a.png"), StringComparison.Ordinal);
        Assert.Contains("--screenshot", Fail("--voxel", "--walk-to", "1,2"), StringComparison.Ordinal);
        Assert.Contains("--screenshot", Fail("--voxel", "--look", "1,2"), StringComparison.Ordinal);
        Assert.Contains("--screenshot", Fail("--voxel", "--interact", "1,2,3"), StringComparison.Ordinal);
        _ = Parse("--voxel", "--beacon", "river"); // an interactive 3D run may start with a beacon
    }

    [Fact]
    public void SayTalksToTheGuideInThreeDimensions()
    {
        var options = Parse("--voxel", "--screenshot", "a.png", "--say", "¿dónde está el río?", "--say", "Quiero agua, por favor.");
        Assert.True(options.Voxel);
        Assert.Equal(["¿dónde está el río?", "Quiero agua, por favor."], options.Says);
    }

    [Fact]
    public void TheGuideAndTheTwoDimensionalWorldDoNotApplyInThreeDimensions()
    {
        foreach (var args in new[]
        {
            new[] { "--wav", "a.wav" }, new[] { "--walk", "500" }, new[] { "--mine", "1,2" }, new[] { "--craft", "mesa" }, new[] { "--teleport", "1,2" },
            new[] { "--grant", "madera:3" }, new[] { "--walk-right", "10" }, new[] { "--point", "1,2" }, new[] { "--speak", "hola" },
        })
        {
            var error = Fail(["--voxel", "--screenshot", "a.png", .. args]);
            Assert.Contains("--voxel", error, StringComparison.Ordinal);
            Assert.Contains("S3", error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SelectInventoryAndJournalWorkInThreeDimensions()
    {
        var options = Parse("--voxel", "--select", "2", "--inventory", "--journal", "--screenshot", "a.png");
        Assert.Equal(VoxelStepKind.Select, Assert.Single(options.VoxelSteps).Kind);
        Assert.True(options.ShowInventory);
        Assert.True(options.Journal);
        Assert.False(Parse("--voxel", "--screenshot", "a.png").ShowInventory);
    }

    [Fact]
    public void EveryTwoDimensionalDemoStillParsesAsBefore()
    {
        var options = Parse("--seed", "42", "--say", "¿dónde está el árbol?", "--screenshot", "a.png", "--frames", "240");
        Assert.False(options.Voxel);
        Assert.Single(options.Steps);
        Assert.Equal(240, options.Frames);
        var known = Parse("--seed", "42", "--point-nearest", "árbol", "--say", "es un árbol", "--screenshot", "b.png", "--frames", "300");
        Assert.Equal("árbol", known.PointNearest);
        Assert.Equal(2, Parse("--walk", "500", "--mine", "1,2", "--screenshot", "c.png").Steps.Count);
    }

    [Fact]
    public void TheUsageTextDescribesTheThreeDimensionalOptions()
    {
        foreach (var word in new[] { "--voxel", "--look", "--walk-to", "--interact", "--beacon", "--scene", "@<x,y,z>" })
        {
            Assert.Contains(word, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }
}
