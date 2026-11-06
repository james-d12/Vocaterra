using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Station;

/// <summary>
/// Draws the instruction strip (W07): the row's Spanish and English, the dot of each step and the Repetir, Más despacio and Ayuda buttons. It is a small layer of its own over the
/// <see cref="InstructionStrip"/> model and a box (<see cref="InstructionStripLayout"/>), not a part of the station panel, so that a page of a carried recipe book (W30) can draw
/// the same strip where it likes.
/// </summary>
public sealed class InstructionStripLayer
{
    private static readonly Color Back = new(24, 26, 44, 240);
    private static readonly Color Edge = new(255, 214, 64, 160);
    private static readonly Color Spanish = new(255, 240, 190);
    private static readonly Color English = new(168, 200, 232);
    private static readonly Color Muted = UiPalette.Muted.ToColor();
    private static readonly Color Done = new(120, 220, 130);
    private static readonly Color Current = new(255, 214, 64);
    private static readonly Color Todo = new(90, 92, 112);
    private static readonly Color Button = new(40, 44, 70, 245);
    private static readonly Color ButtonOff = new(30, 30, 44, 235);

    /// <summary>Draws the strip into its layout.</summary>
    /// <param name="spriteBatch">The batch.</param>
    /// <param name="view">The frame's view (the pixel, the fonts, the locale and the pointer).</param>
    /// <param name="strip">What the strip shows.</param>
    /// <param name="layout">Where its pieces are.</param>
    public void Draw(SpriteBatch spriteBatch, FrameView view, InstructionStrip strip, InstructionStripLayout layout)
    {
        var pixel = view.Pixel;
        var font = view.Fonts.Get(FontProvider.NativeSize);
        var box = layout.Box;
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, box.Height), Back);
        DrawHelpers.Border(spriteBatch, pixel, new Rectangle(box.X, box.Y, box.Width, box.Height), 1, Edge);

        var (spanish, english) = InstructionStripLayout.TextLines(strip, layout.Text.Width, layout.MaxTextLines, text => DrawHelpers.Width(font, text));
        var y = layout.Text.Y;
        foreach (var line in spanish)
        {
            DrawHelpers.Text(spriteBatch, font, line, new Vector2(layout.Text.X, y), Spanish);
            y += InstructionStripLayout.LineHeight;
        }

        foreach (var line in english)
        {
            DrawHelpers.Text(spriteBatch, font, line, new Vector2(layout.Text.X, y), English);
            y += InstructionStripLayout.LineHeight;
        }

        if (strip.Row is not null && !strip.ShowsText)
        {
            DrawHelpers.Text(spriteBatch, font, DrawHelpers.Fit(font, view.Session.Locale.Ui("stationListen"), layout.Text.Width), new Vector2(layout.Text.X, y), Muted);
        }

        DrawDots(spriteBatch, pixel, strip, layout);
        var boxes = new[] { layout.Repeat, layout.Slower, layout.Help };
        for (var i = 0; i < strip.Actions.Count && i < boxes.Length; i++)
        {
            DrawButton(spriteBatch, view, font, strip.Actions[i], boxes[i]);
        }
    }

    private static void DrawDots(SpriteBatch spriteBatch, Texture2D pixel, InstructionStrip strip, InstructionStripLayout layout)
    {
        var step = layout.DotStep(strip.Dots.Count);
        var size = Math.Min(12, step - 2);
        for (var i = 0; i < strip.Dots.Count; i++)
        {
            var dot = new Rectangle(layout.Dots.X + 4 + (i * step), layout.Dots.Y + ((layout.Dots.Height - size) / 2), size, size);
            switch (strip.Dots[i].State)
            {
                case StripDotState.Done:
                    spriteBatch.Draw(pixel, dot, Done);
                    break;
                case StripDotState.Current:
                    DrawHelpers.Border(spriteBatch, pixel, dot, 2, Current);
                    break;
                default:
                    DrawHelpers.Border(spriteBatch, pixel, dot, 1, Todo);
                    break;
            }
        }
    }

    private static void DrawButton(SpriteBatch spriteBatch, FrameView view, FontStashSharp.SpriteFontBase font, StripAction action, PixelBox box)
    {
        var rectangle = new Rectangle(box.X, box.Y, box.Width, box.Height);
        var over = view.Mouse is { } mouse && action.Enabled && box.Contains(mouse.X, mouse.Y);
        spriteBatch.Draw(view.Pixel, rectangle, action.Enabled ? Button : ButtonOff);
        DrawHelpers.Border(spriteBatch, view.Pixel, rectangle, over ? 2 : 1, action.Enabled ? (over ? Current : Edge) : new Color(70, 70, 90));
        var text = DrawHelpers.Fit(font, $"{action.Label} [{action.Key}]", box.Width - 8);
        var x = box.X + ((box.Width - (int)DrawHelpers.Width(font, text)) / 2);
        DrawHelpers.Text(spriteBatch, font, text, new Vector2(x, box.Y + ((box.Height - 20) / 2)), action.Enabled ? Spanish : Muted);
    }
}
