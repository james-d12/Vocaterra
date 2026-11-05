using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The needs' effects on the view (N05, design 9.9): dark edges at thirst 3 and above, a slow blink now and then at sleep 3 and above, red edges at very weak health. They are
/// gentle (<see cref="ViewEffectLevels"/>: nothing brighter than the world, nothing that flashes) and one option turns them off (F7) and leaves the icons and the lines.
/// Drawn under the interface, over the world.
/// </summary>
public sealed class ViewEffectsLayer : IFrameLayer
{
    private const int Bands = 14;

    private static readonly Color Dark = new(0, 0, 0);
    private static readonly Color Red = new(170, 20, 20);

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        if (view.Needs is not { Visible: true } hud)
        {
            return;
        }

        var levels = hud.Effects(view.Tick);
        if (levels.Blink > 0)
        {
            spriteBatch.Draw(view.Pixel, new Rectangle(0, 0, view.ViewportWidth, view.ViewportHeight), Dark * (float)levels.Blink);
        }

        if (levels.DimEdges > 0)
        {
            Edges(spriteBatch, view, Dark, (float)levels.DimEdges);
        }

        if (levels.RedEdges > 0)
        {
            Edges(spriteBatch, view, Red, (float)levels.RedEdges);
        }
    }

    // A vignette of bands along the four edges, strongest at the edge and easing to nothing a sixth of the way in.
    private static void Edges(SpriteBatch spriteBatch, FrameView view, Color colour, float level)
    {
        var width = view.ViewportWidth;
        var height = view.ViewportHeight;
        var depth = Math.Min(width, height) / 6;
        var step = Math.Max(1, depth / Bands);
        for (var band = 0; band < Bands; band++)
        {
            var fade = 1f - (band / (float)Bands);
            var alpha = level * fade * fade;
            var offset = band * step;
            var tint = colour * alpha;
            spriteBatch.Draw(view.Pixel, new Rectangle(0, offset, width, step), tint);
            spriteBatch.Draw(view.Pixel, new Rectangle(0, height - offset - step, width, step), tint);
            spriteBatch.Draw(view.Pixel, new Rectangle(offset, offset + step, step, height - (2 * (offset + step))), tint);
            spriteBatch.Draw(view.Pixel, new Rectangle(width - offset - step, offset + step, step, height - (2 * (offset + step))), tint);
        }
    }
}
