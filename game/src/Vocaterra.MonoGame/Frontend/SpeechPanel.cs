using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What a speech button of the dialogue panel does (S7). The panel's mouse hit-test, its keys and the logic that carries them out all use these.</summary>
public enum SpeechAction
{
    /// <summary><c>Repetir</c> (R): the NPC's last line again.</summary>
    Repeat,

    /// <summary><c>Más despacio</c> (S): the NPC's last line again, slowly.</summary>
    Slower,

    /// <summary><c>Ayuda</c> (H): asks the NPC for help (sends "ayuda").</summary>
    Help,

    /// <summary><c>No sé</c> (N): says "no sé" (a <c>dont_know</c> turn, never a failure).</summary>
    DontKnow,

    /// <summary><c>Escribir</c> (W or Tab): opens the text entry.</summary>
    Type,

    /// <summary>The first offered choice (1).</summary>
    Choice1,

    /// <summary>The second offered choice (2).</summary>
    Choice2,

    /// <summary>The third offered choice (3).</summary>
    Choice3,
}

/// <summary>The keys the dialogue panel's buttons answer to, without the engine's key type (the game maps its keys to these).</summary>
public enum PanelKey
{
    /// <summary>R.</summary>
    R,

    /// <summary>S.</summary>
    S,

    /// <summary>H.</summary>
    H,

    /// <summary>N.</summary>
    N,

    /// <summary>W.</summary>
    W,

    /// <summary>Tab.</summary>
    Tab,

    /// <summary>The key 1.</summary>
    D1,

    /// <summary>The key 2.</summary>
    D2,

    /// <summary>The key 3.</summary>
    D3,
}

/// <summary>One of the five fixed speech buttons: what it does, what it says and its key.</summary>
/// <param name="Action">What it does.</param>
/// <param name="Label">The Spanish label.</param>
/// <param name="Key">The key letter shown before the label.</param>
public sealed record SpeechButton(SpeechAction Action, string Label, string Key)
{
    /// <summary>The text drawn on the button, for example <c>[R] Repetir</c>.</summary>
    public string Text => $"[{Key}] {Label}";
}

/// <summary>The speech buttons of the dialogue panel, their keys and the voice event each press raises (S7, D-game-9: replay, slower audio, help, "no sé" and the typed fallback are always available).</summary>
public static class SpeechButtons
{
    /// <summary>The label of <see cref="SpeechAction.Repeat"/>.</summary>
    public const string RepeatLabel = "Repetir";

    /// <summary>The label of <see cref="SpeechAction.Slower"/>.</summary>
    public const string SlowerLabel = "Más despacio";

    /// <summary>The label of <see cref="SpeechAction.Help"/>.</summary>
    public const string HelpLabel = "Ayuda";

    /// <summary>The label of <see cref="SpeechAction.DontKnow"/>.</summary>
    public const string DontKnowLabel = "No sé";

    /// <summary>The label of <see cref="SpeechAction.Type"/>.</summary>
    public const string TypeLabel = "Escribir";

    /// <summary>The fixed buttons, left to right.</summary>
    public static IReadOnlyList<SpeechButton> All { get; } =
    [
        new(SpeechAction.Repeat, RepeatLabel, "R"),
        new(SpeechAction.Slower, SlowerLabel, "S"),
        new(SpeechAction.Help, HelpLabel, "H"),
        new(SpeechAction.DontKnow, DontKnowLabel, "N"),
        new(SpeechAction.Type, TypeLabel, "W"),
    ];

    /// <summary>The English-text key of a fixed button (<see cref="SpeechButtonKeys"/>), or <see langword="null"/> for a choice.</summary>
    /// <param name="action">The button.</param>
    public static string? KeyOf(SpeechAction action) => action switch
    {
        SpeechAction.Repeat => SpeechButtonKeys.Repeat,
        SpeechAction.Slower => SpeechButtonKeys.Slower,
        SpeechAction.Help => SpeechButtonKeys.Help,
        SpeechAction.DontKnow => SpeechButtonKeys.DontKnow,
        SpeechAction.Type => SpeechButtonKeys.Type,
        _ => null,
    };

    /// <summary>
    /// The text drawn on a fixed button (S17, D-game-13): <c>[R] Repetir (repeat)</c> while the learner is early, <c>[R] Repetir</c> once the button (or its phrase) has
    /// been used <see cref="GuideSession.ButtonEnglishUses"/> times. The English is help on screen only; the key and the button work the same without it (rule 8).
    /// </summary>
    /// <param name="button">The button.</param>
    /// <param name="session">The game whose counts say whether the English is still up, or <see langword="null"/> for the Spanish alone.</param>
    public static string TextFor(SpeechButton button, GuideSession? session)
    {
        ArgumentNullException.ThrowIfNull(button);
        return session is not null && KeyOf(button.Action) is { } key && session.ButtonEnglishUp(key)
            ? $"{button.Text} ({session.English.ButtonEnglish(key)})"
            : button.Text;
    }

