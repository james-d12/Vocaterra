using System.Text;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Queries;

/// <summary>Renders a window of the world as text, for test output and bug reports (no engine needed).</summary>
public static class AsciiMap
{
    /// <summary>The character for a tile: air is a space, dirt <c>d</c>, grass <c>"</c>, stone <c>#</c>, trunk <c>|</c>, leaves <c>*</c>.</summary>
    public static char Glyph(TileId tile) => tile switch
    {
        TileId.Air => ' ',
        TileId.Dirt => 'd',
        TileId.Grass => '"',
        TileId.Stone => '#',
        TileId.Trunk => '|',
        TileId.Leaves => '*',
        _ => '?',
    };

    /// <summary>
    /// Renders <paramref name="width"/> x <paramref name="height"/> tiles centred on (<paramref name="centerX"/>, <paramref name="centerY"/>),
    /// one line per row. Tiles outside the world are drawn as <c>~</c>; with a <paramref name="player"/>, the tiles its box covers are <c>@</c>.
    /// </summary>
    public static string Render(World world, int centerX, int centerY, int width, int height, PlayerState? player = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var left = centerX - (width / 2);
        var top = centerY - (height / 2);
        var sb = new StringBuilder(height * (width + 1));
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (player is { } p && Covers(p, x, y))
                {
                    sb.Append('@');
                }
                else
                {
                    sb.Append(world.Tiles.InBounds(x, y) ? Glyph(world.GetTile(x, y)) : '~');
                }
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool Covers(PlayerState p, int tileX, int tileY)
    {
        const int u = PhysicsConstants.UnitsPerTile;
        return tileX * u < p.Right && (tileX + 1) * u > p.X && tileY * u < p.Bottom && (tileY + 1) * u > p.Y;
    }
}
