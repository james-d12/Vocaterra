using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.Core.Worlds;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>Which launch options open the title screen (Q01), the flags that open its pages for a picture, the folders the worlds and the settings are kept in, and the options a world chosen on the title starts with.</summary>
public sealed class TitleLaunchOptionsTests
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
    public void Given_AnInteractiveRunWithNoFlag_When_Parsed_Then_TheTitleOpens()
    {
        var options = Parse();

        Assert.True(options.ShowsTitle);
        Assert.False(options.HasStartFlag);
        Assert.False(options.Title);
    }

    [Theory]
    [InlineData("--seed", "7")]
    [InlineData("--world", "valley")]
    [InlineData("--world", "streamed")]
    [InlineData("--terrain", "mountains=100")]
    [InlineData("--load", "a.save")]
    [InlineData("--save", "a.save")]
    [InlineData("--session", "2")]
    [InlineData("--skip-to-session", "2")]
    [InlineData("--check", "pre")]
    [InlineData("--playtest")]
    [InlineData("--pregen", "100")]
    [InlineData("--session-dir", "dir")]
    [InlineData("--set-need", "hunger", "50")]
    [InlineData("--needs", "off")]
    [InlineData("--assume-known", "agua")]
    [InlineData("--strict-save")]
    [InlineData("--debug-crash-after", "10")]
    [InlineData("--dev", "--lodging")]
    [InlineData("--dev", "--own-plot", "10,20")]
    [InlineData("--dev", "--mark-plot", "10,20")]
    public void Given_AStartFlag_When_Parsed_Then_NoTitleOpensAndTheRunStartsAsItDid(params string[] args)
    {
        var options = Parse(args);

        Assert.True(options.HasStartFlag, string.Join(' ', args));
        Assert.False(options.ShowsTitle, string.Join(' ', args));
    }

    [Theory]
    [InlineData("--text-scale", "100")]
    [InlineData("--quality", "low")]
    [InlineData("--sfx-volume", "0.5")]
    [InlineData("--mute")]
    [InlineData("--no-speech")]
    [InlineData("--no-sounds")]
    [InlineData("--dev")]
    [InlineData("--time", "12")]
    [InlineData("--dusk")]
    [InlineData("--no-autosave")]
    [InlineData("--tips")]
    [InlineData("--size", "800x600")]
    [InlineData("--content", "some/dir")]
    [InlineData("--saves-dir", "saves")]
    [InlineData("--settings-file", "settings.json")]
    [InlineData("--relaxed-needs")]
    [InlineData("--greyscale")]
    [InlineData("--no-mic-check")]
    public void Given_ASettingOrALookFlag_When_Parsed_Then_TheTitleStillOpens(params string[] args) =>
        Assert.True(Parse(args).ShowsTitle, string.Join(' ', args));

    [Theory]
    [InlineData("--screenshot", "x.png")]
    [InlineData("--screenshot", "x.png", "--frames", "10")]
    [InlineData("--screenshot", "x.png", "--look", "90,0")]
    [InlineData("--screenshot", "x.png", "--menu")]
    [InlineData("--screenshot", "x.png", "--settings")]
    [InlineData("--screenshot", "x.png", "--seed", "3")]
    public void Given_AScriptedRun_When_Parsed_Then_NoTitleOpensWhateverElseIsGiven(params string[] args)
    {
        var options = Parse(args);

        Assert.True(options.IsScreenshotRun);
        Assert.False(options.ShowsTitle, string.Join(' ', args));
    }

    [Fact]
    public void Given_TitleFlags_When_Parsed_Then_TheTitleOpensEvenInAScriptedRunOrWithAStartFlag()
    {
        var scripted = Parse("--screenshot", "x.png", "--title");
        var withSeed = Parse("--seed", "5", "--title");

        Assert.True(scripted.ShowsTitle);
        Assert.True(scripted.IsScreenshotRun);
        Assert.True(withSeed.ShowsTitle);
        Assert.True(withSeed.HasStartFlag);
        Assert.Null(scripted.TitlePage);
    }

    [Theory]
    [InlineData("main", TitlePage.Main)]
    [InlineData("new", TitlePage.New)]
    [InlineData("load", TitlePage.Load)]
    [InlineData("settings", TitlePage.Settings)]
    [InlineData("delete", TitlePage.ConfirmDelete)]
    public void Given_ATitlePage_When_Parsed_Then_ItImpliesTheTitleAndNamesThePage(string name, TitlePage page)
    {
        var options = Parse("--screenshot", "x.png", "--title-page", name);

        Assert.True(options.Title);
        Assert.True(options.ShowsTitle);
        Assert.Equal(page, options.TitlePage);
    }

    [Fact]
    public void Given_ABadTitlePage_When_Parsed_Then_ItIsRefusedWithTheNames()
    {
        var message = Refuse("--title-page", "options");

        Assert.Contains("--title-page needs main, new, load, settings or delete, got 'options'", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheTitleNameAndSeedFlags_When_Parsed_Then_TheyArePrefilledAndImplyTheTitle()
    {
        var options = Parse("--screenshot", "x.png", "--title-page", "new", "--title-name", "My world", "--title-seed", "hola");

        Assert.Equal("My world", options.TitleName);
        Assert.Equal("hola", options.TitleSeed);
        Assert.True(Parse("--title-name", "x").ShowsTitle);
        Assert.True(Parse("--title-seed", "x").Title);
    }

    [Fact]
    public void Given_TheFoldersFlags_When_Parsed_Then_TheyAreRead_AndEmptyOnesAreRefused()
    {
        var options = Parse("--saves-dir", "worlds", "--settings-file", "s.json");

        Assert.Equal("worlds", options.SavesDir);
        Assert.Equal("s.json", options.SettingsFile);
        Assert.Contains("--saves-dir needs a folder", Refuse("--saves-dir", string.Empty), StringComparison.Ordinal);
        Assert.Contains("--settings-file needs a file path", Refuse("--settings-file", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void TheUsageDocumentsTheTitleAndFolderFlags()
    {
        foreach (var flag in new[] { "--title ", "--title-page <page>", "--title-name <text>", "--title-seed <text>", "--saves-dir <dir>", "--settings-file <file>" })
        {
            Assert.Contains(flag, LaunchOptions.Usage, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("save's settings", LaunchOptions.Usage, StringComparison.Ordinal); // the settings are the settings file's now
        Assert.DoesNotContain("or the save's", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_TheFolderOptions_When_TheFoldersAreChosen_Then_TheOptionWinsAndAScriptedRunKeepsNoSettingsFile()
    {
        Assert.Equal("worlds", UserFolders.Saves(Parse("--saves-dir", "worlds")));
        Assert.Equal("s.json", UserFolders.SettingsPath(Parse("--settings-file", "s.json")));
        Assert.Equal("s.json", UserFolders.SettingsPath(Parse("--screenshot", "x.png", "--settings-file", "s.json"))); // a scripted run reads the file it is told of

        if (Environment.GetEnvironmentVariable(UserFolders.SettingsVariable) is null)
        {
            Assert.Null(UserFolders.SettingsPath(Parse("--screenshot", "x.png"))); // and never the player's own: its picture is the same on every machine
            Assert.EndsWith(Path.Combine("vocaterra", "settings.json"), UserFolders.SettingsPath(Parse()), StringComparison.Ordinal);
        }

        if (Environment.GetEnvironmentVariable(UserFolders.SavesVariable) is null)
        {
            Assert.EndsWith(Path.Combine("vocaterra", "saves"), UserFolders.Saves(Parse()), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Given_AWorldChosenOnTheTitle_When_TheStartFlagsAreDropped_Then_TheSettingsFlagsStayAndNothingStartsAWorldOnItsOwn()
    {
        var options = Parse("--seed", "5", "--world", "valley", "--load", "a.save", "--save", "b.save", "--session", "3", "--session-dir", "d", "--needs", "off", "--strict-save", "--set-need", "hunger", "50", "--quality", "low", "--text-scale", "125", "--mute", "--title");

        var stripped = options.WithoutStartFlags();

        Assert.False(stripped.HasStartFlag);
        Assert.False(stripped.Title);
        Assert.True(stripped.ShowsTitle);
        Assert.Equal(QualityLevel.Low, stripped.Quality);
        Assert.Equal(125, stripped.TextScale);
        Assert.True(stripped.Muted);
        Assert.Equal(LaunchOptions.DefaultSeed, stripped.Seed);
        Assert.Equal(WorldChoice.Streamed, stripped.World);
        Assert.Null(stripped.LoadPath);
        Assert.Null(stripped.SavePath);
    }

    [Fact]
    public void Given_AWorldFolder_When_TheSavePathIsAsked_Then_ItIsTheWorldsSaveInIt()
    {
        var plain = Parse();
        var started = plain with { WorldFolder = Path.Combine("saves", "luna") };

        Assert.Null(plain.WorldSavePath);
        Assert.Equal(Path.Combine("saves", "luna", "world.save"), started.WorldSavePath);
    }

    [Fact]
    public void TheSettingsFlagsAreTheRunsLaunchSettings()
    {
        var settings = Parse("--quality", "low", "--text-scale", "125", "--sfx-volume", "0.5", "--fov", "90", "--msaa", "0").ToLaunchSettings();

        Assert.Equal(QualityLevel.Low, settings.Quality);
        Assert.Equal(125, settings.TextPercent);
        Assert.Equal(0.5, settings.SfxVolume);
        Assert.Equal(90, settings.FieldOfView);
        Assert.Equal(0, settings.Msaa);
        Assert.False(settings.Scripted);
        Assert.True(Parse("--screenshot", "x.png").ToLaunchSettings().Scripted);
    }

    [Fact]
    public void TheWorldTypesMapBothWays()
    {
        Assert.Equal(WorldChoice.Valley, WorldChoices.Of(WorldKind.Valley));
        Assert.Equal(WorldChoice.Streamed, WorldChoices.Of(WorldKind.Streamed));
        Assert.Equal(WorldKind.Valley, WorldChoices.KindOf(WorldChoice.Valley));
        Assert.Equal(WorldKind.Streamed, WorldChoices.KindOf(WorldChoice.Streamed));
    }

    [Fact]
    public void Given_AWorldGivenOnTheCommandLine_When_Parsed_Then_WorldGivenIsSetAndTheDefaultIsNot()
    {
        Assert.True(Parse("--world", "streamed").WorldGiven);
        Assert.False(Parse().WorldGiven);
    }
}
