using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Graphics;
using Vocaterra.MonoGame.UI.Dialogue;

namespace Vocaterra.MonoGame.UI.English;

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
