using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The water (B03, H07): every column whose ground is below the sea fills with water up to the sea level, and every water block is a source. A source standing on a bed and held by dry
/// ground of at least its own height has nowhere to run, so the whole procedural sea, river and lake is at rest from the start and a flow simulation woken over it changes nothing. Rivers and
/// lakes are the height model's: their beds are below the sea level, and the water in them is the sea's.
/// </summary>
internal sealed class WaterPass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public WaterPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/water";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var sea = _world.SeaLevel;
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                var height = ctx.HeightAt(x, z);
                if (height < sea)
                {
                    chunk.FillColumn(x, z, height + 1, sea, BlockId.Water);
                }
            }
        }
    }
}
