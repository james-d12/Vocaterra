namespace Vocaterra.Game.Graphics.Water;

/// <summary>The pictures of the water strip: each in a still and a flowing form where it matters, the sheet of falling water last.</summary>
public enum WaterTile
{
    /// <summary>Deep still water: a slow shimmer, no direction.</summary>
    DeepStill,

    /// <summary>Deep flowing water: ripples that run along the picture's horizontal, turned in the mesh to the current.</summary>
    DeepFlow,

    /// <summary>Shallow still water (lighter and clearer than deep).</summary>
    ShallowStill,

    /// <summary>Shallow flowing water.</summary>
    ShallowFlow,

    /// <summary>The sheet of a waterfall: streaks that fall down the picture.</summary>
    Falling,
}

/// <summary>
/// Where the pictures lie in one frame of the water strip and how a point of a block face maps to a texture coordinate (water-plan 4.8). A frame is one
/// texture of five tiles side by side (<see cref="WaterTile"/>) and a second row of the same tiles in grey (the greyscale A/B). A tile is 16 pixels with an
/// eight-pixel gutter on every side holding the tile's own opposite edge, so a bilinear sample at the tile's edge blends with the picture's other side exactly
/// as if the tile repeated: the pictures are drawn to repeat, and a block can show any window of one (rotated to the current) without bleeding into its
/// neighbour. Pure arithmetic, no device, so the mesher and the tests share it.
/// </summary>
public static class WaterTileLayout
{
    /// <summary>The pixels of a tile's picture.</summary>
    public const int Cell = 16;

    /// <summary>
    /// The pixels of gutter on each side of a tile: eight, so the strip's mip levels down to 32 by 32 (one whole tile and its gutter, the sixth level) never
    /// average one tile's picture with its neighbour's.
    /// </summary>
    public const int Gutter = 8;

    /// <summary>The last mip level of the strip that can be used (<see cref="Pitch"/> is 2 to the power of this).</summary>
    public const int MaxMipLevel = 5;

    /// <summary>The pixels from one tile to the next.</summary>
    public const int Pitch = Cell + (2 * Gutter);

    /// <summary>The tiles in a row.</summary>
    public const int Columns = 5;

    /// <summary>The rows: colour, then grey.</summary>
    public const int Rows = 2;

    /// <summary>The width of a frame in pixels.</summary>
    public const int Width = Columns * Pitch;

    /// <summary>The height of a frame in pixels.</summary>
    public const int Height = Rows * Pitch;

    /// <summary>The tile for water of a depth, flowing or still.</summary>
    /// <param name="deep">Whether the water is deep (more than one block of it under the surface).</param>
    /// <param name="flowing">Whether the water has a current.</param>
    public static WaterTile For(bool deep, bool flowing) => (deep, flowing) switch
    {
        (true, false) => WaterTile.DeepStill,
        (true, true) => WaterTile.DeepFlow,
        (false, false) => WaterTile.ShallowStill,
        _ => WaterTile.ShallowFlow,
    };

    /// <summary>The texture coordinate of a point of a tile, given as the fraction across (<paramref name="p"/>, 0 to 1) and down (<paramref name="q"/>, 0 to 1) its picture.</summary>
    public static (float U, float V) Uv(WaterTile tile, bool grey, float p, float q)
    {
        var x = ((int)tile * Pitch) + Gutter + (Math.Clamp(p, 0f, 1f) * Cell);
        var y = ((grey ? 1 : 0) * Pitch) + Gutter + (Math.Clamp(q, 0f, 1f) * Cell);
        return (x / Width, y / Height);
    }

    /// <summary>The tile and colour a texture coordinate falls in (the inverse of <see cref="Uv"/>, for the tests).</summary>
    public static (WaterTile Tile, bool Grey) TileAt(float u, float v) => ((WaterTile)Math.Clamp((int)(u * Width / Pitch), 0, Columns - 1), v * Height / Pitch >= 1f);

    /// <summary>
    /// Turns a still tile's picture a different way in each block, so its sparkles do not line up in a lattice one to a block: by three bits of
    /// <paramref name="hash"/>, mirrored left to right, mirrored top to bottom, and turned a quarter (swapping across and down). The picture is the same
    /// kind of thing whichever way it lies, so every variant is a true window of it.
    /// </summary>
    /// <param name="hash">A number that is the same for the same block (a hash of its position).</param>
    /// <param name="p">The fraction across the picture.</param>
    /// <param name="q">The fraction down the picture.</param>
    public static (float P, float Q) Vary(uint hash, float p, float q)
    {
        if ((hash & 1) != 0)
        {
            p = 1f - p;
        }

        if ((hash & 2) != 0)
        {
            q = 1f - q;
        }

        return (hash & 4) != 0 ? (q, p) : (p, q);
    }

    /// <summary>
    /// Where a point of a block's surface lies in a tile's picture so that the picture's horizontal runs along the current. <paramref name="offsetX"/> and
    /// <paramref name="offsetZ"/> are the point's distance from the block's centre in blocks (-0.5 to 0.5). With no current (<paramref name="flowX"/> and
    /// <paramref name="flowZ"/> both zero) the picture is laid unturned, east to the right and south down. With one the picture is turned so its horizontal points
    /// downstream and scaled by 1 over |cos| + |sin| so the turned block still lies inside the tile: an east or south current uses the whole tile and
    /// repeats seamlessly from block to block, a diagonal one a window of it.
    /// </summary>
    public static (float P, float Q) Along(float offsetX, float offsetZ, int flowX, int flowZ)
    {
        if (flowX == 0 && flowZ == 0)
        {
            return (0.5f + offsetX, 0.5f + offsetZ);
        }

        var length = MathF.Sqrt(((float)flowX * flowX) + ((float)flowZ * flowZ));
        float dx = flowX / length, dz = flowZ / length;
        var along = (offsetX * dx) + (offsetZ * dz);
        var across = (offsetZ * dx) - (offsetX * dz);
        var scale = 1f / (MathF.Abs(dx) + MathF.Abs(dz));
        return (Math.Clamp(0.5f + (along * scale), 0f, 1f), Math.Clamp(0.5f + (across * scale), 0f, 1f));
    }
}
