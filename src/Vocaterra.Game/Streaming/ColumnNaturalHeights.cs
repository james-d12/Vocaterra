using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Streaming;

/// <summary>
/// The natural surface heights (<c>IPlaceInfo.NaturalSurfaceAt</c>) of the resident chunk columns (B44), made by the residency's column jobs on the workers, from the window the column was generated from, and kept here for the sky's exposure on the game thread: the
/// ground the world made is a pure function of the seed, so these heights never change (an edit does not move them), and the game thread's sky tile of a column is lookups over them and the short walk down the blocks, with no height model evaluated. Each column holds
/// the 18 x 18 block columns of its chunk column and the ring round it (1,296 bytes and an array header), put when the column's chunks are installed and removed when the plan lets the column go; about 2,000 columns are resident at High, so about 2.6 MB. Used on the
/// game loop's thread only.
/// </summary>
public sealed class ColumnNaturalHeights
{
    /// <summary>The columns along a tile's side: the chunk's 16 and one on each side.</summary>
    public const int Side = VoxelChunk.Size + 2;

    private readonly Dictionary<long, int[]> _tiles = [];
    private readonly int _limit;

    /// <summary>Creates an empty store.</summary>
    /// <param name="limit">The most columns held; past it the store is cleared (the sky's exposure then makes what it needs itself), a guard against a column that is never let go.</param>
    public ColumnNaturalHeights(int limit = 16_384) => _limit = limit;

    /// <summary>How many columns are held.</summary>
    public int Count => _tiles.Count;

    /// <summary>About how many bytes the heights take.</summary>
    public long Bytes => (long)_tiles.Count * ((Side * Side * sizeof(int)) + 24);

    /// <summary>The tile of a column (<see cref="Side"/> x <see cref="Side"/> heights, row by row from the block column one short of its corner), when it is held.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="tile">The heights.</param>
    public bool TryGet(int chunkX, int chunkZ, out int[] tile) => _tiles.TryGetValue(ColumnBandCache.KeyOf(chunkX, chunkZ), out tile!);

    /// <summary>Keeps the tile of a column.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    /// <param name="tile">The heights.</param>
    public void Put(int chunkX, int chunkZ, int[] tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (_tiles.Count >= _limit)
        {
            _tiles.Clear();
        }

        _tiles[ColumnBandCache.KeyOf(chunkX, chunkZ)] = tile;
    }

    /// <summary>Lets a column's tile go.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public void Remove(int chunkX, int chunkZ) => _ = _tiles.Remove(ColumnBandCache.KeyOf(chunkX, chunkZ));
}
