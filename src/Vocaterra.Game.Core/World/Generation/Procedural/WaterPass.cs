using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The water (B03, H07, B02b round 3): every column whose ground is below its water level (the sea's, or the level of its river or lake: <see cref="ColumnContext.WaterLevelAt"/>) fills with water up to it, and every water block is a
/// source. A source standing on a bed and held by dry ground of at least its own height has nowhere to run, so a reach of water is at rest from the start and a flow simulation woken over it changes nothing.
/// Where a river's surface steps down (a column beside a column of higher water) the step is built as falling water, the way the valley's rapids are (<c>WaterAtRest</c>): the lower column's cells from its surface up to the higher surface are
/// falling water, topped by one cell of thin flowing water fed by the higher reach, so the last source of the upper reach has nowhere to spill. A falling cell is skipped by its neighbours and a cell beside it that is air is not fed,
/// so the flow rules leave the whole of it as it is.
/// </summary>
internal sealed class WaterPass : IChunkDecorator
{
    /// <summary>The pass's name.</summary>
    public const string Name = "gen/water";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Decorate(ColumnContext ctx, ChunkBuilder chunk)
    {
        for (var z = chunk.MinZ; z < chunk.MinZ + VoxelChunk.Size; z++)
        {
            for (var x = chunk.MinX; x < chunk.MinX + VoxelChunk.Size; x++)
            {
                var level = ctx.WaterLevelAt(x, z);
                if (level < 0)
                {
                    continue;
                }

                chunk.FillColumn(x, z, ctx.HeightAt(x, z) + 1, level, BlockId.Water);
                var higher = HigherNeighbour(ctx, x, z, level);
                if (higher > level)
                {
                    for (var y = level; y < higher; y++)
                    {
                        chunk.SetWater(x, y, z, WaterLevel.Flowing(1, falling: true));
                    }

                    chunk.SetWater(x, higher, z, WaterLevel.Flowing(1));
                }
            }
        }
    }

    // The highest water surface among the four neighbours of a water column (the window has a margin, so they are all in it).
    private static int HigherNeighbour(ColumnContext ctx, int x, int z, int own)
    {
        var higher = own;
        higher = Math.Max(higher, ctx.WaterLevelAt(x + 1, z));
        higher = Math.Max(higher, ctx.WaterLevelAt(x - 1, z));
        higher = Math.Max(higher, ctx.WaterLevelAt(x, z + 1));
        return Math.Max(higher, ctx.WaterLevelAt(x, z - 1));
    }
}
