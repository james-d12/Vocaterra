using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

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
/// version has changed since they were last computed, not every frame. It is keyed by chunk coordinate and reads only chunks that are resident (a chunk that is not
/// is air with the key a chunk of air has, and nothing it reads makes the grid generate one), so it serves a streamed grid as well as a bounded one; registered as a
/// residency listener (<see cref="VoxelGrid.AddResidencyListener"/>) it forgets a chunk that is evicted.
/// </summary>
public sealed class ChunkVersionTracker : IChunkResidencyListener
{
    // The signature of a layer of air (the layer of a chunk that is not resident, or that holds nothing).
    private static readonly ulong AirSignature = Sign(new BlockId[VoxelChunk.Volume], default, BlockFace.West);

    private readonly VoxelGrid _grid;
    private readonly Dictionary<VoxelChunkCoord, Entry> _entries = [];

    /// <summary>Creates a tracker for a grid; every chunk is due.</summary>
    public ChunkVersionTracker(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
    }

    /// <summary>
    /// The block kinds drawn grey (one bit per <see cref="BlockId"/>, see <see cref="ChunkMesher.KindBit"/>): the mesh builds use it. It changes
    /// only through <see cref="SetGreyKinds"/>.
    /// </summary>
    public UInt128 GreyKinds { get; private set; }

    /// <summary>How many keys were worked out so far (<see cref="KeyOf"/>, seven lookups each): the renderer asks for a key only where something may have changed, and the tests count it.</summary>
    public long KeysComputed { get; private set; }

    /// <summary>The key of a chunk as the grid is now (a chunk that is not resident has the version 0 and layers of air on every side it would show).</summary>
    public ChunkKey KeyOf(VoxelChunkCoord coord)
    {
        KeysComputed++;
        return new ChunkKey(
            _grid.TryGetChunk(coord, out var chunk) ? chunk.Version : 0,
            Layer(coord.X + 1, coord.Y, coord.Z, BlockFace.West),
            Layer(coord.X - 1, coord.Y, coord.Z, BlockFace.East),
            Layer(coord.X, coord.Y + 1, coord.Z, BlockFace.Down),
            Layer(coord.X, coord.Y - 1, coord.Z, BlockFace.Up),
            Layer(coord.X, coord.Y, coord.Z + 1, BlockFace.North),
            Layer(coord.X, coord.Y, coord.Z - 1, BlockFace.South));
    }

    /// <summary>
    /// The chunks of a bounded grid whose mesh is missing or built from older blocks, in chunk order (layer, row, column), resident or not.
    /// </summary>
    /// <exception cref="NotSupportedException">The grid is streamed: name the chunks with <see cref="Due(IEnumerable{VoxelChunkCoord})"/>.</exception>
    public IReadOnlyList<VoxelChunkCoord> Due()
    {
        if (_grid.IsStreamed)
        {
            throw new NotSupportedException("A streamed grid has no fixed list of chunks; pass the chunks to Due(IEnumerable<VoxelChunkCoord>).");
        }

        var limits = _grid.Limits;
        return Due(Box(limits));
    }

    /// <summary>The chunks among <paramref name="candidates"/> whose mesh is missing or built from older blocks, in the order given.</summary>
    /// <param name="candidates">The chunks to ask about.</param>
    public IReadOnlyList<VoxelChunkCoord> Due(IEnumerable<VoxelChunkCoord> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var due = new List<VoxelChunkCoord>();
        foreach (var coord in candidates)
        {
            if (IsDue(coord))
            {
                due.Add(coord);
            }
        }

        return due;
    }

    /// <summary>Whether a chunk's mesh is missing or built from older blocks (the same test as <see cref="Due()"/>, for one chunk).</summary>
    public bool IsDue(VoxelChunkCoord coord) => !_entries.TryGetValue(coord, out var entry) || entry.Meshed != KeyOf(coord);

    /// <summary>Whether the chunk has a mesh the tracker counts as current or stale (it was meshed and not let go since).</summary>
    public bool HasMesh(VoxelChunkCoord coord) => _entries.TryGetValue(coord, out var entry) && entry.Meshed is not null;

    /// <summary>Forgets a chunk's mesh (the renderer let it go because the chunk is out of view): it is due again if it comes back.</summary>
    public void Discard(VoxelChunkCoord coord)
    {
        if (_entries.TryGetValue(coord, out var entry))
        {
            entry.Meshed = null;
        }
    }

