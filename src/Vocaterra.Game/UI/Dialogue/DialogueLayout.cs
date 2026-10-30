using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Crafting;

namespace Vocaterra.Game.UI.Dialogue;

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

    /// <summary>The canvas height under which the panel is compact (X08): a smaller margin, a panel about half the canvas tall, and the key hints line given up when the conversation needs the room.</summary>
    public const int CompactBelow = 540;

    /// <summary>The gap between the panel and the canvas's edge when the panel is compact.</summary>
    public const int CompactMargin = 8;

    /// <summary>The shortest a compact panel is.</summary>
    public const int CompactMinHeight = 190;

    /// <summary>Whether a canvas of this height gets the compact panel.</summary>
    /// <param name="viewportHeight">The canvas's height.</param>
    public static bool IsCompact(int viewportHeight) => viewportHeight < CompactBelow;

    /// <summary>
    /// The panel's box for a canvas: centred, along the bottom. Up to a canvas 540 tall it is <see cref="PanelHeight"/> and a 16 px margin (the sizes of 100 percent at
    /// 1280 x 720 and 800 x 600); on a shorter canvas (150 percent at 800 x 600 is 533 x 400) it is a little over half the canvas, so the lesson strip and the cards have
    /// the rest.
    /// </summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    public static PixelBox PanelBox(int viewportWidth, int viewportHeight)
    {
        var compact = IsCompact(viewportHeight);
        var margin = compact ? CompactMargin : Margin;
        var width = Math.Min(MaxWidth, Math.Max(viewportWidth - (margin * 2), 200));
        var height = compact
            ? Math.Min(PanelHeight, Math.Max((int)Math.Round(viewportHeight * 0.5), CompactMinHeight))
            : Math.Min(PanelHeight, Math.Max(viewportHeight - (margin * 2), 120));
        height = Math.Min(height, Math.Max(120, viewportHeight - (margin * 2)));
        return new PixelBox((viewportWidth - width) / 2, viewportHeight - height - margin, width, height);
    }

    /// <summary>Computes the layout.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="measure">The width of a text in pixels, in the font the panel draws with.</param>
    /// <param name="choiceLabels">The labels of the offered choices (at most three are laid out).</param>
    /// <param name="buttons">The fixed speech buttons with their labels (<see cref="DialogueModel.Buttons"/>, from <c>game/words.json</c>).</param>
    /// <param name="typing">Whether the text box is shown.</param>
    /// <param name="buttonText">The text drawn on each fixed button (S17: with its English while the learner is early); <see langword="null"/> for the Spanish label alone.</param>
    /// <param name="hints">The key hints line (it wraps to at most two rows, and a compact panel with no room gives it up); <see langword="null"/> for one row.</param>
    public DialogueLayout(int viewportWidth, int viewportHeight, Func<string, float> measure, IReadOnlyList<string> choiceLabels, IReadOnlyList<SpeechButton> buttons, bool typing, Func<SpeechButton, string>? buttonText = null, string? hints = null)
    {
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(choiceLabels);
        ArgumentNullException.ThrowIfNull(buttons);
        Panel = PanelBox(viewportWidth, viewportHeight);
        var compact = IsCompact(viewportHeight);
        var left = Panel.X + Padding;
        InnerWidth = Panel.Width - (Padding * 2);
        Buttons = [];
        Choices = [];
        HintRows = [];
        var rowGap = compact ? 4 : Gap; // the gap between rows (the gap between buttons of one row stays Gap)
        // Three passes: with the key hints line and the buttons' English; on a compact panel that has no room left for the conversation, without the hints line; and, still short of
        // room, with the buttons in Spanish alone (their keys are on them, and the lesson strip has the English).
        for (var pass = 0; pass < 3; pass++)
        {
            var showHints = pass == 0;
            ButtonsGlossed = pass < 2 && buttonText is not null;
            var widths = buttons.Select(button => (int)Math.Ceiling(measure(pass < 2 ? buttonText?.Invoke(button) ?? button.Text : button.Text)) + (ButtonPadding * 2)).ToList();
            HasHints = showHints;
            var hintRows = hints is null ? [string.Empty] : HintRowsFor(hints, InnerWidth, measure);
            HintRows = showHints ? hintRows : [];
            HintsY = Panel.Bottom - Padding - (hintRows.Count * LineHeight) + 2;
            var floor = showHints ? HintsY - 6 : Panel.Bottom - Padding;
            if (typing)
            {
                InputBox = new PixelBox(left, floor - InputHeight, InnerWidth, InputHeight);
                floor = InputBox.Value.Y - 6;
            }

            // The five buttons in a row; with their English (S17) they may not all fit, and the rest go on a second row above, so a button is never off the panel.
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

            var buttonsY = floor - ButtonHeight - ((rowCount - 1) * (ButtonHeight + rowGap));
            var placed = new List<(SpeechAction Action, PixelBox Box)>();
            x = left;
            var rowY = buttonsY;
            for (var i = 0; i < widths.Count; i++)
            {
                if (x > left && x + widths[i] > left + InnerWidth)
                {
                    rowY += ButtonHeight + rowGap;
                    x = left;
                }

                placed.Add((buttons[i].Action, new PixelBox(x, rowY, Math.Min(widths[i], InnerWidth), ButtonHeight)));
                x += widths[i] + Gap;
            }

            Buttons = placed;
            var top = buttonsY;
            var choices = new List<(SpeechAction Action, PixelBox Box)>();
            if (choiceLabels.Count > 0)
            {
                var choicesY = buttonsY - rowGap - ButtonHeight;
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
            LinesBottom = Math.Max(LinesTop, HeardY - 4);

            // A compact panel that is left with less than one line of conversation gives the key hints up (the buttons carry the keys), and the log
            // collapses to nothing when even that is not enough: the bubble and the lesson strip show the line.
            if (!compact || LinesBottom - LinesTop >= LineHeight)
            {
                break;
            }
        }
    }

    // The hints wrapped to at most two rows, the second ending in an ellipsis when text is left out.
    private static List<string> HintRowsFor(string hints, int width, Func<string, float> measure)
    {
        var rows = TextWrap.Wrap(hints, width, measure);
        if (rows.Count <= 2)
        {
            return rows.ToList();
        }

        return [rows[0], rows[1].TrimEnd() + "…"];
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

    /// <summary>The rows of the key hints (empty when they are not shown).</summary>
    public IReadOnlyList<string> HintRows { get; }

    /// <summary>Whether the fixed buttons carry their English (S17); a compact panel short of room shows them in Spanish alone.</summary>
    public bool ButtonsGlossed { get; }

    /// <summary>Whether the key hints line is shown (a compact panel without the room leaves it out).</summary>
    public bool HasHints { get; }

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
