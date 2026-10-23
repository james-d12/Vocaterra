using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The lesson strip of S16 (D-game-13): above the support cards and the guide's panel, the lesson step in progress (its Spanish line in large type, its English under
/// it in its own colour and marked as English, the hint after help or a retry, and the buttons Continue, Skip, Help or the options of a listen step), or, with no
/// lesson engaged, the lesson on offer with its Start button. Plain layout in <see cref="LessonStripLayout"/>; this class only draws it. Only Spanish is spoken: every
/// word of the strip's English is drawn.
/// </summary>
public sealed class LessonLayer : IFrameLayer
{
    private const int LargeSize = 32;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Back = new(12, 16, 30, 232);
    private static readonly Color ButtonBack = new(34, 34, 58, 235);
    private static readonly Color ButtonHover = new(60, 60, 96, 245);
    private static readonly Color Right = new(120, 220, 130);

    /// <summary>The strip's layout for a frame, or <see langword="null"/> when nothing is shown: what the drawing and the mouse hit-test share.</summary>
    /// <param name="view">The frame's view.</param>
    /// <param name="lesson">The lesson front-end (what the strip shows now).</param>
    public static LessonStripLayout? LayoutFor(FrameView view, LessonFrontend lesson)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(lesson);
        var (step, offer) = lesson.State();
        if (step is null && offer is null)
        {
            return null;
        }

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(LargeSize);
        var limit = DialogueLayout.PanelBox(view.ViewportWidth, view.ViewportHeight).Y; // the strip stands on the panel; the cards are placed clear of it (ConversationFrame)
        return LessonStripLayout.For(
            view.Session.English,
            step,
            offer,
            view.ViewportWidth,
            view.ViewportHeight,
            limit,
            text => DrawHelpers.Width(small, text),
            text => DrawHelpers.Width(large, text),
            lesson.EnglishPage);
    }

    private readonly LessonFrontend _lesson;

    /// <summary>Creates the layer over the lesson front-end that says what the strip shows.</summary>
    /// <param name="lesson">The lesson front-end.</param>
    public LessonLayer(LessonFrontend lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        _lesson = lesson;
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (LayoutFor(view, _lesson) is not { } layout)
        {
            return;
        }

        var (step, offer) = _lesson.State();
        var ui = view.Session.English;
        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(LargeSize);
        var box = new Rectangle(layout.Box.X, layout.Box.Y, layout.Box.Width, layout.Box.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        var left = box.X + layout.Pad;
        var heading = step is not null ? LessonStripLayout.Heading(ui, step) : LessonStripLayout.Heading(ui, offer!);
        DrawHelpers.Text(spriteBatch, small, heading, new Vector2(left, layout.HeadingY), Gold);
        if (layout.SpanishY is { } spanishY)
        {
            for (var i = 0; i < layout.SpanishRows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, large, layout.SpanishRows[i], new Vector2(left, spanishY + (i * LessonStripLayout.SpanishHeight)), Ink);
            }
        }

        for (var i = 0; i < layout.EnglishRows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, small, layout.EnglishRows[i], new Vector2(left, layout.EnglishY + (i * LessonStripLayout.LineHeight)), DialoguePanel.EnglishColor);
        }

        for (var i = 0; i < layout.HintRows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, small, layout.HintRows[i], new Vector2(left, layout.HintY + (i * LessonStripLayout.LineHeight)), Gold);
        }

        foreach (var (action, buttonBox, text) in layout.Buttons)
        {
            var rect = new Rectangle(buttonBox.X, buttonBox.Y, buttonBox.Width, buttonBox.Height);
            var hovered = view.Mouse is { } at && buttonBox.Contains(at.X, at.Y);
            var picked = LessonStripLayout.OptionIndex(action) is { } index && step is not null && index == step.Picked;
            spriteBatch.Draw(view.Pixel, rect, hovered ? ButtonHover : ButtonBack);
            DrawHelpers.Border(spriteBatch, view.Pixel, rect, hovered || picked ? 2 : 1, picked ? Right : hovered ? Gold : new Color(110, 110, 150));
            DrawHelpers.Text(spriteBatch, small, text, new Vector2(rect.Left + LessonStripLayout.ButtonPadding, rect.Top + ((rect.Height - 18) / 2)), new Color(236, 236, 246));
        }
    }
}

