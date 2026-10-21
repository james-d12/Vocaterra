namespace Vocaterra.Simulation.Tiles;

/// <summary>A tile position. X grows to the right and Y grows downwards: row 0 is the top of the world.</summary>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
public readonly record struct TilePoint(int X, int Y);

/// <summary>A chunk position in the chunk grid (tile position divided by <see cref="Chunk.Size"/>, rounded down).</summary>
/// <param name="X">The chunk column.</param>
/// <param name="Y">The chunk row.</param>
public readonly record struct ChunkCoord(int X, int Y)
{
    /// <summary>The chunk that holds the tile at (<paramref name="tileX"/>, <paramref name="tileY"/>).</summary>
    public static ChunkCoord FromTile(int tileX, int tileY) => new(tileX >> Chunk.Shift, tileY >> Chunk.Shift);

    /// <summary>The tile at the top-left corner of this chunk.</summary>
    public TilePoint Origin => new(X << Chunk.Shift, Y << Chunk.Shift);
}
