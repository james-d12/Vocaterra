using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame;

/// <summary>
/// Draws the visible tiles and the player. Reads the simulation and the word counts; owns no game state. A tile is drawn at the saturation of
/// the noun that names it (grey until the learner has used the word, then in colour in thirds; colourless tiles always in colour).
/// </summary>
public sealed class WorldRenderer
{
    /// <summary>The sky colour behind the tiles.</summary>
    public static readonly Color Sky = new(135, 190, 235);

    private static readonly Color PlayerBody = new(230, 120, 40);
    private static readonly Color PlayerEye = new(255, 255, 255);
    private readonly TileAtlas _atlas;

    /// <summary>Creates the renderer over an atlas.</summary>
    public WorldRenderer(TileAtlas atlas)
    {
        _atlas = atlas;
    }

    /// <summary>Draws the tiles that touch the viewport (nothing outside it) and the player box.</summary>
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var camera = view.Camera;
        var world = view.Simulation.World;
        var range = camera.VisibleTiles();
        var size = camera.PixelsPerTile;
        for (var ty = range.MinY; ty < range.MaxY; ty++)
        {
            for (var tx = range.MinX; tx < range.MaxX; tx++)
            {
                var id = world.GetTile(tx, ty);
                if (id == TileId.Air)
                {
                    continue;
                }

                var (sx, sy) = camera.TileToScreen(tx, ty);
                _atlas.DrawTile(spriteBatch, id, new Rectangle(sx, sy, size, size), view.Session.Words.SaturationOf(id));
            }
        }

        DrawPlayer(spriteBatch, view);
    }

    private static void DrawPlayer(SpriteBatch spriteBatch, FrameView view)
    {
        var box = view.PlayerUnits;
        var (left, top) = view.Camera.UnitsToScreen(box.Left, box.Top);
        var (right, bottom) = view.Camera.UnitsToScreen(box.Right, box.Bottom);
        var body = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        spriteBatch.Draw(view.Pixel, body, PlayerBody);

        // A small eye on the side the player faces, so the direction is visible.
        var eye = Math.Max(2, body.Width / 5);
        var eyeX = view.Facing >= 0 ? body.Right - (eye * 2) : body.Left + eye;
        spriteBatch.Draw(view.Pixel, new Rectangle(eyeX, body.Top + (body.Height / 6), eye, eye), PlayerEye);
    }
}
