using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game;
using Vocaterra.Simulation.Lighting;
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

    /// <summary>The colour of cave air at full light (warm, so a torch reads as a warm glow); dark where no light reaches.</summary>
    public static readonly Color CaveAir = new(70, 52, 38);

    private static readonly Color PlayerBody = new(230, 120, 40);
    private static readonly Color PlayerEye = new(255, 255, 255);
    private readonly TileAtlas _atlas;

    /// <summary>Creates the renderer over an atlas.</summary>
    public WorldRenderer(TileAtlas atlas)
    {
        _atlas = atlas;
    }

    /// <summary>
    /// The least brightness the player is drawn at, so a body in a dark cave stays visible (the player is not a tile and has no light of its
    /// own; it is lit by the light at its middle, but never less than this).
    /// </summary>
    public const double PlayerMinimumBrightness = 0.3;

    /// <summary>
    /// Draws the tiles that touch the viewport (nothing outside it) and the player box. Each tile is multiplied by its brightness (the light map,
    /// <see cref="Vocaterra.Simulation.Lighting.LightMap.Brightness"/>); open air that is not lit is drawn as a dark square over the sky, so a
    /// cave is black where no light reaches it.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, FrameView view)
    {
        var camera = view.Camera;
        var world = view.Simulation!.World;
        var light = view.Simulation.Light;
        var surface = world.Record.SurfaceHeights;
        var range = camera.VisibleTiles();
        var size = camera.PixelsPerTile;
        for (var ty = range.MinY; ty < range.MaxY; ty++)
        {
            for (var tx = range.MinX; tx < range.MaxX; tx++)
            {
                var id = world.GetTile(tx, ty);
                var brightness = light.Brightness(tx, ty) / (double)LightMap.MaxLevel;
                var (sx, sy) = camera.TileToScreen(tx, ty);
                var destination = new Rectangle(sx, sy, size, size);
                if (!TileDefinition.IsSolid(id))
                {
                    // What stands behind a tile that does not block: air above the ground is the sky (the clear colour, drawn at full
                    // brightness); air under it is cave air, which is dark and warms in torchlight.
                    var aboveGround = tx < 0 || tx >= surface.Count || ty < surface[tx];
                    if (!aboveGround)
                    {
                        spriteBatch.Draw(view.Pixel, destination, Darken(CaveAir, brightness));
                    }
                    else if (brightness < 1.0)
                    {
                        spriteBatch.Draw(view.Pixel, destination, Darken(Sky, brightness));
                    }

                    if (id == TileId.Air)
                    {
                        continue;
                    }
                }

                _atlas.DrawTile(spriteBatch, id, destination, view.Session.Words.SaturationOf(id), brightness);
            }
        }

        DrawPlayer(spriteBatch, view);
    }

    /// <summary>The sky colour at a brightness (0 to 1), opaque.</summary>
    public static Color Darken(Color sky, double brightness)
    {
        var b = Math.Clamp(brightness, 0.0, 1.0);
        return new Color((byte)Math.Round(sky.R * b), (byte)Math.Round(sky.G * b), (byte)Math.Round(sky.B * b), (byte)255);
    }

    private static void DrawPlayer(SpriteBatch spriteBatch, FrameView view)
    {
        var box = view.PlayerUnits;
        var (left, top) = view.Camera.UnitsToScreen(box.Left, box.Top);
        var (right, bottom) = view.Camera.UnitsToScreen(box.Right, box.Bottom);
        var body = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        var centre = WorldFacts.CenterTile(view.Simulation!.Player);
        var lit = view.Simulation.Light.Brightness(centre.X, centre.Y) / (double)LightMap.MaxLevel;
        var brightness = Math.Max(PlayerMinimumBrightness, lit);
        var scale = (float)brightness;
        spriteBatch.Draw(view.Pixel, body, new Color((byte)Math.Round(PlayerBody.R * scale), (byte)Math.Round(PlayerBody.G * scale), (byte)Math.Round(PlayerBody.B * scale)));

        // A small eye on the side the player faces, so the direction is visible.
        var eye = Math.Max(2, body.Width / 5);
        var eyeX = view.Facing >= 0 ? body.Right - (eye * 2) : body.Left + eye;
        spriteBatch.Draw(view.Pixel, new Rectangle(eyeX, body.Top + (body.Height / 6), eye, eye), PlayerEye);
    }
}
