using Vocaterra.Game.Reports;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The keys the report panel takes (S21), named by what they do; the game maps the keyboard to them.</summary>
public enum ReportKey
{
    /// <summary>Cancel: the report is dropped and nothing is kept.</summary>
    Escape,

    /// <summary>Switch between Bug and Feature request.</summary>
    Tab,

    /// <summary>A new line in the text.</summary>
    Enter,

    /// <summary>Save the report (Ctrl+Enter).</summary>
    Save,

    /// <summary>Remove the character before the caret.</summary>
    Backspace,

    /// <summary>Remove the character after the caret.</summary>
    Delete,

    /// <summary>Caret left.</summary>
    Left,

    /// <summary>Caret right.</summary>
    Right,

    /// <summary>Caret up a line.</summary>
    Up,

    /// <summary>Caret down a line.</summary>
    Down,

    /// <summary>Caret to the start of the line.</summary>
    Home,

    /// <summary>Caret to the end of the line.</summary>
    End,
}

/// <summary>The clickable parts of the report panel (S21).</summary>
public enum ReportButton
{
    /// <summary>The Bug choice.</summary>
    Bug,

    /// <summary>The Feature request choice.</summary>
    Feature,

    /// <summary>The Save report button.</summary>
    Save,

    /// <summary>The Cancel button (the same as Esc).</summary>
    Cancel,
}

/// <summary>What a key or a click asks of the game (S21): nothing, save the report, or drop it.</summary>
public enum ReportOutcome
{
    /// <summary>The panel only changed its own state.</summary>
    None,

    /// <summary>The report is to be saved. The game writes it, then calls <see cref="ReportPanel.Hide"/>.</summary>
    Save,

    /// <summary>The report is dropped. The game calls <see cref="ReportPanel.Hide"/> and keeps nothing.</summary>
    Cancel,
}

/// <summary>
/// The report panel's state (S21, D-game-17) as plain logic: whether it is open (or about to be, after F8), the kind, the text, and what the keys
/// and the clicks do. The game takes the screenshot, writes the files and draws the panel; this class decides, and it is tested without a window.
/// </summary>
/// <remarks>
/// <b>The pause.</b> <see cref="Paused"/> is true from F8 until the panel closes: the game steps no tick, applies no turn and keeps the microphone
/// off while it is. <see cref="Gate"/> applies that to a tick count.
/// </remarks>
public sealed class ReportPanel
{
    /// <summary>The line a save with no text says (the panel stays open).</summary>
    public const string BlankNotice = "Write what you saw or wanted first, then save.";

    /// <summary>The kind a report starts with.</summary>
    public const ReportKind DefaultKind = ReportKind.Bug;

    /// <summary>The panel is up (the text box takes the keys).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>F8 was pressed and the frame that will be the screenshot has not been drawn yet.</summary>
    public bool Requested { get; private set; }

    /// <summary>Whether the game is paused for the panel: from the request until the panel closes.</summary>
    public bool Paused => IsOpen || Requested;

    /// <summary>The kind the report is about.</summary>
    public ReportKind Kind { get; private set; } = DefaultKind;

    /// <summary>What was open when F8 was pressed (the report's <c>Open panel</c> row).</summary>
    public string OpenPanel { get; private set; } = "none (the world)";

    /// <summary>The text box.</summary>
    public ReportEntry Entry { get; } = new();

    /// <summary>A line the panel shows under the text (a refused save), or <see langword="null"/>.</summary>
    public string? Notice { get; private set; }

    /// <summary>The player's text, trimmed (what the report holds).</summary>
    public string Text => Entry.Text.Trim();

    /// <summary>F8 was pressed: the game pauses, and draws the screenshot of the next frame before the panel.</summary>
    /// <param name="openPanel">What is open now, for the report's state table.</param>
    public void Request(string openPanel)
    {
        ArgumentNullException.ThrowIfNull(openPanel);
        if (Paused)
        {
            return;
        }

        Requested = true;
        OpenPanel = openPanel;
    }

