using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The mic-check screen at the start (D-game-9, S7): the instruction <c>Di: hola</c>, a live level meter from the capture's RMS, the line
/// <c>Heard: «…»</c> once the recognizer has heard the try, and the result (<c>Got it, I can hear you.</c> or <c>No te oí bien. ¿Otra vez?</c>) with the
/// buttons <c>Seguir</c>, <c>Otra vez</c> and <c>Saltar</c>. It only draws the <see cref="MicCheckModel"/>; the keys and the mouse are the game's.
/// </summary>
public sealed class MicCheckLayer : IFrameLayer
{
    private const int Segments = 24;
    private const int InstructionSize = 32;

    private static readonly Color Gold = new(255, 214, 64);
    private static readonly Color Ink = new(250, 246, 232);
    private static readonly Color Dim = new(140, 140, 150);
    private static readonly Color Good = new(120, 220, 130);
    private static readonly Color Warn = new(255, 150, 120);
    private static readonly Color HeardColor = new(190, 225, 255);

    /// <summary>The layout of the screen for the model's phase and a window, measured in the screen's font: what the drawing and the mouse hit-test share.</summary>
    /// <param name="model">The mic check.</param>
    /// <param name="viewportWidth">The window's width.</param>
    /// <param name="viewportHeight">The window's height.</param>
    /// <param name="font">The font the buttons are drawn with.</param>
    public static MicCheckLayout LayoutFor(MicCheckModel model, int viewportWidth, int viewportHeight, FontStashSharp.SpriteFontBase font)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(font);
        return new MicCheckLayout(viewportWidth, viewportHeight, model.Buttons, text => DrawHelpers.Width(font, text));
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var check = view.MicCheck;
        if (check is null || !check.IsActive)
        {
            return;
        }

        var pixel = view.Pixel;
        var small = view.Fonts.Get(FontProvider.NativeSize);
        var large = view.Fonts.Get(InstructionSize);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), new Color(6, 6, 14, 170));
        var layout = LayoutFor(check, view.ViewportWidth, view.ViewportHeight, small);
        var panel = new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, layout.Panel.Height);
        spriteBatch.Draw(pixel, panel, new Color(18, 18, 34, 250));
        DrawHelpers.Border(spriteBatch, pixel, panel, 2, Gold);

        DrawHelpers.Text(spriteBatch, small, MicCheckModel.Title, new Vector2(panel.Left + 40, panel.Top + 16), Gold);
        DrawHelpers.Text(spriteBatch, large, MicCheckModel.Instruction, new Vector2(panel.Left + 40, panel.Top + 46), Ink);
        var status = check.StatusText;
        var statusColor = check.Phase switch
        {
            MicCheckPhase.Passed => Good,
            MicCheckPhase.Failed => Warn,
            MicCheckPhase.Listening => Good,
            MicCheckPhase.Thinking => Gold,
            _ => Dim,
        };
        var inner = panel.Width - 80;
        var statusRows = Rows(small, status, inner);
        for (var i = 0; i < statusRows.Count; i++)
        {
            DrawHelpers.Text(spriteBatch, small, statusRows[i], new Vector2(panel.Left + 40, panel.Top + 100 + (i * 20)), statusColor);
        }
        if (check.Phase == MicCheckPhase.Listening && (view.Tick / 15) % 2 == 0)
        {
            spriteBatch.Draw(pixel, new Rectangle(panel.Left + 26, panel.Top + 105, 8, 8), new Color(255, 70, 70));
        }

        DrawMeter(spriteBatch, pixel, layout.Meter, check.MeterFill);
        if (check.HeardText is { } heard)
        {
            var heardRows = Rows(small, heard, inner);
            for (var i = 0; i < heardRows.Count; i++)
            {
                DrawHelpers.Text(spriteBatch, small, heardRows[i], new Vector2(panel.Left + 40, panel.Top + 192 + (i * 20)), HeardColor);
            }
        }

        var mouse = view.Mouse;
        foreach (var (button, box) in layout.Buttons)
        {
            var rect = new Rectangle(box.X, box.Y, box.Width, box.Height);
            var hovered = mouse is { } at && box.Contains(at.X, at.Y);
            spriteBatch.Draw(pixel, rect, hovered ? new Color(60, 60, 96, 245) : new Color(34, 34, 58, 235));
            DrawHelpers.Border(spriteBatch, pixel, rect, hovered ? 2 : 1, button == MicCheckButton.Skip ? new Color(110, 110, 150) : Good);
            DrawHelpers.Text(spriteBatch, small, MicCheckModel.LabelOf(button), new Vector2(rect.Left + 14, rect.Top + 8), Ink);
        }

        DrawHelpers.Text(spriteBatch, small, "Esc: skip", new Vector2(panel.Right - 40 - DrawHelpers.Width(small, "Esc: skip"), panel.Bottom - 44), Dim);
    }

    // A text wrapped to the panel's inner width, at most two rows (the second ends in an ellipsis when text is left out): a long recognised line stays on the panel (X08).
    private static IReadOnlyList<string> Rows(FontStashSharp.SpriteFontBase font, string text, int width)
    {
        var rows = TextWrap.Wrap(text, width, t => DrawHelpers.Width(font, t));
        return rows.Count <= 2 ? rows : [rows[0], rows[1].TrimEnd() + "…"];
    }

    // A row of segments that light up to the level: green, then yellow, then red for a very loud input.
    private static void DrawMeter(SpriteBatch spriteBatch, Texture2D pixel, PixelBox meter, double level)
    {
        spriteBatch.Draw(pixel, new Rectangle(meter.X - 2, meter.Y - 2, meter.Width + 4, meter.Height + 4), new Color(10, 10, 18));
        var lit = (int)Math.Round(Math.Clamp(level, 0, 1) * Segments, MidpointRounding.AwayFromZero);
        var gap = 2;
        var width = (meter.Width - ((Segments - 1) * gap)) / Segments;
        for (var i = 0; i < Segments; i++)
        {
            var colour = i < Segments * 0.6 ? new Color(90, 200, 110) : i < Segments * 0.85 ? new Color(230, 200, 70) : new Color(230, 90, 80);
            if (i >= lit)
            {
                colour = new Color(colour.R / 5, colour.G / 5, colour.B / 5);
            }

            spriteBatch.Draw(pixel, new Rectangle(meter.X + (i * (width + gap)), meter.Y, width, meter.Height), colour);
        }
    }
}
