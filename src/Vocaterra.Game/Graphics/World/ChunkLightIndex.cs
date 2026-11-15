using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The lanterns and post torches of the world (S15) indexed by chunk column (B04b): built from the chunks that are resident when it is made and as chunks are installed, and kept
/// current as a light block is placed or removed or a chunk is evicted, so the glow of a torch placed in play lights its surroundings at once. A chunk holds a light only if its
/// <see cref="VoxelChunk.KindMask"/> says so, so a chunk without one is never looked into. The glow itself (its reach and falloff) is <see cref="ChunkMesher"/>'s.
/// Lights are read in one fixed order, the order the one-off scan of the whole grid gave (chunk layer, row, column, then the block's y, z, x), so a mesh built from
/// <see cref="Near"/> is the mesh built from every light of the world.
/// </summary>
public sealed class ChunkLightIndex : IChunkResidencyListener, IBlockChangeListener
{
    private static readonly ulong LightKinds = ChunkMesher.KindBit(BlockId.Lantern) | ChunkMesher.KindBit(BlockId.PostTorch);

    private readonly VoxelGrid _grid;
    private readonly Dictionary<(int X, int Z), List<BlockPos>> _columns = [];
    private readonly List<BlockPos> _changed = [];
    private IReadOnlyList<BlockPos>? _all;

    /// <summary>Creates the index over a grid and registers it for installs, evictions and block changes. A bounded grid's resident chunks are read now; a streamed grid announces its chunks as it installs them.</summary>
    /// <param name="grid">The world.</param>
    public ChunkLightIndex(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
        if (!grid.IsStreamed)
        {
            var limits = grid.Limits;
            for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
            {
                for (var cz = limits.MinChunkZ; cz < limits.EndChunkZ; cz++)
                {
                    for (var cx = limits.MinChunkX; cx < limits.EndChunkX; cx++)
                    {
                        Scan(new VoxelChunkCoord(cx, cy, cz), recordChange: false);
                    }
                }
            }
        }

        grid.AddResidencyListener(this);
        grid.AddListener(this);
    }

    /// <summary>Stops listening to the grid.</summary>
    public void Detach()
    {
        _grid.RemoveResidencyListener(this);
        _grid.RemoveListener(this);
    }

    /// <summary>How many lights the index holds.</summary>
    public int Count { get; private set; }

    /// <summary>The number of chunk columns that hold at least one light.</summary>
    public int ColumnCount => _columns.Count;

    /// <summary>Every light, in the fixed order. The same list object is returned until a light is added or removed.</summary>
    public IReadOnlyList<BlockPos> All => _all ??= Sorted(_columns.Values.SelectMany(static l => l));

