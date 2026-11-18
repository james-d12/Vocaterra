namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The edge of the water (B03): the valley's <c>BankBesideWater</c>, ported (H12, D-game-26). Every dry column beside water is brought into the range a body in the water can leave from: not
/// below the water's top (so the water stays in), and no more than a body in the water can climb: one block over the bed of water too shallow to swim, one block over the surface of water
/// deep enough to swim (the valley sets only the first, because its layout has no steep ground beside deep water, and a generated world has). A face taller than the cut's reach (<see cref="GenerationWorld.BankCutReach"/>) is a rock face, not a bank, and keeps its
/// height; the valley cuts only a shoulder of 4, but its layout has no wall of 5 to 12 blocks beside shallow water, and a generated world has. The cut reads the heights as they were, so it does not cascade. In the procedural world every water block is a source at the sea level, so the water's top is the sea level and the
/// bed of a water column is its height. With rivers and lakes at levels of their own (B02b round 3) the water's top is the column's own water level (<see cref="ColumnContext.WaterLevelAt"/>), and a dry column
/// beside water is never left under the water's top, so a bank holds the level of the water beside it. The outer ring of the window has no neighbours to read and is left alone.
/// </summary>
internal sealed class WaterEdgePass : IColumnShaper
{
    /// <summary>The pass's name.</summary>
    public const string Name = "gen/waterside";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Shape(ColumnContext ctx)
    {
        var size = ctx.Size;
        EvenLevels(ctx);
        CloseCorners(ctx);
        var before = (int[])ctx.Heights.Clone();
        var tops = new int[before.Length]; // the water's top of each column as the ground stood, -1 where it is dry
        for (var j = 0; j < size; j++)
        {
            for (var i = 0; i < size; i++)
            {
                tops[(j * size) + i] = ctx.WaterLevelAt(ctx.OriginX + i, ctx.OriginZ + j);
            }
        }

        for (var j = 1; j < size - 1; j++)
        {
            for (var i = 1; i < size - 1; i++)
            {
                var k = (j * size) + i;
                if (tops[k] >= 0)
                {
                    continue; // water
                }

                var ceiling = int.MaxValue;
                var held = int.MinValue; // the highest water top beside the column
                Beside(before, tops, k + 1, ref held, ref ceiling);
                Beside(before, tops, k - 1, ref held, ref ceiling);
                Beside(before, tops, k + size, ref held, ref ceiling);
                Beside(before, tops, k - size, ref held, ref ceiling);

                if (held >= 0)
                {
                    // A face taller than the reach of the cut is a cliff, not a bank: it keeps its height.
                    var cut = before[k] - ceiling > GenerationWorld.BankCutReach ? before[k] : Math.Min(before[k], ceiling);
                    ctx.Heights[k] = Math.Max(held, cut);
                }
            }
        }
    }

    // A river whose surface steps down where the ground crosses a level can run along that line for a stretch, so one half of it would stand a step higher than the other. Each column of river or lake water takes the highest level of the water within a few columns
    // (the sea's level is left alone), so the step lies across the river and not along it; the ground under the water stays, so the water is a little deeper there, and the banks are raised to hold it.
    private static void EvenLevels(ColumnContext ctx)
    {
        var sea = ctx.World.SeaLevel;
        var raised = new List<(int X, int Z, int Level)>();
        var reach = GenerationWorld.EvenReach;
        for (var z = ctx.OriginZ + reach; z < ctx.OriginZ + ctx.Size - reach; z++)
        {
            for (var x = ctx.OriginX + reach; x < ctx.OriginX + ctx.Size - reach; x++)
            {
                var own = ctx.WaterLevelAt(x, z);
                if (own <= sea)
                {
                    continue; // dry, or the sea's level (a river meets the sea at it)
                }

                var best = own;
                for (var dz = -reach; dz <= reach; dz++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        best = Math.Max(best, ctx.WaterLevelAt(x + dx, z + dz));
                    }
                }

                if (best > own)
                {
                    raised.Add((x, z, best));
                }
            }
        }

        foreach (var (x, z, level) in raised)
        {
            ctx.RaiseWater(x, z, level);
        }
    }

    // A dry column beside water of two different levels would need a wall at the higher one (to hold it in) and be a wall beside the lower one that nobody in the shallow water could climb: it becomes water at the lower level, and the higher water
    // falls into it as into any lower column of a step (see WaterPass). It is the corner where a step in a river's surface meets a bank that runs across it. A column turned into water can make the next one a corner, so it runs twice.
    private static void CloseCorners(ColumnContext ctx)
    {
        for (var round = 0; round < 2; round++)
        {
            var turned = new List<(int X, int Z, int Level)>();
            for (var z = ctx.OriginZ + 1; z < ctx.OriginZ + ctx.Size - 1; z++)
            {
                for (var x = ctx.OriginX + 1; x < ctx.OriginX + ctx.Size - 1; x++)
                {
                    if (ctx.WaterLevelAt(x, z) >= 0)
                    {
                        continue;
                    }

                    int high = -1, low = int.MaxValue;
                    foreach (var level in new[] { ctx.WaterLevelAt(x + 1, z), ctx.WaterLevelAt(x - 1, z), ctx.WaterLevelAt(x, z + 1), ctx.WaterLevelAt(x, z - 1) })
                    {
                        if (level >= 0)
                        {
                            high = Math.Max(high, level);
                            low = Math.Min(low, level);
                        }
                    }

                    if (high > low && low != int.MaxValue)
                    {
                        turned.Add((x, z, low));
                    }
                }
            }

            foreach (var (x, z, level) in turned)
            {
                ctx.TurnToWater(x, z, level);
            }

            if (turned.Count == 0)
            {
                return;
            }
        }
    }

    private static void Beside(int[] before, int[] tops, int n, ref int held, ref int ceiling)
    {
        if (tops[n] < 0)
        {
            return;
        }

        held = Math.Max(held, tops[n]);
        if (tops[n] - before[n] < 2)
        {
            ceiling = Math.Min(ceiling, before[n] + 1); // too shallow to swim: a step over the bed
        }
        else
        {
            ceiling = Math.Min(ceiling, tops[n] + 1); // deep enough to swim: the hop out reaches a block over the surface and no more
        }
    }
}
