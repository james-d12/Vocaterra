namespace Vocaterra.Game.Tests;

/// <summary>
/// The speech lines a milestone added to the bundle whose recordings are not rendered yet: phase 1 of the milestone writes the Spanish and waits for the review pass (D-game-12
/// step 3) before pre-rendering it (<c>scripts/prerender-speech.sh</c>), because a review can change a line. The tests that check every line has its files skip these
/// ids and nothing else. Empty since S27 phase 2 rendered the lines of "Ayer y mañana".
/// </summary>
internal static class PendingSpeech
{
    /// <summary>The line ids whose files are still to be rendered.</summary>
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Whether a line's files are still to be rendered.</summary>
    /// <param name="lineId">The line id, or the id of its slow file.</param>
    public static bool Contains(string lineId) => Ids.Contains(lineId.EndsWith(".slow", StringComparison.Ordinal) ? lineId[..^".slow".Length] : lineId);
}
