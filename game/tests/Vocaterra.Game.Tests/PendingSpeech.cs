namespace Vocaterra.Game.Tests;

/// <summary>
/// The speech lines S17 added to the bundle whose recordings are not rendered yet: phase 1 of the milestone writes the Spanish and waits for the review pass (D-game-12
/// step 3) before pre-rendering it (<c>scripts/prerender-speech.sh</c>), because a review can change a line. The tests that check every line has its files skip these
/// ids and nothing else. Phase 2 renders the files and empties this list.
/// </summary>
internal static class PendingSpeech
{
    /// <summary>The line ids whose files are still to be rendered.</summary>
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "guide_again.1", "guide_again.2", "guide_slower.1", "guide_slower.2",
        "guide.lesson.hello.1", "guide.lesson.name.1", "guide.lesson.is_wood.1", "guide.lesson.is_water.1", "guide.lesson.look.1",
        "guide.lesson.it_is.1", "guide.lesson.it_is.2", "guide.lesson.it_is.3", "guide.lesson.it_is.4", "guide.lesson.it_is.5", "guide.lesson.it_is.6",
        "guide.lesson.offer.1", "guide.lesson.take.1", "guide.lesson.have.1", "guide.lesson.welcome.1", "guide.lesson.fast.1", "guide.lesson.task.1", "guide.lesson.end.1",
        "phrase.si", "phrase.no", "phrase.que-es-esto", "phrase.otra-vez", "phrase.mas-despacio",
        "prompt.lesson-hello", "prompt.lesson-name", "prompt.lesson-offer", "prompt.lesson-task", "prompt.lesson-thanks", "prompt.lesson-unstuck", "prompt.lesson-what", "prompt.lesson-yesno",
    };

    /// <summary>Whether a line's files are still to be rendered.</summary>
    /// <param name="lineId">The line id, or the id of its slow file.</param>
    public static bool Contains(string lineId) => Ids.Contains(lineId.EndsWith(".slow", StringComparison.Ordinal) ? lineId[..^".slow".Length] : lineId);
}
