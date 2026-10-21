using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame;

/// <summary>The debug overlay: FPS, tick, player tile, seed, the tile under the mouse, and a Spanish test line that proves the font's accents.</summary>
public sealed class DebugOverlay : IFrameLayer
{
    private const int Margin = 8;
    private const int LineHeight = 20;

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var sim = view.Simulation!; // the 2D debug overlay: a 3D run draws VoxelDebugOverlay instead
        var mouseTile = view.Mouse is { } m ? view.Camera.ScreenToTile(m.X, m.Y) : (TilePoint?)null;
        var lines = DebugText.Lines(view.Fps, sim.Tick, sim.Player.FeetTile, view.Seed, mouseTile);
        var font = view.Fonts.Get(FontProvider.NativeSize);

        var width = 0;
        foreach (var line in lines)
        {
            width = Math.Max(width, (int)font.MeasureString(line).X);
        }

        var panel = new Rectangle(Margin / 2, Margin / 2, width + Margin, (lines.Count * LineHeight) + Margin);
        spriteBatch.Draw(view.Pixel, panel, new Color(0, 0, 0, 150));
        for (var i = 0; i < lines.Count; i++)
        {
            spriteBatch.DrawString(font, lines[i], new Vector2(Margin, Margin + (i * LineHeight)), Color.White);
        }
    }
}
