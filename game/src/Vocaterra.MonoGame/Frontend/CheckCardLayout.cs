namespace Vocaterra.MonoGame.Frontend;

/// <summary>One option of a comprehension item as laid out: its box and the rows of its label, in the font the layout chose.</summary>
/// <param name="Box">The option's box.</param>
/// <param name="Rows">The label's rows (the number is not included; the layer draws it before the first row).</param>
/// <param name="TextX">The x of the text (after the icon, if there is one).</param>
/// <param name="Large">Whether the label is in the large font (it fits on one line); otherwise the small font, wrapped.</param>
public readonly record struct CheckOptionLayout(PixelBox Box, IReadOnlyList<string> Rows, int TextX, bool Large);

/// <summary>
/// The layout of the check battery's card (S22). The question or the situation, the hints and the option labels were drawn on one line each at fixed rows, so a long
/// one ran off the card (<c>Say no politely. …</c> past the edge at 1280 wide). Every text now wraps to the card's inner width, and what stands below it moves down by
/// the rows above. Plain numbers and a measuring function, so a test checks every item of <c>checks.json</c> against the card with no window.
/// </summary>
public sealed class CheckCardLayout
{
    /// <summary>The card's side padding.</summary>
    public const int Side = 40;

    /// <summary>The height of a row of the large font.</summary>
    public const int LargeRow = 38;

    /// <summary>The height of a row of the small font.</summary>
    public const int SmallRow = 18;

    /// <summary>The height of an option's box.</summary>
    public const int OptionHeight = 56;

    /// <summary>The gap between options.</summary>
    public const int OptionGap = 10;

    /// <summary>The height of the text box of a production item.</summary>
    public const int InputHeight = 44;

    /// <summary>The space kept at the card's bottom for the notice and the footer.</summary>
    public const int FooterSpace = 90;

    /// <summary>The widest a card is.</summary>
    public const int MaxWidth = 820;

    /// <summary>The tallest a card is.</summary>
    public const int MaxHeight = 520;

    /// <summary>The icon's width plus its gap, before an option's text.</summary>
    public const int IconSpace = 52;

    private CheckCardLayout(IReadOnlyList<string> preface, int prefaceY, IReadOnlyList<string> heading, int headingY, IReadOnlyList<string> hint, int hintY, IReadOnlyList<CheckOptionLayout> options, PixelBox? input, int bottom)
    {
        Preface = preface;
        PrefaceY = prefaceY;
        Heading = heading;
        HeadingY = headingY;
        Hint = hint;
        HintY = hintY;
        Options = options;
        Input = input;
        Bottom = bottom;
    }

    /// <summary>The rows of the line before the situation (production items), in the small font.</summary>
    public IReadOnlyList<string> Preface { get; }

    /// <summary>The y of the first preface row.</summary>
    public int PrefaceY { get; }

    /// <summary>The rows of the question or the situation, in the large font.</summary>
    public IReadOnlyList<string> Heading { get; }

    /// <summary>The y of the first heading row.</summary>
    public int HeadingY { get; }

    /// <summary>The rows of the hint under the heading, in the small font.</summary>
    public IReadOnlyList<string> Hint { get; }

    /// <summary>The y of the first hint row.</summary>
    public int HintY { get; }

    /// <summary>The options of a comprehension item, top to bottom.</summary>
    public IReadOnlyList<CheckOptionLayout> Options { get; }

    /// <summary>The text box of a production item, or <see langword="null"/>.</summary>
    public PixelBox? Input { get; }

    /// <summary>The lowest y the item's content reaches (it must stay above the notice and the footer).</summary>
    public int Bottom { get; }

    /// <summary>The card's rectangle in a window: centred, at most <see cref="MaxWidth"/> by <see cref="MaxHeight"/>.</summary>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    public static PixelBox PanelFor(int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(MaxWidth, viewportWidth - 48);
        var height = Math.Min(MaxHeight, viewportHeight - 24);
        return new PixelBox((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }

    /// <summary>The y below which the notice and the footer stand: the content must end above it.</summary>
    /// <param name="panel">The card.</param>
    public static int ContentLimit(PixelBox panel) => panel.Bottom - FooterSpace;

    /// <summary>Lays out a comprehension item: the question, the hint and the options.</summary>
    /// <param name="panel">The card.</param>
    /// <param name="question">The question.</param>
    /// <param name="hint">The line under it.</param>
    /// <param name="options">The label of each option and whether it has an icon.</param>
    /// <param name="large">The width of a text in the large font.</param>
    /// <param name="small">The width of a text in the small font.</param>
    public static CheckCardLayout ForComprehension(PixelBox panel, string question, string hint, IReadOnlyList<(string Label, bool HasIcon)> options, Func<string, float> large, Func<string, float> small)
    {
        ArgumentNullException.ThrowIfNull(options);
        var inner = panel.Width - (2 * Side);
        var y = panel.Y + 66;
        var heading = TextWrap.Wrap(question, inner, large);
        var headingY = y;
        y += (heading.Count * LargeRow) + 4;
        var hintRows = TextWrap.Wrap(hint, inner, small);
        var hintY = y;
        y += (hintRows.Count * SmallRow) + 10;
        var laid = new List<CheckOptionLayout>();
        for (var i = 0; i < options.Count; i++)
        {
            var box = new PixelBox(panel.X + Side, y, inner, OptionHeight);
            var textX = box.X + (options[i].HasIcon ? 64 : 16);
            var room = box.Right - 12 - textX;
            var label = $"{i + 1}  {options[i].Label}";
            if (large(label) <= room)
            {
                laid.Add(new CheckOptionLayout(box, [label], textX, true));
            }
            else
            {
                laid.Add(new CheckOptionLayout(box, TextWrap.Wrap(label, room, small), textX, false));
            }

            y += OptionHeight + OptionGap;
        }

        return new CheckCardLayout([], 0, heading, headingY, hintRows, hintY, laid, null, y - OptionGap);
    }

    /// <summary>Lays out a production item: the line before, the situation, the hint and the text box.</summary>
    /// <param name="panel">The card.</param>
    /// <param name="preface">The line before the situation.</param>
    /// <param name="situation">The situation.</param>
    /// <param name="hint">The line under it.</param>
    /// <param name="large">The width of a text in the large font.</param>
    /// <param name="small">The width of a text in the small font.</param>
    public static CheckCardLayout ForProduction(PixelBox panel, string preface, string situation, string hint, Func<string, float> large, Func<string, float> small)
    {
        var inner = panel.Width - (2 * Side);
        var y = panel.Y + 66;
        var prefaceRows = TextWrap.Wrap(preface, inner, small);
        var prefaceY = y;
        y += (prefaceRows.Count * SmallRow) + 8;
        var heading = TextWrap.Wrap(situation, inner, large);
        var headingY = y;
        y += (heading.Count * LargeRow) + 6;
        var hintRows = TextWrap.Wrap(hint, inner, small);
        var hintY = y;
        y += (hintRows.Count * SmallRow) + 8;
        var input = new PixelBox(panel.X + Side, y, inner, InputHeight);
        return new CheckCardLayout(prefaceRows, prefaceY, heading, headingY, hintRows, hintY, [], input, input.Bottom);
    }
}
