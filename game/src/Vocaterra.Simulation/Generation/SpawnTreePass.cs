using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 6: a tree within <see cref="MaxDistance"/> columns of the spawn. The forest pass only plants a tree in a cell with a 55 % chance, so a
/// world can have no tree near its spawn, and the first demo (the guide pointing at the nearest tree) needs one. If the forest already left a
/// tree base within <see cref="MaxDistance"/> columns of the spawn column, nothing happens. Otherwise the pass tries the columns at distance
/// <see cref="MinDistance"/> to <see cref="MaxDistance"/>, nearest first, the left column before the right one, and plants a tree on the first
/// that suits a tree as the forest pass requires (grass, flat enough, free space) and keeps three columns from every other trunk. It runs after
/// the spawn pass because it needs the spawn column, and stays clear of it: <see cref="MinDistance"/> columns away, a canopy never reaches the
/// spawn's headroom.
/// </summary>
internal sealed class SpawnTreePass : IGenerationPass
{
    /// <summary>The farthest a guaranteed tree may stand from the spawn column, in tiles (the brief: within 20 tiles).</summary>
    public const int MaxDistance = 20;

    /// <summary>The nearest a planted tree may stand to the spawn column, in tiles.</summary>
    public const int MinDistance = 10;

    private const int MinTrunk = 4;
    private const int MaxTrunkExclusive = 9;
    private const int TrunkSpacing = 3;

    public string Name => "spawn-tree";

    public void Run(GenerationContext context)
    {
        var spawn = context.Spawn ?? throw new InvalidOperationException("The spawn pass must run before the spawn-tree pass.");
        foreach (var treeBase in context.Record.TreeBases)
        {
            if (Math.Abs(treeBase.X - spawn.X) <= MaxDistance)
            {
                return;
            }
        }

        // The draw is made whether or not a site is found, so the stream never depends on the search.
        var trunkHeight = context.Rng(Name).NextInt(MinTrunk, MaxTrunkExclusive);
        var tiles = context.Tiles;
        var surface = context.Record.SurfaceY;
        var width = context.Size.Width;
        for (var distance = MinDistance; distance <= MaxDistance; distance++)
        {
            for (var side = 0; side < 2; side++)
            {
                var x = side == 0 ? spawn.X - distance : spawn.X + distance;
                if (x < 3 || x > width - 4)
                {
                    continue;
                }

                var ground = surface[x];
                if (tiles.GetTile(x, ground) == TileId.Grass
                    && Math.Abs(surface[x - 1] - ground) <= 1
                    && Math.Abs(surface[x + 1] - ground) <= 1
                    && ForestPass.TrunkSpaceIsFree(tiles, x, ground, trunkHeight)
                    && !HasTrunkNear(context, x))
                {
                    ForestPass.PlantTree(tiles, x, ground, trunkHeight);
                    context.Record.AddTree(new TilePoint(x, ground));
                    return;
                }
            }
        }

        throw new InvalidOperationException($"No column within {MaxDistance} tiles of the spawn can hold a tree.");
    }

    private static bool HasTrunkNear(GenerationContext context, int x)
    {
        foreach (var treeBase in context.Record.TreeBases)
        {
            if (Math.Abs(treeBase.X - x) < TrunkSpacing)
            {
                return true;
            }
        }

        return false;
    }
}
