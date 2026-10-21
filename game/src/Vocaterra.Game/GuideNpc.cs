using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

/// <summary>
/// The guide's place in the world: a character standing on the surface near the spawn. It is a pure function of the generated world, so it
/// needs no state in the simulation and no field in a save; when characters move, collide or are saved (the innkeeper, G3), they become
/// entities of the simulation and this record becomes their first member. The guide's conversation state lives in the agent state machine
/// (<see cref="GuideService.GuideState"/>), not here.
/// </summary>
/// <param name="Feet">The air tile the guide stands in, directly above the ground.</param>
public sealed record GuideNpc(TilePoint Feet)
{
    /// <summary>The nearest column the guide may stand in from the spawn, so the player has to walk to it.</summary>
    public const int MinDistanceFromSpawn = 8;

    /// <summary>The farthest column the search tries.</summary>
    public const int MaxDistanceFromSpawn = 28;

    /// <summary>
    /// The columns of clear ground the guide prefers on each side: no tree base this close, so that a question about the nearest tree is
    /// answered with a tree the player still has to walk to (and the arrow is not cleared the moment it appears).
    /// </summary>
    public const int TreeClearance = 7;

    /// <summary>The player is near the guide within this many columns.</summary>
    public const int NearColumns = 5;

    /// <summary>The player is near the guide within this many rows.</summary>
    public const int NearRows = 4;

    /// <summary>The air needed above the ground, in tiles, for the guide to fit (the figure is drawn about three tiles tall).</summary>
    private const int Headroom = 3;

    /// <summary>
    /// Finds the guide's tile: a grass column at 8 to 28 columns from the spawn with three tiles of air above it, the right side before the
    /// left and the nearest first, preferring one with no tree within <see cref="TreeClearance"/> columns. If every column is blocked or
    /// crowded by trees, the nearest with headroom is taken, and failing that the column 8 to the right of the spawn.
    /// </summary>
    public static GuideNpc Place(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var requireClearance in new[] { true, false })
        {
            for (var distance = MinDistanceFromSpawn; distance <= MaxDistanceFromSpawn; distance++)
            {
                for (var side = 0; side < 2; side++)
                {
                    var x = side == 0 ? world.Spawn.X + distance : world.Spawn.X - distance;
                    if (x < 1 || x >= world.Width - 1)
                    {
                        continue;
                    }

                    var ground = world.Record.SurfaceHeights[x];
                    if (world.GetTile(x, ground) == TileId.Grass
                        && HasHeadroom(world, x, ground)
                        && (!requireClearance || !HasTreeNear(world, x)))
                    {
                        return new GuideNpc(new TilePoint(x, ground - 1));
                    }
                }
            }
        }

        var fallback = Math.Min(world.Spawn.X + MinDistanceFromSpawn, world.Width - 2);
        return new GuideNpc(new TilePoint(fallback, world.Record.SurfaceHeights[fallback] - 1));
    }

    /// <summary>Whether the player is close enough to talk: within <see cref="NearColumns"/> columns and <see cref="NearRows"/> rows of the guide.</summary>
    public bool IsNear(PlayerState player)
    {
        var center = WorldFacts.CenterTile(player);
        return Math.Abs(center.X - Feet.X) <= NearColumns && Math.Abs(center.Y - Feet.Y) <= NearRows;
    }

    private static bool HasTreeNear(World world, int x)
    {
        foreach (var treeBase in world.Record.TreeBases)
        {
            if (Math.Abs(treeBase.X - x) < TreeClearance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasHeadroom(World world, int x, int ground)
    {
        for (var dy = 1; dy <= Headroom; dy++)
        {
            if (world.GetTile(x, ground - dy) != TileId.Air)
            {
                return false;
            }
        }

        return true;
    }
}
