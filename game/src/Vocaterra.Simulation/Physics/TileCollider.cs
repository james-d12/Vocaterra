using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Physics;

/// <summary>
/// Axis-aligned box against tile collision in integer units. Movement is resolved one axis at a time, X first then Y, and a blocked move
/// snaps the box to the tile edge, so it works for any speed (no tunnelling) and leaves no gap.
/// </summary>
internal static class TileCollider
{
    private const int U = PhysicsConstants.UnitsPerTile;

    /// <summary>Whether the box overlaps no solid tile.</summary>
    public static bool IsFree(TileGrid grid, int x, int y, int width, int height)
    {
        var left = FloorDiv(x, U);
        var right = FloorDiv(x + width - 1, U);
        var top = FloorDiv(y, U);
        var bottom = FloorDiv(y + height - 1, U);
        for (var ty = top; ty <= bottom; ty++)
        {
            for (var tx = left; tx <= right; tx++)
            {
                if (grid.IsSolid(tx, ty))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Moves the box horizontally by <paramref name="dx"/> units; returns true (and snaps to the tile edge) if a solid tile stopped it.</summary>
    public static bool MoveX(TileGrid grid, ref int x, int y, int width, int height, int dx)
    {
        if (dx == 0)
        {
            return false;
        }

        var top = FloorDiv(y, U);
        var bottom = FloorDiv(y + height - 1, U);
        if (dx > 0)
        {
            var from = FloorDiv(x + width, U);
            var to = FloorDiv(x + width + dx - 1, U);
            for (var column = from; column <= to; column++)
            {
                if (ColumnBlocked(grid, column, top, bottom))
                {
                    x = (column * U) - width;
                    return true;
                }
            }
        }
        else
        {
            var from = FloorDiv(x - 1, U);
            var to = FloorDiv(x + dx, U);
            for (var column = from; column >= to; column--)
            {
                if (ColumnBlocked(grid, column, top, bottom))
                {
                    x = (column + 1) * U;
                    return true;
                }
            }
        }

        x += dx;
        return false;
    }

    /// <summary>Moves the box vertically by <paramref name="dy"/> units; returns true (and snaps to the tile edge) if a solid tile stopped it.</summary>
    public static bool MoveY(TileGrid grid, int x, ref int y, int width, int height, int dy)
    {
        if (dy == 0)
        {
            return false;
        }

        var left = FloorDiv(x, U);
        var right = FloorDiv(x + width - 1, U);
        if (dy > 0)
        {
            var from = FloorDiv(y + height, U);
            var to = FloorDiv(y + height + dy - 1, U);
            for (var row = from; row <= to; row++)
            {
                if (RowBlocked(grid, row, left, right))
                {
                    y = (row * U) - height;
                    return true;
                }
            }
        }
        else
        {
            var from = FloorDiv(y - 1, U);
            var to = FloorDiv(y + dy, U);
            for (var row = from; row >= to; row--)
            {
                if (RowBlocked(grid, row, left, right))
                {
                    y = (row + 1) * U;
                    return true;
                }
            }
        }

        y += dy;
        return false;
    }

    /// <summary>The floor division of a by a positive b (rounds towards negative infinity).</summary>
    public static int FloorDiv(int a, int b)
    {
        var q = a / b;
        return (a % b != 0 && a < 0) ? q - 1 : q;
    }

    private static bool ColumnBlocked(TileGrid grid, int column, int top, int bottom)
    {
        for (var ty = top; ty <= bottom; ty++)
        {
            if (grid.IsSolid(column, ty))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RowBlocked(TileGrid grid, int row, int left, int right)
    {
        for (var tx = left; tx <= right; tx++)
        {
            if (grid.IsSolid(tx, row))
            {
                return true;
            }
        }

        return false;
    }
}
