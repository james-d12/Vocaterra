using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.UI;

/// <summary>
/// Where the pieces of the lesson strip are, in window pixels, so the drawing, the mouse hit-test and the tests share one set of numbers (S16). The strip stands
/// above the support cards and the dialogue panel while the guide's panel is open: a heading (<c>Lesson · A first hello · step 1 of 2 · Say it</c>), the Spanish line
/// in large type, the step's English under it in its own colour and marked as English, the hint after help or a retry, and a row of buttons (Continue, Skip, Help,
/// or the options of a listen step). With no lesson engaged, a lesson on offer shows a one-line strip with the Start button.
/// </summary>
public sealed class LessonStripLayout
{
    /// <summary>The padding inside the strip.</summary>
    public const int Padding = 12;

    /// <summary>The height of a line of small text.</summary>
    public const int LineHeight = 20;

    /// <summary>The height of the Spanish line in large type.</summary>
    public const int SpanishHeight = 36;

    /// <summary>The height of a button.</summary>
    public const int ButtonHeight = 28;

    /// <summary>The padding inside the strip on a short canvas (X08).</summary>
    public const int CompactPadding = 8;

    /// <summary>The height of a button on a short canvas (X08): the 16 px text with 4 px above and below.</summary>
    public const int CompactButtonHeight = 24;

    /// <summary>The gap between buttons and between the strip and what is below it.</summary>
    public const int Gap = 8;

    /// <summary>The horizontal padding inside a button around its text.</summary>
    public const int ButtonPadding = 10;

    /// <summary>The most rows of English the strip lays out (the rest is cut).</summary>
    public const int MaxEnglishRows = 3;

    /// <summary>The most rows of the Spanish line in the large type.</summary>
    public const int MaxSpanishRows = 2;

    /// <summary>The most rows of hint the strip lays out.</summary>
    public const int MaxHintRows = 2;

    /// <summary>The highest y the strip may have (X08): on a short canvas it gives up its hint and rows of English to stay under it.</summary>
    public const int MinTop = 4;

    private LessonStripLayout(PixelBox box, int pad, int headingY, int? spanishY, IReadOnlyList<string> spanishRows, int englishY, IReadOnlyList<string> englishRows, int englishPage, int englishPages, int hintY, IReadOnlyList<string> hintRows, IReadOnlyList<(LessonAction Action, PixelBox Box, string Text)> buttons)
    {
        Box = box;
        Pad = pad;
        HeadingY = headingY;
        SpanishY = spanishY;
        SpanishRows = spanishRows;
        EnglishY = englishY;
        EnglishPage = englishPage;
        EnglishPages = englishPages;
        EnglishRows = englishRows;
        HintY = hintY;
        HintRows = hintRows;
        Buttons = buttons;
    }

    /// <summary>The strip's box.</summary>
    public PixelBox Box { get; }

    /// <summary>The padding inside the strip (<see cref="Padding"/>, or <see cref="CompactPadding"/> on a short canvas): the text's left edge is this far from the box's.</summary>
    public int Pad { get; }

    /// <summary>The y of the heading line.</summary>
    public int HeadingY { get; }

    /// <summary>The y of the Spanish line in large type, or <see langword="null"/> when the step has none.</summary>
    public int? SpanishY { get; }

    /// <summary>The rows of the Spanish line (it wraps to at most <see cref="MaxSpanishRows"/> rows in the large type); empty when the step has none.</summary>
    public IReadOnlyList<string> SpanishRows { get; }

    /// <summary>The y of the first row of English.</summary>
    public int EnglishY { get; }

    /// <summary>The page of the English the strip shows (from 0).</summary>
    public int EnglishPage { get; }

    /// <summary>How many pages the English goes over (1 when it fits the strip; more when a short canvas gives the strip fewer rows, and the More button turns them).</summary>
    public int EnglishPages { get; }

    /// <summary>The rows of English, each already marked (the first starts with <c>English:</c>).</summary>
    public IReadOnlyList<string> EnglishRows { get; }

