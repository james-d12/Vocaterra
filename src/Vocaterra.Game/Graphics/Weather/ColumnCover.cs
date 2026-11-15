using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// The cover height of every column the rain can fall in (E06), cached by chunk column in a sparse map (B04b: no array over the world; only the columns the rain reached have a tile):
/// the highest block of each of a chunk column's 256 columns, read from the chunks that are resident (nothing is generated), and read again only when a chunk of that column changes (the
/// sum of its chunks' versions, or which chunks are resident, moves) or when the box first reaches it. <see cref="Refresh"/> makes the tiles round the camera current once a
/// frame; <see cref="TopAt"/> is then a read. The top is <see cref="OverheadCover.Top"/>: the height of the highest block's top face, the y a drop stops at.
/// </summary>
public sealed class ColumnCover
{
    private const int Size = VoxelChunk.Size;

    private readonly VoxelGrid _grid;
    private readonly Dictionary<(int X, int Z), Tile> _tiles = [];
    private long _prunedAt = -1;

    /// <summary>Creates the cache over a grid (nothing is read until a column is asked for or <see cref="Refresh"/> runs).</summary>
    /// <param name="grid">The world.</param>
    public ColumnCover(VoxelGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
    }

    /// <summary>How many times a chunk column's tile was made or made again since the cache was created (for the tests: a quiet world refreshes none).</summary>
    public int Rebuilds { get; private set; }

    /// <summary>How many chunk columns have a tile now.</summary>
    public int TileCount => _tiles.Count;

    /// <summary>Makes current the tiles that overlap the square of <paramref name="halfExtent"/> blocks round a point: a new tile is read, a tile whose chunks changed is read again.</summary>
    /// <param name="centreX">The east-west centre of the square, in blocks.</param>
    /// <param name="centreZ">The north-south centre.</param>
    /// <param name="halfExtent">Half the square's side.</param>
    public void Refresh(double centreX, double centreZ, double halfExtent)
    {
        var limits = _grid.Limits;
        Prune();
        var cx0 = Math.Clamp((int)Math.Floor((centreX - halfExtent) / Size), limits.MinChunkX, limits.EndChunkX - 1);
        var cx1 = Math.Clamp((int)Math.Floor((centreX + halfExtent) / Size), limits.MinChunkX, limits.EndChunkX - 1);
        var cz0 = Math.Clamp((int)Math.Floor((centreZ - halfExtent) / Size), limits.MinChunkZ, limits.EndChunkZ - 1);
        var cz1 = Math.Clamp((int)Math.Floor((centreZ + halfExtent) / Size), limits.MinChunkZ, limits.EndChunkZ - 1);
        for (var cz = cz0; cz <= cz1; cz++)
        {
            for (var cx = cx0; cx <= cx1; cx++)
            {
                _ = Current(cx, cz);
            }
        }
    }

    /// <summary>
    /// The height of the top face of the cover of the column at (<paramref name="x"/>, <paramref name="z"/>), in blocks: the grid's lowest layer where nothing stands (0 on a bounded grid),
    /// and the same outside the grid. A column outside the last <see cref="Refresh"/> is read when it is first asked for and not checked again until the next <see cref="Refresh"/> reaches it.
    /// </summary>
    /// <param name="x">The block column's east-west index.</param>
    /// <param name="z">The block column's north-south index.</param>
    public int TopAt(int x, int z)
    {
        var limits = _grid.Limits;
        if ((uint)(x - limits.MinX) >= (uint)limits.SizeX || (uint)(z - limits.MinZ) >= (uint)limits.SizeZ)
        {
            return OverheadCover.Top(limits.MinY - 1);
        }

        var cx = x >> VoxelChunk.Shift;
        var cz = z >> VoxelChunk.Shift;
        var tile = _tiles.TryGetValue((cx, cz), out var found) ? found : Current(cx, cz);
        return tile.Top[((z & (Size - 1)) * Size) + (x & (Size - 1))];
    }

    private Tile Current(int cx, int cz)
    {
        var stamp = StampOf(cx, cz);
        if (_tiles.TryGetValue((cx, cz), out var tile) && tile.Stamp == stamp)
        {
            return tile;
        }

        if (tile is null)
        {
            tile = new Tile();
            _tiles[(cx, cz)] = tile;
        }

        tile.Stamp = stamp;
        Rebuilds++;
        var floor = _grid.Limits.MinY - 1;
        for (var lz = 0; lz < Size; lz++)
        {
            for (var lx = 0; lx < Size; lx++)
            {
                tile.Top[(lz * Size) + lx] = OverheadCover.Top(Highest(cx, cz, lx, lz, floor));
            }
        }

        return tile;
    }

    // The highest block of a column among the chunks that are resident (the floor, the layer under the grid, when there is none).
    private int Highest(int cx, int cz, int lx, int lz, int floor)
    {
        var limits = _grid.Limits;
        for (var cy = limits.EndChunkY - 1; cy >= limits.MinChunkY; cy--)
        {
            if (!_grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk) || chunk.IsEmpty)
            {
                continue;
            }

            for (var ly = Size - 1; ly >= 0; ly--)
            {
                if (chunk[lx, ly, lz] != BlockId.Air)
                {
                    return (cy << VoxelChunk.Shift) + ly;
                }
            }
        }

        return floor;
    }

    // Every edit of a chunk raises its version, so the sum over the column's resident chunks moves whenever any block of the chunk column changed; the count of resident chunks is
    // in it so that a chunk that comes (at version 0) or goes changes it too.
    private long StampOf(int cx, int cz)
    {
        var limits = _grid.Limits;
        long sum = 0;
        long resident = 0;
        for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
        {
            if (_grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk))
            {
                sum += chunk.Version;
                resident++;
            }
        }

        return (sum << 8) | resident;
    }

    // A streamed grid evicts chunks: a tile with no resident chunk left in its column is dropped, so the map holds only what is in memory (checked when residency moved).
    private void Prune()
    {
        if (_prunedAt == _grid.ResidencyVersion)
        {
            return;
        }

        _prunedAt = _grid.ResidencyVersion;
        var limits = _grid.Limits;
        List<(int X, int Z)>? gone = null;
        foreach (var key in _tiles.Keys)
        {
            var any = false;
            for (var cy = limits.MinChunkY; cy < limits.EndChunkY && !any; cy++)
            {
                any = _grid.IsResident(new VoxelChunkCoord(key.X, cy, key.Z));
            }

            if (!any)
            {
                (gone ??= []).Add(key);
            }
        }

        if (gone is not null)
        {
            foreach (var key in gone)
            {
                _tiles.Remove(key);
            }
        }
    }

    private sealed class Tile
    {
        public long Stamp { get; set; } = -1;

        public int[] Top { get; } = new int[Size * Size];
    }
}
