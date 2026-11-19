namespace Vocaterra.Game.Core.Input;

/// <summary>The parts of the controls list, in the order the controls screen shows them.</summary>
public enum ControlGroup
{
    /// <summary>The menu and the function keys.</summary>
    General,

    /// <summary>Moving and looking.</summary>
    Moving,

    /// <summary>Talking to people, and the panels the world opens.</summary>
    Doing,

    /// <summary>Inside a conversation.</summary>
    Conversation,

    /// <summary>Inside a lesson.</summary>
    Lessons,

    /// <summary>Inside the book.</summary>
    Book,

    /// <summary>At a station (the oven).</summary>
    Station,
}

/// <summary>A mouse control a row of the controls list names alongside, or instead of, its keys.</summary>
public enum MouseControl
{
    /// <summary>No mouse control.</summary>
    None,

    /// <summary>Moving the mouse.</summary>
    Move,

    /// <summary>The left button.</summary>
    LeftButton,

    /// <summary>The right button.</summary>
    RightButton,

    /// <summary>The wheel.</summary>
    Wheel,
}

/// <summary>One control: what it is called, where the controls screen files it, and the keys and mouse control that do it.</summary>
/// <param name="Id">The control's name (<c>walkForward</c>): its description is the locale's <c>ui.control.&lt;id&gt;</c>, and <c>{key:&lt;id&gt;}</c> spells its first key.</param>
/// <param name="Group">The part of the controls list it is filed under.</param>
/// <param name="Keys">The keys that do it, the main one first.</param>
/// <param name="Mouse">The mouse control that does it as well, or <see cref="MouseControl.None"/>.</param>
/// <param name="Range">Whether the keys are one run to show as "first to last" (<c>1 to 0</c>), not each by name.</param>
public sealed record ControlEntry(string Id, ControlGroup Group, IReadOnlyList<GameKey> Keys, MouseControl Mouse = MouseControl.None, bool Range = false)
{
    /// <summary>The main key, or <see cref="GameKey.None"/> for a control that is the mouse alone.</summary>
    public GameKey Key => Keys.Count > 0 ? Keys[0] : GameKey.None;
}

/// <summary>
/// Every control of the game in one table, and the one place its keys are set: the bindings (<see cref="KeyBindings"/>, <see cref="InputKeys"/>), the walking and the
/// hotbar, the dialogue panel's and the lesson strip's keys read their keys from here, and the controls screen of the menu is built from the same entries, each described
/// by the locale's <c>ui.control.&lt;id&gt;</c>. A control the game does by a key that is not in this table does not exist: a test drives the screens with the table's keys. The
/// book's and the station's own keys are listed here for the screen and written in those panels, where a test holds them to the table.
/// </summary>
public static class ControlTable
{
    private static readonly GameKey[] Digits = [GameKey.D1, GameKey.D2, GameKey.D3, GameKey.D4, GameKey.D5, GameKey.D6, GameKey.D7, GameKey.D8, GameKey.D9, GameKey.D0];