    /// <summary>
    /// The lights that can reach the chunk (those in its column and the eight beside it, the glow being shorter than a chunk), in the fixed order. <see cref="ChunkMesher"/> keeps the ones
    /// within its glow radius of the chunk's box.
    /// </summary>
    /// <param name="chunk">The chunk about to be meshed.</param>
    public IReadOnlyList<BlockPos> Near(VoxelChunkCoord chunk)
    {
        List<BlockPos>? found = null;
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (_columns.TryGetValue((chunk.X + dx, chunk.Z + dz), out var column))
                {
                    (found ??= []).AddRange(column);
                }
            }
        }

        return found is null ? [] : Sorted(found);
    }

    /// <summary>The positions of the lights that were added or removed since the last call (a placed or removed block, or a chunk that came or went), and forgets them: the chunks within reach of each are to be meshed again.</summary>
    public IReadOnlyList<BlockPos> TakeChanged()
    {
        if (_changed.Count == 0)
        {
            return [];
        }

        var taken = _changed.ToArray();
        _changed.Clear();
        return taken;
    }

    /// <summary>The chunks whose glow meshes a light at <paramref name="light"/> can change: those whose box lies within the glow radius of it (the test <see cref="ChunkMesher"/> makes).</summary>
    /// <param name="light">The light block.</param>
    public static IEnumerable<VoxelChunkCoord> ChunksReachedBy(BlockPos light)
    {
        const int Size = VoxelChunk.Size;
        var reach = (int)MathF.Ceiling(ChunkMesher.GlowRadius);
        for (var cy = Low(light.Y, reach); cy <= High(light.Y, reach); cy++)
        {
            for (var cz = Low(light.Z, reach); cz <= High(light.Z, reach); cz++)
            {
                for (var cx = Low(light.X, reach); cx <= High(light.X, reach); cx++)
                {
                    yield return new VoxelChunkCoord(cx, cy, cz);
                }
            }
        }

        static int Low(int v, int r) => (int)MathF.Ceiling((v + 0.5f - r - Size) / Size);
        static int High(int v, int r) => (int)MathF.Floor((v + 0.5f + r) / Size);
    }

    /// <inheritdoc />
    public void OnChunkInstalled(VoxelChunkCoord coord) => Scan(coord, recordChange: true);

    /// <inheritdoc />
    public void OnChunkEvicted(VoxelChunkCoord coord)
    {
        if (!_columns.TryGetValue((coord.X, coord.Z), out var column))
        {
            return;
        }

        var gone = column.Where(p => (p.Y >> VoxelChunk.Shift) == coord.Y).ToList();
        if (gone.Count == 0)
        {
            return;
        }

        column.RemoveAll(p => (p.Y >> VoxelChunk.Shift) == coord.Y);
        _changed.AddRange(gone);

        if (column.Count == 0)
        {
            _columns.Remove((coord.X, coord.Z));
        }

        Count -= gone.Count;
        _all = null;
    }

    /// <inheritdoc />
    public void OnBlockChanged(BlockChange change)
    {
        var was = IsLight(change.OldBlock);
        var now = IsLight(change.NewBlock);
        if (was == now)
        {
            return;
        }

        var position = change.Position;
        var key = (position.X >> VoxelChunk.Shift, position.Z >> VoxelChunk.Shift);
        if (now)
        {
            if (!_columns.TryGetValue(key, out var column))
            {
                _columns[key] = column = [];
            }

            column.Add(position);
            Count++;
        }
        else if (_columns.TryGetValue(key, out var column) && column.Remove(position))
        {
            if (column.Count == 0)
            {
                _columns.Remove(key);
            }

            Count--;
        }
        else
        {
            return;
        }

        _changed.Add(position);
        _all = null;
    }

    private static bool IsLight(BlockId block) => block is BlockId.Lantern or BlockId.PostTorch;

    // Reads the chunk's light blocks into its column (replacing what the column held of that chunk).
    private void Scan(VoxelChunkCoord coord, bool recordChange)
    {
        if (!_grid.TryGetChunk(coord, out var chunk) || (chunk.KindMask & LightKinds) == 0)
        {
            return;
        }

        var key = (coord.X, coord.Z);
        if (!_columns.TryGetValue(key, out var column))
        {
            _columns[key] = column = [];
        }

        var before = column.Count;
        column.RemoveAll(p => (p.Y >> VoxelChunk.Shift) == coord.Y);
        Count -= before - column.Count;
        var origin = coord.Origin;
        for (var y = 0; y < VoxelChunk.Size; y++)
        {
            for (var z = 0; z < VoxelChunk.Size; z++)
            {
                for (var x = 0; x < VoxelChunk.Size; x++)
                {
                    if (IsLight(chunk[x, y, z]))
                    {
                        var position = new BlockPos(origin.X + x, origin.Y + y, origin.Z + z);
                        column.Add(position);
                        Count++;
                        if (recordChange)
                        {
                            _changed.Add(position);
                        }
                    }
                }
            }
        }

        if (column.Count == 0)
        {
            _columns.Remove(key);
        }

        _all = null;
    }

    // The fixed order of the whole-grid scan: chunk layer, row, column, then block y, z, x.
    private static List<BlockPos> Sorted(IEnumerable<BlockPos> lights)
    {
        var list = lights.ToList();
        list.Sort(static (a, b) =>
        {
            int c;
            if ((c = (a.Y >> VoxelChunk.Shift).CompareTo(b.Y >> VoxelChunk.Shift)) != 0
                || (c = (a.Z >> VoxelChunk.Shift).CompareTo(b.Z >> VoxelChunk.Shift)) != 0
                || (c = (a.X >> VoxelChunk.Shift).CompareTo(b.X >> VoxelChunk.Shift)) != 0
                || (c = a.Y.CompareTo(b.Y)) != 0
                || (c = a.Z.CompareTo(b.Z)) != 0)
            {
                return c;
            }

            return a.X.CompareTo(b.X);
        });
        return list;
    }
}
