using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The rock layers (B11): the stone the stone pass wrote below each column's filler becomes the layer's rock: stone above the dither, slate below it, a dithered change between, and granite and
/// limestone blobs in the stone (<see cref="Underground.StrataTable"/>). The ground's own top and filler layers stay the biome's.
/// </summary>
internal sealed class StrataPass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public StrataPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name, which is also the key of its noise and hash.</summary>
    public const string Name = "gen/strata";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var strata = _world.Underground!.Strata;
        var spec = _world.Underground.Data.Strata;
        if (chunk.MinY >= spec.DitherToY && !strata.HasBlobs && strata.Stone == BlockId.Stone)
        {
            return; // all stone already
        }

        Span<BlockId> rocks = stackalloc BlockId[VoxelChunk.Volume];
        strata.Fill(chunk.MinX, chunk.MinY, chunk.MinZ, rocks);
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                var column = ctx.SurfaceAt(x, z);
                var top = Math.Min(column.Height - column.FillerDepth - 1, chunk.MaxY);
                for (var y = chunk.MinY; y <= top; y++)
                {
                    var rock = rocks[ChunkData.Index(x - chunk.MinX, y - chunk.MinY, z - chunk.MinZ)];
                    if (rock != BlockId.Stone)
                    {
                        chunk.Set(x, y, z, rock);
                    }
                }
            }
        }
    }
}
