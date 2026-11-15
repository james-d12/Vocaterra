using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// Which chunks of a large world are worth meshing and drawing from where the eye is (S13): the ones whose box is within the view distance of the
/// eye, that are resident and that hold something at all. An empty chunk (the sky over the valley) is never in the set, nor is one the grid does not hold (it is read
/// by <see cref="VoxelGrid.TryGetChunk"/>, which never generates). The list is nearest first, and is rebuilt only when the eye moves to another chunk, a block is edited
/// or a chunk is installed or evicted, so asking for it every frame costs nothing. No graphics type is used, so it is tested without a window.
/// </summary>
public sealed class ChunkViewSet
{
    private const int Size = VoxelChunk.Size;
    private readonly VoxelGrid _grid;
    private List<VoxelChunkCoord> _wanted = [];
    private (int X, int Y, int Z) _eyeChunk = (int.MinValue, 0, 0);
    private long _editCount = -1;
    private long _residency = -1;
    private float _eyeX, _eyeY, _eyeZ;

    /// <summary>Creates the view set of a grid for a view distance in blocks.</summary>
    public ChunkViewSet(VoxelGrid grid, int viewDistance)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfLessThan(viewDistance, Size);
        _grid = grid;
        ViewDistance = viewDistance;
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
            EmptyChunks = 0;
            for (var cy = y0; cy <= y1; cy++)
            {
                for (var cz = z0; cz <= z1; cz++)
                {
                    for (var cx = x0; cx <= x1; cx++)
                    {
                        Consider(new VoxelChunkCoord(cx, cy, cz), limit, ordered, countEmpty: true);
                    }
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
