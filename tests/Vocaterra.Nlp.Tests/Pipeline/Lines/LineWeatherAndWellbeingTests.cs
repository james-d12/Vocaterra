namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): the weather and <c>¿cómo estás?</c> (M74). Since the game's V29 the
/// NPCs' open prompts allow <c>state_weather</c> and <c>ask_weather</c>, so a weather line is understood there as one of them (M74's rows had it unknown, with the intent
/// off the prompt; <c>¿qué tiempo hace?</c> was <c>ask_activity</c> at <c>smith-open</c> and <c>farmer-open</c>, and is <c>ask_weather</c> now: its grammar comes first).
/// <c>¿cómo estás?</c> and its forms are <c>ask_wellbeing</c> wherever <c>greet</c> is allowed. A yes-no question is its statement (<c>¿hace frío?</c>): no transcript
/// carries the question mark. Their neighbours keep what they had; the near misses are not understood.
/// </summary>
public class LineWeatherAndWellbeingTests
{
    public static TheoryData<string, string, string, bool, string[], string> Rows => new()
    {
        { "villager-open", "hace frío", "state_weather", true, [], "none" },
        { "villager-open", "hace calor", "state_weather", true, [], "none" },
        { "villager-open", "hace sol", "state_weather", true, [], "none" },
        { "villager-open", "hace buen tiempo", "state_weather", true, [], "none" },
        { "villager-open", "hace mal tiempo", "state_weather", true, [], "none" },
        { "villager-open", "hoy hace frío", "state_weather", true, [], "none" },
        { "villager-open", "llueve", "state_weather", true, [], "none" },
        { "villager-open", "está nublado", "state_weather", true, [], "none" },
        { "villager-open", "¿qué tiempo hace?", "ask_weather", true, [], "none" },
        { "villager-open", "¿hace frío?", "state_weather", true, [], "none" },
        { "villager-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "smith-open", "hace frío", "state_weather", true, [], "none" },
        { "smith-open", "hace calor", "state_weather", true, [], "none" },
        { "smith-open", "hace sol", "state_weather", true, [], "none" },
        { "smith-open", "hace buen tiempo", "state_weather", true, [], "none" },
        { "smith-open", "hace mal tiempo", "state_weather", true, [], "none" },
        { "smith-open", "hoy hace frío", "state_weather", true, [], "none" },
        { "smith-open", "llueve", "state_weather", true, [], "none" },
        { "smith-open", "está nublado", "state_weather", true, [], "none" },
        { "smith-open", "¿qué tiempo hace?", "ask_weather", true, [], "none" },
        { "smith-open", "¿hace frío?", "state_weather", true, [], "none" },
        { "smith-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "smith-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "baker-open", "hace frío", "state_weather", true, [], "none" },
        { "baker-open", "hace calor", "state_weather", true, [], "none" },
        { "baker-open", "hace sol", "state_weather", true, [], "none" },
        { "baker-open", "hace buen tiempo", "state_weather", true, [], "none" },
        { "baker-open", "hace mal tiempo", "state_weather", true, [], "none" },
        { "baker-open", "hoy hace frío", "state_weather", true, [], "none" },
        { "baker-open", "llueve", "state_weather", true, [], "none" },
        { "baker-open", "está nublado", "state_weather", true, [], "none" },
        { "baker-open", "¿qué tiempo hace?", "ask_weather", true, [], "none" },
        { "baker-open", "¿hace frío?", "state_weather", true, [], "none" },
        { "baker-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo están ustedes?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo estáis?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "baker-open", "¿cómo estás usted?", "ask_wellbeing", true, ["conj.person=está"], "none" },
        { "villager-open", "hoy hace buen tiempo", "state_weather", true, [], "none" },
        { "villager-open", "hace mucho calor", "state_weather", true, [], "none" },
        { "villager-open", "ayer hizo sol", "state_weather", true, [], "none" },
        { "villager-open", "hoy llueve mucho", "state_weather", true, [], "none" },
        { "villager-open", "¡qué frío!", "state_weather", true, [], "none" },
        { "villager-open", "¿qué tiempo hace hoy?", "ask_weather", true, [], "none" },
        { "villager-open", "¿cómo está el tiempo?", "ask_weather", true, [], "none" },
        { "villager-open", "buenos días, ¿cómo está usted?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿qué tal?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hola, ¿qué tal?", "ask_wellbeing", true, [], "none" },
        { "villager-open", "¿cómo está tú?", "ask_wellbeing", true, ["conj.person=estás"], "none" },
        { "villager-open", "hace frio", "state_weather", true, [], "none" },
        { "villager-open", "como estas", "ask_wellbeing", true, [], "none" },
        { "villager-open", "hacen frío", "unknown", false, [], "none" },
        { "villager-open", "hase calor", "unknown", false, ["vocab.unknown"], "none" },
        { "villager-open", "está nublada", "state_weather", true, [], "none" },
        { "villager-open", "hola", "greet", true, [], "none" },
        { "lesson-will-make", "hace calor", "unknown", false, [], "state_weather" },
        { "lesson-luz-made", "hace sol", "unknown", false, [], "state_weather" },
        { "lesson-hello", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "lesson-hello", "hola", "greet", true, [], "none" },
        { "smith-open", "hace dos días compré pan", "state_action", true, [], "none" },
        { "smith-open", "compré pan hace dos días", "state_action", true, [], "none" },
        { "smith-open", "¿qué haces?", "ask_activity", true, [], "none" },
        { "smith-open", "hago pan", "state_action", true, [], "none" },
        { "smith-open", "quiero hacer un hacha", "state_want", true, [], "none" }, // M75: correct Spanish, a word added to an answer: no near miss
        { "guide-open", "¿cómo hago una antorcha?", "ask_craft", true, [], "none" },
        { "guide-open", "quiero hacer una antorcha", "ask_craft", true, [], "none" },
        { "farmer-open", "¿qué tiempo hace?", "ask_weather", true, [], "none" },
        { "farmer-open", "¿qué haces?", "ask_activity", true, [], "none" },
        { "fisher-open", "hola, ¿cómo estás?", "ask_wellbeing", true, [], "none" },
        { "guide-open", "¿cómo estás?", "ask_wellbeing", true, [], "none" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors, string offPrompt) =>
        LineGrading.Check(prompt, line, intent, communicated, errors, offPrompt);
}
