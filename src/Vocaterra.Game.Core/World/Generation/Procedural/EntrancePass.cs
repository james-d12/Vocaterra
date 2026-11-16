using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The cave entrances (B11b, <see cref="EntranceSites"/>): every passage that crosses the chunk is cut. In the passage's cross-section the cells from one above the floor up to the headroom become air, and a
/// floor block is set where the ground is a block or more under it (the sides are filled, so the stair is as wide all along), so a body walks down on steps of one block. The first blocks of the passage
/// stand in a rim of rock (<see cref="EntranceSpec.RimBlocks"/>) where the layers of the ground (the grass and the dirt) are replaced, so the mouth reads as an opening in stone and not a hollow in a
/// meadow. Only the chunk's own cells are written. It runs after the caves so the passage joins their air, and before the lava and the ores.
/// </summary>
internal sealed class EntrancePass : IChunkDecorator
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly GenerationWorld _world;
    private readonly BlockId[] _rim;
    private readonly int[] _rimWeights;
    private readonly int _rimTotal;
    private readonly ulong _key;

    public EntrancePass(GenerationWorld world)
    {
        _world = world;
        var spec = world.Underground!.Data.Entrances;
        _rim = [.. spec.RimBlocks.Select(static b => UndergroundWorld.BlockNamed(b.Block))];
        _rimWeights = [.. spec.RimBlocks.Select(static b => b.Weight)];
        _rimTotal = _rimWeights.Sum();
        _key = WorldRng.For(world.Seed, Name).NextUInt64();
    }

    /// <summary>The pass's name, which is also the key of its hash.</summary>
    public const string Name = "gen/entrances";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        foreach (var ramp in ctx.Ramps)
        {
            var b = ramp.Bounds(0);
            if (b.MaxX < chunk.MinX || b.MinX > chunk.MinX + VoxelChunk.Size - 1 || b.MaxZ < chunk.MinZ || b.MinZ > chunk.MinZ + VoxelChunk.Size - 1 || chunk.MaxY < ramp.LowestFloor - 1)
            {
                continue;
            }

            Cut(ctx, chunk, ramp);
        }
    }

    private void Cut(ColumnContext ctx, ChunkBuilder chunk, EntranceRamp ramp)
    {
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                ramp.Locate(x, z, out var along, out var lateral);
                if (!ramp.InRim(along, lateral) && !ramp.InPassage(along, lateral))
                {
                    continue;
                }

                if (ramp.InRim(along, lateral) && along <= ramp.RimLength + ramp.RimWidth)
                {
                    LayRim(ctx, chunk, x, z);
                }

                if (ramp.InPassage(along, lateral))
                {
                    CutColumn(ctx, chunk, ramp, x, z, along);
                }
            }
        }
    }

    // The ground's own layers (the top and the filler) become rock.
    private void LayRim(ColumnContext ctx, ChunkBuilder chunk, int x, int z)
    {
        var column = ctx.SurfaceAt(x, z);
        if (column.Water)
        {
            return;
        }

        var top = Math.Min(column.Height, chunk.MaxY);
        for (var y = Math.Max(column.Height - column.FillerDepth, chunk.MinY); y <= top; y++)
        {
            if (BlockDefinition.IsSolid(chunk.Get(x, y, z)))
            {
                chunk.Set(x, y, z, RimRock(x, y, z));
            }
        }
    }

    private void CutColumn(ColumnContext ctx, ChunkBuilder chunk, EntranceRamp ramp, int x, int z, int along)
    {
        var floor = ramp.FloorAt(along);
        var height = ctx.HeightAt(x, z);
        var bedrock = _world.Underground!.Bedrock;

        // Ground under the floor: a column that lies lower is built up to it, so the stair is whole.
        for (var y = Math.Max(Math.Min(height + 1, floor), chunk.MinY); y <= Math.Min(floor, chunk.MaxY); y++)
        {
            chunk.Set(x, y, z, RimRock(x, y, z));
        }

        if (floor >= chunk.MinY && floor <= chunk.MaxY && !BlockDefinition.IsSolid(chunk.Get(x, floor, z)))
        {
            chunk.Set(x, floor, z, RimRock(x, floor, z));
        }

        for (var y = Math.Max(floor + 1, chunk.MinY); y <= Math.Min(floor + ramp.Headroom, chunk.MaxY); y++)
        {
            var block = chunk.Get(x, y, z);
            if (block != BlockId.Air && block != BlockId.Water && block != bedrock)
            {
                chunk.Set(x, y, z, BlockId.Air);
            }
        }
    }

    // A rock of the rim, by weight, from a hash of the block.
    private BlockId RimRock(int x, int y, int z)
    {
        var hash = WorldRng.Mix(_key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)y * GoldenY) ^ ((ulong)(uint)z * GoldenZ));
        var pick = (int)(hash % (ulong)_rimTotal);
        for (var i = 0; i < _rim.Length; i++)
        {
            if (pick < _rimWeights[i])
            {
                return _rim[i];
            }

            pick -= _rimWeights[i];
        }

        return _rim[^1];
    }
}
