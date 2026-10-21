using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vocaterra.Simulation.Tiles;

/// <summary>The foreground tiles of a world, stored in 32 x 32 chunks. Reading is public; writing stays inside the simulation.</summary>
public sealed class TileGrid
{
    private readonly Chunk[] _chunks;

    internal TileGrid(int width, int height)
    {
        if (width <= 0 || height <= 0 || width % Chunk.Size != 0 || height % Chunk.Size != 0)
        {
            throw new ArgumentException($"The grid size must be a positive multiple of {Chunk.Size} in both directions.");
        }

        Width = width;
        Height = height;
        ChunksX = width / Chunk.Size;
        ChunksY = height / Chunk.Size;
        _chunks = new Chunk[ChunksX * ChunksY];
        for (var cy = 0; cy < ChunksY; cy++)
        {
            for (var cx = 0; cx < ChunksX; cx++)
            {
                _chunks[(cy * ChunksX) + cx] = new Chunk(new ChunkCoord(cx, cy));
            }
        }
    }

    /// <summary>The width in tiles.</summary>
    public int Width { get; }

    /// <summary>The height in tiles.</summary>
    public int Height { get; }

    /// <summary>The number of chunk columns.</summary>
    public int ChunksX { get; }

    /// <summary>The number of chunk rows.</summary>
    public int ChunksY { get; }

    /// <summary>Every chunk, row by row (the order the digest and saves use).</summary>
    public IReadOnlyList<Chunk> Chunks => _chunks;

    /// <summary>Whether the tile position lies inside the world.</summary>
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>The tile at (<paramref name="x"/>, <paramref name="y"/>); <see cref="TileId.Air"/> outside the world.</summary>
    public TileId GetTile(int x, int y) => InBounds(x, y) ? ChunkAtTile(x, y)[x & (Chunk.Size - 1), y & (Chunk.Size - 1)] : TileId.Air;

    /// <summary>
    /// Whether bodies collide at the tile position. Everything outside the world counts as solid (side walls, floor and ceiling), so a body
    /// can never leave the grid.
    /// </summary>
    public bool IsSolid(int x, int y) => !InBounds(x, y) || TileDefinition.IsSolid(GetTile(x, y));

    /// <summary>The chunk at the given chunk coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The coordinates lie outside the chunk grid.</exception>
    public Chunk GetChunk(ChunkCoord coord)
    {
        if ((uint)coord.X >= (uint)ChunksX || (uint)coord.Y >= (uint)ChunksY)
        {
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "The chunk lies outside the world.");
        }

        return _chunks[(coord.Y * ChunksX) + coord.X];
    }

    /// <summary>Clears the dirty flag of every chunk.</summary>
    public void ClearDirty()
    {
        foreach (var chunk in _chunks)
        {
            chunk.Dirty = false;
        }
    }

    /// <summary>
    /// The SHA-256 of the tile arrays: the width and the height as 32-bit little-endian integers, then each chunk (row by row) as its 1 024
    /// tile ids, each a 16-bit little-endian integer. Goldens and the game's state hash use this.
    /// </summary>
    public byte[] ComputeDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, Width);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], Height);
        hash.AppendData(header);

        var buffer = new byte[Chunk.Size * Chunk.Size * sizeof(ushort)];
        foreach (var chunk in _chunks)
        {
            var tiles = chunk.Tiles;
            for (var i = 0; i < tiles.Length; i++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(i * sizeof(ushort)), (ushort)tiles[i]);
            }

            hash.AppendData(buffer);
        }

        return hash.GetHashAndReset();
    }

    internal void SetTile(int x, int y, TileId tile)
    {
        if (!InBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) lies outside the world.");
        }

        ChunkAtTile(x, y).Set(x & (Chunk.Size - 1), y & (Chunk.Size - 1), tile);
    }

    private Chunk ChunkAtTile(int x, int y) => _chunks[((y >> Chunk.Shift) * ChunksX) + (x >> Chunk.Shift)];
}
