using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>
/// How many valid plots fit in a meadow without overlapping (settlement-plan 9.2's "fit"): the best of six greedy picks (by row, by column, by distance to the centre, each way) among the
/// valid origins whose middle column lies within the radius of the meadow's centre. A lower bound, as the plan's was.
/// </summary>
internal static class PlotFit
{
    public static int Best(LandRules rules, VoxelGrid grid, int centreX, int centreZ, int radius)
    {
        var valid = new List<(int X, int Z)>();
        for (var z = centreZ - radius - rules.PlotDepth; z <= centreZ + radius; z++)
        {
            for (var x = centreX - radius - rules.PlotWidth; x <= centreX + radius; x++)
            {
                var plot = rules.PlotAt(x, z);
                var (cx, cz) = plot.Centre;
                if (((cx - centreX) * (cx - centreX)) + ((cz - centreZ) * (cz - centreZ)) <= radius * radius && rules.CheckPlot(grid, plot).Ok)
                {
                    valid.Add((x, z));
                }
            }
        }

        long Distance((int X, int Z) v) => ((long)(v.X - centreX) * (v.X - centreX)) + ((long)(v.Z - centreZ) * (v.Z - centreZ));
        var orders = new List<IEnumerable<(int X, int Z)>>
        {
            valid.OrderBy(static v => v.Z).ThenBy(static v => v.X),
            valid.OrderByDescending(static v => v.Z).ThenByDescending(static v => v.X),
            valid.OrderBy(static v => v.X).ThenBy(static v => v.Z),
            valid.OrderByDescending(static v => v.X).ThenByDescending(static v => v.Z),
            valid.OrderBy(Distance),
            valid.OrderByDescending(Distance),
        };
        return orders.Max(order => Pick(order, rules));
    }

    private static int Pick(IEnumerable<(int X, int Z)> order, LandRules rules)
    {
        var picked = new List<(int X, int Z)>();
        foreach (var origin in order)
        {
            if (picked.All(p => Math.Abs(p.X - origin.X) >= rules.PlotWidth || Math.Abs(p.Z - origin.Z) >= rules.PlotDepth))
            {
                picked.Add(origin);
            }
        }

        return picked.Count;
    }
}
