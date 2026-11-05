using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

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
    public void TheSaveAndLoadFilesAndTheSeedGivenAreRead()
    {
        var options = Parse("--save", "a.save", "--load", "b.save", "--seed", "7");

        Assert.Equal("a.save", options.SavePath);
        Assert.Equal("b.save", options.LoadPath);
        Assert.True(options.SeedGiven);
        Assert.Equal(7UL, options.Seed);

        var plain = Parse("--screenshot", "x.png");
        Assert.False(plain.SeedGiven); // a load then takes the save's seed
        Assert.Null(plain.SavePath);
        Assert.Null(plain.LoadPath);

        Assert.Contains("--load needs a file path", Refuse("--load", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSaveFileIsWrittenAndReadBackAsUtf8WithoutAByteOrderMark()
    {
        var path = Path.Combine(Path.GetTempPath(), "vocaterra-" + Path.GetRandomFileName() + ".save");
        try
        {
            SaveFile.Write(path, "{\"lemma\":\"río\"}");

            Assert.Equal("{\"lemma\":\"río\"}", SaveFile.Read(path));
            Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]);
            Assert.False(File.Exists(path + ".tmp"));

            SaveFile.Write(path, "{}"); // a second write replaces the first
            Assert.Equal("{}", SaveFile.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
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
    public void TheBookPlaySteps_S20b_KeepTheirTargetsAndTheirSpeed()
    {
        var options = Parse("--screenshot", "a.png", "--book-play", "madera", "--book-play-slow", "phrase:pedir-agua", "--book-play", "fruta");

        Assert.Equal([ScriptStepKind.BookPlay, ScriptStepKind.BookPlaySlow, ScriptStepKind.BookPlay], options.Script.Select(static step => step.Kind).ToArray());
        Assert.Equal(["madera", "phrase:pedir-agua", "fruta"], options.Script.Select(static step => step.Value).ToArray());
    }

    [Fact]
    public void TheSessionFlag_S26_StartsTheRunAtAGivenPlaySessionAndRefusesNonsense()
    {
        Assert.Null(Parse("--screenshot", "a.png").Session);
        Assert.Equal(2, Parse("--session", "2", "--lesson-run", "personas", "--screenshot", "a.png").Session);
        Assert.Equal(99, Parse("--session", "99").Session);
        Assert.Contains("--session needs", Refuse("--session", "0"), StringComparison.Ordinal);
        Assert.Contains("--session needs", Refuse("--session", "100"), StringComparison.Ordinal);
        Assert.Contains("--session needs", Refuse("--session", "two"), StringComparison.Ordinal);
        Assert.Contains("--session", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSkipToSessionFlag_V12c_IsANewGamesAndRefusesALoadASessionAndAPlaytest()
    {
        Assert.Null(Parse("--screenshot", "a.png").SkipToSession);
        Assert.Equal(4, Parse("--skip-to-session", "4").SkipToSession);
        Assert.Equal(6, Parse("--skip-to-session", "6", "--screenshot", "a.png", "--lesson-run", "gente-del-pueblo").SkipToSession);
        Assert.Contains("--skip-to-session needs", Refuse("--skip-to-session", "1"), StringComparison.Ordinal);
        Assert.Contains("--skip-to-session needs", Refuse("--skip-to-session", "four"), StringComparison.Ordinal);
        foreach (var other in new[] { new[] { "--load", "a.save" }, ["--session", "4"], ["--playtest"] })
        {
            var error = Refuse(["--skip-to-session", "4", .. other]);
            Assert.Contains("--skip-to-session starts a new game", error, StringComparison.Ordinal);
            Assert.Contains(other[0], error, StringComparison.Ordinal);
        }

        Assert.Contains("--skip-to-session", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSleepStep_V12c_IsAStepOfTheScript()
    {
        var options = Parse("--screenshot", "a.png", "--place", "antorcha@153,74,105", "--sleep", "--shot", "b.png");

        Assert.Equal([ScriptStepKind.Move, ScriptStepKind.Sleep, ScriptStepKind.Shot], options.Script.Select(static step => step.Kind).ToArray());
        Assert.Contains("--screenshot", Refuse("--sleep"), StringComparison.Ordinal);
        Assert.Contains("--sleep", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBookPractiseStep_S26_KeepsItsLessonAndNeedsTheScriptScreenshot()
    {
        var options = Parse("--screenshot", "a.png", "--lesson-run", "first-words", "--book-practise", "first-words", "--lesson-run", "first-words");

        Assert.Equal([ScriptStepKind.LessonRun, ScriptStepKind.BookPractise, ScriptStepKind.LessonRun], options.Script.Select(static step => step.Kind).ToArray());
        Assert.Equal("first-words", options.Script[1].Value);
        Assert.Contains("--screenshot", Refuse("--book-practise", "first-words"), StringComparison.Ordinal);
        Assert.Contains("--book-practise needs", Refuse("--screenshot", "a.png", "--book-practise", " "), StringComparison.Ordinal);
        Assert.Contains("--book-practise", LaunchOptions.Usage, StringComparison.Ordinal);
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
        Assert.Contains("Unknown argument '--dig'", Refuse("--screenshot", "a.png", "--dig", "1,2"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheQualityAndTheViewDistanceAreRead()
    {
        Assert.Equal(QualityLevel.High, Parse("--screenshot", "x.png").Quality); // the default is for a real graphics card
        Assert.Null(Parse("--screenshot", "x.png").ViewDistance);
        Assert.Equal(224, Parse("--quality", "low").QualityPreset.ViewDistance);
        Assert.Equal(304, Parse("--quality", "medium").QualityPreset.ViewDistance);
        Assert.Equal(400, Parse("--quality", "high").QualityPreset.ViewDistance);
        var distance = Parse("--quality", "low", "--view-distance", "96");
        Assert.Equal(96, distance.QualityPreset.ViewDistance);
        Assert.Equal(QualityLevel.Low, distance.QualityPreset.Level);
        Assert.Contains("--quality needs low, medium or high", Refuse("--quality", "ultra"), StringComparison.Ordinal);
        Assert.Contains("--view-distance needs a number of blocks, at least 48", Refuse("--view-distance", "10"), StringComparison.Ordinal);
        Assert.Contains("--view-distance needs a number", Refuse("--view-distance", "far"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeleportStepIsOneOfTheScriptsStepsInTheOrderGiven()
    {
        var options = Parse("--screenshot", "x.png", "--teleport", "230,222", "--look", "10,5", "--walk-to", "1,2");
        Assert.Equal([VoxelStepKind.Teleport, VoxelStepKind.Look, VoxelStepKind.WalkTo], options.VoxelSteps.Select(static s => s.Kind));
        Assert.Equal((230, 222), (options.VoxelSteps[0].X, options.VoxelSteps[0].Z));
        Assert.Contains("--teleport needs a block column and row", Refuse("--screenshot", "x.png", "--teleport", "230"), StringComparison.Ordinal);
        Assert.Contains("need --screenshot", Refuse("--teleport", "1,2"), StringComparison.Ordinal);
        Assert.Equal("none", Parse("--screenshot", "x.png", "--beacon", "none").Beacon); // no beacon at the start
    }

    [Fact]
    public void AnUnknownArgumentIsNamed()
    {
        Assert.Equal("Unknown argument '--nope'.", Refuse("--nope"));
    }

    [Fact]
    public void TheNeedsAreOnByDefaultAndNeedsOffFreezesThem()
    {
        Assert.False(Parse().NeedsOff);
        Assert.False(Parse("--needs", "on").NeedsOff);
        Assert.True(Parse("--needs", "off").NeedsOff);
        Assert.Contains("--needs", Refuse("--needs", "maybe"), StringComparison.Ordinal);
    }

    [Fact]
    public void SetNeedIsRepeatableAndKeepsTheOrderGiven()
    {
        var options = Parse("--set-need", "thirst", "30", "--seed", "7", "--set-need", "Hunger", "12.5");

        Assert.Equal([(NeedMeter.Thirst, 30.0), (NeedMeter.Hunger, 12.5)], options.SetNeeds.ToArray());
        Assert.Empty(Parse().SetNeeds);
    }

    [Theory]
    [InlineData("--set-need")]
    [InlineData("--set-need", "thirst")]
    [InlineData("--set-need", "courage", "5")]
    [InlineData("--set-need", "thirst", "101")]
    [InlineData("--set-need", "thirst", "-1")]
    [InlineData("--set-need", "thirst", "many")]
    [InlineData("--set-need", "1", "5")]
    public void ABadSetNeedIsRefused(params string[] args) => Assert.Contains("--set-need", Refuse(args), StringComparison.Ordinal);

    [Fact]
    public void TheHelpTextListsTheNeedsFlags()
    {
        Assert.Contains("--needs on|off", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--set-need", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEatAndDrinkStepsAreStepsOfTheScriptAndNeedTheScriptScreenshot()
    {
        var options = Parse("--screenshot", "a.png", "--set-need", "hunger", "40", "--eat", "pan", "--drink", "--shot", "b.png");

        Assert.Equal([ScriptStepKind.Eat, ScriptStepKind.Drink, ScriptStepKind.Shot], options.Script.Select(static step => step.Kind).ToArray());
        Assert.Equal("pan", options.Script[0].Value);
        Assert.Contains("--screenshot", Refuse("--drink"), StringComparison.Ordinal);
        Assert.Contains("--eat", Refuse("--screenshot", "a.png", "--eat", " "), StringComparison.Ordinal);
        Assert.Contains("--eat <item>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--drink", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNeedAndTakeBelongingsStepsAreStepsOfTheScriptAndNeedTheScriptScreenshot()
    {
        var options = Parse("--screenshot", "a.png", "--need", "thirst", "51", "--take-belongings", "--shot", "b.png");

        Assert.Equal([ScriptStepKind.SetNeed, ScriptStepKind.TakeBelongings, ScriptStepKind.Shot], options.Script.Select(static step => step.Kind).ToArray());
        Assert.Equal("thirst 51", options.Script[0].Value);
        Assert.Contains("--screenshot", Refuse("--take-belongings"), StringComparison.Ordinal);
        Assert.Contains("--need", Refuse("--screenshot", "a.png", "--need", "thirst", "101"), StringComparison.Ordinal);
        Assert.Contains("--need", Refuse("--screenshot", "a.png", "--need", "hunger"), StringComparison.Ordinal);
        Assert.Contains("--need <need>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--take-belongings", LaunchOptions.Usage, StringComparison.Ordinal);
    }
}
