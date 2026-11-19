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

        // How high the stone reaches in each column of the chunk: a chunk that holds no stone of any column (the air over the ground, the topsoil's layers) is left alone before any rock is sampled.
        Span<int> tops = stackalloc int[VoxelChunk.Size * VoxelChunk.Size];
        var reaches = false;
        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                var column = ctx.SurfaceAt(chunk.MinX + lx, chunk.MinZ + lz);
                var top = Math.Min(column.Height - column.FillerDepth - 1, chunk.MaxY);
                tops[(lz * VoxelChunk.Size) + lx] = top;
                reaches |= top >= chunk.MinY;
            }
        }

        if (!reaches)
        {
            return;
        }

        Span<BlockId> rocks = stackalloc BlockId[VoxelChunk.Volume];
        if (!strata.Fill(chunk.MinX, chunk.MinY, chunk.MinZ, rocks) && strata.Stone == BlockId.Stone)
        {
            return; // every block is the stone the stone pass wrote
        }

        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                var top = tops[(lz * VoxelChunk.Size) + lx];
                for (var ly = 0; ly <= top - chunk.MinY; ly++)
                {
                    var rock = rocks[ChunkData.Index(lx, ly, lz)];
                    if (rock != BlockId.Stone)
                    {
                        chunk.Set(chunk.MinX + lx, chunk.MinY + ly, chunk.MinZ + lz, rock);
                    }
                }
            }
        }
    }
}
