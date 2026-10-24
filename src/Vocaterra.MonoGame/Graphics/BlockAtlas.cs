using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Simulation.Items;

namespace Vocaterra.MonoGame.Graphics;

/// <summary>Draws an item as a small icon (the hotbar, the inventory screen, the held item).</summary>
public interface IItemIcons
{
    /// <summary>Draws the icon of <paramref name="item"/> in <paramref name="destination"/> at a saturation (0 grey to 1 full colour).</summary>
    void DrawIcon(SpriteBatch spriteBatch, ItemId item, Rectangle destination, double saturation);

    /// <summary>Draws a side of a block as an icon (S22: the book's nouns that name a block and no item, <c>árbol</c> or <c>río</c>), grey at saturation 0.</summary>
    void DrawBlock(SpriteBatch spriteBatch, Vocaterra.Simulation.Voxel.BlockId block, Rectangle destination, double saturation);
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
        Texture = new Texture2D(device, BlockAtlasLayout.Width, BlockAtlasLayout.Height, true, SurfaceFormat.Color);
        var level0 = BlockArt.AtlasPixels();
        for (var level = 0; level < Math.Min(Texture.LevelCount, BlockArt.MipLevelCount); level++)
        {
            var pixels = BlockArt.MipLevel(level0, level);
            Texture.SetData(level, null, pixels, 0, pixels.Length);
        }
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
        // The world is drawn in full colour (D-game-6 amended): an icon is in colour at any saturation above 0. A caller that wants it grey (the book in
        // --greyscale, for a noun not known yet, S22) passes 0 and gets the cell's grey copy.
        DrawCell(spriteBatch, BlockAtlasLayout.IconFor(item) ?? BlockCell.White, destination, saturation);
    }

    /// <inheritdoc />
    public void DrawBlock(SpriteBatch spriteBatch, Vocaterra.Simulation.Voxel.BlockId block, Rectangle destination, double saturation) =>
        DrawCell(spriteBatch, BlockAtlasLayout.CellFor(block, Vocaterra.Simulation.Voxel.BlockFace.East), destination, saturation);

    private void DrawCell(SpriteBatch spriteBatch, BlockCell cell, Rectangle destination, double saturation)
    {
        var (x, y, w, h) = saturation <= 0 ? BlockAtlasLayout.GreyPixelRectangle(cell) : BlockAtlasLayout.PixelRectangle(cell);
        var colour = cell == BlockCell.White ? new Color(200, 200, 215) : Color.White;
        if (UiTransform.IsIdentity)
        {
            spriteBatch.Draw(Texture, destination, new Rectangle(x, y, w, h), colour);
            return;
        }

        // Under the interface scale the picture is drawn in window pixels (UiTransform): the batch is flushed, the picture drawn untransformed, and the batch resumed in order.
        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(Texture, UiTransform.ToWindow(destination), new Rectangle(x, y, w, h), colour);
        spriteBatch.End();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform.Matrix);
    }

    /// <inheritdoc />
    public void Dispose() => Texture.Dispose();
}
