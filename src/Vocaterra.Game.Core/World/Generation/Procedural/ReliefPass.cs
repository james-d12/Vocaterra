using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The first shaper (B03): the surface strategies the biome rows name change the ground of their own biome. Every column asks its row's strategy for a change of height at full strength and
/// takes the part of it that the column's distance from the biome's border allows: a column right at the border takes 1 / (fade + 1), one fade width inside takes all. So two columns that
/// stand either side of a border differ by at most what the strategy's own relief differs by plus a fade width's worth of its slope, and a border never makes a cliff. With no row naming a
/// strategy but the plain one, the pass does nothing and classifies nothing.
/// </summary>
internal sealed class ReliefPass : IColumnShaper
{
    private readonly GenerationWorld _world;

    public ReliefPass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/relief";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Shape(ColumnContext ctx)
    {
        if (!_world.HasSurfaceStrategies)
        {
            return;
        }

        var clamp = _world.Data.Height.Clamp;
        var floor = _world.MinY + 1;
        for (var z = ctx.OriginZ; z < ctx.OriginZ + ctx.Size; z++)
        {
            for (var x = ctx.OriginX; x < ctx.OriginX + ctx.Size; x++)
            {
                var row = ctx.BiomeAt(x, z).Row;
                var strategy = _world.Strategies.Surface[row.Index];
                var shape = ctx.ShapeAt(x, z);
                var delta = strategy.HeightDelta(new SurfaceColumn(x, z, shape.Height, ctx.ClimateAt(x, z), row));
                if (delta == 0)
                {
                    continue;
                }

                var weight = strategy.FadeWidth <= 0 ? IntMath.One : FadeWeight(ctx, x, z, row.Index, strategy.FadeWidth);
                ctx.SetHeight(x, z, Math.Clamp(ctx.HeightAt(x, z) + (int)((long)delta * weight / IntMath.One), floor, clamp));
            }
        }
    }

    // 1 / (fade + 1) at the column next to a column of another biome, up to 1 once the nearest such column is more than one fade width away.
    private static int FadeWeight(ColumnContext ctx, int x, int z, int rowIndex, int fade)
    {
        for (var r = 1; r <= fade; r++)
        {
            for (var dz = -r; dz <= r; dz++)
            {
                var edge = dz == -r || dz == r;
                for (var dx = -r; dx <= r; dx += edge || dx == r ? 1 : 2 * r)
                {
                    if (ctx.InWindow(x + dx, z + dz) && ctx.BiomeAt(x + dx, z + dz).Row.Index != rowIndex)
                    {
                        return r * IntMath.One / (fade + 1);
                    }
                }
            }
        }

        return IntMath.One;
    }
}
