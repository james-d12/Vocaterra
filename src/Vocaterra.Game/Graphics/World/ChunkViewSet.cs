using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// Which chunks of a large world are worth meshing and drawing from where the eye is (S13): the ones whose box is within the view distance of the
/// eye, that are resident and that hold something at all. An empty chunk (the sky over the valley) is never in the set, nor is one the grid does not hold (it is read
/// by <see cref="VoxelGrid.TryGetChunk"/>, which never generates). The list is nearest first, and is rebuilt only when the eye moves to another chunk, a block is edited
/// or a chunk is installed or evicted, so asking for it every frame costs nothing. No graphics type is used, so it is tested without a window.
/// On a streamed grid (B07) a chunk is listed only when all 26 chunks round it are resident too (the mesher reads the diagonal cells for the corner shading, three cells down for the water and nine
/// blocks across for a lantern's glow, and a chunk that is not resident reads as air, which would draw a wall at the seam of a half-loaded world); the readiness of a chunk is kept as chunks come and go,
/// the distance is across (the surface band, not the height, decides what is above and below), and at most <c>maxChunks</c> of the nearest are listed.
/// </summary>
public sealed class ChunkViewSet : IChunkResidencyListener
{
    private const int Size = VoxelChunk.Size;
    private readonly VoxelGrid _grid;
    private List<VoxelChunkCoord> _wanted = [];
    private (int X, int Y, int Z) _eyeChunk = (int.MinValue, 0, 0);
    private long _editCount = -1;
    private long _residency = -1;
    private float _eyeX, _eyeY, _eyeZ;
    private readonly int _maxChunks;
    private readonly HashSet<VoxelChunkCoord> _ready = [];

