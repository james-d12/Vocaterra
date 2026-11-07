using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The bubble of the breath meter (H04), drawn in code with no asset file (CC0): a round light-blue bubble with a rim and a glint, and an empty one as
/// a faint ghost of the same shape. A popping bubble is drawn smaller, centred in its box.
/// </summary>
public static class BubbleIcon
{
    private static readonly Color Water = new(120, 190, 255);
    private static readonly Color Rim = new(214, 238, 255);
    private static readonly Color Glint = new(255, 255, 255);
    private static readonly Color Ghost = new(150, 190, 220);

    /// <summary>Draws a full (or popping) bubble.</summary>
    /// <param name="spriteBatch">The batch the layer draws with.</param>
    /// <param name="pixel">The 1 x 1 white texture.</param>
    /// <param name="box">The bubble's box (a square).</param>
    /// <param name="size">The fraction of the box the bubble fills, 0 to 1.</param>
    /// <param name="alpha">The opacity, 0 to 1.</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, float size, float alpha)
    {
        var side = (int)Math.Round(box.Width * Math.Clamp(size, 0f, 1f));
        if (side < 2 || alpha <= 0f)
        {
            return;
        }

        var area = Centred(box, side);
        DrawHelpers.RoundedPanel(spriteBatch, pixel, area, side / 2, Fade(Water, alpha), Fade(Rim, alpha), 1);
        var glint = Math.Max(1, side / 5);
        spriteBatch.Draw(pixel, new Rectangle(area.X + (side / 4), area.Y + (side / 4), glint, glint), Fade(Glint, alpha));
    }

    /// <summary>Draws an empty bubble: a faint ghost of the full one, so the row shows its whole length.</summary>
    /// <param name="spriteBatch">The batch the layer draws with.</param>
    /// <param name="pixel">The 1 x 1 white texture.</param>
    /// <param name="box">The bubble's box (a square).</param>
    /// <param name="alpha">The opacity of the meter, 0 to 1.</param>
    public static void DrawEmpty(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        DrawHelpers.RoundedPanel(spriteBatch, pixel, box, box.Width / 2, Fade(Ghost, alpha * 0.25f), Fade(Ghost, alpha * 0.6f), 1);
    }

    private static Rectangle Centred(Rectangle box, int side) =>
        new(box.X + ((box.Width - side) / 2), box.Y + ((box.Height - side) / 2), side, side);

    // SpriteBatch blends premultiplied, so a fade scales the colour as well as its alpha (scaling only A would leave a faded colour at full strength).
    private static Color Fade(Color color, float alpha) => color * alpha;
}