    /// <summary>The action a key presses while the panel shows its buttons (not while the text entry has the keys), or <see langword="null"/>.</summary>
    /// <param name="key">The key.</param>
    public static SpeechAction? ForKey(PanelKey key) => key switch
    {
        PanelKey.R => SpeechAction.Repeat,
        PanelKey.S => SpeechAction.Slower,
        PanelKey.H => SpeechAction.Help,
        PanelKey.N => SpeechAction.DontKnow,
        PanelKey.W or PanelKey.Tab => SpeechAction.Type,
        PanelKey.D1 => SpeechAction.Choice1,
        PanelKey.D2 => SpeechAction.Choice2,
        PanelKey.D3 => SpeechAction.Choice3,
        _ => null,
    };

    /// <summary>The voice event a press of the button raises (the choice buttons raise <see cref="VoiceEventKind.Choice"/> when their turn is submitted).</summary>
    /// <param name="action">The button.</param>
    public static VoiceEventKind EventOf(SpeechAction action) => action switch
    {
        SpeechAction.Repeat => VoiceEventKind.Replay,
        SpeechAction.Slower => VoiceEventKind.Slower,
        SpeechAction.Help => VoiceEventKind.Hint,
        SpeechAction.DontKnow => VoiceEventKind.NoSe,
        SpeechAction.Type => VoiceEventKind.TypeFallback,
        _ => VoiceEventKind.Choice,
    };

    /// <summary>The action of the choice at <paramref name="index"/> (0 to 2), or <see langword="null"/>.</summary>
    /// <param name="index">The position among the offered choices.</param>
    public static SpeechAction? ChoiceAt(int index) => index switch
    {
        0 => SpeechAction.Choice1,
        1 => SpeechAction.Choice2,
        2 => SpeechAction.Choice3,
        _ => null,
    };

    /// <summary>The position among the choices that a choice action stands for, or <see langword="null"/> for the fixed buttons.</summary>
    /// <param name="action">The action.</param>
    public static int? ChoiceIndex(SpeechAction action) => action switch
    {
        SpeechAction.Choice1 => 0,
        SpeechAction.Choice2 => 1,
        SpeechAction.Choice3 => 2,
        _ => null,
    };
}

