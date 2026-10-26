using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// What a chunk's mesh was built from: the chunk's own <see cref="VoxelChunk.Version"/> and, for each of the six chunks next to it, a signature
/// of the layer of that chunk which touches this one (the faces on the edge of a chunk depend on the blocks across the edge, and on nothing else
/// outside the chunk). So an edit in the middle of a chunk makes that chunk due and no other, and an edit on an edge also makes the neighbour
/// across that edge due.
/// </summary>
/// <param name="Self">The chunk's version.</param>
/// <param name="East">The signature of the east neighbour's west layer (zero outside the grid).</param>
/// <param name="West">The signature of the west neighbour's east layer.</param>
/// <param name="Up">The signature of the upper neighbour's bottom layer.</param>
/// <param name="Down">The signature of the lower neighbour's top layer.</param>
/// <param name="South">The signature of the south neighbour's north layer.</param>
/// <param name="North">The signature of the north neighbour's south layer.</param>
public readonly record struct ChunkKey(uint Self, ulong East, ulong West, ulong Up, ulong Down, ulong South, ulong North);

/// <summary>
/// Remembers the <see cref="ChunkKey"/> each chunk was meshed at and says which chunks must be meshed again. A chunk never meshed is always due.
/// This is the re-meshing rule of the renderer, without any graphics type so it can be tested. The border signatures are computed when a chunk's
/// version has changed since they were last computed, not every frame.
/// </summary>
public sealed class ChunkVersionTracker
{
    private readonly VoxelGrid _grid;
    private readonly ChunkKey?[] _meshed;
    private readonly ulong[] _kinds; // the block kinds each chunk was meshed with (ChunkMeshes.Kinds)
    private readonly ulong[] _signatures; // six per chunk, in the order of BlockFace
    private readonly long[] _signedAt; // the chunk version the signatures belong to, or -1

    /// <summary>Creates a tracker for a grid; every chunk is due.</summary>
    public ChunkVersionTracker(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
        _meshed = new ChunkKey?[grid.Chunks.Count];
        _kinds = new ulong[grid.Chunks.Count];
        _signatures = new ulong[grid.Chunks.Count * 6];
        _signedAt = new long[grid.Chunks.Count];
        Array.Fill(_signedAt, -1L);
    }

    /// <summary>
    /// The block kinds drawn grey (one bit per <see cref="BlockId"/>, see <see cref="ChunkMesher.KindBit"/>): the mesh builds use it. It changes
    /// only through <see cref="SetGreyKinds"/>.
    /// </summary>
    public ulong GreyKinds { get; private set; }

    /// <summary>The index of a chunk in <see cref="VoxelGrid.Chunks"/>.</summary>
    public int IndexOf(VoxelChunkCoord coord) => (((coord.Y * _grid.ChunksZ) + coord.Z) * _grid.ChunksX) + coord.X;

    /// <summary>The key of a chunk as the grid is now.</summary>
    public ChunkKey KeyOf(VoxelChunkCoord coord) => new(
        _grid.GetChunk(coord).Version,
        Layer(coord.X + 1, coord.Y, coord.Z, BlockFace.West),
        Layer(coord.X - 1, coord.Y, coord.Z, BlockFace.East),
        Layer(coord.X, coord.Y + 1, coord.Z, BlockFace.Down),
        Layer(coord.X, coord.Y - 1, coord.Z, BlockFace.Up),
        Layer(coord.X, coord.Y, coord.Z + 1, BlockFace.North),
        Layer(coord.X, coord.Y, coord.Z - 1, BlockFace.South));

    /// <summary>The chunks whose mesh is missing or built from older blocks, in chunk order.</summary>
    public IReadOnlyList<VoxelChunkCoord> Due()
    {
        var due = new List<VoxelChunkCoord>();
        foreach (var chunk in _grid.Chunks)
        {
            if (_meshed[IndexOf(chunk.Coord)] != KeyOf(chunk.Coord))
            {
                due.Add(chunk.Coord);
            }
        }

        return due;
    }