    /// <summary>Every control, in the order the controls screen lists them.</summary>
    public static IReadOnlyList<ControlEntry> All { get; } =
    [
        new("menu", ControlGroup.General, [GameKey.Escape]),
        new("save", ControlGroup.General, [GameKey.F6]),
        new("view", ControlGroup.General, [GameKey.F5]),
        new("viewEffects", ControlGroup.General, [GameKey.F7]),
        new("report", ControlGroup.General, [GameKey.F8]),
        new("textSize", ControlGroup.General, [GameKey.F9]),
        new("fullScreen", ControlGroup.General, [GameKey.F11]),
        new("debug", ControlGroup.General, [GameKey.F3]),
        new("endRun", ControlGroup.General, [GameKey.F10]),

        new("walkForward", ControlGroup.Moving, [GameKey.W, GameKey.Up]),
        new("walkBack", ControlGroup.Moving, [GameKey.S, GameKey.Down]),
        new("walkLeft", ControlGroup.Moving, [GameKey.A, GameKey.Left]),
        new("walkRight", ControlGroup.Moving, [GameKey.D, GameKey.Right]),
        new("jump", ControlGroup.Moving, [GameKey.Space]),
        new("sprint", ControlGroup.Moving, [GameKey.LeftShift]), // held: walk about 1.6 times as fast while stamina lasts (N02)
        new("flyDown", ControlGroup.Moving, [GameKey.LeftControl]), // held while the fly cheat is on: sink (Space rises)
        new("look", ControlGroup.Moving, [], MouseControl.Move),
        new("gather", ControlGroup.Moving, [], MouseControl.LeftButton),
        new("place", ControlGroup.Moving, [], MouseControl.RightButton),
        new("hotbar", ControlGroup.Moving, Digits, MouseControl.Wheel, Range: true),

        new("talk", ControlGroup.Doing, [GameKey.E]),
        new("type", ControlGroup.Doing, [GameKey.T]),
        new("listen", ControlGroup.Doing, [GameKey.V]),
        new("listenAlt", ControlGroup.Doing, [GameKey.Space]),
        new("inventory", ControlGroup.Doing, [GameKey.I]),
        new("crafting", ControlGroup.Doing, [GameKey.C]),
        new("book", ControlGroup.Doing, [GameKey.J]),
        new("nextTip", ControlGroup.Doing, [GameKey.Enter]),
        new("sleep", ControlGroup.Doing, [GameKey.Enter]), // while the session's closing card is up: sleep until the next session (V12c)

        new("repeat", ControlGroup.Conversation, [GameKey.R]),
        new("slower", ControlGroup.Conversation, [GameKey.S]),
        new("help", ControlGroup.Conversation, [GameKey.H]),
        new("dontKnow", ControlGroup.Conversation, [GameKey.N]),
        new("typeLine", ControlGroup.Conversation, [GameKey.W, GameKey.Tab]),
        new("choose", ControlGroup.Conversation, [GameKey.D1, GameKey.D2, GameKey.D3], Range: true),
        new("history", ControlGroup.Conversation, [GameKey.PageUp, GameKey.O]),

        new("lessonStart", ControlGroup.Lessons, [GameKey.L]),
        new("lessonContinue", ControlGroup.Lessons, [GameKey.Enter]),
        new("lessonSkip", ControlGroup.Lessons, [GameKey.K]),
        new("lessonMore", ControlGroup.Lessons, [GameKey.M]),
        new("lessonPick", ControlGroup.Lessons, [GameKey.D1, GameKey.D2, GameKey.D3, GameKey.D4], Range: true),

        new("bookTurn", ControlGroup.Book, [GameKey.Tab, GameKey.Left, GameKey.Right]),
        new("bookScroll", ControlGroup.Book, [GameKey.PageUp, GameKey.PageDown]),
        new("bookSelect", ControlGroup.Book, [GameKey.Up, GameKey.Down]),
        new("bookPlay", ControlGroup.Book, [GameKey.Enter]),
        new("bookPractise", ControlGroup.Book, [GameKey.P]),

        new("stationHold", ControlGroup.Station, [GameKey.D1, GameKey.D2, GameKey.D3, GameKey.D4, GameKey.D5, GameKey.D6, GameKey.D7, GameKey.D8, GameKey.D9], Range: true),
        new("stationEmpty", ControlGroup.Station, [GameKey.D0]),
        new("stationTarget", ControlGroup.Station, [GameKey.Tab]),
        new("stationUse", ControlGroup.Station, [GameKey.Enter]),
        new("stationPage", ControlGroup.Station, [GameKey.PageUp, GameKey.PageDown]),
        new("stationHelp", ControlGroup.Station, [GameKey.H]),
        new("stationRepeat", ControlGroup.Station, [GameKey.R]),
        new("stationSlower", ControlGroup.Station, [GameKey.S]),
        new("stationTalk", ControlGroup.Station, [GameKey.V, GameKey.Space]),
        new("stationType", ControlGroup.Station, [GameKey.W]),
        new("stationBake", ControlGroup.Station, [GameKey.B]),
    ];

    private static readonly Dictionary<string, ControlEntry> ById = All.ToDictionary(static entry => entry.Id, StringComparer.Ordinal);

    /// <summary>The control of this name.</summary>
    /// <param name="id">The control's name (<c>walkForward</c>).</param>
    /// <exception cref="KeyNotFoundException">No control has that name.</exception>
    public static ControlEntry Entry(string id) => ById[id];

    /// <summary>The keys of a control, the main one first.</summary>
    /// <param name="id">The control's name.</param>
    public static IReadOnlyList<GameKey> Keys(string id) => ById[id].Keys;

    /// <summary>One key of a control (the main one at <paramref name="index"/> 0).</summary>
    /// <param name="id">The control's name.</param>
    /// <param name="index">Which of its keys.</param>
    public static GameKey Key(string id, int index = 0) => ById[id].Keys[index];

    /// <summary>The numeric keypad's key for a top-row digit (<c>D2</c> gives <c>NumPad2</c>), or <see cref="GameKey.None"/> for a key that is no digit.</summary>
    /// <param name="digit">The key.</param>
    public static GameKey PadOf(GameKey digit) => digit is >= GameKey.D0 and <= GameKey.D9 ? GameKey.NumPad0 + (digit - GameKey.D0) : GameKey.None;

    /// <summary>Whether any of the control's keys went down this frame.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="id">The control's name.</param>
    public static bool Pressed(IInputFrame input, string id)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ById[id].Keys.Any(input.Pressed);
    }

    /// <summary>Whether any of the control's keys is held.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="id">The control's name.</param>
    public static bool Down(IInputFrame input, string id)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ById[id].Keys.Any(input.Down);
    }
}
