using Vocaterra.Game.Core.Worlds;

namespace Vocaterra.Game.Core.Title;

/// <summary>The pages of the title screen (Q01).</summary>
public enum TitlePage
{
    /// <summary>The game's name and New game, Load game, Settings and Quit.</summary>
    Main,

    /// <summary>The new world's name, seed and type, with Create.</summary>
    New,

    /// <summary>The worlds on disk, with Load and Delete.</summary>
    Load,

    /// <summary>The settings page (the Esc menu's, without its testing section).</summary>
    Settings,

    /// <summary>The question before a world is deleted.</summary>
    ConfirmDelete,
}

/// <summary>
/// What a point of the title screen's pages is, in numbers the page and its layout agree on. Slots are per page; the negative ones are the same on every page that has them. The New page's slots
/// are in the order the keyboard walks them: Q03 adds the terrain options' rows between <see cref="WorldType"/> and <see cref="Create"/>.
/// </summary>
public static class TitleSlot
{
    /// <summary>The Back button (any page that has one).</summary>
    public const int Back = -1;

    /// <summary>The Load page's Load button.</summary>
    public const int LoadAction = -2;

    /// <summary>The Load page's Delete button.</summary>
    public const int DeleteAction = -3;

    /// <summary>The main page's New game.</summary>
    public const int NewGame = 0;

    /// <summary>The main page's Load game.</summary>
    public const int LoadGame = 1;

    /// <summary>The main page's Settings.</summary>
    public const int Settings = 2;

    /// <summary>The main page's Quit.</summary>
    public const int Quit = 3;

    /// <summary>How many buttons the main page has.</summary>
    public const int MainCount = 4;

    /// <summary>The New page's world name field.</summary>
    public const int NameField = 0;

    /// <summary>The New page's seed field.</summary>
    public const int SeedField = 1;

    /// <summary>The New page's Random button (beside the seed).</summary>
    public const int RandomSeed = 2;

    /// <summary>The New page's world type.</summary>
    public const int WorldType = 3;

    /// <summary>The New page's Create button.</summary>
    public const int Create = 4;

    /// <summary>The New page's Back button as the keyboard reaches it.</summary>
    public const int NewBack = 5;

    /// <summary>How many things the keyboard walks on the New page.</summary>
    public const int NewCount = 6;

    /// <summary>The confirmation's Delete button.</summary>
    public const int ConfirmDelete = 0;

    /// <summary>The confirmation's Keep button.</summary>
    public const int ConfirmKeep = 1;
}

/// <summary>What the title screen asks of how its pages are drawn: how many worlds the Load page shows, and what is under a point.</summary>
public interface ITitleMetrics
{
    /// <summary>How many worlds the Load page shows at once.</summary>
    int LoadCapacity { get; }

    /// <summary>What is under a point of the canvas on this page (a <see cref="TitleSlot"/>, or a world's index on the Load page), or <see langword="null"/>.</summary>
    /// <param name="page">The page shown.</param>
    /// <param name="loadScroll">The first world shown on the Load page.</param>
    /// <param name="worldCount">How many worlds there are.</param>
    /// <param name="x">The canvas x.</param>
    /// <param name="y">The canvas y.</param>
    int? SlotAt(TitlePage page, int loadScroll, int worldCount, int x, int y);
}

/// <summary>What the player chose on the title screen.</summary>
public enum TitleChoiceKind
{
    /// <summary>Close the game.</summary>
    Quit,

    /// <summary>Start a world that has just been made.</summary>
    NewWorld,

    /// <summary>Load a world.</summary>
    LoadWorld,
}

/// <summary>The title screen's choice: Quit, or a world to start (new or loaded).</summary>
/// <param name="Kind">What was chosen.</param>
/// <param name="World">The world, for <see cref="TitleChoiceKind.NewWorld"/> and <see cref="TitleChoiceKind.LoadWorld"/>.</param>
public sealed record TitleChoice(TitleChoiceKind Kind, WorldEntry? World = null);

/// <summary>A line the title screen shows under a page: a locale key (<c>ui.&lt;key&gt;</c>) and the values its text names.</summary>
/// <param name="Key">The locale key without its <c>ui.</c>.</param>
/// <param name="Name">The world's name the text names (the <c>{name}</c> value), or empty.</param>
/// <param name="Detail">The detail the text names (the <c>{why}</c> value, an error's words), or empty.</param>
public sealed record TitleNotice(string Key, string Name = "", string Detail = "");

/// <summary>The title's text keys that name a name problem, so the page can say why a name is refused.</summary>
public static class TitleText
{
    /// <summary>The locale key (without <c>ui.</c>) that says why a name is refused.</summary>
    /// <param name="problem">The problem.</param>
    public static string KeyOf(NameProblem problem) => problem switch
    {
        NameProblem.Empty => "titleNameEmpty",
        NameProblem.TooLong => "titleNameLong",
        NameProblem.Taken => "titleNameTaken",
        _ => string.Empty,
    };

    /// <summary>Every <c>ui.</c> key of the title screen, so the locale check holds a locale to each.</summary>
    public static IReadOnlyList<string> UiKeys { get; } =
    [
        "titleName", "titleMaking", "titleDefaultName", "titleNew", "titleLoad", "titleSettings", "titleQuit", "titleHint", "titleNewHint", "titleLoadHint", "titleConfirmHint",
        "titleNewHeading", "titleLoadHeading", "titleDeleteHeading", "titleNameLabel", "titleSeedLabel", "titleSeedRandom", "titleSeedBlank", "titleTypeLabel",
        "titleTypeStreamed", "titleTypeValley", "titleCreate", "titleLoadButton", "titleDeleteButton", "titleKeepButton", "titleConfirmDelete", "titleNoWorlds", "titleNoSave",
        "titleNameEmpty", "titleNameLong", "titleNameTaken", "titleCannotLoad", "titleLoadFailed", "titleDeleted", "titleDeleteFailed", "titleCreateFailed", "titleWorldLine",
    ];
}
