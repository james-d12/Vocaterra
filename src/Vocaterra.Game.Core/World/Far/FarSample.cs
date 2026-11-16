using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Far;

/// <summary>
/// What the far view needs of one column of the world (B07, D-game-34 item 21): where the ground is, whether the sea covers it, the block that makes its top, whether snow lies on it and how
/// thickly trees stand on it. Read from the pure height model and the biome table (or from a region's own chunks), with no chunk generated.
/// </summary>
/// <param name="Height">The y of the topmost solid block of the column (the bed under water); trees are not part of it.</param>
/// <param name="WaterLevel">The y of the water's top block when water covers the column, otherwise -1.</param>
/// <param name="Top">The block the ground's top layer is made of (the biome's underwater block under water, snow under snow): the far view colours the land by it.</param>
/// <param name="Snow">Whether snow covers the ground.</param>
/// <param name="Forest">How thickly trees stand on the column, 0 to 100 (the biome's tree density where trees grow; 100 under a region's own crowns; 0 under water).</param>
public readonly record struct FarSample(int Height, int WaterLevel, BlockId Top, bool Snow, int Forest)
{
    /// <summary>Whether water covers the column.</summary>
    public bool IsWater => WaterLevel >= 0;

    /// <summary>The y of the surface the eye sees: the water's top, or the ground's.</summary>
    public int SurfaceY => IsWater ? WaterLevel : Height;
}
