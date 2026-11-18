using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// Which chunk layers of a chunk column meet the ground (B07, biomes plan section 3.7): the surface band that a distant column keeps resident, decided from the pure height model and the
/// regions' own chunks, with no chunk generated. Outside a region the band is exactly what the generator's own shortcuts leave: from the layer of the lowest ground less the deepest filler,
/// up to the layer of the highest ground (or the sea) and the tallest tree, so a layer outside it is all air or all stone whatever the neighbours do. Inside a region it is the region's layers
/// that hold something and are not uniformly solid. Pure and immutable: a band is a function of the seed, the data, the regions and the column, whenever and from whichever thread it is asked.
/// </summary>
public sealed class SurfaceBands
{
    private readonly ProceduralChunkSource _procedural;
    private readonly RegionCatalog _regions;

    internal SurfaceBands(ProceduralChunkSource procedural, RegionCatalog regions)
    {
        ArgumentNullException.ThrowIfNull(procedural);
        ArgumentNullException.ThrowIfNull(regions);
        _procedural = procedural;
        _regions = regions;
    }

    /// <summary>The lowest chunk layer of the world.</summary>
    public int WorldMinLayer => _procedural.MinChunkY;

    /// <summary>The highest chunk layer of the world.</summary>
    public int WorldMaxLayer => _procedural.MaxChunkY - 1;

    /// <summary>The band of a chunk column.</summary>
    /// <param name="chunkX">The chunk column.</param>
    /// <param name="chunkZ">The chunk row.</param>
    public ColumnBand Of(int chunkX, int chunkZ) =>
        _regions.ColumnOwner(chunkX, chunkZ) is { } owner ? OfRegion(owner, chunkX, chunkZ) : _procedural.SurfaceBand(chunkX, chunkZ);

    private static ColumnBand OfRegion(IRegion owner, int chunkX, int chunkZ)
    {
        var bounds = owner.Bounds;
        var min = int.MaxValue;
        var max = int.MinValue;
        for (var cy = bounds.MinChunkY; cy < bounds.EndChunkY; cy++)
        {
            var data = owner.Copy(new VoxelChunkCoord(chunkX, cy, chunkZ));
            if (data.IsEmpty || IsUniformSolid(data))
            {
                continue;
            }

            min = Math.Min(min, cy);
            max = Math.Max(max, cy);
        }

        return min > max ? ColumnBand.Empty : new ColumnBand(min, max);
    }

    /// <summary>Whether every block of the chunk is one and the same solid block (a chunk that is nothing but filler, which no face of a neighbour shows).</summary>
    /// <param name="data">The chunk.</param>
    internal static bool IsUniformSolid(ChunkData data)
    {
        var blocks = data.Blocks;
        var first = blocks[0];
        if (!BlockDefinition.IsSolid(first))
        {
            return false;
        }

        for (var i = 1; i < blocks.Length; i++)
        {
            if (blocks[i] != first)
            {
                return false;
            }
        }

        return !data.HasWaterLevels;
    }
}
