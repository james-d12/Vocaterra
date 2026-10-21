namespace Vocaterra.Simulation.Tiles;

/// <summary>
/// A 32 x 32 block of foreground tiles, the unit of dirty tracking, saves and (later) lighting and rendering culls. Tiles are stored row by
/// row: index = localY * 32 + localX.
/// </summary>
public sealed class Chunk
{
    /// <summary>The chunk edge in tiles.</summary>
    public const int Size = 32;

    /// <summary>log2 of <see cref="Size"/>, for shifts.</summary>
    public const int Shift = 5;

    private readonly TileId[] _tiles = new TileId[Size * Size];

    internal Chunk(ChunkCoord coord) => Coord = coord;

    /// <summary>Where this chunk sits in the chunk grid.</summary>
    public ChunkCoord Coord { get; }

    /// <summary>The tiles, row by row.</summary>
    public ReadOnlySpan<TileId> Tiles => _tiles;

    /// <summary>Whether a tile in this chunk changed since the last <see cref="TileGrid.ClearDirty"/> (a save stores dirty chunks only).</summary>
    public bool Dirty { get; internal set; }

    /// <summary>The tile at the chunk-local position.</summary>
    public TileId this[int localX, int localY] => _tiles[(localY << Shift) + localX];

    internal void Set(int localX, int localY, TileId tile)
    {
        var index = (localY << Shift) + localX;
        if (_tiles[index] != tile)
        {
            _tiles[index] = tile;
            Dirty = true;
        }
    }
}
