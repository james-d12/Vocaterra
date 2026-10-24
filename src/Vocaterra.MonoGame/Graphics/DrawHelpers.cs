using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.UI;

namespace Vocaterra.MonoGame.Graphics;

/// <summary>Small drawing helpers over the 1 x 1 white texture and the font: rectangles, borders, rotated bars and text with a shadow.</summary>
public static class DrawHelpers
{
    /// <summary>A palette colour (<see cref="UiPalette"/>) as an engine colour.</summary>
    /// <param name="colour">The colour.</param>
    public static Color ToColor(this Rgb colour) => new(colour.R, colour.G, colour.B);

    /// <summary>Fills a rectangle.</summary>
    public static void Fill(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, Color color) => spriteBatch.Draw(pixel, area, color);

    /// <summary>Draws a hollow rectangle with a border of <paramref name="thickness"/> pixels inside the area.</summary>
    public static void Border(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, int thickness, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(area.Left, area.Top, area.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(area.Left, area.Bottom - thickness, area.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(area.Left, area.Top, thickness, area.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(area.Right - thickness, area.Top, thickness, area.Height), color);
    }

    /// <summary>
    /// Fills a rectangle with its corners cut in steps of a quarter circle of <paramref name="radius"/> pixels (the pixel-art rounding of the panels of S15), with a border of
    /// <paramref name="thickness"/> pixels in <paramref name="border"/>.
    /// </summary>
    public static void RoundedPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, int radius, Color fill, Color border, int thickness)
    {
        RoundedFill(spriteBatch, pixel, area, radius, border);
        var inner = new Rectangle(area.X + thickness, area.Y + thickness, area.Width - (2 * thickness), area.Height - (2 * thickness));
        if (inner.Width > 0 && inner.Height > 0)
        {
            RoundedFill(spriteBatch, pixel, inner, Math.Max(0, radius - thickness), fill);
        }
    }

    /// <summary>Fills a rectangle with its corners cut in steps of a quarter circle.</summary>
    public static void RoundedFill(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area, int radius, Color color)
    {
        var r = Math.Min(radius, Math.Min(area.Width, area.Height) / 2);
        if (r <= 0)
        {
            spriteBatch.Draw(pixel, area, color);
            return;
        }

        spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y + r, area.Width, area.Height - (2 * r)), color);
        for (var i = 0; i < r; i++)
        {
            // The row i from the edge starts further in the nearer it is to the edge: the cut of a circle of radius r.
            var dy = r - i - 0.5;
            var inset = r - (int)Math.Round(Math.Sqrt((r * (double)r) - (dy * dy)));
            spriteBatch.Draw(pixel, new Rectangle(area.X + inset, area.Y + i, area.Width - (2 * inset), 1), color);
            spriteBatch.Draw(pixel, new Rectangle(area.X + inset, area.Bottom - 1 - i, area.Width - (2 * inset), 1), color);
        }
    }

    /// <summary>Draws a bar of the given length and thickness from a point at an angle (radians, 0 is to the right, positive turns clockwise on screen).</summary>
    public static void Bar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, float angle, float length, float thickness, Color color)
    {
        spriteBatch.Draw(pixel, from, null, color, angle, new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>Draws text with a one-pixel dark shadow so it reads on any background.</summary>
    public static void Text(SpriteBatch spriteBatch, SpriteFontBase font, string text, Vector2 position, Color color)
    {
        spriteBatch.DrawString(font, text, position + new Vector2(1, 1), new Color(0, 0, 0, 200));
        spriteBatch.DrawString(font, text, position, color);
    }

    /// <summary>
    /// Draws text with a one-pixel dark outline on all four sides (X08), for a label in the world that stands on whatever is behind it (the beacon's, an NPC's name):
    /// the plate behind the label does most of the work, the outline keeps the letters whole where the plate is translucent over a bright sky or water.
    /// </summary>
    public static void Outlined(SpriteBatch spriteBatch, SpriteFontBase font, string text, Vector2 position, Color color)
    {
        var outline = new Color(0, 0, 0, (int)(color.A * 0.85));
        foreach (var offset in new[] { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1), new Vector2(1, 1) })
        {
            spriteBatch.DrawString(font, text, position + offset, outline);
        }

        spriteBatch.DrawString(font, text, position, color);
    }

    /// <summary>
    /// Draws a stack count on its plate (<see cref="CountBadge"/>: white on an opaque dark plate, so it reads on any icon), with the plate's
    /// bottom-right corner at <paramref name="corner"/>.
    /// </summary>
    public static void Badge(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, string text, Point corner)
    {
        var width = (int)Width(font, text) + 6;
        var plate = new Rectangle(corner.X - width, corner.Y - 17, width, 17);
        var back = CountBadge.PlateColour;
        var front = CountBadge.TextColour;
        spriteBatch.Draw(pixel, plate, new Color(back.R, back.G, back.B));
        spriteBatch.DrawString(font, text, new Vector2(plate.X + 3, plate.Y + 1), new Color(front.R, front.G, front.B));
    }

    /// <summary>The text cut to <paramref name="maxWidth"/> pixels, ending in "..." when it is cut (a last resort for one line that has no room to wrap).</summary>
    public static string Fit(SpriteFontBase font, string text, int maxWidth)
    {
        if (Width(font, text) <= maxWidth)
        {
            return text;
        }

        var cut = text.Length;
        while (cut > 0 && Width(font, text[..cut] + "...") > maxWidth)
        {
            cut--;
        }

        return text[..cut] + "...";
    }

    /// <summary>The width of a text in pixels.</summary>
    public static float Width(SpriteFontBase font, string text) => font.MeasureString(text).X;
}