/// <summary>
/// The first-time tip of S16 (D-game-13), top left: a short English line the first time something happens (looking, walking, collecting, aiming at someone, talking),
/// with <c>Enter: next tip</c>, and a small <c>F1: controls</c> reminder. Tips come from <see cref="TipFeed"/> (game content); a scripted run draws none unless it asks
/// (<c>--tips</c>), so the demo screenshots stay uncluttered.
/// </summary>
public sealed class TipLayer : IFrameLayer
{
    /// <summary>The distance from the top and the left edge.</summary>
    public const int Margin = 12;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(10, 10, 20, 205);
    private static readonly Color Dim = UiPalette.TipDim.ToColor();

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.ShowTips || view.Check is not null || view.MicCheck is { IsActive: true })
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.English;
        var y = Margin;
        if (view.Session.Tips.Current is { } tip)
        {
            var width = Math.Min(480, view.ViewportWidth / 2);
            var rows = TextWrap.Wrap(tip.Text, width - 16, t => DrawHelpers.Width(font, t));
            var height = 8 + 20 + (rows.Count * 20) + 20;
            var box = new Rectangle(Margin, y, width, height);
            spriteBatch.Draw(view.Pixel, box, Back);
            DrawHelpers.Border(spriteBatch, view.Pixel, box, 1, DialoguePanel.EnglishColor);
            DrawHelpers.Text(spriteBatch, font, ui.Ui("english") + " · tip", new Vector2(box.X + 8, box.Y + 6), Gold);
            for (var i = 0; i < rows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, rows[i], new Vector2(box.X + 8, box.Y + 26 + (i * 20)), DialoguePanel.EnglishColor);
            }

            DrawHelpers.Text(spriteBatch, font, ui.Ui("tipNext"), new Vector2(box.X + 8, box.Bottom - 22), Dim);
            y = box.Bottom + 6;
        }

        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsHint"), new Vector2(Margin, y), Dim);
    }
}

/// <summary>
/// The controls help of S16 (D-game-13), on F1: every key and button of the game in English, from the game's content (<c>game/english.json</c>), in a panel over the
/// world. F1 or Esc closes it.
/// </summary>
public sealed class ControlsLayer : IFrameLayer
{
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Back = new(12, 16, 30, 240);
    private static readonly Color Dim = new(180, 180, 200);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (!view.ControlsOpen)
        {
            return;
        }

        var font = view.Fonts.Get(FontProvider.NativeSize);
        var ui = view.Session.English;
        var (layout, rows) = LayoutFor(view, font, view.ControlsScroll);
        var box = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsTitle"), new Vector2(box.X + 16, box.Y + 12), Gold);
        var keysWidth = KeysWidth(layout.Panel.Width);
        var y = box.Y + ControlsLayout.HeaderHeight;
        for (var index = layout.First; index < layout.First + layout.Count; index++)
        {
            var (keys, text) = (rows[index].Keys, rows[index].Text);
            for (var i = 0; i < keys.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, keys[i], new Vector2(box.X + 16, y + (i * 20)), Gold);
            }