    /// <summary>Forgets every mesh (the renderer let all of them go): every chunk is due again.</summary>
    public void DiscardAll()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Meshed = null;
        }
    }

    /// <summary>Records that a chunk was meshed from the blocks as they are now, with the given block kinds (<see cref="ChunkMeshes.Kinds"/>) and in the given light band.</summary>
    public void MarkMeshed(VoxelChunkCoord coord, UInt128 kinds = default, LightBand band = LightBand.Day)
    {
        var key = KeyOf(coord);
        var entry = EntryOf(coord);
        entry.Meshed = key;
        entry.Kinds = kinds;
        entry.Band = band;
    }

    /// <summary>The light band a chunk's mesh was built in (<see cref="LightBand.Day"/> for a chunk never meshed).</summary>
    public LightBand BandOf(VoxelChunkCoord coord) => _entries.TryGetValue(coord, out var entry) ? entry.Band : LightBand.Day;

    /// <summary>
    /// Whether a chunk has a mesh that was built in another light band than <paramref name="band"/> (E02): the mesh is still right for the blocks and is drawn with a tint
    /// until it is built again, so it is stale, not due. A chunk with no mesh is never stale (it is due).
    /// </summary>
    /// <param name="coord">The chunk.</param>
    /// <param name="band">The band in force now.</param>
    public bool IsBandStale(VoxelChunkCoord coord, LightBand band) => _entries.TryGetValue(coord, out var entry) && entry.Meshed is not null && entry.Band != band;

    /// <summary>
    /// Sets the grey set (the greyscale A/B). A chunk that holds a block kind whose grey state changed is due again; the others are not. Setting
    /// the same set again changes nothing.
    /// </summary>
    /// <returns>How many chunks became due.</returns>
    public int SetGreyKinds(UInt128 greyKinds)
    {
        var changed = greyKinds ^ GreyKinds;
        GreyKinds = greyKinds;
        if (changed == 0)
        {
            return 0;
        }

        var due = 0;
        foreach (var entry in _entries.Values)
        {
            if ((entry.Kinds & changed) != 0 && entry.Meshed is not null)
            {
                entry.Meshed = null;
                due++;
            }
        }

        return due;
    }

    /// <summary>
    /// Sets the grey set like <see cref="SetGreyKinds"/>, without making any chunk due: the meshes stay current in the tracker's eyes and the chunks that hold a kind whose grey state changed are put in <paramref name="affected"/>, for a
    /// renderer that builds them again in the background and keeps the old mesh on screen until then (B21).
    /// </summary>
    /// <param name="greyKinds">One bit per block kind; 0 draws the whole world in colour.</param>
    /// <param name="affected">Where to put the chunks to build again.</param>
    /// <returns>Whether the set changed.</returns>
    public bool ChangeGreyKinds(UInt128 greyKinds, ICollection<VoxelChunkCoord> affected)
    {
        ArgumentNullException.ThrowIfNull(affected);
        var changed = greyKinds ^ GreyKinds;
        GreyKinds = greyKinds;
        if (changed == 0)
        {
            return false;
        }

        foreach (var (coord, entry) in _entries)
        {
            if ((entry.Kinds & changed) != 0 && entry.Meshed is not null)
            {
                affected.Add(coord);
            }
        }

        return true;
    }

    /// <summary>Nothing to do: a chunk that comes in is due until it is meshed, and the chunks beside it see its layers in their keys.</summary>
    public void OnChunkInstalled(VoxelChunkCoord coord)
    {
    }

    /// <summary>Forgets what the tracker knows of the chunk (its mesh, its signatures).</summary>
    public void OnChunkEvicted(VoxelChunkCoord coord) => _entries.Remove(coord);

    private static IEnumerable<VoxelChunkCoord> Box(WorldLimits limits)
    {
        for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
        {
            for (var cz = limits.MinChunkZ; cz < limits.EndChunkZ; cz++)
            {
                for (var cx = limits.MinChunkX; cx < limits.EndChunkX; cx++)
                {
                    yield return new VoxelChunkCoord(cx, cy, cz);
                }
            }
        }
    }

    private Entry EntryOf(VoxelChunkCoord coord)
    {
        if (!_entries.TryGetValue(coord, out var entry))
        {
            _entries[coord] = entry = new Entry();
        }

        return entry;
    }

    // The signature of the layer of the chunk at (x, y, z) that faces the given direction: zero outside the grid, that of air where the chunk is not resident.
    private ulong Layer(int x, int y, int z, BlockFace face)
    {
        var coord = new VoxelChunkCoord(x, y, z);
        if (!_grid.Limits.Contains(coord))
        {
            return 0;
        }

        if (!_grid.TryGetChunk(coord, out var chunk))
        {
            return AirSignature;
        }

        var entry = EntryOf(coord);
        if (!ReferenceEquals(entry.Chunk, chunk) || entry.SignedAt != chunk.Version)
        {
            for (var f = 0; f < 6; f++)
            {
                entry.Signatures[f] = Sign(chunk.Blocks, chunk.WaterLevels, (BlockFace)f);
            }

            entry.Chunk = chunk;
            entry.SignedAt = chunk.Version;
        }

        return entry.Signatures[(int)face];
    }

    // FNV-1a over the 256 block ids of one outer layer of the chunk and, when the chunk holds flowing water, the 256 water levels, seeded so that an all-air layer is
    // not zero. The levels are in it because a water surface takes its corners' heights from the cells beside it: a level that changes on the edge of a chunk changes
    // how the chunk across the edge is drawn.
    private static ulong Sign(ReadOnlySpan<BlockId> blocks, ReadOnlySpan<byte> levels, BlockFace face)
    {
        const int N = VoxelChunk.Size;
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
                if (!levels.IsEmpty && levels[index] != 0)
                {
                    // Only the cells that hold a level count, with their place in the layer, so a layer with none signs exactly as it did before the chunk held any.
                    hash = (hash ^ (ulong)(((((a * N) + b) + 1) << 8) | levels[index])) * 1099511628211UL;
                }
            }
        }

        return hash | 1UL; // never zero, so a chunk outside the grid (zero) differs from every real layer
    }

    private sealed class Entry
    {
        public ChunkKey? Meshed { get; set; }

        public UInt128 Kinds { get; set; } // the block kinds the chunk was meshed with (ChunkMeshes.Kinds)

        public LightBand Band { get; set; } // the light band the chunk was meshed in (E02)

        public ulong[] Signatures { get; } = new ulong[6]; // in the order of BlockFace

        public VoxelChunk? Chunk { get; set; } // the chunk the signatures were read from

        public long SignedAt { get; set; } = -1; // the chunk version the signatures belong to
    }
}
