using Vocaterra.Game.Core.Menu;
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
    public void TheMenuFlagsOpenTheMenuOnAPageOnceTheStepsAreDone()
    {
        Assert.Null(Parse("--screenshot", "x.png").ShowMenu);
        Assert.Equal(MenuPage.Main, Parse("--menu").ShowMenu);
        Assert.Equal(MenuPage.Settings, Parse("--settings").ShowMenu);
        Assert.Equal(MenuPage.Controls, Parse("--controls").ShowMenu);
        Assert.Equal(7, Parse("--settings", "--menu-scroll", "7").MenuScroll);
        Assert.Contains("--menu-scroll needs a number", Refuse("--menu-scroll", "many"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheQualityAndTheViewDistanceAreRead()
    {
        Assert.Null(Parse("--screenshot", "x.png").Quality); // not given: the saved setting, high for a new game (the menu issue)
        Assert.Null(Parse("--screenshot", "x.png").ViewDistance);
        Assert.Equal(224, QualityPreset.For(Parse("--quality", "low").Quality!.Value).ViewDistance);
        Assert.Equal(QualityLevel.Medium, Parse("--quality", "medium").Quality);
        Assert.Equal(304, QualityPreset.For(Parse("--quality", "medium").Quality!.Value).ViewDistance);
        Assert.Equal(400, QualityPreset.For(Parse("--quality", "high").Quality!.Value).ViewDistance);
        var distance = Parse("--quality", "low", "--view-distance", "96");
        Assert.Equal(96, distance.ViewDistance);
        Assert.Equal(96, QualityPreset.For(QualityLevel.High).WithViewDistance(96).ViewDistance); // a chosen distance holds when the level changes
        Assert.Equal(QualityLevel.Low, distance.Quality);
        Assert.Contains("--quality needs low, medium or high", Refuse("--quality", "ultra"), StringComparison.Ordinal);
        Assert.Contains("--view-distance needs a number of blocks, at least 48", Refuse("--view-distance", "10"), StringComparison.Ordinal);
        Assert.Contains("--view-distance needs a number", Refuse("--view-distance", "far"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheMsaaFlagTakesTheSampleCountsAndTheQualityPresetsCarryTheirOwn()
    {
        Assert.Null(Parse("--quality", "low").Msaa);
        Assert.Equal(0, Parse("--msaa", "0").Msaa);
        Assert.Equal(8, Parse("--msaa", "8").Msaa);
        Assert.Contains("--msaa needs 0, 2, 4 or 8", Refuse("--msaa", "3"), StringComparison.Ordinal);
        Assert.Contains("--msaa needs 0, 2, 4 or 8", Refuse("--msaa", "lots"), StringComparison.Ordinal);
        Assert.Contains("--msaa", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.All(Enum.GetValues<QualityLevel>(), static level => Assert.Contains(QualityPreset.MultiSamplesOf(level), new[] { 0, 2, 4, 8 }));
        Assert.True(QualityPreset.MultiSamplesOf(QualityLevel.High) >= 4, "the default look is antialiased");
    }

    [Fact]
    public void TheGrassFlagTakesTheMenusRangeAndStepsAndRefusesTheRest()
    {
        Assert.Null(Parse("--screenshot", "x.png").Grass); // not given: the saved setting, the default for a new game
        Assert.Equal(0, Parse("--grass", "0").Grass);
        Assert.Equal(100, Parse("--grass", "100").Grass);
        Assert.Equal(40, Parse("--grass", "40").Grass);
        Assert.Contains("--grass needs a whole number of percent from 0 to 100 in steps of 10", Refuse("--grass", "45"), StringComparison.Ordinal);
        Assert.Contains("--grass needs a whole number", Refuse("--grass", "110"), StringComparison.Ordinal);
        Assert.Contains("--grass needs a whole number", Refuse("--grass", "lots"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorkersFlagTakesAutoOrOneToEightAndRefusesTheRest()
    {
        Assert.Null(Parse("--screenshot", "x.png").Workers); // not given: the saved setting (Auto for a new game)
        Assert.Equal(0, Parse("--workers", "auto").Workers);
        Assert.Equal(1, Parse("--workers", "1").Workers);
        Assert.Equal(8, Parse("--workers", "8").Workers);
        Assert.Contains("--workers needs auto or a number from 1 to 8", Refuse("--workers", "9"), StringComparison.Ordinal);
        Assert.Contains("--workers needs auto or a number from 1 to 8", Refuse("--workers", "0"), StringComparison.Ordinal);
        Assert.Contains("--workers needs auto or a number from 1 to 8", Refuse("--workers", "many"), StringComparison.Ordinal);
        Assert.Contains("--workers", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Equal(2, Parse("--prefetch", "2").Prefetch); // the old option keeps its range
        Assert.Contains("--prefetch needs 0, 1 or 2", Refuse("--prefetch", "4"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheFarViewFlagsTakeTheSettingsRangesAndRefuseTheRest()
    {
        var options = Parse("--far-distance", "12", "--far-detail", "150", "--far-trees", "1024", "--far-textures", "3", "--far-exact", "off");
        Assert.Equal(12, options.FarDistance);
        Assert.Equal(150, options.FarDetail);
        Assert.Equal(4, options.FarTrees); // 256, 512, 1024: the setting's choice 4
        Assert.Equal(3, options.FarTextures);
        Assert.Equal(1, options.FarExact);
        Assert.Equal(0, Parse("--far-distance", "auto").FarDistance);
        Assert.Equal(0, Parse("--far-trees", "auto").FarTrees);
        Assert.Equal(1, Parse("--far-trees", "off").FarTrees);
        Assert.Equal(6, Parse("--far-trees", "4096").FarTrees);
        Assert.Equal(5, Parse("--far-exact", "2000").FarExact);
        Assert.Null(Parse("--screenshot", "x.png").FarDetail);
        Assert.Contains("--far-distance needs", Refuse("--far-distance", "17"), StringComparison.Ordinal);
        Assert.Contains("--far-distance needs", Refuse("--far-distance", "0"), StringComparison.Ordinal);
        Assert.Contains("--far-detail needs", Refuse("--far-detail", "110"), StringComparison.Ordinal);
        Assert.Contains("--far-detail needs", Refuse("--far-detail", "300"), StringComparison.Ordinal);
        Assert.Contains("--far-trees needs", Refuse("--far-trees", "300"), StringComparison.Ordinal);
        Assert.Contains("--far-textures needs", Refuse("--far-textures", "4"), StringComparison.Ordinal);
        Assert.Contains("--far-exact needs", Refuse("--far-exact", "700"), StringComparison.Ordinal);
        foreach (var flag in new[] { "--far-distance", "--far-detail", "--far-trees", "--far-textures", "--far-exact", "--pregen", "--pregen-at" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePregenFlagsTakeARadiusAndAColumn()
    {
        Assert.Null(Parse("--screenshot", "x.png").Pregen);
        var options = Parse("--pregen", "4096", "--pregen-at", "-120,300");
        Assert.Equal(4096, options.Pregen);
        Assert.Equal((-120, 300), options.PregenAt);
        Assert.Contains("--pregen needs a radius", Refuse("--pregen", "10"), StringComparison.Ordinal);
        Assert.Contains("--pregen needs a radius", Refuse("--pregen", "99999"), StringComparison.Ordinal);
        Assert.Contains("--pregen-at needs", Refuse("--pregen-at", "somewhere"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheFieldOfViewMouseAndInvertFlagsTakeTheMenusRangesAndRefuseTheRest()
    {
        var options = Parse("--fov", "95", "--mouse-sensitivity", "250", "--invert-y", "on");
        Assert.Equal(95, options.FieldOfView);
        Assert.Equal(250, options.MouseSensitivity);
        Assert.True(options.InvertY);
        Assert.False(Parse("--invert-y", "off").InvertY);

        var none = Parse("--screenshot", "x.png");
        Assert.Null(none.FieldOfView);
        Assert.Null(none.MouseSensitivity);
        Assert.Null(none.InvertY);

        Assert.Contains("--fov needs a whole number of degrees from 60 to 110 in steps of 5", Refuse("--fov", "57"), StringComparison.Ordinal);
        Assert.Contains("--fov needs", Refuse("--fov", "120"), StringComparison.Ordinal);
        Assert.Contains("--mouse-sensitivity needs a whole number of percent from 25 to 300", Refuse("--mouse-sensitivity", "10"), StringComparison.Ordinal);
        Assert.Contains("--mouse-sensitivity needs", Refuse("--mouse-sensitivity", "fast"), StringComparison.Ordinal);
        Assert.Contains("--invert-y needs on or off", Refuse("--invert-y", "yes"), StringComparison.Ordinal);
        Assert.Contains("--invert-y needs a value", Refuse("--invert-y"), StringComparison.Ordinal);
        foreach (var flag in new[] { "--fov", "--mouse-sensitivity", "--invert-y", "--view-distance" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheTeleportStepsTakeSignedCoordinatesAndTeleportToTakesAHeight()
    {
        var options = Parse("--screenshot", "x.png", "--teleport", "-230,-222", "--teleport-to", "-40,-54,120", "--shot", "a.png");
        Assert.Equal([VoxelStepKind.Teleport, VoxelStepKind.TeleportTo], options.VoxelSteps.Select(static s => s.Kind));
        Assert.Equal((-230, -222), (options.VoxelSteps[0].X, options.VoxelSteps[0].Z));
        Assert.Equal((-40, -54, 120), (options.VoxelSteps[1].X, options.VoxelSteps[1].Y, options.VoxelSteps[1].Z));
        Assert.Contains("--teleport-to needs a block", Refuse("--screenshot", "x.png", "--teleport-to", "1,2"), StringComparison.Ordinal);
        Assert.Contains("need --screenshot", Refuse("--teleport-to", "1,2,3"), StringComparison.Ordinal);
        Assert.Contains("--teleport-to", LaunchOptions.Usage, StringComparison.Ordinal);
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
    public void ThePourStepIsOneOfTheScriptsStepsInTheOrderGiven()
    {
        var options = Parse("--screenshot", "x.png", "--pour", "20,11,30", "--wait", "3", "--look", "10,5");
        Assert.Equal([VoxelStepKind.Pour, VoxelStepKind.Look], options.VoxelSteps.Select(static s => s.Kind));
        Assert.Equal((20, 11, 30), (options.VoxelSteps[0].X, options.VoxelSteps[0].Y, options.VoxelSteps[0].Z));
        Assert.Contains("--pour needs a block", Refuse("--screenshot", "x.png", "--pour", "20,11"), StringComparison.Ordinal);
        Assert.Contains("need --screenshot", Refuse("--pour", "1,2,3"), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownArgumentIsNamed()
    {
        Assert.Equal("Unknown argument '--nope'.", Refuse("--nope"));
    }

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1", 1.0)]
    [InlineData("2.75", 2.75)]
    [InlineData("4", 4.0)]
    public void TheNeedsRateFlagTakesAStepOfAQuarterFromHalfToFour(string text, double expected)
    {
        Assert.Null(Parse().NeedsRate);
        Assert.Equal(expected, Parse("--needs-rate", text).NeedsRate);
    }

    [Theory]
    [InlineData("0.25")]
    [InlineData("4.5")]
    [InlineData("1.3")]
    [InlineData("fast")]
    [InlineData("NaN")]
    public void TheNeedsRateFlagRefusesAValueOutOfRangeOrOffAStep(string text)
    {
        Assert.Contains("--needs-rate", Refuse("--needs-rate", text), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePassFlagOpensThePassAndRefusesAnyOtherValue()
    {
        Assert.False(Parse().PassOpen);
        Assert.True(Parse("--pass", "open").PassOpen);
        Assert.Contains("--pass", Refuse("--pass", "shut"), StringComparison.Ordinal);
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

    [Fact]
    public void AScriptedRunTimesItsTurnsWithNoClockSoItsLatencyLabelsDoNotMove()
    {
        // T8: r30's declined.png moved with the turn's wall time (the dialogue note's "10.0 ms"); a scripted run has no clock, so its turns report zero.
        var scripted = Parse("--screenshot", "a.png", "--wait", "1");
        Assert.True(scripted.IsScreenshotRun && scripted.Script.Count > 0);
        Assert.Null(scripted.TurnClock);

        var play = Parse("--seed", "7");
        Assert.NotNull(play.TurnClock);
        Assert.True(play.TurnClock!() >= 0);
    }
}
