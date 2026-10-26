using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// Which chunks of a large world are worth meshing and drawing from where the eye is (S13): the ones whose box is within the view distance of the
/// eye and that hold something at all. An empty chunk (the sky over the valley) is never in the set. The list is nearest first, and is rebuilt only
/// when the eye moves to another chunk or a block is edited, so asking for it every frame costs nothing. No graphics type is used, so it is tested
/// without a window.
/// </summary>
public sealed class ChunkViewSet
{
    private const int Size = VoxelChunk.Size;
    private readonly VoxelGrid _grid;
    private List<int> _wanted = [];
    private (int X, int Y, int Z) _eyeChunk = (int.MinValue, 0, 0);
    private long _editCount = -1;

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

    /// <summary>How many chunks of the grid are empty (no block but air): the ones the set never holds.</summary>
    public int EmptyChunks { get; private set; }

    /// <summary>
    /// The chunks (indices into <see cref="VoxelGrid.Chunks"/>) within the view distance of the eye that are not empty, nearest first (ties by
    /// index). The same list object is returned until the eye changes chunk or the grid is edited, when a new one is made.
    /// </summary>
    public IReadOnlyList<int> Update(float eyeX, float eyeY, float eyeZ)
    {
        var eyeChunk = (FloorChunk(eyeX), FloorChunk(eyeY), FloorChunk(eyeZ));
        if (eyeChunk == _eyeChunk && _editCount == _grid.EditCount)
        {
            return _wanted;
        }

        _eyeChunk = eyeChunk;
        _editCount = _grid.EditCount;
        _wanted = [];
        var reach = (ViewDistance / Size) + 1;
        var limit = (long)ViewDistance * ViewDistance;
        var ordered = new List<(long Distance, int Index)>();
        var empty = 0;
        foreach (var chunk in _grid.Chunks)
        {
            if (chunk.IsEmpty)
            {
                empty++;
            }
        }

        EmptyChunks = empty;
        for (var cy = Math.Max(0, eyeChunk.Item2 - reach); cy <= Math.Min(_grid.ChunksY - 1, eyeChunk.Item2 + reach); cy++)
        {
            for (var cz = Math.Max(0, eyeChunk.Item3 - reach); cz <= Math.Min(_grid.ChunksZ - 1, eyeChunk.Item3 + reach); cz++)
            {
                for (var cx = Math.Max(0, eyeChunk.Item1 - reach); cx <= Math.Min(_grid.ChunksX - 1, eyeChunk.Item1 + reach); cx++)
                {
                    var index = (((cy * _grid.ChunksZ) + cz) * _grid.ChunksX) + cx;
                    if (_grid.Chunks[index].IsEmpty)
                    {
                        continue;
                    }

                    var distance = DistanceSquared(eyeX, eyeY, eyeZ, cx, cy, cz);
                    if (distance <= limit)
                    {
                        ordered.Add((distance, index));
                    }
                }
            }
        }

        ordered.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Index.CompareTo(b.Index));
        foreach (var (_, index) in ordered)
        {
            _wanted.Add(index);
        }

        return _wanted;
    }

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
