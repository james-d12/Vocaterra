using System.Globalization;

namespace Vocaterra.Game.Startup;

/// <summary>
/// The text of <c>--terrain</c> (Q02): a comma-separated list of <c>id=value</c>, each value a whole number. Whether an id exists and a value is on its grid is the bundle's
/// (<c>game/world-options.json</c>), so the check waits for the data (<see cref="LaunchOptions.CheckTerrain"/>); this class only refuses what cannot be a list of such pairs.
/// </summary>
public static class TerrainChoices
{
    /// <summary>What is wrong with the shape of the text, or <see langword="null"/> when it is a list of <c>id=whole number</c> pairs.</summary>
    /// <param name="text">The option's value.</param>
    public static string? SyntaxProblem(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "the list is empty.";
        }

        foreach (var part in parts)
        {
            var at = part.IndexOf('=', StringComparison.Ordinal);
            if (at <= 0 || !int.TryParse(part.AsSpan(at + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                return $"'{part}' is not <id>=<whole number>.";
            }
        }

        return null;
    }
}
