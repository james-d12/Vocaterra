namespace Vocaterra.Game.Tests;

/// <summary>
/// The speech lines a milestone added to the bundle whose recordings are not rendered yet: phase 1 of the milestone writes the Spanish and waits for the review pass (D-game-12
/// step 3) before pre-rendering it (<c>scripts/prerender-speech.sh</c>), because a review can change a line. The tests that check every line has its files skip these
/// ids and nothing else. S27 phase 1: the lines of "Ayer y mañana"; they are cleared when phase 2 renders them.
/// </summary>
internal static class PendingSpeech
{
    /// <summary>The line ids whose files are still to be rendered.</summary>
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // the dialogue lines
        "guide.lesson.villager.today.1", "guide.lesson.villager.goes.1", "guide.lesson.villager.plan.1", "guide.lesson.villager.plan_we.1", "guide.lesson.villager.eat.1",
        "guide.lesson.shop.ayer.1", "guide.lesson.shop.ate.1", "guide.lesson.shop.went.1", "guide.lesson.shop.had.1", "guide.lesson.shop.you.1", "guide.lesson.shop.tomorrow.1",

        // the prompts' texts
        "prompt.lesson-plan", "prompt.lesson-plan-we", "prompt.lesson-ate", "prompt.lesson-went", "prompt.lesson-had", "prompt.lesson-tomorrow",

        // the learner phrases spoken as their model answers
        "phrase.voy-a-comer-pan", "phrase.vamos-a-comer-fruta", "phrase.ayer-comi-fruta", "phrase.ayer-fui-al-rio", "phrase.ayer-tuve-agua", "phrase.voy-a-ir-al-rio",
    };

    /// <summary>Whether a line's files are still to be rendered.</summary>
    /// <param name="lineId">The line id, or the id of its slow file.</param>
    public static bool Contains(string lineId) => Ids.Contains(lineId.EndsWith(".slow", StringComparison.Ordinal) ? lineId[..^".slow".Length] : lineId);
}
