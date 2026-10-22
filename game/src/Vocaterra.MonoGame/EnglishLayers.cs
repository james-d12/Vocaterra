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
        var limit = SupportLayout.For(view.Session, view.ViewportWidth, view.ViewportHeight).Top;
        return LessonStripLayout.For(
            view.Session.English,
            step,
            offer,
            view.ViewportWidth,
            view.ViewportHeight,
            limit,
            text => DrawHelpers.Width(small, text),
            text => DrawHelpers.Width(large, text));
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
        var left = box.X + LessonStripLayout.Padding;
        var heading = step is not null ? LessonStripLayout.Heading(ui, step) : LessonStripLayout.Heading(ui, offer!);
        DrawHelpers.Text(spriteBatch, small, heading, new Vector2(left, layout.HeadingY), Gold);
        if (layout.SpanishY is { } spanishY && step?.Spanish is { } spanish)
        {
            DrawHelpers.Text(spriteBatch, large, spanish, new Vector2(left, spanishY), Ink);
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
            DrawHelpers.Text(spriteBatch, small, text, new Vector2(rect.Left + LessonStripLayout.ButtonPadding, rect.Top + 5), new Color(236, 236, 246));
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
    private static readonly Color Dim = new(180, 180, 200);

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
        var controls = view.Session.English.Controls;
        var width = Math.Min(820, view.ViewportWidth - 32);
        var keysWidth = Math.Min(300, width / 3);
        var textWidth = width - keysWidth - 32;
        var rows = controls.Select(c => (c, Text: TextWrap.Wrap(c.Text, textWidth, t => DrawHelpers.Width(font, t)), Keys: TextWrap.Wrap(c.Keys, keysWidth - 8, t => DrawHelpers.Width(font, t)))).ToList();
        var height = 24 + 28 + rows.Sum(r => Math.Max(r.Text.Count, r.Keys.Count) * 20 + 6) + 30;
        var box = new Rectangle((view.ViewportWidth - width) / 2, Math.Max(8, (view.ViewportHeight - height) / 2), width, height);
        spriteBatch.Draw(view.Pixel, box, Back);
        DrawHelpers.Border(spriteBatch, view.Pixel, box, 2, DialoguePanel.EnglishColor);
        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsTitle"), new Vector2(box.X + 16, box.Y + 12), Gold);
        var y = box.Y + 40;
        foreach (var (control, text, keys) in rows)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, keys[i], new Vector2(box.X + 16, y + (i * 20)), Gold);
            }

            for (var i = 0; i < text.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, font, text[i], new Vector2(box.X + 16 + keysWidth, y + (i * 20)), DialoguePanel.EnglishColor);
            }

            y += (Math.Max(text.Count, keys.Count) * 20) + 6;
        }

        DrawHelpers.Text(spriteBatch, font, ui.Ui("controlsClose"), new Vector2(box.X + 16, box.Bottom - 26), Dim);
    }
}