    /// <summary>Creates the view set of a grid for a view distance in blocks.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="viewDistance">The distance in blocks.</param>
    /// <param name="maxChunks">The most chunks the set lists (the nearest ones): a cap on the meshes of a streamed world. A bounded grid is never capped.</param>
    public ChunkViewSet(VoxelGrid grid, int viewDistance, int maxChunks = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfLessThan(viewDistance, Size);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChunks, 1);
        _grid = grid;
        ViewDistance = viewDistance;
        _maxChunks = maxChunks;
        if (grid.IsStreamed)
        {
            foreach (var coord in grid.ResidentChunks)
            {
                Refresh(coord);
            }

            grid.AddResidencyListener(this);
        }
    }

    /// <summary>Stops listening to the grid (the renderer made another view set, or is gone).</summary>
    public void Detach() => _grid.RemoveResidencyListener(this);

    /// <summary>How many chunks are ready to mesh on a streamed grid (resident, with all 26 neighbours resident or outside the world), whether or not they are in view.</summary>
    public int ReadyChunks => _ready.Count;

    /// <summary>Whether the chunk is resident with all its 26 neighbours (the world's edge counts as present): the condition for meshing it on a streamed grid.</summary>
    /// <param name="coord">The chunk.</param>
    public bool IsReady(VoxelChunkCoord coord) => _ready.Contains(coord);

    /// <inheritdoc />
    public void OnChunkInstalled(VoxelChunkCoord coord) => RefreshAround(coord);

    /// <inheritdoc />
    public void OnChunkEvicted(VoxelChunkCoord coord) => RefreshAround(coord);

    private void RefreshAround(VoxelChunkCoord coord)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    Refresh(new VoxelChunkCoord(coord.X + dx, coord.Y + dy, coord.Z + dz));
                }
            }
        }
    }

    // A chunk is ready when it is resident and so is everything round it (a chunk outside the limits is no chunk and cannot be missing).
    private void Refresh(VoxelChunkCoord coord)
    {
        if (!_grid.IsResident(coord))
        {
            _ = _ready.Remove(coord);
            return;
        }

        var limits = _grid.Limits;
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var n = new VoxelChunkCoord(coord.X + dx, coord.Y + dy, coord.Z + dz);
                    if ((dx != 0 || dy != 0 || dz != 0) && limits.Contains(n) && !_grid.IsResident(n))
                    {
                        _ = _ready.Remove(coord);
                        return;
                    }
                }
            }
        }

        _ = _ready.Add(coord);
    }

    /// <summary>The distance in blocks from the eye to the nearest point of a chunk's box within which the chunk is drawn.</summary>
    public int ViewDistance { get; }

    /// <summary>
    /// The distance beyond which a chunk's mesh is let go (the view distance and two chunks more), so walking about does not pile meshes up and a chunk at
    /// the edge of the view is not meshed again and again.
    /// </summary>
    public int KeepDistance => ViewDistance + (2 * Size);

    /// <summary>
    /// How many chunks of the grid are empty (no block but air): the ones the set never holds. On a bounded grid every chunk of the box counts, resident or not; on a streamed grid only
    /// the resident ones within reach of the eye are counted (the box is too large to walk).
    /// </summary>
    public int EmptyChunks { get; private set; }

    /// <summary>
    /// The chunks within the view distance of the eye that are resident and not empty, nearest first (ties by layer, row, column). The same list object is returned until the eye
    /// changes chunk, the grid is edited or a chunk comes or goes, when a new one is made.
    /// </summary>
    public IReadOnlyList<VoxelChunkCoord> Update(float eyeX, float eyeY, float eyeZ)
    {
        var eyeChunk = (FloorChunk(eyeX), FloorChunk(eyeY), FloorChunk(eyeZ));
        if (eyeChunk == _eyeChunk && _editCount == _grid.EditCount && _residency == _grid.ResidencyVersion)
        {
            return _wanted;
        }

        _eyeChunk = eyeChunk;
        (_eyeX, _eyeY, _eyeZ) = (eyeX, eyeY, eyeZ);
        _editCount = _grid.EditCount;
        _residency = _grid.ResidencyVersion;
        _wanted = [];
        var limits = _grid.Limits;
        var reach = (ViewDistance / Size) + 1;
        var limit = (long)ViewDistance * ViewDistance;
        var ordered = new List<(long Distance, VoxelChunkCoord Coord)>();
        var x0 = Math.Max(limits.MinChunkX, eyeChunk.Item1 - reach);
        var x1 = Math.Min(limits.EndChunkX - 1, eyeChunk.Item1 + reach);
        var y0 = Math.Max(limits.MinChunkY, eyeChunk.Item2 - reach);
        var y1 = Math.Min(limits.EndChunkY - 1, eyeChunk.Item2 + reach);
        var z0 = Math.Max(limits.MinChunkZ, eyeChunk.Item3 - reach);
        var z1 = Math.Min(limits.EndChunkZ - 1, eyeChunk.Item3 + reach);
        if (_grid.IsStreamed)
        {
            // Across, not up: a column's own band decides which of its layers are resident, so the layers above and below the eye are as far as the residency goes.
            EmptyChunks = 0;
            var across = (long)ViewDistance * ViewDistance;
            foreach (var coord in _grid.ResidentChunks)
            {
                if (Math.Abs(coord.X - eyeChunk.Item1) > reach || Math.Abs(coord.Z - eyeChunk.Item3) > reach)
                {
                    continue;
                }

                if (!_ready.Contains(coord) || !_grid.TryGetChunk(coord, out var chunk))
                {
                    continue;
                }

                if (chunk.IsEmpty)
                {
                    EmptyChunks++;
                    continue;
                }

                var distance = AcrossDistanceSquared(_eyeX, _eyeZ, coord.X, coord.Z);
                if (distance <= across)
                {
                    ordered.Add((distance + DepthGap(_eyeY, coord.Y), coord));
                }
            }
        }
        else
        {
            // A bounded grid is small enough to count its empty chunks (one for each of the box's chunks that holds nothing, resident or not).
            var total = (limits.EndChunkX - limits.MinChunkX) * (limits.EndChunkY - limits.MinChunkY) * (limits.EndChunkZ - limits.MinChunkZ);
            var filled = 0;
            for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
            {
                for (var cz = limits.MinChunkZ; cz < limits.EndChunkZ; cz++)
                {
                    for (var cx = limits.MinChunkX; cx < limits.EndChunkX; cx++)
                    {
                        var coord = new VoxelChunkCoord(cx, cy, cz);
                        if (!_grid.TryGetChunk(coord, out var chunk) || chunk.IsEmpty)
                        {
                            continue;
                        }

                        filled++;
                        if (cx >= x0 && cx <= x1 && cy >= y0 && cy <= y1 && cz >= z0 && cz <= z1)
                        {
                            Consider(coord, limit, ordered, countEmpty: false);
                        }
                    }
                }
            }

            EmptyChunks = total - filled;
        }

        ordered.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : CompareCoords(a.Coord, b.Coord));
        foreach (var (_, coord) in ordered)
        {
            if (_wanted.Count >= _maxChunks && _grid.IsStreamed)
            {
                break;
            }

            _wanted.Add(coord);
        }

        return _wanted;
    }

    private void Consider(VoxelChunkCoord coord, long limit, List<(long Distance, VoxelChunkCoord Coord)> ordered, bool countEmpty)
    {
        if (!_grid.TryGetChunk(coord, out var chunk))
        {
            return;
        }

        if (chunk.IsEmpty)
        {
            if (countEmpty)
            {
                EmptyChunks++;
            }

            return;
        }

        var distance = DistanceSquared(_eyeX, _eyeY, _eyeZ, coord.X, coord.Y, coord.Z);
        if (distance <= limit)
        {
            ordered.Add((distance, coord));
        }
    }

    /// <summary>The squared distance across, in blocks, from a point to the nearest point of the box of the chunk column (cx, cz).</summary>
    public static long AcrossDistanceSquared(float x, float z, int cx, int cz)
    {
        var dx = Gap(x, cx * Size, (cx + 1) * Size);
        var dz = Gap(z, cz * Size, (cz + 1) * Size);
        return (long)((dx * dx) + (dz * dz));
    }

    // How much farther up or down a layer is than the eye's own, squared, so that within one column the nearer layers come first and the order is the same on every run.
    private static long DepthGap(float eyeY, int layer)
    {
        var gap = (long)Math.Abs(((layer * Size) + (Size / 2)) - eyeY);
        return gap * gap / 256;
    }

    private static int CompareCoords(VoxelChunkCoord a, VoxelChunkCoord b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);

    /// <summary>The squared distance in blocks from a point to the nearest point of the box of chunk (cx, cy, cz).</summary>
    public static long DistanceSquared(float x, float y, float z, int cx, int cy, int cz)
    {
        var dx = Gap(x, cx * Size, (cx + 1) * Size);
        var dy = Gap(y, cy * Size, (cy + 1) * Size);
        var dz = Gap(z, cz * Size, (cz + 1) * Size);
        return (long)((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static float Gap(float value, int low, int high) => value < low ? low - value : value > high ? value - high : 0f;

    private static int FloorChunk(float coordinate) => (int)MathF.Floor(coordinate / Size);
}
