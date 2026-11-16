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

            Cut(ctx, chunk, ramp);
        }
    }

    private void Cut(ColumnContext ctx, ChunkBuilder chunk, EntranceRamp ramp)
    {
        var bedrock = _world.Underground!.Bedrock;
        var spec = _world.Underground.Data.Entrances;
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                Bowl(ctx, chunk, ramp, spec, x, z, bedrock);
                if (!ramp.Column(x, z, out var floor, out var node, out var forced, out var distance2) || !ramp.Carves(x, floor + 1, z, floor, node, forced, distance2))
                {
                    continue; // the column is outside the tube, or the tube does not reach its first block
                }

                // The floor under the passage: a gap in it (the landing in a cave) is built up, and where the floor is still the meadow's own layers (grass, dirt) it is loose gravel, cobble and stone, a scree slope into the hole.
                if (floor >= chunk.MinY && floor <= chunk.MaxY)
                {
                    var column = ctx.SurfaceAt(x, z);
                    var layers = !column.Water && floor >= column.Height - column.FillerDepth && floor <= column.Height;
                    if (layers || !BlockDefinition.IsSolid(chunk.Get(x, floor, z)))
                    {
                        chunk.Set(x, floor, z, FloorBlock(x, floor, z));
                    }
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

    // The bowl at the mouth: where the ground lies at most BowlDepth blocks over the floor of the nearest of the path's first cells, the ground is cut away above a surface that rises from that floor, one block for
    // every BowlSlope thousandths of a block outwards from the tube's edge (the edge itself ragged), so a mouth on level ground is a dip with sloping sides and stone showing in them; deeper than that the ground
    // stays and the tube is a tunnel under it.
    private static void Bowl(ColumnContext ctx, ChunkBuilder chunk, EntranceRamp ramp, EntranceSpec spec, int x, int z, BlockId bedrock)
    {
        var ground = ctx.HeightAt(x, z);
        if (spec.BowlDepth <= 0 || ground < chunk.MinY)
        {
            return;
        }

        var node = ramp.NearestEarly(x, z, 14, out var distance2);
        if (node < 0 || distance2 > 400)
        {
            return;
        }

        var n = ramp.Nodes[node];
        var depth = ground - n.Floor;
        if (depth > spec.BowlDepth)
        {
            return;
        }

        var edge16 = n.HalfWidth16 + (int)((long)ramp.Noise(x, 0, z, spec.RoughnessCell + 2) * spec.Roughness * 16 / 1_000_000);
        var over16 = (int)IntMath.ISqrt(distance2 * 256L) - edge16;
        var level = n.Floor + 1 + (over16 <= 0 ? 0 : (int)(over16 * 1000L / (16L * spec.BowlSlope)));
        for (var y = Math.Max(level, chunk.MinY); y <= Math.Min(ground, chunk.MaxY); y++)
        {
            var block = chunk.Get(x, y, z);
            if (block != BlockId.Air && block != BlockId.Water && block != bedrock)
            {
                chunk.Set(x, y, z, BlockId.Air);
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
