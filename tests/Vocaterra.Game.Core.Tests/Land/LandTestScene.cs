using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>Real places of the route on seed 42 for the land tests, and the moves a headless player makes to reach them.</summary>
internal static class LandTestScene
{
    /// <summary>The seed the tests with real coordinates use.</summary>
    public const ulong Seed = 42;

    /// <summary>A valid 10 by 10 plot of the north meadow on seed 42 (grass, no tree, level within one block).</summary>
    public static readonly (int X, int Z) NorthPlot = (261, 160);

    /// <summary>The first valid plot of the north meadow, scanning by row, that does not overlap <see cref="NorthPlot"/>.</summary>
    public static (int X, int Z) OtherPlot(VoxelSession session)
    {
        var rules = session.Land.Rules;
        var centre = session.Scene.Point("meadow.north");
        for (var z = centre.Z - 30; z <= centre.Z + 22; z++)
        {
            for (var x = centre.X - 32; x <= centre.X + 22; x++)
            {
                if ((Math.Abs(x - NorthPlot.X) >= rules.PlotWidth || Math.Abs(z - NorthPlot.Z) >= rules.PlotDepth) && rules.CheckPlot(session.Scene.Grid, rules.PlotAt(x, z)).Ok)
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException("No second plot in the north meadow.");
    }

    /// <summary>The rules for a session's scene, built as the session builds them.</summary>
    public static LandRules RulesOf(VoxelSession session) => session.Land.Rules;

    /// <summary>The cell on top of a column's ground, where a block is placed.</summary>
    public static BlockPos AboveGround(VoxelSession session, int x, int z) => new(x, LandRules.GroundOf(session.Scene.Grid, x, z).Y + 1, z);

    /// <summary>Puts the player two blocks south of the column on the ground there and returns the cell above the column's ground.</summary>
    public static BlockPos StandBeside(VoxelSession session, int x, int z)
    {
        var (standY, _, _) = LandRules.GroundOf(session.Scene.Grid, x, z + 2);
        session.Simulation.TeleportTo(new BlockPos(x, standY + 1, z + 2));
        return AboveGround(session, x, z);
    }

    /// <summary>A column within the radius of the centre whose ground is grass, dirt or a path, with free air above it and the two columns south of it, so a player can stand there and place on it.</summary>
    public static (int X, int Z) OpenColumn(VoxelGrid grid, int centreX, int centreZ, int radius, Func<int, int, bool> accept)
    {
        for (var z = centreZ - radius; z <= centreZ + radius; z++)
        {
            for (var x = centreX - radius; x <= centreX + radius; x++)
            {
                if (accept(x, z) && Open(grid, x, z) && Open(grid, x, z + 1) && Open(grid, x, z + 2))
                {
                    return (x, z);
                }
            }
        }

        throw new InvalidOperationException($"No open column within {radius} of ({centreX}, {centreZ}).");
    }

    private static bool Open(VoxelGrid grid, int x, int z)
    {
        var (y, block, tree) = LandRules.GroundOf(grid, x, z);
        return block is BlockId.Grass or BlockId.Dirt or BlockId.Path && !tree
            && grid.GetBlock(x, y + 1, z) == BlockId.Air && grid.GetBlock(x, y + 2, z) == BlockId.Air && grid.GetBlock(x, y + 3, z) == BlockId.Air;
    }
}
