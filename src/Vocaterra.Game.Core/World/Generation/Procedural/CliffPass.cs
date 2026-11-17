using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The cliff faces (B11b, <see cref="SurfaceRock"/>): where a column stands the cliff's drop over its lowest neighbour, the layers above that neighbour's surface, which show on the face, become banded rock
/// (the data's rocks in bands a few blocks thick, tilted across the face) in place of the biome's filler layers and the plain stone. The top block is the surface's (already banded rock on a cliff column,
/// snow where snow lies), and the pockets of gravel, sand, dirt and the other rocks the strata laid keep their place in the face. It runs after the strata and before the caves, so a cave opening on a cliff cuts the banded rock.
/// </summary>
internal sealed class CliffPass : IChunkDecorator
{
    private readonly SurfaceRock _rock;
    private readonly BlockId _stone;

    public CliffPass(GenerationWorld world)
    {
        _rock = world.SurfaceRock!;
        _stone = world.Underground!.Strata.Stone;
    }

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/cliffs";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                var height = ctx.HeightAt(x, z);
                if (height < chunk.MinY || ctx.WaterLevelAt(x, z) >= 0)
                {
                    continue;
                }

                var low = _rock.LowestNeighbour(ctx, x, z);
                if (!_rock.IsCliff(ctx, x, z))
                {
                    continue;
                }

                var surface = ctx.SurfaceAt(x, z);
                for (var y = Math.Max(low + 1, chunk.MinY); y <= Math.Min(height - 1, chunk.MaxY); y++)
                {
                    var block = chunk.Get(x, y, z);
                    if (block == _stone || block == surface.Filler)
                    {
                        chunk.Set(x, y, z, _rock.BandAt(x, y, z));
                    }
                }
            }
        }
    }
}
