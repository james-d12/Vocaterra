using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// Solid stone from the bottom of the world up to the layers the ground's own pass wrote (B03). The floor and the rock strata come in B11; until then the underground is plain stone, so a
/// column is solid from y -64 to its surface and a body that digs down (nothing can yet) would find nothing else.
/// </summary>
internal sealed class StonePass : IChunkDecorator
{
    /// <summary>The pass's name.</summary>
    public const string Name = "gen/stone";

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
                chunk.FillColumn(x, z, chunk.MinY, column.Height - column.FillerDepth - 1, BlockId.Stone);
            }
        }
    }
}
