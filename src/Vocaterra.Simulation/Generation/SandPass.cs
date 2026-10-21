using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 7: sand patches. Where one-dimensional value noise (wavelength 24 columns) is above a threshold, the grass at the surface becomes sand,
/// and the dirt under it down to two tiles. The pass runs after every pass that places trees and the spawn, and it leaves untouched the
/// columns within <see cref="TreeColumns"/>' clearance of a tree base (so no trunk stands on sand and no canopy changes) and within
/// <see cref="SpawnClearance"/> of the spawn column (so the spawn's ground stays grass). Terrain and trees do not move.
/// </summary>
internal sealed class SandPass : IGenerationPass
{
    /// <summary>The noise threshold: about one column in six is a sand column.</summary>
    private const int Threshold = 56000;

    private const int Wavelength = 24;
    private const int Depth = 2;
    private const int TreeClearance = 2;
    private const int SpawnClearance = 4;

    public string Name => "sand";

    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        var surface = context.Record.SurfaceY;
        var width = context.Size.Width;
        var spawn = context.Spawn ?? throw new InvalidOperationException("The spawn pass must run before the sand pass.");
        var seed = context.NoiseSeed(Name);
        var nearTree = TreeColumns.Mark(context.Record, width, TreeClearance);

        for (var x = 0; x < width; x++)
        {
            if (ValueNoise.Sample1D(seed, x, Wavelength) < Threshold || nearTree[x] || Math.Abs(x - spawn.X) <= SpawnClearance)
            {
                continue;
            }

            var top = surface[x];
            if (tiles.GetTile(x, top) != TileId.Grass)
            {
                continue;
            }

            tiles.SetTile(x, top, TileId.Sand);
            for (var dy = 1; dy <= Depth; dy++)
            {
                if (tiles.GetTile(x, top + dy) == TileId.Dirt)
                {
                    tiles.SetTile(x, top + dy, TileId.Sand);
                }
            }
        }
    }
}

/// <summary>Which columns lie near a tree base, for passes that must not touch a tree.</summary>
internal static class TreeColumns
{
    /// <summary>One flag per column: true within <paramref name="clearance"/> columns of a tree base.</summary>
    public static bool[] Mark(GenerationRecord record, int width, int clearance)
    {
        var flags = new bool[width];
        foreach (var treeBase in record.TreeBases)
        {
            for (var x = Math.Max(0, treeBase.X - clearance); x <= Math.Min(width - 1, treeBase.X + clearance); x++)
            {
                flags[x] = true;
            }
        }

        return flags;
    }
}
