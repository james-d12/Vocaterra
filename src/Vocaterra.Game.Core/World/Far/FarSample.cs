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
/// <param name="Filler">The block of the layers under the top one, which a step of the far land shows on its side (B07c).</param>
/// <param name="FillerDepth">How many layers of <paramref name="Filler"/> lie under the top one; the stone is under them.</param>
/// <param name="Leaves">The leaf block of the trees that stand on the column (the heaviest species of its biome, a region's own topmost leaf), or <see cref="BlockId.Air"/> when none grow.</param>
/// <param name="Canopy">How far over the ground the trees' crowns reach on average, in blocks: the height a far forest is raised by when it is drawn as a canopy layer (0 when no tree grows).</param>
public readonly record struct FarSample(int Height, int WaterLevel, BlockId Top, bool Snow, int Forest, BlockId Filler = BlockId.Dirt, int FillerDepth = 3, BlockId Leaves = BlockId.Air, int Canopy = 0)
{
    /// <summary>Whether water covers the column.</summary>
    public bool IsWater => WaterLevel >= 0;

    /// <summary>The y of the surface the eye sees: the water's top, or the ground's.</summary>
    public int SurfaceY => IsWater ? WaterLevel : Height;
}

/// <summary>
/// One tree of the far view (B07c): where it stands and the box or two its crown fills, from the tree pass's own placement (<see cref="FarTerrainSampler.TreesIn"/>). The crown is two tiers, the upper layers and the lower, each a square of
/// the widest reach of its layers (a cone's lower tier is wider than its upper; a round crown's middle layers are the widest), so a box is the crown's own size and a cone still reads as a cone.
/// </summary>
/// <param name="X">The trunk's x.</param>
/// <param name="Z">The trunk's z.</param>
/// <param name="BaseY">The y of the ground under the trunk.</param>
/// <param name="Length">The trunk's length in blocks (the trunk's blocks are y <paramref name="BaseY"/> + 1 to <paramref name="BaseY"/> + <paramref name="Length"/>).</param>
/// <param name="Trunk">The trunk's block.</param>
/// <param name="Leaves">The leaves' block.</param>
/// <param name="CrownTop">The y of the crown's topmost layer; layer k (from the top, 0 first) is at y <paramref name="CrownTop"/> - k.</param>
/// <param name="UpperLayers">How many layers the upper tier has (the top ones).</param>
/// <param name="UpperReach">How far, in columns, the upper tier's widest layer reaches from the trunk.</param>
/// <param name="LowerLayers">How many layers the lower tier has.</param>
/// <param name="LowerReach">How far, in columns, the lower tier's widest layer reaches from the trunk.</param>
public readonly record struct FarTree(int X, int Z, int BaseY, int Length, BlockId Trunk, BlockId Leaves, int CrownTop, int UpperLayers, int UpperReach, int LowerLayers, int LowerReach)
{
    /// <summary>The y of the topmost block of the tree (the crown's top layer).</summary>
    public int TopY => CrownTop;

    /// <summary>The y of the lowest layer of the crown.</summary>
    public int CrownBottom => CrownTop - (UpperLayers + LowerLayers) + 1;
}
