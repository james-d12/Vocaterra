namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// B47: how a far tile's faces are sorted so that any set of whole leaves' footprints can be drawn on its own, in MonoGame's effects as in the game's own program (neither can clip a fragment to a rectangle without a shader of its own, and the
/// back buffer has no stencil). The tile's ground is cut into a square of <see cref="Side"/> granules a side, each <see cref="Blocks"/> blocks wide (the finest leaf's footprint, 32 blocks, or two cells of the tile's level where a cell is 16 blocks or more: the finest leaf
/// that can stand in the place of a tile's own is four levels under it, and its footprint is two of the tile's cells); the mesher keeps every quad inside one granule (a merged run of faces stops at a granule's edge; a wall belongs to the granule of the voxel it is a face of)
/// and writes each bucket's quads granule by granule, row after row, so the faces of a row of granules are one run of indices: a window (a rectangle of granules) is one run per row of it. Nothing is clipped on the card, so the ground a window leaves out is not drawn at all.
/// </summary>
public sealed class FarGranules
{
    /// <summary>The finest footprint a window can have (a level 0 leaf's side), in blocks: no granule is narrower.</summary>
    public const int FinestBlocks = 32;

    /// <summary>How a tile's ground is cut: where it starts, the granule's side in blocks and how many granules there are along a side.</summary>
    /// <param name="OriginX">The tile's first block column.</param>
    /// <param name="OriginZ">The tile's first block row.</param>
    /// <param name="Blocks">A granule's side in blocks.</param>
    /// <param name="Side">The granules along a side.</param>
    public readonly record struct Shape(int OriginX, int OriginZ, int Blocks, int Side);

    private readonly int[] _starts;

    /// <summary>Makes the table of a tile.</summary>
    /// <param name="originX">The tile's first block column.</param>
    /// <param name="originZ">The tile's first block row.</param>
    /// <param name="blocks">A granule's side in blocks.</param>
    /// <param name="side">How many granules there are along a side.</param>
    /// <param name="starts">For each bucket (opaque, lit, water, and B51 the cut edges' walls) and each granule in row order, the index at which its quads start, and after them the index after the bucket's last: <c>4 * (side * side + 1)</c> entries.</param>
    public FarGranules(int originX, int originZ, int blocks, int side, int[] starts)
    {
        ArgumentNullException.ThrowIfNull(starts);
        if (starts.Length != 4 * ((side * side) + 1))
        {
            throw new ArgumentException("a start for each granule of each bucket and the end of each bucket", nameof(starts));
        }

        OriginX = originX;
        OriginZ = originZ;
        Blocks = blocks;
        Side = side;
        _starts = starts;
    }

    /// <summary>The tile's first block column.</summary>
    public int OriginX { get; }

    /// <summary>The tile's first block row.</summary>
    public int OriginZ { get; }

    /// <summary>A granule's side in blocks.</summary>
    public int Blocks { get; }

    /// <summary>The granules along a side.</summary>
    public int Side { get; }

    /// <summary>A granule's side in blocks for a level: the finest leaf's footprint, but two cells at least.</summary>
    /// <param name="settings">The far view's settings.</param>
    /// <param name="level">The tile's level.</param>
    public static int BlocksOf(FarViewSettings settings, int level)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Math.Max(FinestBlocks, 2 * settings.CellOf(level));
    }

    /// <summary>A granule's side in the tile's own cells (at least two, so a run of faces can still merge a little).</summary>
    /// <param name="settings">The far view's settings.</param>
    /// <param name="level">The tile's level.</param>
    public static int CellsOf(FarViewSettings settings, int level) => BlocksOf(settings, level) / settings.CellOf(level);

    /// <summary>How many granules there are along a side of a tile of a level.</summary>
    /// <param name="settings">The far view's settings.</param>
    /// <param name="level">The tile's level.</param>
    public static int SideOf(FarViewSettings settings, int level) => settings.TileCells / CellsOf(settings, level);

    /// <summary>Whether a leaf of a level has a footprint of whole granules of a tile of another level (the leaf is no finer than the granule): the windows of a stand-in can be drawn exactly only for such leaves.</summary>
    /// <param name="settings">The far view's settings.</param>
    /// <param name="tileLevel">The stand-in's level.</param>
    /// <param name="leafLevel">The level of a leaf under it.</param>
    public static bool Fits(FarViewSettings settings, int tileLevel, int leafLevel) => settings.SizeOf(leafLevel) >= BlocksOf(settings, tileLevel);

    /// <summary>The first index of a bucket's faces and the index after the last.</summary>
    /// <param name="bucket">The bucket.</param>
    public (int First, int End) Bucket(FarBucket bucket)
    {
        var width = (Side * Side) + 1;
        return (_starts[((int)bucket * width) + 0], _starts[((int)bucket * width) + (Side * Side)]);
    }

    /// <summary>The runs of a bucket's indices that draw the ground inside a set of windows: one run for each row of granules of each window (neighbouring runs joined). A granule is drawn when its centre is in a window; windows are whole leaves' footprints, so a granule is in one wholly or not at all.</summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="windows">The windows, in blocks.</param>
    /// <param name="into">Receives the runs (first index, index count), emptied first.</param>
    /// <param name="append">Whether to add to <paramref name="into"/> instead of emptying it first (B51: the opaque land's runs, then the cut edges' walls).</param>
    public void Runs(FarBucket bucket, IReadOnlyList<FarWindow> windows, List<(int First, int Count)> into, bool append = false)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(into);
        if (!append)
        {
            into.Clear();
        }

        var width = (Side * Side) + 1;
        var baseAt = (int)bucket * width;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            var x0 = Math.Clamp(CeilDiv(w.MinX - OriginX, Blocks), 0, Side);
            var x1 = Math.Clamp((w.MaxX - OriginX) / Blocks, 0, Side);
            var z0 = Math.Clamp(CeilDiv(w.MinZ - OriginZ, Blocks), 0, Side);
            var z1 = Math.Clamp((w.MaxZ - OriginZ) / Blocks, 0, Side);
            if (x1 <= x0)
            {
                continue;
            }

            for (var z = z0; z < z1; z++)
            {
                var first = _starts[baseAt + (z * Side) + x0];
                var end = _starts[baseAt + (z * Side) + x1];
                if (end <= first)
                {
                    continue;
                }

                if (into.Count > 0 && into[^1].First + into[^1].Count == first)
                {
                    into[^1] = (into[^1].First, into[^1].Count + (end - first));
                }
                else
                {
                    into.Add((first, end - first));
                }
            }
        }
    }

    // A window's edge is a leaf's edge, a whole number of granules from the tile's; rounding the start up and the end down keeps a window that is not (it would be drawn whole by the view) from reaching into its neighbour.
    private static int CeilDiv(int value, int by) => value <= 0 ? -((-value) / by) : (value + by - 1) / by;
}
