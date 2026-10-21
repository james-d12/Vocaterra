using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame;

/// <summary>Draws an item as a small icon (the hotbar, the inventory screen, the held item).</summary>
public interface IItemIcons
{
    /// <summary>Draws the icon of <paramref name="item"/> in <paramref name="destination"/> at a saturation (0 grey to 1 full colour).</summary>
    void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination, double saturation);
}

/// <summary>
/// The block atlas as a texture: <see cref="BlockArt"/> uploaded once, point sampled by the renderer. It also draws the 3D world's item icons
/// (a piece of wood, a berry, or the block an item places).
/// </summary>
public sealed class BlockAtlas : IItemIcons, IDisposable
{
    /// <summary>Builds the atlas on the given device.</summary>
    public BlockAtlas(GraphicsDevice device)
    {
        Texture = new Texture2D(device, BlockAtlasLayout.Width, BlockAtlasLayout.Height);
        Texture.SetData(BlockArt.AtlasPixels());
    }

    /// <summary>The atlas texture.</summary>
    public Texture2D Texture { get; }

    /// <summary>The source rectangle of a cell in pixels.</summary>
    public static Rectangle Source(BlockCell cell)
    {
        var (x, y, w, h) = BlockAtlasLayout.PixelRectangle(cell);
        return new Rectangle(x, y, w, h);
    }

    /// <inheritdoc />
    public void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination, double saturation)
    {
        // The world is drawn in full colour (D-game-6 amended), so the saturation is not used here.
        var cell = BlockAtlasLayout.IconFor(item) ?? BlockCell.White;
        spriteBatch.Draw(Texture, destination, Source(cell), cell == BlockCell.White ? new Color(200, 200, 215) : Color.White);
    }

    /// <inheritdoc />
    public void Dispose() => Texture.Dispose();
}
