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
public sealed class TileAtlas : IDisposable
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
    /// to the saturation (unless it is fully grey). Premultiplied alpha, so <c>Color.White * s</c> fades the colour copy to s.
    /// </summary>
    public void DrawTile(SpriteBatch spriteBatch, TileId id, Rectangle destination, double saturation) =>
        DrawCell(spriteBatch, Source(id), destination, saturation);

    /// <summary>
    /// Draws an item's icon at a saturation (the colour of the noun that names it, see <c>WordProgress.SaturationOf</c>), with the same grey
    /// then colour rule as a tile. A material or a placed block shows its tile's art; the axe and the pickaxe have their own icons.
    /// </summary>
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination, double saturation)
    {
        var source = TileArt.TileOfItem(item) is { } tile
            ? Source(tile)
            : new Rectangle((TileArt.TileCount + (item == ItemId.Axe ? 0 : 1)) * TileArt.Size, 0, TileArt.Size, TileArt.Size);
        DrawCell(spriteBatch, source, destination, saturation);
    }

    private void DrawCell(SpriteBatch spriteBatch, Rectangle source, Rectangle destination, double saturation)
    {
        if (saturation < 1.0)
        {
            spriteBatch.Draw(Grey, destination, source, Color.White);
        }

        if (saturation > 0.0)
        {
            spriteBatch.Draw(Texture, destination, source, Color.White * (float)Math.Clamp(saturation, 0.0, 1.0));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Texture.Dispose();
        Grey.Dispose();
    }
}
