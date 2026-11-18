using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The ores (B11): the veins of the chunk (<see cref="Underground.OreTable"/>, a function of the seed and the chunk's coordinates) are set into the rock they may replace. A cell that is air, water, lava,
/// another ore, bedrock or a rock the row does not list is left alone, so an ore never fills a cave. It runs after the caves, so veins show in their walls.
/// </summary>
internal sealed class OrePass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public OrePass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name. (Each row's own draws are keyed <c>ore:</c> and its id.)</summary>
    public const string Name = "gen/ores";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        var ores = _world.Underground!.Ores;
        if (chunk.MaxY < ores.MinY || chunk.MinY > ores.MaxY)
        {
            return;
        }

        foreach (var vein in ores.Veins(chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z))
        {
            foreach (var cell in vein.Cells)
            {
                var x = chunk.MinX + (cell & (VoxelChunk.Size - 1));
                var z = chunk.MinZ + ((cell >> VoxelChunk.Shift) & (VoxelChunk.Size - 1));
                var y = chunk.MinY + (cell >> (2 * VoxelChunk.Shift));
                if (ores.Replaces(vein.Row, chunk.Get(x, y, z)))
                {
                    chunk.Set(x, y, z, vein.Block);
                }
            }
        }
    }
}
