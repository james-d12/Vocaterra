using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The plants on the ground (B03): in every dry column the decoration strategy the biome row names (the row's own <c>decor</c> blocks by noise unless it names another) says which plant, if any,
/// stands in the cell over the ground, and the pass puts it there when the cell is free. It runs after the trees, so a trunk or a crown is never planted over; it reads only the chunk's own blocks
/// and the column's surface, so it gives the same result whichever chunk a tree came from.
/// </summary>
internal sealed class UndergrowthPass : IChunkDecorator
{
    private readonly GenerationWorld _world;

    public UndergrowthPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name, which is also the key of the default strategy's noise.</summary>
    public const string Name = "gen/undergrowth";

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
                var y = column.Height + 1;
                if (column.Water || y < chunk.MinY || y > chunk.MaxY || chunk.Get(x, y, z) != BlockId.Air)
                {
                    continue;
                }

                var row = ctx.BiomeAt(x, z).Row;
                var plant = _world.Strategies.Decoration[row.Index].PlantAt(new DecorationColumn(x, z, row, column.Top));
                if (plant != BlockId.Air)
                {
                    chunk.Set(x, y, z, plant);
                }
            }
        }
    }
}
