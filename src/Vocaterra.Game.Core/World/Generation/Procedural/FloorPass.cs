using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>The unbreakable floor (B11): a ragged layer of bedrock at the bottom of every column, 1 to 4 blocks thick in the shipped data (<see cref="Underground.BedrockFloor"/>).</summary>
internal sealed class FloorPass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public FloorPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name, which is also the key of its hash.</summary>
    public const string Name = "gen/floor";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var underground = _world.Underground!;
        var floor = underground.Floor;
        if (chunk.MinY > floor.HighestPossibleY)
        {
            return;
        }

        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                chunk.FillColumn(x, z, floor.BottomY, floor.TopAt(x, z), underground.Bedrock);
            }
        }
    }
}
