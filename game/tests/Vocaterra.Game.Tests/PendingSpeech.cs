namespace Vocaterra.Game.Tests;

/// <summary>
/// The speech lines a milestone added to the bundle whose recordings are not rendered yet: phase 1 of the milestone writes the Spanish and waits for the review pass (D-game-12
/// step 3) before pre-rendering it (<c>scripts/prerender-speech.sh</c>), because a review can change a line. The tests that check every line has its files skip these
/// ids and nothing else. S26 phase 1: the verb lines of the guide's lesson and of Personas.
/// </summary>
internal static class PendingSpeech
{
    /// <summary>The line ids whose files are still to be rendered.</summary>
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // the dialogue lines
        "guide.lesson.want.1", "guide.lesson.have_wood.1", "guide.lesson.need.1", "guide.lesson.i_have.1",
        "guide.lesson.villager.she.1", "guide.lesson.villager.luna.1", "guide.lesson.villager.we.1", "guide.lesson.villager.they.1", "guide.lesson.villager.be.1", "guide.lesson.villager.go.1",
        "guide.lesson.shop.need.1", "guide.lesson.shop.bottle.1", "guide.lesson.shop.all.1", "guide.lesson.shop.go.1", "guide.lesson.shop.here.1", "guide.lesson.shop.they.1", "guide.lesson.villager.where.1", "guide.lesson.villager.want.1",

        // the prompts' texts
        "prompt.lesson-want", "prompt.lesson-have", "prompt.lesson-need", "prompt.lesson-she", "prompt.lesson-we", "prompt.lesson-usted", "prompt.lesson-bottle", "prompt.lesson-ustedes",

        // the learner phrases spoken as their model answers
        "phrase.quiero-madera", "phrase.tengo-madera", "phrase.necesito-agua", "phrase.ella-quiere-fruta", "phrase.necesitamos-agua", "phrase.necesito-pan", "phrase.tengo-botella", "phrase.queremos-pan",
    };

    /// <summary>Whether a line's files are still to be rendered.</summary>
    /// <param name="lineId">The line id, or the id of its slow file.</param>
    public static bool Contains(string lineId) => Ids.Contains(lineId.EndsWith(".slow", StringComparison.Ordinal) ? lineId[..^".slow".Length] : lineId);
}
