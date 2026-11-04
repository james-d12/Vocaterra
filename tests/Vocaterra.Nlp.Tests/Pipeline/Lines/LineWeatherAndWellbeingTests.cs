namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): the weather and <c>¿cómo estás?</c> (M74), with the intent each
/// has off the prompt. No prompt allows the weather intents yet (the game's V29 will), so a weather line is unknown at every prompt and understood off it as
/// <c>state_weather</c> or <c>ask_weather</c>, never as the action of a lesson's <c>[VERB:hacer] [NOUN:item]</c> or a craft question; only an allowed keyword
/// rule still reads one (<c>¿qué tiempo hace?</c> is <c>ask_activity</c>'s <c>qué</c> with <c>hacer</c> at <c>smith-open</c> and <c>farmer-open</c> until the game
/// allows <c>ask_weather</c> there, when the grammar comes first). <c>¿cómo estás?</c> and its forms are <c>ask_wellbeing</c> wherever <c>greet</c> is allowed.
/// A yes-no question is its statement (<c>¿hace frío?</c>): no transcript carries the question mark. Their neighbours keep what they had; the near misses are
/// not understood.
/// </summary>
public class LineWeatherAndWellbeingTests
{
    public static TheoryData<string, string, string, bool, string[], string> Rows => new()
    {
        { "villager-open", "hace frío", "unknown", false, [], "state_weather" },
        { "villager-open", "hace calor", "unknown", false, [], "state_weather" },
        { "villager-open", "hace sol", "unknown", false, [], "state_weather" },
        { "villager-open", "hace buen tiempo", "unknown", false, [], "state_weather" },
        { "villager-open", "hace mal tiempo", "unknown", false, [], "state_weather" },
        { "villager-open", "hoy hace frío", "unknown", false, [], "state_weather" },
        { "villager-open", "llueve", "unknown", false, [], "state_weather" },
        { "villager-open", "está nublado", "unknown", false, [], "state_weather" },
        { "villager-open", "¿qué tiempo hace?", "unknown", false, [], "ask_weather" },
        { "villager-open", "¿hace frío?", "unknown", false, [], "state_weather" },
        { "villager-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "smith-open", "hace frío", "unknown", false, [], "state_weather" },
        { "smith-open", "hace calor", "unknown", false, [], "state_weather" },
        { "smith-open", "hace sol", "unknown", false, [], "state_weather" },
        { "smith-open", "hace buen tiempo", "unknown", false, [], "state_weather" },
        { "smith-open", "hace mal tiempo", "unknown", false, [], "state_weather" },
        { "smith-open", "hoy hace frío", "unknown", false, [], "state_weather" },
        { "smith-open", "llueve", "unknown", false, [], "state_weather" },
        { "smith-open", "está nublado", "unknown", false, [], "state_weather" },
        { "smith-open", "¿qué tiempo hace?", "ask_activity", true, [], "none" },
        { "smith-open", "¿hace frío?", "unknown", false, [], "state_weather" },
        { "smith-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "baker-open", "hace frío", "unknown", false, [], "state_weather" },
        { "baker-open", "hace calor", "unknown", false, [], "state_weather" },
        { "baker-open", "hace sol", "unknown", false, [], "state_weather" },
        { "baker-open", "hace buen tiempo", "unknown", false, [], "state_weather" },
        { "baker-open", "hace mal tiempo", "unknown", false, [], "state_weather" },
        { "baker-open", "hoy hace frío", "unknown", false, [], "state_weather" },
        { "baker-open", "llueve", "unknown", false, [], "state_weather" },
        { "baker-open", "está nublado", "unknown", false, [], "state_weather" },
        { "baker-open", "¿qué tiempo hace?", "unknown", false, [], "ask_weather" },
        { "baker-open", "¿hace frío?", "unknown", false, [], "state_weather" },
        { "baker-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "villager-open", "hoy hace buen tiempo", "unknown", false, [], "state_weather" },
        { "villager-open", "hace mucho calor", "unknown", false, [], "state_weather" },
        { "villager-open", "ayer hizo sol", "unknown", false, [], "state_weather" },
        { "villager-open", "hoy llueve mucho", "unknown", false, [], "state_weather" },
        { "villager-open", "¡qué frío!", "unknown", false, [], "state_weather" },
        { "villager-open", "¿qué tiempo hace hoy?", "unknown", false, [], "ask_weather" },
        { "villager-open", "¿cómo está el tiempo?", "unknown", false, [], "ask_weather" },
        { "villager-open", "buenos días, ¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿qué tal?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hola, ¿qué tal?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo está tú?", "ask_wellbeing", true, ["conj.person=estás"], "none" },
        { "villager-open", "hace frio", "unknown", false, [], "state_weather" },
        { "villager-open", "como estas", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hacen frío", "unknown", false, [], "none" },
        { "villager-open", "hase calor", "unknown", false, ["vocab.unknown"], "none" },
        { "villager-open", "está nublada", "unknown", false, [], "state_weather" },
        { "villager-open", "hola", "greet", true, [], "none" },
        { "lesson-will-make", "hace calor", "unknown", false, [], "state_weather" },
        { "lesson-luz-made", "hace sol", "unknown", false, [], "state_weather" },
        { "lesson-hello", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "lesson-hello", "hola", "greet", true, [], "none" },
        { "smith-open", "hace dos días compré pan", "state_action", true, [], "none" },
        { "smith-open", "compré pan hace dos días", "state_action", true, [], "none" },
        { "smith-open", "¿qué haces?", "ask_activity", true, [], "none" },
        { "smith-open", "hago pan", "state_action", true, [], "none" },
        { "smith-open", "quiero hacer un hacha", "state_want", true, ["vocab.wrong"], "none" },
        { "guide-open", "¿cómo hago una antorcha?", "ask_craft", true, [], "none" },
        { "guide-open", "quiero hacer una antorcha", "ask_craft", true, [], "none" },
        { "farmer-open", "¿qué tiempo hace?", "ask_activity", true, [], "none" },
        { "farmer-open", "¿qué haces?", "ask_activity", true, [], "none" },
        { "fisher-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "guide-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors, string offPrompt) =>
        LineGrading.Check(prompt, line, intent, communicated, errors, offPrompt);
}
