using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// Where a region's layout stands in the world (B15): the chunk its first column, first layer and first row land on. The region's size comes from its layout, so a placement is all the
/// data a town needs besides the layout file (B16 reads it from <c>world.json</c>). A layout's own coordinates (x east, z south, y up from the grid's floor) are the placement's origin plus
/// the local ones.
/// </summary>
/// <param name="ChunkX">The chunk column of the layout's column 0.</param>
/// <param name="ChunkY">The chunk layer of the layout's layer 0 (y 0 of the layout is the floor of this layer).</param>
/// <param name="ChunkZ">The chunk row of the layout's row 0.</param>
public readonly record struct RegionPlacement(int ChunkX, int ChunkY, int ChunkZ)
{
    /// <summary>The world x of the layout's column 0.</summary>
    public int OriginX => ChunkX << VoxelChunk.Shift;

    /// <summary>The world y of the layout's layer 0.</summary>
    public int OriginY => ChunkY << VoxelChunk.Shift;

    /// <summary>The world z of the layout's row 0.</summary>
    public int OriginZ => ChunkZ << VoxelChunk.Shift;

    /// <summary>The world position of the layout's origin.</summary>
    public BlockPos Origin => new(OriginX, OriginY, OriginZ);

    /// <summary>The world position of a position in the layout's own coordinates.</summary>
    /// <param name="local">The layout's position.</param>
    public BlockPos ToWorld(BlockPos local) => new(OriginX + local.X, OriginY + local.Y, OriginZ + local.Z);

    /// <summary>The layout's position of a world position.</summary>
    /// <param name="world">The world's position.</param>
    public BlockPos ToLocal(BlockPos world) => new(world.X - OriginX, world.Y - OriginY, world.Z - OriginZ);
}

/// <summary>A named place of a region (a site, a point, a named prop) that knows where it is both in the region's own coordinates and in the world's (B15).</summary>
/// <param name="Name">The place's name in the layout.</param>
/// <param name="Local">Its block in the layout's coordinates.</param>
/// <param name="World">Its block in the world: the layout's plus the region's origin.</param>
public readonly record struct RegionPoint(string Name, BlockPos Local, BlockPos World);
