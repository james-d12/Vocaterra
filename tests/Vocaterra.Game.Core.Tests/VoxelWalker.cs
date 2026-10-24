using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// A headless stand-in for the player in the tests of the 3D scene: it walks a body along the shortest path over standable cells (a breadth
/// first search, one level up or down per step), aims at a block through the simulation's own ray, and sends the same commands a front-end
/// does, one tick each. Every tick goes through <see cref="VoxelSession.Step"/>, so the objective and the word cards see the events.
/// </summary>
internal static class VoxelWalker
{
    private const int U = VoxelPhysics.UnitsPerBlock;
    private const double Arrived = 0.12;
    private const int ReachBlocks = 4;
    private const double TurnSteps = 4096.0;

    private static readonly (int Dx, int Dz)[] Directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Walks the body until its feet stand on a cell the goal accepts. Returns false when there is no path or the tick limit runs out.</summary>
    public static bool WalkTo(VoxelSession session, Func<BlockPos, bool> goal, int maxTicks = 30000)
    {
        var sim = session.Simulation;
        var path = FindPath(session.Scene.Grid, sim.Player.FeetBlock, goal);
        if (path is null)
        {
            return false;
        }

        var ticks = 0;
        foreach (var cell in path.Skip(1))
        {
            var tx = cell.X + 0.5;
            var tz = cell.Z + 0.5;
            while (true)
            {
                var x = sim.Player.CenterX / (double)U;
                var z = sim.Player.CenterZ / (double)U;
                var ex = tx - x;
                var ez = tz - z;
                if (Math.Abs(ex) < Arrived && Math.Abs(ez) < Arrived)
                {
                    break;
                }

                if (++ticks > maxTicks)
                {
                    return false;
                }

                var dx = Math.Abs(ex) >= Arrived ? Math.Sign(ex) : 0;
                var dz = Math.Abs(ez) >= Arrived ? Math.Sign(ez) : 0;
                session.Step([VoxelCommand.Move(dx, dz)]);
            }
        }

        return true;
    }

    /// <summary>Holds the gather command on a block until it is gone (gathered) or the tick limit runs out. Returns whether it went.</summary>
    public static bool Gather(VoxelSession session, BlockPos target, int maxTicks = 200)
    {
        var grid = session.Scene.Grid;
        var before = grid.GetBlock(target.X, target.Y, target.Z);
        var (yaw, pitch) = AimAt(session, target);
        for (var ticks = 0; ticks < maxTicks; ticks++)
        {
            session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(target.X, target.Y, target.Z)]);
            if (grid.GetBlock(target.X, target.Y, target.Z) != before)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Places one item in a cell (the simulation checks the reach and the neighbour). Returns whether the cell now holds the block.</summary>
    public static bool Place(VoxelSession session, ItemId item, BlockPos cell)
    {
        session.Step([VoxelCommand.Place(item, cell.X, cell.Y, cell.Z)]);
        return session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z) != BlockId.Air;
    }

    /// <summary>Crafts a recipe by index for one tick. Returns the simulation's answer as the events show it (the output count rises when it worked).</summary>
    public static void Craft(VoxelSession session, int recipeIndex) => session.Step([VoxelCommand.Craft(recipeIndex)]);

    /// <summary>The yaw and pitch steps the body needs to look at the middle of a block, and a check that the simulation's ray hits it.</summary>
    public static (int Yaw, int Pitch) AimAt(VoxelSession session, BlockPos target)
    {
        var body = session.Simulation.Player;
        var dx = ((long)target.X * U) + (U / 2) - body.EyeX;
        var dy = ((long)target.Y * U) + (U / 2) - body.EyeY;
        var dz = ((long)target.Z * U) + (U / 2) - body.EyeZ;
        var yaw = (int)Math.Round(Math.Atan2(dx, -dz) * TurnSteps / (2 * Math.PI));
        var pitch = (int)Math.Round(Math.Atan2(dy, Math.Sqrt((dx * dx) + (dz * dz))) * TurnSteps / (2 * Math.PI));
        pitch = Math.Clamp(pitch, -1023, 1023);
        yaw = Angles.Wrap(yaw);
        var grid = session.Scene.Grid;
        for (var dyaw = -8; dyaw <= 8; dyaw++)
        {
            for (var dpitch = -8; dpitch <= 8; dpitch++)
            {
                var hit = BlockRay.Cast(grid, body.EyeX, body.EyeY, body.EyeZ, Angles.Wrap(yaw + dyaw), Math.Clamp(pitch + dpitch, -1023, 1023), ReachBlocks);
                if (hit is { Block: var block } && block == target)
                {
                    return (Angles.Wrap(yaw + dyaw), Math.Clamp(pitch + dpitch, -1023, 1023));
                }
            }
        }

        return (yaw, pitch);
    }

    /// <summary>The first standable cell (a breadth first search over feet cells) that the goal accepts, as the path from the start.</summary>
    public static List<BlockPos>? FindPath(VoxelGrid grid, BlockPos start, Func<BlockPos, bool> goal)
    {
        var previous = new Dictionary<BlockPos, BlockPos> { [start] = start };
        var queue = new Queue<BlockPos>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (goal(current))
            {
                var path = new List<BlockPos> { current };
                while (!previous[current].Equals(current))
                {
                    current = previous[current];
                    path.Add(current);
                }

                path.Reverse();
                return path;
            }

            foreach (var (dx, dz) in Directions)
            {
                // One block up, level, or down by up to three (a fall is free in this game), with the shaft of the move free.
                for (var dy = 1; dy >= -3; dy--)
                {
                    var next = new BlockPos(current.X + dx, current.Y + dy, current.Z + dz);
                    if (previous.ContainsKey(next) || !Standable(grid, next))
                    {
                        continue;
                    }

                    var clear = true;
                    for (var y = next.Y; y <= Math.Max(next.Y + 1, current.Y + 1) && clear; y++)
                    {
                        clear = !grid.IsSolid(next.X, y, next.Z);
                    }

                    if (clear && (dy <= 0 || !grid.IsSolid(current.X, current.Y + 2, current.Z)))
                    {
                        previous[next] = current;
                        queue.Enqueue(next);
                        break;
                    }
                }
            }
        }

        return null;
    }

    private static bool Standable(VoxelGrid grid, BlockPos feet) =>
        grid.InBounds(feet.X, feet.Y, feet.Z)
        && !grid.IsSolid(feet.X, feet.Y, feet.Z)
        && !grid.IsSolid(feet.X, feet.Y + 1, feet.Z)
        && grid.IsSolid(feet.X, feet.Y - 1, feet.Z)
        && grid.GetBlock(feet.X, feet.Y, feet.Z) != BlockId.Water
        && grid.GetBlock(feet.X, feet.Y - 1, feet.Z) != BlockId.Water;
}
