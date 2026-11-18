using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Climate;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The shaped window of a group of neighbouring chunk columns (B39): one climate fill, one height-model shape and one run of the shapers over a square of <see cref="Span"/> x <see cref="Span"/> chunk columns and
/// the margin round it, instead of one such window for each column (a column's own window is 42 columns wide for its 16, so neighbours compute the same climate and shape about 7 times over). A chunk column of
/// the group takes its own window out of it by a copy (<see cref="ColumnContext"/>), and so does its surface band: a window wider than the margin changes no chunk (the tests widen the margin and compare),
/// so what a column makes from the group equals what it makes alone. Immutable once made, so any number of threads may read it; about 125 bytes a column of the window (a group of 4 x 4 chunk columns holds a
/// window of 90 x 90 columns, about a megabyte).
/// </summary>
public sealed class ColumnGroup
{
    private const int BytesPerWindowColumn = 24 + 32 + 4 + 4; // a ClimateSample, a ColumnShape (padded), a height, a water level

    private readonly ClimateSample[] _climate;
    private readonly ColumnShape[] _shapes;
    private readonly int[] _heights;
    private readonly int[] _wet;
    private readonly int _size;
    private readonly int _originX;
    private readonly int _originZ;
    private readonly int _margin;

    internal ColumnGroup(ColumnContext shaped, int minChunkX, int minChunkZ, int span)
    {
        MinChunkX = minChunkX;
        MinChunkZ = minChunkZ;
        Span = span;
        _size = shaped.Size;
        _originX = shaped.OriginX;
        _originZ = shaped.OriginZ;
        _margin = shaped.World.Margin;
        _climate = shaped.Climate;
        _shapes = shaped.Shapes;
        _heights = shaped.Heights;
        _wet = shaped.Wet;
    }

    /// <summary>The first chunk column's x.</summary>
    public int MinChunkX { get; }

    /// <summary>The first chunk column's z.</summary>
    public int MinChunkZ { get; }

    /// <summary>How many chunk columns the group spans on a side.</summary>
    public int Span { get; }

    /// <summary>About how many bytes the group holds (its window's arrays).</summary>
    public long Bytes => (long)_size * _size * BytesPerWindowColumn;

    /// <summary>Whether a chunk column belongs to the group (its whole window lies in the group's).</summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public bool Contains(int chunkX, int chunkZ) => (uint)(chunkX - MinChunkX) < (uint)Span && (uint)(chunkZ - MinChunkZ) < (uint)Span;

    /// <summary>Copies the part of the group's window that a smaller window covers, row by row.</summary>
    internal void CopyWindowTo(int originX, int originZ, int size, ClimateSample[] climate, ColumnShape[] shapes, int[] heights, int[] wet)
    {
        var dx = originX - _originX;
        for (var j = 0; j < size; j++)
        {
            var from = ((originZ - _originZ + j) * _size) + dx;
            var to = j * size;
            Array.Copy(_climate, from, climate, to, size);
            Array.Copy(_shapes, from, shapes, to, size);
            Array.Copy(_heights, from, heights, to, size);
            Array.Copy(_wet, from, wet, to, size);
        }
    }

    /// <summary>The highest shaped height in the window of a chunk column of the group (what <see cref="ColumnContext.HighestInWindow"/> says of a context cut from it).</summary>
    internal int HighestInWindowOf(int chunkX, int chunkZ) => Extreme((chunkX << VoxelChunk.Shift) - _margin, (chunkZ << VoxelChunk.Shift) - _margin, VoxelChunk.Size + (2 * _margin), highest: true);

    /// <summary>The lowest shaped height among the own 16 x 16 columns of a chunk column of the group.</summary>
    internal int LowestOwnOf(int chunkX, int chunkZ) => Extreme(chunkX << VoxelChunk.Shift, chunkZ << VoxelChunk.Shift, VoxelChunk.Size, highest: false);

    private int Extreme(int x0, int z0, int width, bool highest)
    {
        var result = highest ? int.MinValue : int.MaxValue;
        for (var j = z0 - _originZ; j < z0 - _originZ + width; j++)
        {
            var row = (j * _size) - _originX;
            for (var x = x0; x < x0 + width; x++)
            {
                var h = _heights[row + x];
                result = highest ? Math.Max(result, h) : Math.Min(result, h);
            }
        }

        return result;
    }
}
