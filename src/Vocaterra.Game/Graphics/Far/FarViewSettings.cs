namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The shape of the far view (B07, B07c; D-game-34 items 21 and 25): how far it reaches and how its level-of-detail tiles are laid out, the way the Distant Horizons mod does it. The land beyond the voxel chunks is drawn as square tiles of
/// <see cref="TileCells"/> cells each side; a tile at level L has cells of <c>1 &lt;&lt; L</c> blocks, so level 0 is the land block by block and each level doubles its cell and covers four times the ground with the same number of columns.
/// The tiles that are drawn are chosen by a quadtree from the roots of the top level: level L is used from <see cref="SplitDistance"/> of the eye out to the start of level L + 1, so the cells grow with distance (<see cref="BandStarts"/>, in
/// voxel view distances) and the whole is watertight (no gap or overlap between levels, skirts hiding the steps). Which tiles there are is a function of where the player stands and nothing else.
/// </summary>
/// <param name="Range">How far the land is drawn, in blocks from the eye; 0 turns the far view off.</param>
/// <param name="TileCells">The cells along a tile's side (the same at every level).</param>
/// <param name="Levels">How many levels there are (at least 1); the top level's tiles are the quadtree's roots. Eight: cells of 1, 2, 4, 8, 16, 32, 64 and 128 blocks.</param>
/// <param name="BandScale">What the bands are multiplied by: below 1 the coarser levels start nearer (less detail, cheaper), above 1 further out.</param>
/// <param name="InnerFraction">The fraction of the voxel view distance inside which no tile is drawn (the voxel chunks cover it); a little below 1, so a chunk at the edge of the voxel zone that is not meshed yet shows the far land and not the sky.</param>
/// <param name="MaxTiles">The most tiles kept (built and cached): past it the farthest unused ones are dropped.</param>
/// <param name="TreeLevels">The tiles of the levels below this get every tree as its blocks (the tree pass's own placement); the tiles above get a forest as a layer of leaves on a trunk. 0 draws no single tree.</param>
public sealed record FarViewSettings(int Range, int TileCells = 32, int Levels = 8, double BandScale = 1.0, double InnerFraction = 0.88, int MaxTiles = 4000, int TreeLevels = 4)
{
    /// <summary>Where each level starts, in voxel view distances: level 0 is the ring just past the voxel zone (to 1.1), then each level is used out to the start of the next.</summary>
    public static IReadOnlyList<double> BandStarts { get; } = [0, 1.1, 1.5, 2.5, 4.5, 8.5, 16.5, 32.5];

    /// <summary>The far view off.</summary>
    public static FarViewSettings Off { get; } = new(0);

    /// <summary>Whether the far view is drawn at all.</summary>
    public bool Enabled => Range > 0;

    /// <summary>The cell of a level in blocks.</summary>
    /// <param name="level">The level.</param>
    public int CellOf(int level) => 1 << level;

    /// <summary>The side of a tile of a level in blocks.</summary>
    /// <param name="level">The level.</param>
    public int SizeOf(int level) => TileCells * CellOf(level);

    /// <summary>The top level (the roots').</summary>
    public int TopLevel => Levels - 1;

    /// <summary>The distance from the eye, in blocks, at which a level starts: a tile of it nearer than this is split into the finer level's tiles (0 for level 0, which is never split).</summary>
    /// <param name="level">The level.</param>
    /// <param name="voxelViewDistance">The voxel zone's radius in blocks.</param>
    public double SplitDistance(int level, int voxelViewDistance) => level <= 0 ? 0 : BandStarts[Math.Min(level, BandStarts.Count - 1)] * voxelViewDistance * BandScale;

    /// <summary>How far a skirt hangs below its edge cell's ground, for a level: enough to hide the step to a coarser neighbour, whose own cells are twice as wide (the cells grow with the level, so does the step).</summary>
    /// <param name="level">The level.</param>
    public int SkirtOf(int level) => Math.Max(12, 4 * CellOf(level));

    /// <summary>The settings with another range (the preset's, or <c>--far-view</c>), the levels kept.</summary>
    /// <param name="range">The range in blocks; 0 for off.</param>
    public FarViewSettings WithRange(int range) => this with { Range = Math.Max(0, range) };
}