    /// <summary>The y of the first row of hint.</summary>
    public int HintY { get; }

    /// <summary>The rows of hint (empty when there is none).</summary>
    public IReadOnlyList<string> HintRows { get; }

    /// <summary>The buttons with their boxes and their texts, left to right.</summary>
    public IReadOnlyList<(LessonAction Action, PixelBox Box, string Text)> Buttons { get; }

    /// <summary>The button under a point, or <see langword="null"/>.</summary>
    /// <param name="x">The pointer's x in window pixels.</param>
    /// <param name="y">The pointer's y.</param>
    public LessonAction? ActionAt(int x, int y)
    {
        foreach (var (action, box, _) in Buttons)
        {
            if (box.Contains(x, y))
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>The key a button answers to, as the button's text shows it.</summary>
    /// <param name="action">The button.</param>
    public static string KeyLabel(LessonAction action) => action switch
    {
        LessonAction.Start => "L",
        LessonAction.Continue => "Enter",
        LessonAction.Skip => "K",
        LessonAction.Help => "H",
        LessonAction.More => "M",
        LessonAction.Option1 => "1",
        LessonAction.Option2 => "2",
        LessonAction.Option3 => "3",
        _ => "4",
    };

    /// <summary>The lesson action a key presses, or <see langword="null"/>.</summary>
    /// <param name="key">The key.</param>
    public static LessonAction? ForKey(LessonKey key) => key switch
    {
        LessonKey.Enter => LessonAction.Continue,
        LessonKey.K => LessonAction.Skip,
        LessonKey.L => LessonAction.Start,
        LessonKey.D1 => LessonAction.Option1,
        LessonKey.D2 => LessonAction.Option2,
        LessonKey.D3 => LessonAction.Option3,
        LessonKey.D4 => LessonAction.Option4,
        LessonKey.M => LessonAction.More,
        _ => null,
    };

    /// <summary>The index of an option action (0 to 3), or <see langword="null"/> for the other buttons.</summary>
    /// <param name="action">The action.</param>
    public static int? OptionIndex(LessonAction action) => action switch
    {
        LessonAction.Option1 => 0,
        LessonAction.Option2 => 1,
        LessonAction.Option3 => 2,
        LessonAction.Option4 => 3,
        _ => null,
    };

    /// <summary>
    /// The layout of the strip for a lesson step, or for a lesson on offer when <paramref name="view"/> is <see langword="null"/>; <see langword="null"/> when there
    /// is neither. The strip stands above <paramref name="limitTop"/> (the top of the support cards, or of the panel).
    /// </summary>
    /// <param name="ui">The game's English text (the interface words).</param>
    /// <param name="view">The lesson step in progress, or <see langword="null"/>.</param>
    /// <param name="offer">The lesson on offer, or <see langword="null"/>.</param>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="limitTop">The y the strip must end above.</param>
    /// <param name="measure">The width of a text in pixels in the small font.</param>
    /// <param name="measureLarge">The width of a text in pixels in the large font (the Spanish line).</param>
    /// <param name="englishPage">The page of the English to show (it wraps round): a step's English longer than the strip has room for goes over several pages, and the <c>[M] More</c> button turns them.</param>
    public static LessonStripLayout? For(
        EnglishText ui,
        LessonView? view,
        LessonOffer? offer,
        int viewportWidth,
        int viewportHeight,
        int limitTop,
        Func<string, float> measure,
        Func<string, float> measureLarge,
        int englishPage = 0)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(measureLarge);
        var panel = DialogueLayout.PanelBox(viewportWidth, viewportHeight);
        var width = panel.Width;
        var compact = DialogueLayout.IsCompact(viewportHeight);
        var pad = compact ? CompactPadding : Padding; // a short canvas tightens the strip: smaller padding and buttons (X08)
        var buttonHeight = compact ? CompactButtonHeight : ButtonHeight;
        var rowGap = compact ? 4 : Gap;
        var inner = width - (pad * 2);
        if (view is null)
        {
            return offer is null ? null : ForOffer(ui, offer, panel, limitTop, inner, measure);
        }

        var englishAll = TextWrap.Wrap(DialoguePanel.EnglishPrefix + view.English, inner, measure);
        var hintAll = view.Hint is { } text ? TextWrap.Wrap(DialoguePanel.EnglishPrefix + text, inner, measure) : [];
        var hint = Cut(hintAll, MaxHintRows, measure, inner);
        var showsSpanish = view.Spanish is not null;
        var spanishAll = showsSpanish ? TextWrap.Wrap(view.Spanish!, inner, measureLarge) : [];
        var spanishRows = Cut(spanishAll, MaxSpanishRows, measureLarge, inner);

        // The English goes over pages of up to MaxEnglishRows rows; the page is as tall as its rows, and the More button turns them. On a short canvas
        // the page gets fewer rows (down to one) and the hint fewer, until the strip stands under the limit; the Spanish line and the buttons stay, and no English is left out.
        var perPage = MaxEnglishRows;
        List<(LessonAction Action, string Text)> labels;
        List<int> widths;
        List<int> rowOf;
        int rowCount;
        int pages;
        int HeightOf() => pad + LineHeight + (spanishRows.Count * SpanishHeight) + (Math.Min(perPage, englishAll.Count) * LineHeight) + (hint.Count * LineHeight) + rowGap + (rowCount * buttonHeight) + ((rowCount - 1) * rowGap) + pad;
        while (true)
        {
            pages = (englishAll.Count + perPage - 1) / perPage;
            labels = ButtonLabels(ui, view, pages, englishPage, perPage);
            widths = labels.Select(l => Math.Min((int)Math.Ceiling(measure(l.Text)) + (ButtonPadding * 2), inner)).ToList();
            rowOf = Rows(widths, inner, out rowCount);
            if (limitTop - Gap - HeightOf() >= MinTop || (hint.Count == 0 && perPage == 1 && spanishRows.Count <= 1))
            {
                break;
            }

            if (perPage > 1)
            {
                perPage--;
            }
            else if (hint.Count > 1)
            {
                hint = Cut(hintAll, 1, measure, inner);
            }
            else if (hint.Count == 1)
            {
                hint = [];
            }
            else
            {
                spanishRows = Cut(spanishAll, 1, measureLarge, inner); // the last resort: a second row of the Spanish line goes (the panel's log and the bubble have the line)
            }
        }

        var page = pages <= 1 ? 0 : ((englishPage % pages) + pages) % pages;
        var english = englishAll.Skip(page * perPage).Take(perPage).ToList();
        var height = HeightOf();
        var y = Math.Max(MinTop, limitTop - Gap - height);
        var box = new PixelBox(panel.X, y, width, height);
        var headingY = y + pad - 4;
        var cursor = headingY + LineHeight;
        int? spanishY = null;
        if (showsSpanish)
        {
            spanishY = cursor;
            cursor += spanishRows.Count * SpanishHeight;
        }

        var englishY = cursor;
        cursor += english.Count * LineHeight;
        var hintY = cursor;
        var buttonsTop = box.Bottom - pad - (rowCount * buttonHeight) - ((rowCount - 1) * rowGap);
        var buttons = new List<(LessonAction, PixelBox, string)>();
        var x = panel.X + pad;
        for (var i = 0; i < labels.Count; i++)
        {
            if (i > 0 && rowOf[i] != rowOf[i - 1])
            {
                x = panel.X + pad;
            }

            buttons.Add((labels[i].Action, new PixelBox(x, buttonsTop + (rowOf[i] * (buttonHeight + rowGap)), widths[i], buttonHeight), labels[i].Text));
            x += widths[i] + Gap;
        }

        return new LessonStripLayout(box, pad, headingY, spanishY, spanishRows, englishY, english, page, pages, hintY, hint, buttons);
    }

    // The buttons of a step, left to right: the options of a listen step, Continue, More (when the English goes over pages), Help and Skip.
    private static List<(LessonAction Action, string Text)> ButtonLabels(EnglishText ui, LessonView view, int pages, int englishPage, int perPage)
    {
        _ = perPage;
        var labels = new List<(LessonAction Action, string Text)>();
        void Label(LessonAction action, string label) => labels.Add((action, $"[{KeyLabel(action)}] {label}"));
        if (view.Type == LessonStepType.Listen)
        {
            for (var i = 0; i < view.Options.Count && i < 4; i++)
            {
                Label(LessonAction.Option1 + i, view.Options[i]);
            }
        }

        if (view.CanContinue)
        {
            Label(LessonAction.Continue, ui.Ui("continue"));
        }

        if (pages > 1)
        {
            var page = ((englishPage % pages) + pages) % pages;
            Label(LessonAction.More, $"More ({page + 1}/{pages})");
        }

        Label(LessonAction.Help, ui.Ui("help"));
        Label(LessonAction.Skip, ui.Ui("skip"));
        return labels;
    }

    // The row of each button: a row too wide for the strip goes on to the next (a button is never off the strip).
    private static List<int> Rows(IReadOnlyList<int> widths, int inner, out int rowCount)
    {
        var rowOf = new List<int>();
        var rowX = 0;
        rowCount = 1;
        foreach (var buttonWidth in widths)
        {
            if (rowX > 0 && rowX + buttonWidth > inner)
            {
                rowCount++;
                rowX = 0;
            }

            rowOf.Add(rowCount - 1);
            rowX += buttonWidth + Gap;
        }

        return rowOf;
    }

    // The first rows of a wrapped text, the last one ending in an ellipsis when rows were left out.
    private static List<string> Cut(IReadOnlyList<string> rows, int max, Func<string, float> measure, float width)
    {
        if (rows.Count <= max)
        {
            return rows.ToList();
        }

        var cut = rows.Take(max).ToList();
        var last = cut[^1].TrimEnd();
        while (last.Length > 1 && measure(last + "…") > width)
        {
            last = last[..^1].TrimEnd();
        }

        cut[^1] = last + "…";
        return cut;
    }

    private static LessonStripLayout ForOffer(EnglishText ui, LessonOffer offer, PixelBox panel, int limitTop, int inner, Func<string, float> measure)
    {
        var rows = TextWrap.Wrap(DialoguePanel.EnglishPrefix + offer.English, inner, measure).Take(2).ToList();
        var height = Padding + LineHeight + (rows.Count * LineHeight) + Gap + ButtonHeight + Padding;
        var y = Math.Max(4, limitTop - Gap - height);
        var box = new PixelBox(panel.X, y, panel.Width, height);
        var headingY = y + Padding - 4;
        var text = $"[{KeyLabel(LessonAction.Start)}] {ui.Ui("startLesson")}";
        var buttonWidth = (int)Math.Ceiling(measure(text)) + (ButtonPadding * 2);
        var button = new PixelBox(panel.X + Padding, box.Bottom - Padding - ButtonHeight, buttonWidth, ButtonHeight);
        return new LessonStripLayout(box, Padding, headingY, null, [], headingY + LineHeight, rows, 0, 1, box.Bottom, [], [(LessonAction.Start, button, text)]);
    }

    /// <summary>The heading of a strip for a lesson step: <c>Lesson · A first hello · step 1 of 2 · Say it</c>.</summary>
    /// <param name="ui">The game's English text.</param>
    /// <param name="view">The lesson step in progress.</param>
    public static string Heading(EnglishText ui, LessonView view)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(view);
        var step = ui.Ui("stepOf", ("n", view.StepNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("total", view.StepCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return $"{ui.Ui("lesson")} · {view.Title} · {step} · {view.Heading}";
    }

    /// <summary>The heading of the strip of a lesson on offer: <c>Lesson · A first hello</c>.</summary>
    /// <param name="ui">The game's English text.</param>
    /// <param name="offer">The offer.</param>
    public static string Heading(EnglishText ui, LessonOffer offer)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(offer);
        return $"{ui.Ui("lesson")} · {offer.Title}";
    }
}
