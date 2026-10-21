using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame;

/// <summary>
/// Two textures holding every tile id's art in a row (id n at x = n * 16), made in code from <see cref="TileArt"/>: the colour atlas, and its
/// greyscale copy made on the CPU when the atlas is built (<see cref="Greyscale"/>; no shader, so no Wine on Linux). A tile is drawn from the
/// grey copy first and then from the colour copy at its saturation (see <see cref="DrawTile"/>).
/// </summary>
public sealed class TileAtlas : IItemIcons, IDisposable
{
    /// <summary>Builds the atlas on the given device.</summary>
    public TileAtlas(GraphicsDevice device)
    {
        var count = TileArt.TileCount;
        var width = (count + TileArt.ToolCount) * TileArt.Size;
        var data = new byte[width * TileArt.Size * 4];
        for (var cell = 0; cell < count + TileArt.ToolCount; cell++)
        {
            var art = cell < count ? TileArt.Pixels((TileId)cell) : TileArt.ToolPixels(cell - count);
            for (var y = 0; y < TileArt.Size; y++)
            {
                Array.Copy(art, y * TileArt.Size * 4, data, ((y * width) + (cell * TileArt.Size)) * 4, TileArt.Size * 4);
            }
        }

        Texture = new Texture2D(device, width, TileArt.Size);
        Texture.SetData(data);
        Grey = new Texture2D(device, width, TileArt.Size);
        Grey.SetData(Greyscale.ToLuma(data));
    }

    /// <summary>The colour atlas.</summary>
    public Texture2D Texture { get; }

    /// <summary>The greyscale copy of the atlas, the same layout.</summary>
    public Texture2D Grey { get; }

    /// <summary>The source rectangle of a tile id's art.</summary>
    public static Rectangle Source(TileId id) => new((int)id * TileArt.Size, 0, TileArt.Size, TileArt.Size);

    /// <summary>
    /// Draws one tile at a saturation (0 to 1): the grey copy first (unless the tile is fully coloured), then the colour copy with alpha equal
    /// to the saturation (unless it is fully grey). Premultiplied alpha, so <c>Color.White * s</c> fades the colour copy to s. The
    /// <paramref name="brightness"/> (0 to 1, the tile's light) multiplies what the colour rule draws, so a dark tile is dark in grey and in colour.
    /// </summary>
    public void DrawTile(SpriteBatch spriteBatch, TileId id, Rectangle destination, double saturation, double brightness = 1.0) =>
        DrawCell(spriteBatch, Source(id), destination, saturation, brightness);

    /// <summary>
    /// The tint of one draw in premultiplied alpha: the colour is <paramref name="alpha"/> times <paramref name="brightness"/> and the alpha is
    /// <paramref name="alpha"/>, so a tile drawn at alpha s and brightness b comes out at b times its colour, blended at s.
    /// </summary>
    public static Color Tint(double alpha, double brightness)
    {
        var a = Math.Clamp(alpha, 0.0, 1.0);
        var rgb = a * Math.Clamp(brightness, 0.0, 1.0);
        return new Color((float)rgb, (float)rgb, (float)rgb, (float)a);
    }

    /// <summary>
    /// Draws an item's icon at a saturation (the colour of the noun that names it, see <c>WordProgress.SaturationOf</c>), with the same grey
    /// then colour rule as a tile. A material or a placed block shows its tile's art; the axe, the pickaxe and the sword have their own icons.
    /// </summary>
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination, double saturation)
    {
        var source = TileArt.TileOfItem(item) is { } tile
            ? Source(tile)
            : new Rectangle((TileArt.TileCount + TileArt.ToolIndexOf(item)) * TileArt.Size, 0, TileArt.Size, TileArt.Size);
        DrawCell(spriteBatch, source, destination, saturation, 1.0);
    }

    private void DrawCell(SpriteBatch spriteBatch, Rectangle source, Rectangle destination, double saturation, double brightness)
    {
        if (saturation < 1.0)
        {
            spriteBatch.Draw(Grey, destination, source, Tint(1.0, brightness));
        }

        if (saturation > 0.0)
        {
            spriteBatch.Draw(Texture, destination, source, Tint(saturation, brightness));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Texture.Dispose();
        Grey.Dispose();
    }
}