    /// <summary>Whether a chunk's mesh is missing or built from older blocks (the same test as <see cref="Due"/>, for one chunk).</summary>
    public bool IsDue(VoxelChunkCoord coord) => _meshed[IndexOf(coord)] != KeyOf(coord);

    /// <summary>Whether the chunk has a mesh the tracker counts as current or stale (it was meshed and not let go since).</summary>
    public bool HasMesh(VoxelChunkCoord coord) => _meshed[IndexOf(coord)] is not null;

    /// <summary>Forgets a chunk's mesh (the renderer let it go because the chunk is out of view): it is due again if it comes back.</summary>
    public void Discard(VoxelChunkCoord coord) => _meshed[IndexOf(coord)] = null;

    /// <summary>Records that a chunk was meshed from the blocks as they are now, with the given block kinds (<see cref="ChunkMeshes.Kinds"/>).</summary>
    public void MarkMeshed(VoxelChunkCoord coord, ulong kinds = 0)
    {
        var index = IndexOf(coord);
        _meshed[index] = KeyOf(coord);
        _kinds[index] = kinds;
    }

    /// <summary>
    /// Sets the grey set (the greyscale A/B). A chunk that holds a block kind whose grey state changed is due again; the others are not. Setting
    /// the same set again changes nothing.
    /// </summary>
    /// <returns>How many chunks became due.</returns>
    public int SetGreyKinds(ulong greyKinds)
    {
        var changed = greyKinds ^ GreyKinds;
        GreyKinds = greyKinds;
        if (changed == 0)
        {
            return 0;
        }

        var due = 0;
        for (var index = 0; index < _kinds.Length; index++)
        {
            if ((_kinds[index] & changed) != 0 && _meshed[index] is not null)
            {
                _meshed[index] = null;
                due++;
            }
        }

        return due;
    }

    // The signature of the layer of the chunk at (x, y, z) that faces the given direction (zero outside the grid).
    private ulong Layer(int x, int y, int z, BlockFace face)
    {
        if ((uint)x >= (uint)_grid.ChunksX || (uint)y >= (uint)_grid.ChunksY || (uint)z >= (uint)_grid.ChunksZ)
        {
            return 0;
        }

        var coord = new VoxelChunkCoord(x, y, z);
        var chunk = _grid.GetChunk(coord);
        var index = IndexOf(coord);
        if (_signedAt[index] != chunk.Version)
        {
            for (var f = 0; f < 6; f++)
            {
                _signatures[(index * 6) + f] = Sign(chunk, (BlockFace)f);
            }

            _signedAt[index] = chunk.Version;
        }

        return _signatures[(index * 6) + (int)face];
    }

    // FNV-1a over the 256 block ids of one outer layer of the chunk, seeded so that an all-air layer is not zero.
    private static ulong Sign(VoxelChunk chunk, BlockFace face)
    {
        const int N = VoxelChunk.Size;
        var blocks = chunk.Blocks;
        var hash = 14695981039346656037UL;
        for (var a = 0; a < N; a++)
        {
            for (var b = 0; b < N; b++)
            {
                var index = face switch
                {
                    BlockFace.West => (((a * N) + b) * N) + 0, // x = 0: a is y, b is z
                    BlockFace.East => (((a * N) + b) * N) + (N - 1),
                    BlockFace.Down => (((0 * N) + a) * N) + b, // y = 0: a is z, b is x
                    BlockFace.Up => ((((N - 1) * N) + a) * N) + b,
                    BlockFace.North => (((a * N) + 0) * N) + b, // z = 0: a is y, b is x
                    _ => (((a * N) + (N - 1)) * N) + b,
                };
                hash = (hash ^ (ushort)blocks[index]) * 1099511628211UL;
            }
        }

        return hash | 1UL; // never zero, so a chunk outside the grid (zero) differs from every real layer
    }
}
