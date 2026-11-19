using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// The small icons of the equipment panel's stats line (I04), drawn from rectangles on a 7 x 7 grid, as the needs' icons are: no picture file, CC0 by construction. Each is a row of data keyed by the stat's key; a stat
/// with no row (a new accessory's) gets <see cref="Fallback"/>, a plain diamond, so the line never fails for want of an icon.
/// </summary>
public static class StatIcons
{
    /// <summary>The grid's side.</summary>
    public const int Size = 7;

    /// <summary>The icon of a stat nobody drew one for yet.</summary>
    public static readonly string[] Fallback =
    [
        "...#...",
        "..###..",
        ".#####.",
        "#######",
        ".#####.",
        "..###..",
        "...#...",
    ];

    private static readonly Dictionary<string, string[]> Rows = new(StringComparer.Ordinal)
    {
        [StatKeys.Armour] = ["#######", "#######", "#######", ".#####.", ".#####.", "..###..", "...#..."],
        [StatKeys.Warmth] = ["...#...", "..##...", ".###.#.", ".#####.", "#######", "#######", ".#####."],
        [StatKeys.WalkSpeed] = ["#..#...", ".#..#..", "..#..#.", "...#..#", "..#..#.", ".#..#..", "#..#..."],
        [StatKeys.HungerDrain] = ["...#...", "..###..", ".#####.", "#######", "#######", ".#####.", "..#.#.."],
        [StatKeys.ThirstDrain] = ["...#...", "...#...", "..###..", ".#####.", "#######", "#######", ".#####."],
        [StatKeys.Light] = ["...#...", ".#.#.#.", "..###..", "###.###", "..###..", ".#.#.#.", "...#..."],
        [StatKeys.MaxStamina] = ["....##.", "...##..", "..##...", ".#####.", "...##..", "..##...", ".##...."],
    };

    /// <summary>The rows of an icon: seven strings of seven cells, <c>#</c> filled.</summary>
    /// <param name="key">The stat's key.</param>
    public static IReadOnlyList<string> Of(string key) => Rows.TryGetValue(key, out var rows) ? rows : Fallback;

    /// <summary>Draws the stat's icon in a square box.</summary>
    /// <param name="spriteBatch">The batch.</param>
    /// <param name="pixel">The 1 x 1 white texture.</param>
    /// <param name="key">The stat's key.</param>
    /// <param name="box">The square to draw in; each grid cell is a whole number of pixels (at least one), the picture centred.</param>
    /// <param name="colour">The icon's colour.</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string key, Rectangle box, Color colour)
    {
        var rows = Of(key);
        var unit = Math.Max(1, Math.Min(box.Width, box.Height) / Size);
        var left = box.X + ((box.Width - (unit * Size)) / 2);
        var top = box.Y + ((box.Height - (unit * Size)) / 2);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (rows[y][x] == '#')
                {
                    spriteBatch.Draw(pixel, new Rectangle(left + (x * unit), top + (y * unit), unit, unit), colour);
                }
            }
        }
    }
}