            for (var i = 0; i < text.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, text[i], new Vector2(box.X + 16 + keysWidth, y + (i * 20)), DialoguePanel.EnglishColor);
            }

            y += RowHeight(rows[index]);
        }

        var close = layout.Scrolls ? ui.Ui("controlsClose") + "   " + ScrollHint : ui.Ui("controlsClose");
        DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, close, box.Width - 32), new Vector2(box.X + 16, box.Bottom - 26), Dim);
    }

    /// <summary>What the footer adds when the controls do not all fit the canvas.</summary>
    public const string ScrollHint = "Up and Down: scroll";

    /// <summary>The key the help adds for the text size (X08); the other controls are the game's content.</summary>
    public const string TextSizeKeys = "F9";

    /// <summary>The help line for the text size key.</summary>
    public const string TextSizeText = "Change the size of the text: 100, 125 or 150 percent. It is kept in the save.";

    private static int KeysWidth(int panelWidth) => Math.Min(300, panelWidth / 3);

    private static int RowHeight((IReadOnlyList<string> Keys, IReadOnlyList<string> Text) row) => (Math.Max(row.Text.Count, row.Keys.Count) * 20) + 6;

    private static (ControlsLayout Layout, List<(IReadOnlyList<string> Keys, IReadOnlyList<string> Text)> Rows) LayoutFor(FrameView view, FontStashSharp.SpriteFontBase font, int scroll)
    {
        var width = Math.Min(820, view.ViewportWidth - 32);
        var keysWidth = KeysWidth(width);
        var textWidth = width - keysWidth - 32;
        var controls = view.Session.English.Controls.Select(c => (Keys: c.Keys, Text: c.Text)).Append((Keys: TextSizeKeys, Text: TextSizeText)).ToList();
        var rows = controls
            .Select(c => (Keys: TextWrap.Wrap(c.Keys, keysWidth - 8, t => DrawHelpers.Width(font, t)), Text: TextWrap.Wrap(c.Text, textWidth, t => DrawHelpers.Width(font, t))))
            .ToList();
        return (new ControlsLayout(view.ViewportWidth, view.ViewportHeight, rows.Select(RowHeight).ToList(), scroll), rows);
    }

    /// <summary>How far the controls help can scroll on the frame's canvas (the game clamps the scroll with it).</summary>
    /// <param name="view">The frame's view.</param>
    public static int MaxScrollFor(FrameView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return LayoutFor(view, view.Fonts.Get(FontProvider.NativeSize), 0).Layout.MaxScroll;
    }
}

/// <summary>
/// The closing card of the session (S17, D-game-15): <c>Session complete</c>, the guide's closing line in Spanish (large) and in English (its own colour), for a few
/// seconds after the player's own fire is lit with the camp done. The English is drawn, the Spanish is spoken. The card is up for <see cref="SessionEnding.ShowTicks"/>
/// or until a key is pressed; the objective area keeps saying that the session is complete.
/// </summary>
public sealed class EndingLayer : IFrameLayer
{
    private const int LargeSize = 32;
    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Back = new(12, 16, 30, 236);
    private static readonly Color Dim = new(180, 180, 200);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Ending is not { } ending || !ending.Visible(view.Tick) || view.Check is not null)
        {
            return;
        }

        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(LargeSize);
        var ui = view.Session.English;
        var width = Math.Min(760, view.ViewportWidth - 48);
        var spanish = TextWrap.Wrap(ending.Spanish, width - 40, t => DrawHelpers.Width(large, t));
        var english = TextWrap.Wrap(DialoguePanel.EnglishPrefix + ending.English, width - 40, t => DrawHelpers.Width(small, t));
        var summary = ending.Summary.SelectMany(line => TextWrap.Wrap(line, width - 40, t => DrawHelpers.Width(small, t))).ToList();
        var layout = new EndingLayout(view.ViewportWidth, view.ViewportHeight, spanish.Count, english.Count, summary.Count);
        var box = new Rectangle(layout.Box.X, layout.Box.Y, layout.Box.Width, layout.Box.Height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 3, Gold);
        var y = box.Y + 14;
        DrawHelpers.Text(spriteBatch, large, ui.Ui("sessionCompleteTitle"), new Vector2(box.X + 20, y), Gold);
        y += 44;
        foreach (var row in spanish)
        {
            DrawHelpers.Text(spriteBatch, large, row, new Vector2(box.X + 20, y), Ink);
            y += 40;
        }

        y += 4;
        foreach (var row in english)
        {
            DrawHelpers.Text(spriteBatch, small, row, new Vector2(box.X + 20, y), DialoguePanel.EnglishColor);
            y += 22;
        }

        if (layout.SummaryShown > 0)
        {
            // The session summary (X04, R01): what was done this session, in English, under the closing line. A short canvas gives up the last rows (X08), ending in an ellipsis.
            y += 14;
            DrawHelpers.Text(spriteBatch, small, ui.Ui("summaryTitle"), new Vector2(box.X + 20, y), Gold);
            y += 22;
            for (var i = 0; i < layout.SummaryShown; i++)
            {
                var row = layout.SummaryCut && i == layout.SummaryShown - 1 ? "…" : summary[i];
                DrawHelpers.Text(spriteBatch, small, row, new Vector2(box.X + 20, y), Ink);
                y += 20;
            }
        }

        DrawHelpers.Text(spriteBatch, small, ui.Ui("sessionCompleteKeys"), new Vector2(box.X + 20, box.Bottom - 26), Dim);
    }
}
