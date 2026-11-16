using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Underground;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// What the caves may do in the 16 x 16 columns of one chunk column (B11, biomes plan section 3.5), settled once from the shaped ground and shared by all 24 layers:
/// <list type="bullet">
/// <item>the <see cref="Ceiling"/> of each column, the highest y a cave may reach: the ground less the surface cover, at most the caves' top, at most the ceiling under an authored region, and kept
/// clear of water: at most <c>bed - margin</c> under any water column within the water margin (a cube round the column), and <c>bed - clearance</c> under the column's own water;</item>
/// <item>the entrance <see cref="ShaftFrom"/> of each column: where the entrance mask puts a zone on dry procedural land (no region or margin, the ground over the sea, no water within the entrances'
/// reach) and a cave lies within the probe depth under the cave top, a shaft is cut from the highest such cave air up through the ground.</item>
/// </list>
/// Every number is read from final heights inside the window (its margin covers the reach), so a column's plan is the same whichever chunk column it is made in.
/// </summary>
internal sealed class CavePlan
{
    private CavePlan(int[] ceiling, int[] shaftFrom, int highestAffected, int entrances)
    {
        Ceiling = ceiling;
        ShaftFrom = shaftFrom;
        HighestAffected = highestAffected;
        Entrances = entrances;
    }

    /// <summary>The highest y a cave reaches in each column, row by row (z), x fastest, of the chunk column's 16 x 16 columns.</summary>
    public int[] Ceiling { get; }

    /// <summary>The lowest y of each column's entrance shaft (it runs up to the ground), or <see cref="int.MaxValue"/> where the column has none.</summary>
    public int[] ShaftFrom { get; }

    /// <summary>The highest y any cave or shaft of the chunk column can touch: a chunk above it is untouched.</summary>
    public int HighestAffected { get; }

    /// <summary>How many columns of the chunk column open an entrance.</summary>
    public int Entrances { get; }

    /// <summary>The plan of a shaped chunk column.</summary>
    /// <param name="ctx">The shaped column; its world must have an underground.</param>
    public static CavePlan Build(ColumnContext ctx)
    {
        var world = ctx.World;
        var u = world.Underground ?? throw new InvalidOperationException("The world has no underground.");
        var caves = u.Data.Caves;
        var entrances = u.Entrances;
        var ceilings = new int[VoxelChunk.Size * VoxelChunk.Size];
        var shafts = new int[VoxelChunk.Size * VoxelChunk.Size];
        var highest = int.MinValue;
        var opened = 0;
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
                shafts[k] = int.MaxValue;
                highest = Math.Max(highest, ceiling);
                if (entrances.InZone(x, z) && Opens(ctx, u, x, z, height) is { } from)
                {
                    shafts[k] = from;
                    highest = Math.Max(highest, height);
                    opened++;
                }
            }
        }

        return new CavePlan(ceilings, shafts, highest, opened);
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

    // The y a column's shaft starts at, or null when the zone stays closed here.
    private static int? Opens(ColumnContext ctx, UndergroundWorld u, int x, int z, int height)
    {
        var world = ctx.World;
        var spec = u.Data.Entrances;
        if (height < world.SeaLevel + spec.MinHeightAboveSea || (world.Regions is { } blend && blend.Regions.Nearest(x, z).Distance < blend.Data.Margin))
        {
            return null;
        }

        for (var dz = -spec.WaterReach; dz <= spec.WaterReach; dz++)
        {
            for (var dx = -spec.WaterReach; dx <= spec.WaterReach; dx++)
            {
                if (ctx.WaterLevelAt(x + dx, z + dz) >= 0)
                {
                    return null;
                }
            }
        }

        var top = Math.Min(height - u.Data.Caves.SurfaceCover, u.Data.Caves.TopY);
        var air = u.Caves.HighestCaveBlock(x, z, top, Math.Max(top - spec.ProbeDepth, u.Floor.TopAt(x, z) + 1));
        if (air != int.MinValue)
        {
            return air + 1;
        }

        return null;
    }
}
