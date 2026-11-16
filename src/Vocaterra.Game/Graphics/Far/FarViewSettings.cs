namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The shape of the far view (B07, D-game-34 item 21): how far it reaches and how its level-of-detail terrain tiles are laid out. The land beyond the voxel chunks is drawn as square tiles of
/// <see cref="TileCells"/> cells each side; a tile at level L has cells of <c>BaseCell &lt;&lt; L</c> blocks, so a level covers twice the ground of the one below it with the same number of vertices. The
/// tiles that are drawn are chosen by a quadtree from the roots of the top level: a tile is split into its four children while it is nearer than <see cref="SplitFactor"/> of its own size, so the
/// cells grow with distance and the whole is watertight (no gap or overlap between levels, skirts hiding the steps). Which tiles there are is a function of where the player stands and nothing else.
/// </summary>
/// <param name="Range">How far the land is drawn, in blocks from the eye; 0 turns the far view off.</param>
/// <param name="BaseCell">The cell, in blocks, of the finest level (level 0).</param>
/// <param name="TileCells">The cells along a tile's side (the same at every level).</param>
/// <param name="Levels">How many levels there are (at least 1); the top level's tiles are the quadtree's roots.</param>
/// <param name="SplitFactor">A tile is split while the ground distance from the eye to it is under this many times its size.</param>
/// <param name="InnerFraction">The fraction of the voxel view distance inside which no tile is drawn (the voxel chunks cover it); below 1 the tiles reach in under the chunks, so a chunk not loaded yet shows the far land and not the sky.</param>
/// <param name="MaxTiles">The most tiles kept (built and cached): past it the farthest unused ones are dropped.</param>
public sealed record FarViewSettings(int Range, int BaseCell = 4, int TileCells = 32, int Levels = 5, double SplitFactor = 2.0, double InnerFraction = 0.5, int MaxTiles = 3000)
{
    /// <summary>The far view off.</summary>
    public static FarViewSettings Off { get; } = new(0);

    /// <summary>Whether the far view is drawn at all.</summary>
    public bool Enabled => Range > 0;

    /// <summary>The cell of a level in blocks.</summary>
    /// <param name="level">The level.</param>
    public int CellOf(int level) => BaseCell << level;

    /// <summary>The side of a tile of a level in blocks.</summary>
    /// <param name="level">The level.</param>
    public int SizeOf(int level) => TileCells * CellOf(level);

    /// <summary>The top level (the roots').</summary>
    public int TopLevel => Levels - 1;

    /// <summary>How far above the ground a skirt's foot hangs below its edge, for a level: enough to hide the step to a coarser neighbour (the cells grow with the level, so does the step).</summary>
    /// <param name="level">The level.</param>
    public int SkirtOf(int level) => Math.Max(12, 3 * CellOf(level));

    /// <summary>The settings with another range (the preset's, or <c>--far-view</c>), the levels kept: the top level is raised or lowered until its tiles are a good deal larger than the range's smallest reach.</summary>
    /// <param name="range">The range in blocks; 0 for off.</param>
    public FarViewSettings WithRange(int range) => this with { Range = Math.Max(0, range) };
}
