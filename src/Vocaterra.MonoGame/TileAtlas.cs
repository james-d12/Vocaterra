using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame;

/// <summary>
/// One texture holding every tile id's art in a row (id n at x = n * 16), made in code from <see cref="TileArt"/>. P4 builds a second atlas the
/// same way for the greyscale copy; the renderer only needs <see cref="Texture"/> and <see cref="Source"/>.
/// </summary>
public sealed class TileAtlas : IDisposable
{
    /// <summary>Builds the atlas on the given device.</summary>
    public TileAtlas(GraphicsDevice device)
    {
        var count = TileArt.TileCount;
        var width = count * TileArt.Size;
        var data = new byte[width * TileArt.Size * 4];
        for (var id = 0; id < count; id++)
        {
            var art = TileArt.Pixels((TileId)id);
            for (var y = 0; y < TileArt.Size; y++)
            {
                Array.Copy(art, y * TileArt.Size * 4, data, ((y * width) + (id * TileArt.Size)) * 4, TileArt.Size * 4);
            }
        }

        Texture = new Texture2D(device, width, TileArt.Size);
        Texture.SetData(data);
    }

    /// <summary>The atlas texture.</summary>
    public Texture2D Texture { get; }

    /// <summary>The source rectangle of a tile id's art.</summary>
    public static Rectangle Source(TileId id) => new((int)id * TileArt.Size, 0, TileArt.Size, TileArt.Size);

    /// <inheritdoc />
    public void Dispose() => Texture.Dispose();
}
