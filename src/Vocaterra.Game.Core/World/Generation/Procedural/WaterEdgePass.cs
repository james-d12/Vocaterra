namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// The edge of the water (B03): the valley's <c>BankBesideWater</c>, ported (H12, D-game-26). Every dry column beside water is brought into the range a body in the water can leave from: not
/// below the water's top (so the water stays in), and no more than a body in the water can climb: one block over the bed of water too shallow to swim, one block over the surface of water
/// deep enough to swim (the valley sets only the first, because its layout has no steep ground beside deep water, and a generated world has). A face taller than the cut's reach (<see cref="GenerationWorld.BankCutReach"/>) is a rock face, not a bank, and keeps its
/// height; the valley cuts only a shoulder of 4, but its layout has no wall of 5 to 12 blocks beside shallow water, and a generated world has. The cut reads the heights as they were, so it does not cascade. In the procedural world every water block is a source at the sea level, so the water's top is the sea level and the
/// bed of a water column is its height. The outer ring of the window has no neighbours to read and is left alone.
/// </summary>
internal sealed class WaterEdgePass : IColumnShaper
{
    private readonly GenerationWorld _world;

    public WaterEdgePass(GenerationWorld world) => _world = world;

    /// <summary>The pass's name.</summary>
    public const string Name = "gen/waterside";

    /// <inheritdoc />
    public string PassName => Name;

    /// <inheritdoc />
    public void Shape(ColumnContext ctx)
    {
        var size = ctx.Size;
        var sea = _world.SeaLevel;
        var before = (int[])ctx.Heights.Clone();
        for (var j = 1; j < size - 1; j++)
        {
            for (var i = 1; i < size - 1; i++)
            {
                var k = (j * size) + i;
                if (before[k] < sea)
                {
                    continue; // water
                }

                var ceiling = int.MaxValue;
                var wet = false;
                Beside(before, k + 1, sea, ref wet, ref ceiling);
                Beside(before, k - 1, sea, ref wet, ref ceiling);
                Beside(before, k + size, sea, ref wet, ref ceiling);
                Beside(before, k - size, sea, ref wet, ref ceiling);

                if (wet)
                {
                    // A face taller than the reach of the cut is a cliff, not a bank: it keeps its height.
                    var cut = before[k] - ceiling > GenerationWorld.BankCutReach ? before[k] : Math.Min(before[k], ceiling);
                    ctx.Heights[k] = Math.Max(sea, cut);
                }
            }
        }
    }

    private static void Beside(int[] before, int n, int sea, ref bool wet, ref int ceiling)
    {
        if (before[n] >= sea)
        {
            return;
        }

        wet = true;
        if (sea - before[n] < 2)
        {
            ceiling = Math.Min(ceiling, before[n] + 1); // too shallow to swim: a step over the bed
        }
        else
        {
            ceiling = Math.Min(ceiling, sea + 1); // deep enough to swim: the hop out reaches a block over the surface and no more
        }
    }
}