/// <summary>A rectangle in window pixels.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct PixelBox(int X, int Y, int Width, int Height)
{
    /// <summary>The right edge (exclusive).</summary>
    public int Right => X + Width;

    /// <summary>The bottom edge (exclusive).</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether the point is inside the box.</summary>
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>
/// Where the pieces of the dialogue panel are for a window, in pixels, so that the drawing and the mouse hit-test use the same numbers and a test
/// can click a button without a window (as <see cref="CraftingLayout"/> does for the crafting panel). From the bottom up: the key hints, the text
/// box (while typing), the row of the five speech buttons, the row of the offered choices (when there are any), the persistent heard-versus-expected
/// line, and above them the conversation. The word card and the other layers use <see cref="Panel"/> to stay clear of the panel.
/// </summary>
public readonly record struct DialogueLayout
{
    /// <summary>The widest panel.</summary>
    public const int MaxWidth = 980;

    /// <summary>The tallest panel.</summary>
    public const int PanelHeight = 300;

    /// <summary>The gap between the panel and the window's edge.</summary>
    public const int Margin = 16;

    /// <summary>The padding inside the panel.</summary>
    public const int Padding = 12;

    /// <summary>The height of a line of text.</summary>
    public const int LineHeight = 20;

    /// <summary>The height of a button.</summary>
    public const int ButtonHeight = 28;

    /// <summary>The gap between buttons, in a row and between rows.</summary>
    public const int Gap = 8;

    /// <summary>The horizontal padding inside a button around its text.</summary>
    public const int ButtonPadding = 10;

    /// <summary>The height of the text box.</summary>
    public const int InputHeight = 28;

    /// <summary>The panel's box for a window: centred, along the bottom.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public static PixelBox PanelBox(int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(MaxWidth, Math.Max(viewportWidth - (Margin * 2), 200));
        var height = Math.Min(PanelHeight, Math.Max(viewportHeight - (Margin * 2), 120));
        return new PixelBox((viewportWidth - width) / 2, viewportHeight - height - Margin, width, height);
    }

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="measure">The width of a text in pixels, in the font the panel draws with.</param>
    /// <param name="choiceLabels">The labels of the offered choices (at most three are laid out).</param>
    /// <param name="typing">Whether the text box is shown.</param>
    /// <param name="buttonText">The text drawn on each fixed button (S17: with its English while the learner is early); <see langword="null"/> for the Spanish label alone.</param>
    public DialogueLayout(int viewportWidth, int viewportHeight, Func<string, float> measure, IReadOnlyList<string> choiceLabels, bool typing, Func<SpeechButton, string>? buttonText = null)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(choiceLabels);
        Panel = PanelBox(viewportWidth, viewportHeight);
        var left = Panel.X + Padding;
        InnerWidth = Panel.Width - (Padding * 2);
        HintsY = Panel.Bottom - Padding - LineHeight + 2;
        var floor = HintsY - 6;
        if (typing)
        {
            InputBox = new PixelBox(left, floor - InputHeight, InnerWidth, InputHeight);
            floor = InputBox.Value.Y - 6;
        }

        // The five buttons in a row; with their English (S17) they may not all fit, and the rest go on a second row above, so a button is never off the panel.
        var widths = SpeechButtons.All.Select(button => (int)Math.Ceiling(measure(buttonText?.Invoke(button) ?? button.Text)) + (ButtonPadding * 2)).ToList();
        var rowCount = 1;
        var x = left;
        foreach (var width in widths)
        {
            if (x > left && x + width > left + InnerWidth)
            {
                rowCount++;
                x = left;
            }

            x += width + Gap;
        }

        var buttonsY = floor - ButtonHeight - ((rowCount - 1) * (ButtonHeight + Gap));
        var buttons = new List<(SpeechAction Action, PixelBox Box)>();
        x = left;
        var rowY = buttonsY;
        for (var i = 0; i < widths.Count; i++)
        {
            if (x > left && x + widths[i] > left + InnerWidth)
            {
                rowY += ButtonHeight + Gap;
                x = left;
            }

            buttons.Add((SpeechButtons.All[i].Action, new PixelBox(x, rowY, widths[i], ButtonHeight)));
            x += widths[i] + Gap;
        }

        Buttons = buttons;
        var top = buttonsY;
        var choices = new List<(SpeechAction Action, PixelBox Box)>();
        if (choiceLabels.Count > 0)
        {
            var choicesY = buttonsY - Gap - ButtonHeight;
            x = left;
            for (var i = 0; i < Math.Min(choiceLabels.Count, 3); i++)
            {
                var text = ChoiceText(i, choiceLabels[i]);
                var width = Math.Min((int)Math.Ceiling(measure(text)) + (ButtonPadding * 2), InnerWidth);
                choices.Add((SpeechButtons.ChoiceAt(i)!.Value, new PixelBox(x, choicesY, width, ButtonHeight)));
                x += width + Gap;
            }

            top = choicesY;
        }

        Choices = choices;
        HeardY = top - 4 - LineHeight;
        LinesTop = Panel.Y + 8 + LineHeight + 6;
        LinesBottom = HeardY - 4;
    }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The width inside the padding.</summary>
    public int InnerWidth { get; }

    /// <summary>The top of the conversation's lines.</summary>
    public int LinesTop { get; }

    /// <summary>The bottom of the conversation's lines.</summary>
    public int LinesBottom { get; }

    /// <summary>The top of the persistent heard-versus-expected line.</summary>
    public int HeardY { get; }

    /// <summary>The top of the key hints line.</summary>
    public int HintsY { get; }

    /// <summary>The text box, or <see langword="null"/> when the panel is not in typing mode.</summary>
    public PixelBox? InputBox { get; }

    /// <summary>The five fixed buttons with their boxes.</summary>
    public IReadOnlyList<(SpeechAction Action, PixelBox Box)> Buttons { get; }

    /// <summary>The offered choices with their boxes (empty when none were offered).</summary>
    public IReadOnlyList<(SpeechAction Action, PixelBox Box)> Choices { get; }

    /// <summary>The text drawn on a choice button: its key and its label, for example <c>[1] Quiero agua, por favor.</c>.</summary>
    /// <param name="index">The position among the choices (0 to 2).</param>
    /// <param name="label">The choice's label.</param>
    public static string ChoiceText(int index, string label) => $"[{index + 1}] {label}";

    /// <summary>The button or choice under a point, or <see langword="null"/>.</summary>
    /// <param name="x">The pointer's x in window pixels.</param>
    /// <param name="y">The pointer's y.</param>
    public SpeechAction? ActionAt(int x, int y)
    {
        foreach (var (action, box) in Choices)
        {
            if (box.Contains(x, y))
            {
                return action;
            }
        }

        foreach (var (action, box) in Buttons)
        {
            if (box.Contains(x, y))
            {
                return action;
            }
        }

        return null;
    }
}
