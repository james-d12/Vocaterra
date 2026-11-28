using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Title;
using Vocaterra.Game.Core.Worlds;

namespace Vocaterra.Game.Core.Tests.Title;

/// <summary>The title screen's page flow (Q01): the main page, New game with its name rules and seed, Load with its order and refusals, Delete behind a confirmation, Settings and Quit.</summary>
public sealed class TitleScreenTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vocaterra-title-" + Path.GetRandomFileName());
    private long _now = 1_000;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class Metrics : ITitleMetrics, IMenuMetrics
    {
        public int LoadCapacity { get; set; } = 3;

        public Func<TitlePage, int, int, int, int, int?> Slot { get; set; } = static (_, _, _, _, _) => null;

        public int? SlotAt(TitlePage page, int loadScroll, int worldCount, int x, int y) => Slot(page, loadScroll, worldCount, x, y);

        public int SettingsCapacity => 8;

        public int ControlsMaxScroll => 0;

        public MenuHit? HitAt(MenuPage page, int settingsScroll, int x, int y) => null;

        public double TrackFraction(int settingsScroll, int row, int x) => 0;
    }

    private sealed class Rig(string folder, Func<long> now)
    {
        public Metrics Metrics { get; } = new();

        public WorldStore Store { get; } = new(folder, now);

        public PlayerSettings Settings { get; } = new();

        public ulong Draw { get; set; } = 99UL;

        public int Draws { get; private set; }

        public TitleScreen Make()
        {
            var menu = new MenuScreen(MenuRows.Settings(Settings, null), Metrics, static () => { });
            return new TitleScreen(Store, Metrics, menu, () => { Draws++; return Draw; }, static n => "World " + n);
        }
    }

    private Rig NewRig() => new(_folder, () => _now);

    private static TestInputFrame Press(params GameKey[] keys) => TestInputFrame.Pressing(keys);

    private static TestInputFrame Click(int x, int y) => new() { LeftDown = true, PointerX = x, PointerY = y };

    private static void Keys(TitleScreen title, params GameKey[] keys)
    {
        foreach (var key in keys)
        {
            title.HandleInput(Press(key));
            title.HandleInput(new TestInputFrame()); // the key comes up again
        }
    }

    private static void TypeText(TitleScreen title, string text)
    {
        foreach (var c in text)
        {
            title.Type(c);
        }
    }

    [Fact]
    public void Given_NoWorlds_When_TheTitleOpens_Then_ItIsTheMainPageOnNewGameAndLoadGameIsGreyed()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.Main);

        Assert.Equal(TitlePage.Main, title.Page);
        Assert.Equal(TitleSlot.NewGame, title.Selected);
        Assert.False(title.HasWorlds);

        Keys(title, GameKey.Down); // the cursor passes the greyed Load game
        Assert.Equal(TitleSlot.Settings, title.Selected);
        Keys(title, GameKey.Up);
        Assert.Equal(TitleSlot.NewGame, title.Selected);

        title.Start(TitlePage.Main);
        Keys(title, GameKey.Down, GameKey.Down, GameKey.Down, GameKey.Down);
        Assert.Equal(TitleSlot.Settings, title.Selected);
    }

    [Fact]
    public void Given_NoWorlds_When_LoadGameIsClicked_Then_NothingOpensAndTheNoticeSaysThereAreNone()
    {
        var rig = NewRig();
        rig.Metrics.Slot = static (page, _, _, _, _) => page == TitlePage.Main ? TitleSlot.LoadGame : null;
        var title = rig.Make();
        title.Start(TitlePage.Main);

        title.HandleInput(Click(5, 5));

        Assert.Equal(TitlePage.Main, title.Page);
        Assert.Equal("titleNoWorlds", title.Notice!.Key);
        Assert.NotEqual(TitleSlot.LoadGame, title.Selected); // the pointer does not choose a greyed button either
    }

    [Fact]
    public void Given_TheMainPage_When_QuitIsChosen_Then_TheChoiceIsQuit()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.Main);

        Keys(title, GameKey.Up); // wraps to Quit
        Keys(title, GameKey.Enter);

        Assert.Equal(new TitleChoice(TitleChoiceKind.Quit), title.Choice);
    }

    [Fact]
    public void Given_TheMainPage_When_EscIsPressed_Then_NothingHappens()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.Main);

        Keys(title, GameKey.Escape);

        Assert.Equal(TitlePage.Main, title.Page);
        Assert.Null(title.Choice);
    }

    [Fact]
    public void Given_NewGame_When_Entered_Then_TheNewPageHasTheDefaultNameABlankSeedAndTheStreamedWorld()
    {
        var rig = NewRig();
        _ = rig.Store.Create("World 1", 5UL, WorldKind.Streamed);
        var title = rig.Make();
        title.Start(TitlePage.Main);

        Keys(title, GameKey.Enter);

        Assert.Equal(TitlePage.New, title.Page);
        Assert.Equal("World 2", title.Name.Text); // the next free N
        Assert.Equal(string.Empty, title.Seed.Text);
        Assert.Equal(WorldKind.Streamed, title.WorldType);
        Assert.Equal(TitleSlot.NameField, title.Selected);
        Assert.True(title.TakesText);
        Assert.Equal(NameProblem.None, title.NameIssue);

        Keys(title, GameKey.Escape);
        Assert.Equal(TitlePage.Main, title.Page);
    }

    [Fact]
    public void Given_TheNewPage_When_TheNameIsTypedOverTheDefault_Then_TheDefaultIsReplacedAndEditsWork()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.New);

        TypeText(title, "Luna");
        Assert.Equal("Luna", title.Name.Text); // the first typing replaces the default "World 1"
        title.HandleInput(Press(GameKey.Back));
        title.HandleInput(new TestInputFrame());
        Assert.Equal("Lun", title.Name.Text);
        TypeText(title, "\u0001"); // a control character is not text
        Assert.Equal("Lun", title.Name.Text);
        title.HandleInput(Press(GameKey.Left));
        title.HandleInput(new TestInputFrame());
        TypeText(title, "-");
        Assert.Equal("Lu-n", title.Name.Text);
    }

    [Fact]
    public void Given_TheDefaultNameAndABackspace_When_NothingElseWasTyped_Then_TheWholeDefaultGoes()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.New);

        title.HandleInput(Press(GameKey.Back));

        Assert.Equal(string.Empty, title.Name.Text);
        Assert.Equal(NameProblem.Empty, title.NameIssue);
    }

    [Fact]
    public void Given_AnEmptyName_When_CreateIsChosen_Then_ItIsRefusedWithTheReasonAndNoWorldIsMade()
    {
        var rig = NewRig();
        var title = rig.Make();
        title.Start(TitlePage.New);
        title.Prefill("   ", null);

        Walk(title, TitleSlot.Create);
        Keys(title, GameKey.Enter);

        Assert.Null(title.Choice);
        Assert.Equal("titleNameEmpty", title.Notice!.Key);
        Assert.Equal(TitleSlot.NameField, title.Selected); // the cursor goes to the field to fix
        Assert.Empty(rig.Store.List());
    }

    private static void Walk(TitleScreen title, int slot)
    {
        for (var i = 0; i < TitleSlot.NewCount && title.Selected != slot; i++)
        {
            Keys(title, GameKey.Down);
        }
    }

    [Fact]
    public void Given_ATakenName_When_CreateIsChosen_Then_ItIsRefusedAndTheReasonIsShownLiveToo()
    {
        var rig = NewRig();
        _ = rig.Store.Create("Mine", 1UL, WorldKind.Streamed);
        var title = rig.Make();
        title.Start(TitlePage.New);

        title.Prefill("mine", null);
        Assert.Equal(NameProblem.Taken, title.NameIssue); // shown as it is typed
        Walk(title, TitleSlot.Create);
        Keys(title, GameKey.Enter);

        Assert.Null(title.Choice);
        Assert.Equal(new TitleNotice("titleNameTaken", "mine"), title.Notice);
        Assert.Single(rig.Store.List());
    }

    [Fact]
    public void Given_ANameAtTheLimit_When_MoreIsTyped_Then_TheFieldTakesNoMore()
    {
        var rig = NewRig();
        var title = rig.Make();
        title.Start(TitlePage.New);
        title.Prefill(new string('x', WorldNames.MaxLength), null);
        TypeText(title, "y"); // the field takes no more than the limit

        Assert.Equal(WorldNames.MaxLength, title.Name.Text.Length);
        Assert.Equal(NameProblem.None, title.NameIssue);
    }

    [Fact]
    public void Given_AFilledPage_When_CreateIsChosen_Then_TheWorldIsMadeOnDiskWithTheSeedAndTypeAndTheChoiceNamesIt()
    {
        var rig = NewRig();
        var title = rig.Make();
        title.Start(TitlePage.New);
        TypeText(title, "Luna");
        Keys(title, GameKey.Enter); // the name's Enter goes to the seed
        Assert.Equal(TitleSlot.SeedField, title.Selected);
        TypeText(title, "1234");
        Keys(title, GameKey.Down, GameKey.Down); // Random -> World type
        Assert.Equal(TitleSlot.WorldType, title.Selected);
        Keys(title, GameKey.Right); // Streamed -> Valley
        Assert.Equal(WorldKind.Valley, title.WorldType);

        Walk(title, TitleSlot.Create);
        Keys(title, GameKey.Enter);

        var choice = title.Choice!;
        Assert.Equal(TitleChoiceKind.NewWorld, choice.Kind);
        Assert.Equal(new WorldMeta(1, "Luna", 1234UL, WorldKind.Valley, 1_000, 1_000), choice.World!.Meta);
        Assert.Equal("luna", choice.World.Folder);
        Assert.True(File.Exists(choice.World.MetaPath));
        Assert.Equal(0, rig.Draws);
    }

    [Fact]
    public void Given_TextForTheSeed_When_CreateIsChosen_Then_ItIsHashedWithTheFixedHash()
    {
        var rig = NewRig();
        var title = rig.Make();
        title.Start(TitlePage.New);
        Keys(title, GameKey.Down); // the seed field
        TypeText(title, "hola mundo");

        Walk(title, TitleSlot.Create);
        Keys(title, GameKey.Enter);

        Assert.Equal(SeedText.Hash("hola mundo"), title.Choice!.World!.Meta!.Seed);
    }

    [Fact]
    public void Given_ABlankSeed_When_CreateIsChosen_Then_TheRandomSeedIsUsed_AndTheRandomButtonFillsTheFieldWithOne()
    {
        var rig = NewRig();
        rig.Draw = 424242UL;
        var title = rig.Make();
        title.Start(TitlePage.New);

        Keys(title, GameKey.Down, GameKey.Down, GameKey.Enter); // Seed -> Random, pressed
        Assert.Equal("424242", title.Seed.Text);
        Assert.Equal(1, rig.Draws);

        title.Prefill(null, string.Empty);
        Walk(title, TitleSlot.Create);
        Keys(title, GameKey.Enter);

        Assert.Equal(424242UL, title.Choice!.World!.Meta!.Seed);
        Assert.Equal(2, rig.Draws);
    }

    [Fact]
    public void Given_TheNewPage_When_TabIsUsed_Then_TheFocusWalksTheFieldsAndButtonsAndWraps()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.New);
        var seen = new List<int> { title.Selected };

        for (var i = 0; i < TitleSlot.NewCount; i++)
        {
            Keys(title, GameKey.Tab);
            seen.Add(title.Selected);
        }

        Assert.Equal([0, 1, 2, 3, 4, 5, 0], seen);
        title.HandleInput(new TestInputFrame { Keys = new HashSet<GameKey> { GameKey.Tab, GameKey.LeftShift } });
        Assert.Equal(TitleSlot.NewBack, title.Selected); // Shift+Tab walks back
    }

    [Fact]
    public void Given_TheNewPageWithMouse_When_FieldsAndButtonsAreClicked_Then_TheyAct_AndHoverDoesNotMoveTheFocusOffAField()
    {
        var rig = NewRig();
        rig.Metrics.Slot = static (page, _, _, x, _) => page == TitlePage.New ? x : null; // the x of the click names the slot
        var title = rig.Make();
        title.Start(TitlePage.New);

        title.HandleInput(new TestInputFrame { PointerX = TitleSlot.Create }); // the pointer moves over Create
        Assert.Equal(TitleSlot.NameField, title.Selected); // the focus stays
        Assert.Equal(TitleSlot.Create, title.Hover);

        title.HandleInput(Click(TitleSlot.SeedField, 0));
        Assert.Equal(TitleSlot.SeedField, title.Selected);
        TypeText(title, "x");
        Assert.Equal("x", title.Seed.Text);

        title.HandleInput(new TestInputFrame { PointerX = TitleSlot.WorldType }); // release
        title.HandleInput(Click(TitleSlot.WorldType, 0));
        Assert.Equal(WorldKind.Valley, title.WorldType);

        title.HandleInput(new TestInputFrame { PointerX = TitleSlot.RandomSeed });
        title.HandleInput(Click(TitleSlot.RandomSeed, 0));
        Assert.Equal("99", title.Seed.Text);

        title.HandleInput(new TestInputFrame { PointerX = TitleSlot.Back });
        title.HandleInput(Click(TitleSlot.Back, 0));
        Assert.Equal(TitlePage.Main, title.Page);
    }

    private (Rig Rig, TitleScreen Title) WithWorlds(Func<TitlePage, int, int, int, int, int?>? slot = null)
    {
        var rig = NewRig();
        if (slot is not null)
        {
            rig.Metrics.Slot = slot;
        }

        _now = 100;
        _ = rig.Store.Create("Oldest", 1UL, WorldKind.Streamed);
        _now = 200;
        _ = rig.Store.Create("Middle", 2UL, WorldKind.Valley);
        _now = 300;
        _ = rig.Store.Create("Newest", 3UL, WorldKind.Streamed);
        return (rig, rig.Make());
    }

    [Fact]
    public void Given_Worlds_When_LoadGameIsOpened_Then_TheyAreListedNewestPlayedFirstAndTheFirstIsChosen()
    {
        var (_, title) = WithWorlds();
        title.Start(TitlePage.Main);
        Keys(title, GameKey.Down);
        Assert.Equal(TitleSlot.LoadGame, title.Selected);

        Keys(title, GameKey.Enter);

        Assert.Equal(TitlePage.Load, title.Page);
        Assert.Equal(["Newest", "Middle", "Oldest"], title.Worlds.Select(static w => w.Name));
        Assert.Equal(0, title.Selected);
        Keys(title, GameKey.Down, GameKey.Down, GameKey.Down); // stops at the last
        Assert.Equal(2, title.Selected);
        Keys(title, GameKey.Up);
        Assert.Equal(1, title.Selected);
    }

    [Fact]
    public void Given_ALongList_When_TheCursorWalksPastTheCapacity_Then_TheListScrollsToKeepItInView()
    {
        var rig = NewRig();
        for (var i = 1; i <= 7; i++)
        {
            _now = i;
            _ = rig.Store.Create("W" + i, (ulong)i, WorldKind.Streamed);
        }

        var title = rig.Make();
        title.Start(TitlePage.Load);
        for (var i = 0; i < 5; i++)
        {
            Keys(title, GameKey.Down);
        }

        Assert.Equal(5, title.Selected);
        Assert.Equal(3, title.LoadScroll); // 3 rows show: 3, 4, 5
        title.HandleInput(new TestInputFrame { Wheel = 120 * 9 });
        Assert.Equal(2, title.LoadScroll); // one row a frame's turn, up
        title.HandleInput(new TestInputFrame { Wheel = -120 * 9 });
        Assert.Equal(3, title.LoadScroll);
        Keys(title, GameKey.End);
        Assert.Equal(6, title.Selected);
        Assert.Equal(4, title.LoadScroll);
        Keys(title, GameKey.Home);
        Assert.Equal(0, title.Selected);
        Assert.Equal(0, title.LoadScroll);
    }

    [Fact]
    public void Given_AChosenWorld_When_LoadIsPressed_Then_TheChoiceIsThatWorld()
    {
        var (_, title) = WithWorlds();
        title.Start(TitlePage.Load);
        Keys(title, GameKey.Down); // Middle

        Keys(title, GameKey.Enter);

        Assert.Equal(TitleChoiceKind.LoadWorld, title.Choice!.Kind);
        Assert.Equal("Middle", title.Choice.World!.Name);
        Assert.Equal(2UL, title.Choice.World.Meta!.Seed);
        Assert.Equal(WorldKind.Valley, title.Choice.World.Meta.World);
    }

    [Fact]
    public void Given_AWorldWhoseSaveIsOfAnOlderVersion_When_LoadIsPressed_Then_ItIsNotLoadedAndTheNoticeSaysWhy()
    {
        var (rig, title) = WithWorlds();
        var newest = rig.Store.List()[0];
        File.WriteAllText(newest.SavePath, "{\"format\":\"vocaterra-save\",\"version\":3}");
        title.Start(TitlePage.Load);

        Keys(title, GameKey.Enter);

        Assert.Null(title.Choice);
        Assert.Equal("titleCannotLoad", title.Notice!.Key);
        Assert.Equal("Newest", title.Notice.Name);
        Assert.Contains("version 3", title.Notice.Detail, StringComparison.Ordinal);
        Assert.Equal(TitlePage.Load, title.Page);
    }

    [Fact]
    public void Given_ALoadFailureReportedByTheHost_When_TheTitleReopens_Then_TheNoticeIsOnThePage()
    {
        var (_, title) = WithWorlds();

        title.Start(TitlePage.Load, new TitleNotice("titleLoadFailed", "Newest", "the bundle changed"));

        Assert.Equal(TitlePage.Load, title.Page);
        Assert.Equal("the bundle changed", title.Notice!.Detail);
    }

    [Fact]
    public void Given_TheLoadPage_When_DeleteIsPressed_Then_AConfirmationAsksAndKeepIsTheDefault()
    {
        var (rig, title) = WithWorlds();
        title.Start(TitlePage.Load);
        Keys(title, GameKey.Down);

        Keys(title, GameKey.Delete);

        Assert.Equal(TitlePage.ConfirmDelete, title.Page);
        Assert.Equal("Middle", title.PendingDelete!.Name);
        Assert.Equal(TitleSlot.ConfirmKeep, title.Selected);

        Keys(title, GameKey.Enter); // Keep

        Assert.Equal(TitlePage.Load, title.Page);
        Assert.Equal(1, title.Selected); // back on the same world
        Assert.Equal(3, rig.Store.List().Count);
    }

    [Fact]
    public void Given_TheConfirmation_When_EscIsPressed_Then_NothingIsDeleted()
    {
        var (rig, title) = WithWorlds();
        title.Start(TitlePage.Load);
        Keys(title, GameKey.Delete);

        Keys(title, GameKey.Escape);

        Assert.Equal(TitlePage.Load, title.Page);
        Assert.Equal(3, rig.Store.List().Count);
        Assert.Null(title.PendingDelete);
    }

    [Fact]
    public void Given_TheConfirmation_When_DeleteIsChosen_Then_TheWorldIsGoneFromDiskAndFromTheList()
    {
        var (rig, title) = WithWorlds();
        title.Start(TitlePage.Load);
        Keys(title, GameKey.Down);
        var middle = title.Worlds[1];
        File.WriteAllText(middle.SavePath, "x");
        Keys(title, GameKey.Delete);

        Keys(title, GameKey.Left); // Keep -> Delete
        Assert.Equal(TitleSlot.ConfirmDelete, title.Selected);
        Keys(title, GameKey.Enter);

        Assert.Equal(TitlePage.Load, title.Page);
        Assert.False(Directory.Exists(middle.Directory));
        Assert.Equal(["Newest", "Oldest"], title.Worlds.Select(static w => w.Name));
        Assert.Equal(["Newest", "Oldest"], rig.Store.List().Select(static w => w.Name));
        Assert.Equal("titleDeleted", title.Notice!.Key);
        Assert.Equal("Middle", title.Notice.Name);
        Assert.Equal(1, title.Selected);
    }

    [Fact]
    public void Given_TheLastWorld_When_ItIsDeleted_Then_TheTitleGoesBackToTheMainPageWithLoadGreyed()
    {
        var rig = NewRig();
        _ = rig.Store.Create("Only", 1UL, WorldKind.Streamed);
        var title = rig.Make();
        title.Start(TitlePage.ConfirmDelete);
        Assert.Equal(TitlePage.ConfirmDelete, title.Page);

        Keys(title, GameKey.Left, GameKey.Enter);

        Assert.Equal(TitlePage.Main, title.Page);
        Assert.False(title.HasWorlds);
        Assert.Equal("titleDeleted", title.Notice!.Key);
    }

    [Fact]
    public void Given_AWorldThatCannotBeDeleted_When_DeleteIsChosen_Then_TheFailureIsShownAndTheWorldStays()
    {
        var (rig, title) = WithWorlds();
        title.Start(TitlePage.Load);
        Keys(title, GameKey.Delete);
        Directory.Delete(title.PendingDelete!.Directory, recursive: true); // gone from under the title: the store refuses the entry
        Keys(title, GameKey.Left, GameKey.Enter);

        Assert.Equal("titleDeleteFailed", title.Notice!.Key);
        Assert.Equal(2, rig.Store.List().Count);
    }

    [Fact]
    public void Given_ClicksOnTheLoadPage_When_RowsAndButtonsAreClicked_Then_ARowChoosesAndTheButtonsAct()
    {
        var (_, titleWithMouse) = WithWorlds(static (_, _, _, x, _) => x); // the x of the pointer names the slot
        titleWithMouse.Start(TitlePage.Load);

        titleWithMouse.HandleInput(Click(2, 0)); // the third world
        Assert.Equal(2, titleWithMouse.Selected);
        titleWithMouse.HandleInput(new TestInputFrame { PointerX = TitleSlot.DeleteAction });
        titleWithMouse.HandleInput(Click(TitleSlot.DeleteAction, 0));
        Assert.Equal(TitlePage.ConfirmDelete, titleWithMouse.Page);
        titleWithMouse.HandleInput(new TestInputFrame { PointerX = TitleSlot.ConfirmKeep });
        titleWithMouse.HandleInput(Click(TitleSlot.ConfirmKeep, 0));
        Assert.Equal(TitlePage.Load, titleWithMouse.Page);
        titleWithMouse.HandleInput(new TestInputFrame { PointerX = TitleSlot.LoadAction });
        titleWithMouse.HandleInput(Click(TitleSlot.LoadAction, 0));

        Assert.Equal("Oldest", titleWithMouse.Choice!.World!.Name);
    }

    [Fact]
    public void Given_TheMainPage_When_SettingsIsChosen_Then_TheSettingsPageIsTheEscMenusAndEscComesBackToTheMainPage()
    {
        var rig = NewRig();
        var title = rig.Make();
        title.Start(TitlePage.Main);
        Keys(title, GameKey.Down); // Settings (Load is greyed)

        Keys(title, GameKey.Enter);

        Assert.Equal(TitlePage.Settings, title.Page);
        Assert.True(title.SettingsMenu.IsOpen);
        Assert.Equal(MenuPage.Settings, title.SettingsMenu.Page);
        Assert.DoesNotContain(title.SettingsMenu.Rows, static row => row.Id == "heading.headingTesting"); // the testing section needs a world

        var before = rig.Settings.Get(SettingId.AmbientVolume);
        Keys(title, GameKey.Right); // the cursor starts on the first row that can change: Ambient volume
        Assert.NotEqual(before, rig.Settings.Get(SettingId.AmbientVolume));

        Keys(title, GameKey.Escape);

        Assert.Equal(TitlePage.Main, title.Page);
        Assert.Equal(TitleSlot.Settings, title.Selected);
        Assert.False(title.SettingsMenu.IsOpen);
    }

    [Fact]
    public void Given_AScriptedStart_When_EachPageIsAsked_Then_ItOpensThere()
    {
        var (_, title) = WithWorlds();

        title.Start(TitlePage.New);
        Assert.Equal(TitlePage.New, title.Page);
        title.Start(TitlePage.Load);
        Assert.Equal(TitlePage.Load, title.Page);
        title.Start(TitlePage.Settings);
        Assert.Equal(TitlePage.Settings, title.Page);
        title.Start(TitlePage.ConfirmDelete);
        Assert.Equal(TitlePage.ConfirmDelete, title.Page);
        Assert.Equal("Newest", title.PendingDelete!.Name);
        title.Start(TitlePage.Main);
        Assert.Equal(TitlePage.Main, title.Page);
    }

    [Fact]
    public void Given_NoWorlds_When_ALoadOrDeletePageIsAsked_Then_TheMainPageOpensInstead()
    {
        var title = NewRig().Make();

        title.Start(TitlePage.Load);
        Assert.Equal(TitlePage.Main, title.Page);
        title.Start(TitlePage.ConfirmDelete);
        Assert.Equal(TitlePage.Main, title.Page);
    }

    [Fact]
    public void Given_AChoiceIsMade_When_MoreInputComes_Then_ItIsIgnored()
    {
        var title = NewRig().Make();
        title.Start(TitlePage.Main);
        Keys(title, GameKey.Up, GameKey.Enter);
        var choice = title.Choice;

        Keys(title, GameKey.Up, GameKey.Enter);
        title.Type('x');

        Assert.Same(choice, title.Choice);
    }

    [Fact]
    public void TheTitleKeysAreAllInTheLocaleCheck()
    {
        Assert.Contains("titleName", TitleText.UiKeys);
        Assert.Contains("titleNameTaken", TitleText.UiKeys);
        Assert.Equal(TitleText.UiKeys.Count, TitleText.UiKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(TitleText.UiKeys, static key => Assert.False(TestGame.Locale.Ui(key, ("name", "n"), ("why", "w"), ("seed", "1"), ("type", "t"), ("played", "p")).StartsWith('⟦'), key));
        Assert.Equal("titleNameEmpty", TitleText.KeyOf(NameProblem.Empty));
        Assert.Equal(string.Empty, TitleText.KeyOf(NameProblem.None));
    }
}
