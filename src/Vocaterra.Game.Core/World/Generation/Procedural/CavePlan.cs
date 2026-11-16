using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// What the caves may do in the 16 x 16 columns of one chunk column (B11, biomes plan section 3.5), settled once from the shaped ground and shared by all 24 layers:
/// <list type="bullet">
/// <item>the <see cref="Ceiling"/> of each column, the highest y a cave may reach: the ground less the surface cover, at most the caves' top, at most the ceiling under an authored region, and kept
/// clear of water: at most <c>bed - margin</c> under any water column within the water margin (a cube round the column), and <c>bed - clearance</c> under the column's own water;</item>
/// <item>which columns are <see cref="Wet"/> (B11b): a water column within the linings' wet reach, where the linings lay sand, gravel and moss.</item>
/// </list>
/// The entrances are no longer cut from here: a passage walks down from the surface to a cave (<see cref="EntranceSites"/>). Every number is read from final heights inside the window (its margin
/// covers the reach), so a column's plan is the same whichever chunk column it is made in.
/// </summary>
internal sealed class CavePlan
{
    private CavePlan(int[] ceiling, bool[] wet, int highestAffected)
    {
        Ceiling = ceiling;
        Wet = wet;
        HighestAffected = highestAffected;
    }

    /// <summary>The highest y a cave reaches in each column, row by row (z), x fastest, of the chunk column's 16 x 16 columns.</summary>
    public int[] Ceiling { get; }

    /// <summary>Whether each column lies within the linings' wet reach of a water column (same order as <see cref="Ceiling"/>); all false without linings.</summary>
    public bool[] Wet { get; }

    /// <summary>The highest y any cave of the chunk column can touch: a chunk above it is untouched.</summary>
    public int HighestAffected { get; }

    /// <summary>The plan of a shaped chunk column.</summary>
    /// <param name="ctx">The shaped column; its world must have an underground.</param>
    public static CavePlan Build(ColumnContext ctx)
    {
        var world = ctx.World;
        var u = world.Underground ?? throw new InvalidOperationException("The world has no underground.");
        var caves = u.Data.Caves;
        var ceilings = new int[VoxelChunk.Size * VoxelChunk.Size];
        var highest = int.MinValue;
        var margin = caves.WaterMargin;
        var water = WaterLimits(ctx, margin, caves.BedClearance);
        for (var lz = 0; lz < VoxelChunk.Size; lz++)
        {
            for (var lx = 0; lx < VoxelChunk.Size; lx++)
            {
                var (x, z) = (ctx.ChunkMinX + lx, ctx.ChunkMinZ + lz);
                var k = (lz * VoxelChunk.Size) + lx;
                var height = ctx.HeightAt(x, z);
                var ceiling = Math.Min(Math.Min(height - caves.SurfaceCover, caves.TopY), water[k]);
                if (world.Regions is { } blend && blend.Regions.Nearest(x, z) is { Region: { } region, Distance: 0 })
                {
                    // Measured from the region's own bottom when that is under y 0 (a town cut deeper than the valley), so a cave never touches the region's lowest layer.
                    ceiling = Math.Min(ceiling, caves.UnderRegionCeiling + Math.Min(0, region.Bounds.MinY));
                }

                ceilings[k] = ceiling;
                highest = Math.Max(highest, ceiling);
            }
        }

        return new CavePlan(ceilings, WetColumns(ctx, u.Data.Linings?.WetReach ?? 0), highest);
    }

    // Which of the chunk column's columns have a water column within reach columns (a square round each).
    private static bool[] WetColumns(ColumnContext ctx, int reach)
    {
        const int Size = VoxelChunk.Size;
        var wet = new bool[Size * Size];
        if (reach <= 0)
        {
            return wet;
        }

        var span = Size + (2 * reach);
        var rows = new bool[span * Size];
        for (var rz = 0; rz < span; rz++)
        {
            var z = ctx.ChunkMinZ - reach + rz;
            for (var lx = 0; lx < Size; lx++)
            {
                for (var rx = 0; rx <= 2 * reach && !rows[(rz * Size) + lx]; rx++)
                {
                    rows[(rz * Size) + lx] = ctx.WaterLevelAt(ctx.ChunkMinX - reach + lx + rx, z) >= 0;
                }
            }
        }

        for (var lz = 0; lz < Size; lz++)
        {
            for (var lx = 0; lx < Size; lx++)
            {
                for (var rz = 0; rz <= 2 * reach && !wet[(lz * Size) + lx]; rz++)
                {
                    wet[(lz * Size) + lx] = rows[((lz + rz) * Size) + lx];
                }
            }
        }

        return wet;
    }

    // For each of the chunk column's 16 x 16 columns, the highest y the water rule lets a cave reach: under any water column of the cube of columns round it, the bed less the margin; under its own
    // water, the bed less the clearance. The minimum over the cube is taken along x and then along z.
    private static int[] WaterLimits(ColumnContext ctx, int margin, int clearance)
    {
        const int Size = VoxelChunk.Size;
        var reach = Size + (2 * margin);
        var rows = new int[reach * Size];
        for (var rz = 0; rz < reach; rz++)
        {
            var z = ctx.ChunkMinZ - margin + rz;
            for (var lx = 0; lx < Size; lx++)
            {
                var low = int.MaxValue;
                for (var rx = 0; rx <= 2 * margin; rx++)
                {
                    var x = ctx.ChunkMinX - margin + lx + rx;
                    if (ctx.WaterLevelAt(x, z) >= 0)
                    {
                        low = Math.Min(low, ctx.HeightAt(x, z) - margin); // a river's or a lake's bed counts as the sea's: the water's own level does not matter, the bed does
                    }
                }

                rows[(rz * Size) + lx] = low;
            }
        }

        var limits = new int[Size * Size];
        for (var lz = 0; lz < Size; lz++)
        {
            for (var lx = 0; lx < Size; lx++)
            {
                var low = int.MaxValue;
                for (var rz = 0; rz <= 2 * margin; rz++)
                {
                    low = Math.Min(low, rows[((lz + rz) * Size) + lx]);
                }

                var own = ctx.HeightAt(ctx.ChunkMinX + lx, ctx.ChunkMinZ + lz);
                limits[(lz * Size) + lx] = ctx.WaterLevelAt(ctx.ChunkMinX + lx, ctx.ChunkMinZ + lz) >= 0 ? Math.Min(low, own - clearance) : low;
            }
        }

        return limits;
    }
}
