using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What a button of the lesson strip does (S16). The strip's mouse hit-test, its keys and the code that carries them out all use these.</summary>
public enum LessonAction
{
    /// <summary><c>L</c>: starts the lesson on offer, or takes the paused one up again.</summary>
    Start,

    /// <summary><c>Enter</c>: ends the step that waits for it (explain, model, recap, a picked listen step).</summary>
    Continue,

    /// <summary><c>K</c>: skips the step.</summary>
    Skip,

    /// <summary><c>H</c>: the step's help path (the panel's Ayuda button does the same while a lesson is engaged).</summary>
    Help,

    /// <summary><c>1</c>: the first option of a listen step.</summary>
    Option1,

    /// <summary><c>2</c>: the second option of a listen step.</summary>
    Option2,

    /// <summary><c>3</c>: the third option of a listen step.</summary>
    Option3,

    /// <summary><c>4</c>: the fourth option of a listen step.</summary>
    Option4,
}

/// <summary>The keys of the lesson strip, without the engine's key type (the game maps its keys to these).</summary>
public enum LessonKey
{
    /// <summary>Enter.</summary>
    Enter,

    /// <summary>K.</summary>
    K,

    /// <summary>L.</summary>
    L,

    /// <summary>The key 1.</summary>
    D1,

    /// <summary>The key 2.</summary>
    D2,

    /// <summary>The key 3.</summary>
    D3,

    /// <summary>The key 4.</summary>
    D4,
}

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

    /// <summary>The gap between buttons and between the strip and what is below it.</summary>
    public const int Gap = 8;

    /// <summary>The horizontal padding inside a button around its text.</summary>
    public const int ButtonPadding = 10;

    /// <summary>The most rows of English the strip lays out (the rest is cut).</summary>
    public const int MaxEnglishRows = 3;

    /// <summary>The most rows of hint the strip lays out.</summary>
    public const int MaxHintRows = 2;

    private LessonStripLayout(PixelBox box, int headingY, int? spanishY, int englishY, IReadOnlyList<string> englishRows, int hintY, IReadOnlyList<string> hintRows, IReadOnlyList<(LessonAction Action, PixelBox Box, string Text)> buttons)
    {
        Box = box;
        HeadingY = headingY;
        SpanishY = spanishY;
        EnglishY = englishY;
        EnglishRows = englishRows;
        HintY = hintY;
        HintRows = hintRows;
        Buttons = buttons;
    }

    /// <summary>The strip's box.</summary>
    public PixelBox Box { get; }

    /// <summary>The y of the heading line.</summary>
    public int HeadingY { get; }

    /// <summary>The y of the Spanish line in large type, or <see langword="null"/> when the step has none.</summary>
    public int? SpanishY { get; }

    /// <summary>The y of the first row of English.</summary>
    public int EnglishY { get; }

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
    public static LessonStripLayout? For(
        EnglishText ui,
        LessonView? view,
        LessonOffer? offer,
        int viewportWidth,
        int viewportHeight,
        int limitTop,
        Func<string, float> measure,
        Func<string, float> measureLarge)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentNullException.ThrowIfNull(measureLarge);
        var panel = DialogueLayout.PanelBox(viewportWidth, viewportHeight);
        var width = panel.Width;
        var inner = width - (Padding * 2);
        if (view is null)
        {
            return offer is null ? null : ForOffer(ui, offer, panel, limitTop, inner, measure);
        }

        var english = TextWrap.Wrap(DialoguePanel.EnglishPrefix + view.English, inner, measure).Take(MaxEnglishRows).ToList();
        var hint = view.Hint is { } text ? TextWrap.Wrap(DialoguePanel.EnglishPrefix + text, inner, measure).Take(MaxHintRows).ToList() : [];
        var showsSpanish = view.Spanish is not null;
        var height = Padding + LineHeight + (showsSpanish ? SpanishHeight : 0) + (english.Count * LineHeight) + (hint.Count * LineHeight) + Gap + ButtonHeight + Padding;
        var y = Math.Max(4, limitTop - Gap - height);
        var box = new PixelBox(panel.X, y, width, height);
        var headingY = y + Padding - 4;
        var cursor = headingY + LineHeight;
        int? spanishY = null;
        if (showsSpanish)
        {
            spanishY = cursor;
            cursor += SpanishHeight;
        }

        var englishY = cursor;
        cursor += english.Count * LineHeight;
        var hintY = cursor;
        cursor += hint.Count * LineHeight;
        var buttonsY = box.Bottom - Padding - ButtonHeight;
        var buttons = new List<(LessonAction, PixelBox, string)>();
        var x = panel.X + Padding;
        void Add(LessonAction action, string label)
        {
            var text = $"[{KeyLabel(action)}] {label}";
            var buttonWidth = (int)Math.Ceiling(measure(text)) + (ButtonPadding * 2);
            buttons.Add((action, new PixelBox(x, buttonsY, buttonWidth, ButtonHeight), text));
            x += buttonWidth + Gap;
        }

        if (view.Type == LessonStepType.Listen)
        {
            for (var i = 0; i < view.Options.Count && i < 4; i++)
            {
                Add(LessonAction.Option1 + i, view.Options[i]);
            }
        }

        if (view.CanContinue)
        {
            Add(LessonAction.Continue, ui.Ui("continue"));
        }

        Add(LessonAction.Help, ui.Ui("help"));
        Add(LessonAction.Skip, ui.Ui("skip"));
        return new LessonStripLayout(box, headingY, spanishY, englishY, english, hintY, hint, buttons);
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
        return new LessonStripLayout(box, headingY, null, headingY + LineHeight, rows, box.Bottom, [], [(LessonAction.Start, button, text)]);
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
