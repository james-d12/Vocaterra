using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;

namespace Vocaterra.Game.Core.World.Streaming;

/// <summary>
/// The streamed world's chunks (B06, biomes plan section 3.1): a region's own chunks inside it, the procedural world everywhere else. Under a region (the chunk layers below it, in its
/// footprint) the procedural world fills the chunk (B11: its strata, ores, caves, lava and bedrock floor, kept at or under y -9 and clear of the region's own water), above it is air, and outside, the procedural world
/// is eased into the region's edge (<see cref="RegionBlend"/>). Pure and immutable: a chunk is a function of the seed, the data, the regions and its coordinates, so it is the same whenever and
/// from whichever thread it is made.
/// </summary>
public sealed class CompositeChunkSource : IChunkSource
{
    private readonly RegionCatalog _regions;
    private readonly ProceduralChunkSource _procedural;

    /// <summary>Creates the source over a procedural world that was made with the same regions' blend.</summary>
    /// <param name="regions">The regions.</param>
    /// <param name="procedural">The procedural source (<see cref="WorldFactory"/> builds it with the blend).</param>
    internal CompositeChunkSource(RegionCatalog regions, ProceduralChunkSource procedural)
    {
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(procedural);
        _regions = regions;
        _procedural = procedural;
    }

    /// <summary>The regions the world holds.</summary>
    public RegionCatalog Regions => _regions;

    /// <summary>The lowest chunk layer of the world.</summary>
    public int MinChunkY => _procedural.MinChunkY;

    /// <summary>One above the highest chunk layer of the world.</summary>
    public int MaxChunkY => _procedural.MaxChunkY;

    /// <inheritdoc />
    public ChunkData Generate(VoxelChunkCoord coord)
    {
        if (_regions.Covering(coord) is { } region)
        {
            return region.Copy(coord);
        }

        if (_regions.ColumnOwner(coord.X, coord.Z) is { } owner && coord.Y >= owner.Bounds.EndChunkY)
        {
            return ChunkData.Empty(coord);
        }

        return _procedural.Generate(coord);
    }

    /// <summary>
    /// Every chunk of a chunk column, from the bottom layer up, sharing one window for the procedural part: the cheap way to make many layers (about 1.6 ms for a column of 24 layers against about
    /// 1.1 ms for each chunk alone). Each chunk equals what <see cref="Generate"/> gives for its coordinates.
    /// </summary>
    /// <param name="chunkX">The chunk column's x.</param>
    /// <param name="chunkZ">The chunk column's z.</param>
    public IReadOnlyList<ChunkData> GenerateColumn(int chunkX, int chunkZ)
    {
        var owner = _regions.ColumnOwner(chunkX, chunkZ);
        if (owner is null)
        {
            return _procedural.GenerateColumn(chunkX, chunkZ);
        }

        var below = _procedural.GenerateColumn(chunkX, chunkZ);
        var chunks = new ChunkData[MaxChunkY - MinChunkY];
        for (var i = 0; i < chunks.Length; i++)
        {
            var coord = new VoxelChunkCoord(chunkX, MinChunkY + i, chunkZ);
            chunks[i] = owner.Covers(coord) ? owner.Copy(coord) : coord.Y >= owner.Bounds.EndChunkY ? ChunkData.Empty(coord) : below[i];
        }

        return chunks;
    }
}
