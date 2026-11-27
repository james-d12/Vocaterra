using Vocaterra.Game.Core.World.Lod;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>
/// The shape of the far view (B07, B07c; D-game-34 items 21 and 25): how far it reaches and how its level-of-detail tiles are laid out, the way the Distant Horizons mod does it. The land beyond the voxel chunks is drawn as square tiles of
/// <see cref="TileCells"/> cells each side; a tile at level L has cells of <c>1 &lt;&lt; L</c> blocks, so level 0 is the land block by block and each level doubles its cell and covers four times the ground with the same number of columns.
/// The tiles that are drawn are chosen by a quadtree from the roots of the top level: level L is used from <see cref="SplitDistance"/> of the eye out to the start of level L + 1, so the cells grow with distance (<see cref="BandStarts"/>, in
/// voxel view distances) and the whole is watertight (no gap or overlap between levels, skirts hiding the steps). Which tiles there are is a function of where the player stands and nothing else.
/// </summary>
/// <param name="Range">How far the land is drawn, in blocks from the eye; 0 turns the far view off.</param>
/// <param name="Levels">How many levels there are (at least 1); the top level's tiles are the quadtree's roots. Eight: cells of 1, 2, 4, 8, 16, 32, 64 and 128 blocks.</param>
/// <param name="BandScale">What the bands are multiplied by: below 1 the coarser levels start nearer (less detail, cheaper), above 1 further out.</param>
/// <param name="InnerFraction">The fraction of the voxel view distance inside which no tile is drawn (the voxel chunks cover it); a little below 1 (0.96, a chunk and a half at High), so a chunk at the very edge of the voxel zone that is not meshed yet shows the far land and not the sky, and no far geometry shows through the zone's own faces.</param>
/// <param name="MaxTiles">The most tiles kept (built and cached): past it the farthest unused ones are dropped.</param>
/// <param name="TreeLevels">Where the store is filled from the generator's model (no real chunk yet): the levels below this get every tree as its blocks (the tree pass's own placement), the levels from it up a forest as a layer of leaves on a trunk. 0 makes no single tree.</param>
/// <param name="TexturedLevels">The tiles of the levels below this draw every face with its block's own atlas texture (mipmapped); the tiles above draw flat averaged colours with a noise texture, merged into large quads.</param>
/// <param name="Ground">B22: the ground temperature and humidity of a block column, from which grass and oak leaves take their colour (<see cref="FarPalette.LandTint"/>), the same map the chunk mesher reads; <see langword="null"/> for the map's middle everywhere.</param>
/// <param name="RealRadius">How far from the eye, in blocks, workers generate the real chunks of the surface and ingest them into the store (so trees, houses and regions near the voxel zone are exact, not the model's); 0 for none.</param>
public sealed record FarViewSettings(int Range, int Levels = 8, double BandScale = 1.0, double InnerFraction = 0.96, int MaxTiles = 4000, int TreeLevels = 4, int TexturedLevels = 2, int RealRadius = 1000, Func<int, int, int, (int Temperature, int Humidity)>? Ground = null)
{
    /// <summary>Where each level starts, in voxel view distances: level 0 is the ring just past the voxel zone (to 1.1), then each level is used out to the start of the next.</summary>
    public static IReadOnlyList<double> BandStarts { get; } = [0, 1.1, 1.5, 2.5, 4.5, 8.5, 16.5, 32.5];

    /// <summary>
    /// B51: the radius, in blocks from the eye, inside which no far land is drawn where the chunks are drawn: <see cref="InnerFraction"/> of the voxel zone's radius, but no nearer its edge than a chunk (16 blocks), so the far land always lies under the outermost chunk of the zone
    /// (drawn before the chunks, which always draw over it): a chunk that is missing or lower at the zone's edge shows far ground there, not the sky. At 400 blocks and more the fraction already leaves a chunk; below it the zone is small and the margin was a few blocks.
    /// </summary>
    /// <param name="voxelViewDistance">The voxel zone's radius in blocks.</param>
    public double InnerRadius(int voxelViewDistance) => Math.Min(InnerFraction * voxelViewDistance, voxelViewDistance - 16.0);

    /// <summary>The cells along a tile's side: a section's voxels, so a tile is the stack of sections of its footprint (the same at every level).</summary>
    public int TileCells => LodSection.Size;

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
    public int SkirtOf(int level) => Math.Max(6, 4 * CellOf(level));

    /// <summary>The settings with another range (the preset's, or <c>--far-view</c>), the levels kept.</summary>
    /// <param name="range">The range in blocks; 0 for off.</param>
    public FarViewSettings WithRange(int range) => this with { Range = Math.Max(0, range) };
}
