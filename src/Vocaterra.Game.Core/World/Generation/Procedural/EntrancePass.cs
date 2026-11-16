using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The cave entrances (B11b, <see cref="EntranceSites"/>): every passage that crosses the chunk is cut. Each column near a passage's path belongs to the nearest cell of it, and from that cell's floor up the
/// tube of the cell (a width and a height of its own, bulged by a noise) becomes air, so the walls and the roof are rough and the passage is wide in one place and narrow in the next. Where the ground lies a
/// block lower than a cell's floor in a cave (the landing) the floor is built of the data's floor blocks. The mouth is whatever the tube leaves of the ground: a ragged dip where the ground is flat, an
/// opening under the rock where it rises. Only the chunk's own cells are written. It runs after the caves so the passage joins their air, and before the lava and the ores.
/// </summary>
internal sealed class EntrancePass : IChunkDecorator
{
    private const ulong GoldenX = 0x9E3779B97F4A7C15UL;
    private const ulong GoldenY = 0xC2B2AE3D27D4EB4FUL;
    private const ulong GoldenZ = 0x165667B19E3779F9UL;

    private readonly GenerationWorld _world;
    private readonly BlockId[] _floor;
    private readonly int[] _floorWeights;
    private readonly int _floorTotal;
    private readonly ulong _key;

    public EntrancePass(GenerationWorld world)
    {
        _world = world;
        var spec = world.Underground!.Data.Entrances;
        _floor = [.. spec.FloorBlocks.Select(static b => UndergroundWorld.BlockNamed(b.Block))];
        _floorWeights = [.. spec.FloorBlocks.Select(static b => b.Weight)];
        _floorTotal = _floorWeights.Sum();
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

            Cut(chunk, ramp);
        }
    }

    private void Cut(ChunkBuilder chunk, EntranceRamp ramp)
    {
        var bedrock = _world.Underground!.Bedrock;
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                if (!ramp.Column(x, z, out var floor, out var node, out var forced, out var distance2) || !ramp.Carves(x, floor + 1, z, floor, node, forced, distance2))
                {
                    continue; // the column is outside the tube, or the tube does not reach its first block
                }

                // The floor under the passage: a gap in it (the landing in a cave) is built up.
                if (floor >= chunk.MinY && floor <= chunk.MaxY && !BlockDefinition.IsSolid(chunk.Get(x, floor, z)))
                {
                    chunk.Set(x, floor, z, FloorBlock(x, floor, z));
                }

                var top = Math.Min(floor + 14, chunk.MaxY);
                for (var y = Math.Max(floor + 1, chunk.MinY); y <= top; y++)
                {
                    if (!ramp.Carves(x, y, z, floor, node, forced, distance2))
                    {
                        continue;
                    }

                    var block = chunk.Get(x, y, z);
                    if (block != BlockId.Air && block != BlockId.Water && block != bedrock)
                    {
                        chunk.Set(x, y, z, BlockId.Air);
                    }
                }
            }
        }
    }

    // A rock of the floor, by weight, from a hash of the block.
    private BlockId FloorBlock(int x, int y, int z)
    {
        var hash = WorldRng.Mix(_key ^ ((ulong)(uint)x * GoldenX) ^ ((ulong)(uint)y * GoldenY) ^ ((ulong)(uint)z * GoldenZ));
        var pick = (int)(hash % (ulong)_floorTotal);
        for (var i = 0; i < _floor.Length; i++)
        {
            if (pick < _floorWeights[i])
            {
                return _floor[i];
            }

            pick -= _floorWeights[i];
        }

        return _floor[^1];
    }
}
