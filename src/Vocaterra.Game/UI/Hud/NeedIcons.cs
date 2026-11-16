using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.UI.Hud;

/// <summary>
/// The icons of the HUD (N05), drawn in code from plain shapes: a loaf for hunger, a drop for thirst, a moon for sleep and, for the cold (I06), a snowflake. Each is a set of horizontal runs on an
/// 18 x 18 grid (computed once from integer-free geometry, the same on every machine), so the drawing is a few rectangles and a test can check a shape stays on its grid.
/// No asset is used.
/// </summary>
public static class NeedIcons
{
    /// <summary>The icon's grid size.</summary>
    public const int Size = 18;

    private static readonly Dictionary<Drive, IReadOnlyList<(int Y, int X0, int X1)>> Shapes = new()
    {
        [Drive.Hunger] = Runs(static (x, y) => Math.Pow((x - 9) / 8.2, 2) + Math.Pow((y - 10) / 6.0, 2) <= 1.0),
        [Drive.Thirst] = Runs(static (x, y) => Math.Pow(x - 9, 2) + Math.Pow(y - 11.2, 2) <= 6.6 * 6.6 || (y is >= 1 and <= 11 && Math.Abs(x - 9) <= (y - 1) / 10.0 * 6.4)),
        [Drive.Sleep] = Runs(static (x, y) => Math.Pow(x - 9, 2) + Math.Pow(y - 9, 2) <= 8.0 * 8.0 && Math.Pow(x - 13, 2) + Math.Pow(y - 6.5, 2) > 6.5 * 6.5),
    };

    /// <summary>The cold's icon (I06): a snowflake of four crossing arms, each as thick as a pixel and a half, inside a circle of radius 8.</summary>
    public static IReadOnlyList<(int Y, int X0, int X1)> ColdShape { get; } = Runs(static (x, y) =>
    {
        double dx = x - 9;
        double dy = y - 9;
        return (dx * dx) + (dy * dy) <= 8.2 * 8.2 && (Math.Abs(dx) <= 0.8 || Math.Abs(dy) <= 0.8 || Math.Abs(dx - dy) <= 1.1 || Math.Abs(dx + dy) <= 1.1);
    });

    /// <summary>The runs of a drive's icon: row <c>Y</c>, from column <c>X0</c> to <c>X1</c> inclusive, top to bottom.</summary>
    /// <param name="drive">Which drive.</param>
    public static IReadOnlyList<(int Y, int X0, int X1)> Shape(Drive drive) => Shapes[drive];

    /// <summary>The marks drawn over the loaf (its three slashes), as <c>X, Y, Width, Height</c> on the grid; none for the other icons.</summary>
    /// <param name="drive">Which drive.</param>
    public static IReadOnlyList<(int X, int Y, int Width, int Height)> Marks(Drive drive) =>
        drive == Drive.Hunger ? [(5, 7, 2, 4), (8, 7, 2, 4), (11, 7, 2, 4)] : [];

    private static List<(int Y, int X0, int X1)> Runs(Func<double, double, bool> inside)
    {
        var runs = new List<(int, int, int)>();
        for (var y = 0; y < Size; y++)
        {
            var start = -1;
            for (var x = 0; x <= Size; x++)
            {
                var on = x < Size && inside(x + 0.5, y + 0.5);
                if (on && start < 0)
                {
                    start = x;
                }
                else if (!on && start >= 0)
                {
                    runs.Add((y, start, x - 1));
                    start = -1;
                }
            }
        }

        return runs;
    }
}
