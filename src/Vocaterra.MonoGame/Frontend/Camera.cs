using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>A half-open rectangle of tile coordinates: columns <c>MinX..MaxX-1</c>, rows <c>MinY..MaxY-1</c>.</summary>
public readonly record struct TileRange(int MinX, int MinY, int MaxX, int MaxY)
{
    /// <summary>The number of columns.</summary>
    public int Width => Math.Max(0, MaxX - MinX);

    /// <summary>The number of rows.</summary>
    public int Height => Math.Max(0, MaxY - MinY);

    /// <summary>Whether the range holds no tile.</summary>
    public bool IsEmpty => Width == 0 || Height == 0;
}

/// <summary>
/// The camera as plain integer arithmetic: it follows a point given in simulation units (1/4096 tile), clamps to the world, and converts
/// between screen pixels, world pixels and tiles. <see cref="OriginX"/>/<see cref="OriginY"/> are the world pixel shown at the screen's top
/// left, always a whole pixel, so tiles never shimmer. When the world is smaller than the viewport on an axis it is centred on that axis.
/// The mouse-to-tile conversion for mining and building (P6) is <see cref="ScreenToTile"/>.
/// </summary>
public sealed class Camera
{
    private const int UnitsShift = 12;

    /// <summary>Creates a camera for a world of the given size in tiles.</summary>
    /// <param name="worldWidth">The world width in tiles.</param>
    /// <param name="worldHeight">The world height in tiles.</param>
    /// <param name="pixelsPerTile">The on-screen size of one tile (the art's size times the zoom).</param>
    public Camera(int worldWidth, int worldHeight, int pixelsPerTile)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(worldWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(worldHeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerTile, 1);
        WorldWidth = worldWidth;
        WorldHeight = worldHeight;
        PixelsPerTile = pixelsPerTile;
    }

    /// <summary>The world width in tiles.</summary>
    public int WorldWidth { get; }

    /// <summary>The world height in tiles.</summary>
    public int WorldHeight { get; }

    /// <summary>The on-screen size of one tile.</summary>
    public int PixelsPerTile { get; }

    /// <summary>The viewport width in pixels.</summary>
    public int ViewportWidth { get; private set; } = 1;

    /// <summary>The viewport height in pixels.</summary>
    public int ViewportHeight { get; private set; } = 1;

    /// <summary>The world pixel column shown at the left edge (negative when the world is narrower than the viewport).</summary>
    public int OriginX { get; private set; }

    /// <summary>The world pixel row shown at the top edge (negative when the world is shorter than the viewport).</summary>
    public int OriginY { get; private set; }

    /// <summary>Sets the viewport size in pixels; call <see cref="Follow"/> afterwards to re-clamp.</summary>
    public void Resize(int width, int height)
    {
        ViewportWidth = Math.Max(1, width);
        ViewportHeight = Math.Max(1, height);
    }

    /// <summary>Centres the view on a point in simulation units and clamps it to the world.</summary>
    /// <param name="centerUnitsX">The x of the point in units of 1/4096 tile.</param>
    /// <param name="centerUnitsY">The y of the point in units of 1/4096 tile.</param>
    public void Follow(long centerUnitsX, long centerUnitsY)
    {
        OriginX = ClampAxis(((centerUnitsX * PixelsPerTile) >> UnitsShift) - (ViewportWidth / 2), (long)WorldWidth * PixelsPerTile, ViewportWidth);
        OriginY = ClampAxis(((centerUnitsY * PixelsPerTile) >> UnitsShift) - (ViewportHeight / 2), (long)WorldHeight * PixelsPerTile, ViewportHeight);
    }

    /// <summary>The screen position of a point given in simulation units.</summary>
    public (int X, int Y) UnitsToScreen(long unitsX, long unitsY) =>
        ((int)((unitsX * PixelsPerTile) >> UnitsShift) - OriginX, (int)((unitsY * PixelsPerTile) >> UnitsShift) - OriginY);

    /// <summary>The screen position of a tile's top-left corner.</summary>
    public (int X, int Y) TileToScreen(int tileX, int tileY) => ((tileX * PixelsPerTile) - OriginX, (tileY * PixelsPerTile) - OriginY);

    /// <summary>The tile under a screen pixel; it may lie outside the world (check <c>TileGrid.InBounds</c>).</summary>
    public TilePoint ScreenToTile(int screenX, int screenY) =>
        new(FloorDiv(screenX + OriginX, PixelsPerTile), FloorDiv(screenY + OriginY, PixelsPerTile));

    /// <summary>The tiles that touch the viewport, clipped to the world.</summary>
    public TileRange VisibleTiles()
    {
        var minX = Math.Clamp(FloorDiv(OriginX, PixelsPerTile), 0, WorldWidth);
        var minY = Math.Clamp(FloorDiv(OriginY, PixelsPerTile), 0, WorldHeight);
        var maxX = Math.Clamp(CeilDiv(OriginX + ViewportWidth, PixelsPerTile), 0, WorldWidth);
        var maxY = Math.Clamp(CeilDiv(OriginY + ViewportHeight, PixelsPerTile), 0, WorldHeight);
        return new TileRange(minX, minY, Math.Max(minX, maxX), Math.Max(minY, maxY));
    }

    /// <summary>The chunks that touch the viewport, as a half-open range of chunk coordinates.</summary>
    public TileRange VisibleChunks()
    {
        var tiles = VisibleTiles();
        if (tiles.IsEmpty)
        {
            return new TileRange(0, 0, 0, 0);
        }

        return new TileRange(
            tiles.MinX >> Chunk.Shift,
            tiles.MinY >> Chunk.Shift,
            ((tiles.MaxX - 1) >> Chunk.Shift) + 1,
            ((tiles.MaxY - 1) >> Chunk.Shift) + 1);
    }

    private static int ClampAxis(long origin, long worldPixels, int viewport)
    {
        var room = worldPixels - viewport;
        return (int)(room <= 0 ? room >> 1 : Math.Clamp(origin, 0, room));
    }

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);

    private static int CeilDiv(int value, int divisor) => (int)Math.Ceiling((double)value / divisor);
}
