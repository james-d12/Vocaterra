using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// The cover height of every column the rain can fall in (E06), cached by chunk column: <see cref="VoxelGrid.HighestBlock"/> once for each of a chunk column's 256 columns, and again only
/// when a block in that chunk column changes (the sum of its chunks' versions moves) or when the box first reaches it. <see cref="Refresh"/> makes the tiles round the camera current once a
/// frame; <see cref="TopAt"/> is then an array read. The top is <see cref="OverheadCover.Top"/>: the height of the highest block's top face, the y a drop stops at.
/// </summary>
public sealed class ColumnCover
{
    private const int Size = VoxelChunk.Size;

    private readonly VoxelGrid _grid;
    private readonly Tile?[] _tiles;

    /// <summary>Creates the cache over a grid (nothing is read until a column is asked for or <see cref="Refresh"/> runs).</summary>
    /// <param name="grid">The world.</param>
    public ColumnCover(VoxelGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _tiles = new Tile?[grid.ChunksX * grid.ChunksZ];
    }

    /// <summary>How many times a chunk column's tile was made or made again since the cache was created (for the tests: a quiet world refreshes none).</summary>
    public int Rebuilds { get; private set; }

    /// <summary>Makes current the tiles that overlap the square of <paramref name="halfExtent"/> blocks round a point: a new tile is read, a tile whose chunks changed is read again.</summary>
    /// <param name="centreX">The east-west centre of the square, in blocks.</param>
    /// <param name="centreZ">The north-south centre.</param>
    /// <param name="halfExtent">Half the square's side.</param>
    public void Refresh(double centreX, double centreZ, double halfExtent)
    {
        var cx0 = Math.Clamp((int)Math.Floor((centreX - halfExtent) / Size), 0, _grid.ChunksX - 1);
        var cx1 = Math.Clamp((int)Math.Floor((centreX + halfExtent) / Size), 0, _grid.ChunksX - 1);
        var cz0 = Math.Clamp((int)Math.Floor((centreZ - halfExtent) / Size), 0, _grid.ChunksZ - 1);
        var cz1 = Math.Clamp((int)Math.Floor((centreZ + halfExtent) / Size), 0, _grid.ChunksZ - 1);
        for (var cz = cz0; cz <= cz1; cz++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                _ = Current(cx, cz);
            }
        }
    }

    /// <summary>
    /// The height of the top face of the cover of the column at (<paramref name="x"/>, <paramref name="z"/>), in blocks: 0 where nothing stands (or outside the grid). A column outside the
    /// last <see cref="Refresh"/> is read when it is first asked for and not checked again until the next <see cref="Refresh"/> reaches it.
    /// </summary>
    /// <param name="x">The block column's east-west index.</param>
    /// <param name="z">The block column's north-south index.</param>
    public int TopAt(int x, int z)
    {
        if ((uint)x >= (uint)_grid.SizeX || (uint)z >= (uint)_grid.SizeZ)
        {
            return 0;
        }

        var cx = x >> VoxelChunk.Shift;
        var cz = z >> VoxelChunk.Shift;
        var tile = _tiles[(cz * _grid.ChunksX) + cx] ?? Current(cx, cz);
        return tile.Top[((z & (Size - 1)) * Size) + (x & (Size - 1))];
    }

    private Tile Current(int cx, int cz)
    {
        var index = (cz * _grid.ChunksX) + cx;
        var stamp = StampOf(cx, cz);
        var tile = _tiles[index];
        if (tile is not null && tile.Stamp == stamp)
        {
            return tile;
        }

        tile ??= new Tile();
        _tiles[index] = tile;
        tile.Stamp = stamp;
        Rebuilds++;
        for (var lz = 0; lz < Size; lz++)
        {
            for (var lx = 0; lx < Size; lx++)
            {
                tile.Top[(lz * Size) + lx] = OverheadCover.Top(_grid.HighestBlock((cx * Size) + lx, (cz * Size) + lz));
            }
        }

        return tile;
    }

    // Every edit of a chunk raises its version, so the sum over the column's chunks moves whenever any block of the chunk column changed.
    private long StampOf(int cx, int cz)
    {
        long sum = 0;
        for (var cy = 0; cy < _grid.ChunksY; cy++)
        {
            sum += _grid.GetChunk(new VoxelChunkCoord(cx, cy, cz)).Version;
        }

        return sum;
    }

    private sealed class Tile
    {
        public long Stamp { get; set; } = -1;

        public int[] Top { get; } = new int[Size * Size];
    }
}
