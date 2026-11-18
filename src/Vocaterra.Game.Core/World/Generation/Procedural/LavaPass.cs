using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The lava (B11): cave air at or below the lava level becomes lava, so the deepest caverns hold lakes of it. Below the level nothing else is air (the ground and the sea are far above), and above it no
/// lava is ever made. It is a flat source at one level, so it is at rest at once; in this slice lava does not flow.
/// </summary>
internal sealed class LavaPass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public LavaPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/lava";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var underground = _world.Underground!;
        if (chunk.MinY > underground.LavaLevel)
        {
            return;
        }

        var top = Math.Min(chunk.MaxY, underground.LavaLevel);
        for (var y = chunk.MinY; y <= top; y++)
        {
            for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
            {
                for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
                {
                    if (chunk.Get(x, y, z) == BlockId.Air)
                    {
                        chunk.Set(x, y, z, underground.Lava);
                    }
                }
            }
        }
    }
}
