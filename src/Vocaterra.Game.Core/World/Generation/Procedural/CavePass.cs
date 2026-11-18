using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The caves (B11): cheese caverns and spaghetti tunnels (<see cref="CaveField"/>, sampled on a coarse lattice and interpolated) are cut out of the rock in every cell the column's plan allows
/// (<see cref="CavePlan"/>: under the surface cover, clear of water, under the region floor). Only rock is cut: never air, water or the bedrock floor. The entrances' passages are
/// <see cref="EntrancePass"/>'s.
/// </summary>
internal sealed class CavePass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public CavePass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/caves";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var plan = ctx.CavePlan;
        if (chunk.MinY > plan.HighestAffected)
        {
            return;
        }

        Span<CaveKind> kinds = stackalloc CaveKind[VoxelChunk.Volume];
        _world.Underground!.Caves.Fill(chunk.MinX, chunk.MinY, chunk.MinZ, kinds);
        var index = 0;
        for (var ly = 0; ly < VoxelChunk.Size; ly++)
        {
            var y = chunk.MinY + ly;
            for (var lz = 0; lz < VoxelChunk.Size; lz++)
            {
                for (var lx = 0; lx < VoxelChunk.Size; lx++, index++)
                {
                    var k = (lz * VoxelChunk.Size) + lx;
                    if (kinds[index] == CaveKind.None || y > plan.Ceiling[k])
                    {
                        continue;
                    }

                    var x = chunk.MinX + lx;
                    var z = chunk.MinZ + lz;
                    var block = chunk.Get(x, y, z);
                    if (block != BlockId.Air && block != BlockId.Water && block != _world.Underground.Bedrock)
                    {
                        chunk.Set(x, y, z, BlockId.Air);
                    }
                }
            }
        }
    }
}
