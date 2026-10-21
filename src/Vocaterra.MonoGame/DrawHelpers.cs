using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>Small drawing helpers over the 1 x 1 white texture and the font: rectangles, borders, rotated bars and text with a shadow.</summary>
public static class DrawHelpers
{
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

    /// <summary>The width of a text in pixels.</summary>
    public static float Width(SpriteFontBase font, string text) => font.MeasureString(text).X;
}
