using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The ground's own layers (B03): in every column the biome's top block at the surface (snow where the surface temperature is below the snow line and the row allows it, the biome's
/// underwater block under the sea) and its filler block in the layers under it. What lies deeper is the stone pass's.
/// </summary>
internal sealed class SurfacePass : IChunkDecorator
{
    /// <summary>The pass's name.</summary>
    public const string Name = "gen/surface";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                var column = ctx.SurfaceAt(x, z);
                if (column.Height < chunk.MinY)
                {
                    continue;
                }

                chunk.Set(x, column.Height, z, column.Top);
                chunk.FillColumn(x, z, column.Height - column.FillerDepth, column.Height - 1, column.Filler);
            }
        }
    }
}