    /// <summary>The frame before the panel is drawn: the panel opens with the kind given (the game's <c>--report-kind</c>, or Bug).</summary>
    /// <param name="kind">The kind the report starts with.</param>
    public void Show(ReportKind kind)
    {
        Requested = false;
        IsOpen = true;
        Kind = kind;
        Entry.Clear();
        Notice = null;
    }

    /// <summary>Closes the panel and forgets the text: nothing of the report is kept.</summary>
    public void Hide()
    {
        IsOpen = false;
        Requested = false;
        Entry.Clear();
        Notice = null;
    }

    /// <summary>Gives the tick count the game may step: none while the panel is paused.</summary>
    /// <param name="steps">The ticks the frame clock asks for.</param>
    public int Gate(int steps) => Paused ? 0 : steps;

    /// <summary>A key of the panel.</summary>
    /// <param name="key">The key.</param>
    public ReportOutcome Press(ReportKey key)
    {
        if (!IsOpen)
        {
            return ReportOutcome.None;
        }

        switch (key)
        {
            case ReportKey.Escape:
                return ReportOutcome.Cancel;
            case ReportKey.Tab:
                Kind = Kind == ReportKind.Bug ? ReportKind.Feature : ReportKind.Bug;
                return ReportOutcome.None;
            case ReportKey.Save:
                return Save();
            case ReportKey.Enter:
                Edit(() => Entry.Insert('\n'));
                return ReportOutcome.None;
            case ReportKey.Backspace:
                Edit(() => Entry.Backspace());
                return ReportOutcome.None;
            case ReportKey.Delete:
                Edit(() => Entry.Delete());
                return ReportOutcome.None;
            case ReportKey.Left:
                Entry.MoveLeft();
                return ReportOutcome.None;
            case ReportKey.Right:
                Entry.MoveRight();
                return ReportOutcome.None;
            case ReportKey.Up:
                Entry.MoveUp();
                return ReportOutcome.None;
            case ReportKey.Down:
                Entry.MoveDown();
                return ReportOutcome.None;
            case ReportKey.Home:
                Entry.Home();
                return ReportOutcome.None;
            case ReportKey.End:
                Entry.End();
                return ReportOutcome.None;
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown report key.");
        }
    }

    /// <summary>A click on a button of the panel.</summary>
    /// <param name="button">The button.</param>
    public ReportOutcome Click(ReportButton button)
    {
        if (!IsOpen)
        {
            return ReportOutcome.None;
        }

        switch (button)
        {
            case ReportButton.Bug:
                Kind = ReportKind.Bug;
                return ReportOutcome.None;
            case ReportButton.Feature:
                Kind = ReportKind.Feature;
                return ReportOutcome.None;
            case ReportButton.Save:
                return Save();
            default:
                return ReportOutcome.Cancel;
        }
    }

    /// <summary>
    /// A typed character (the window's text event). With Alt held, a letter that has an accent gives the accented letter (<see cref="AccentShortcuts"/>).
    /// </summary>
    /// <param name="c">The character the platform typed.</param>
    /// <param name="alt">Whether Alt is held.</param>
    public void Type(char c, bool alt)
    {
        if (IsOpen)
        {
            Edit(() => Entry.Insert(AccentShortcuts.ForTyped(c, alt)));
        }
    }

    /// <summary>Types a line (a scripted report's text). Each character goes in as if typed.</summary>
    /// <param name="text">The text.</param>
    public void TypeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsOpen)
        {
            Edit(() => Entry.Insert(text));
        }
    }

    private ReportOutcome Save()
    {
        if (Entry.IsBlank)
        {
            Notice = BlankNotice;
            return ReportOutcome.None;
        }

        return ReportOutcome.Save;
    }

    // An edit clears the notice: the player is writing again.
    private void Edit(Action edit)
    {
        Notice = null;
        edit();
    }
}
