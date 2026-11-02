namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): questions.</summary>
public class LineQuestionsTests
{
    // Questions: a question is understood at the prompts that take it, with or without its marks and accents; a wrong person keeps its tag.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "baker-open", "¿quiere usted comprar pan?", "state_action", true, [] },
        { "farmer-open", "¿va usted a comprar fruta?", "state_action", true, [] },
        { "villager-open", "ellas adónde van", "ask_how", true, [] },
        { "smith-open", "¿cómo se llama usted?", "ask_name", true, [] },
        { "fisher-open", "¿dónde está la panadería?", "ask_directions", true, [] },
        { "villager-open", "¿dónde es la fiesta?", "ask_directions", true, [] },
        { "villager-open", "¿es en serio?", "unknown", false, [] },
        { "villager-open", "¿dónde estás tú?", "unknown", false, [] },
        { "villager-open", "¿qué quieres tú?", "unknown", false, [] },
        { "villager-open", "¿dónde vive usted?", "unknown", false, [] },
        { "villager-open", "¿qué haces tú?", "unknown", false, [] },
        { "villager-open", "¿dónde están ustedes?", "unknown", false, [] },
        { "villager-open", "¿cómo te llamas tú?", "unknown", false, [] },
        { "villager-open", "¿quién eres tú?", "unknown", false, [] },
        { "villager-open", "¿dónde están tú y luz?", "ask_directions", true, [] },
        { "villager-open", "qué quieres yo te doy pan", "unknown", false, [] },
        { "villager-open", "¿cómo es tu casa?", "unknown", false, [] },
        { "villager-open", "¿dónde está la herrería?", "ask_directions", true, [] },
        { "villager-open", "¿qué es esto?", "unknown", false, [] },
        { "lesson-like-bread", "¿te gusta?", "state_like", true, [] },
        { "farmer-open", "me llamo ana", "ask_name", true, [] },
        { "smith-open", "me llamo luis", "ask_name", true, [] },
        { "farmer-open", "¿te gustan las manzanas?", "state_like", true, [] },
        { "smith-open", "cómo te llamas", "ask_name", true, [] },
        { "smith-open", "qué haces", "ask_activity", true, [] },
        { "smith-open", "qué haces tú", "ask_activity", true, [] },
        { "smith-open", "qué hace usted", "ask_activity", true, [] },
        { "smith-open", "en qué trabajas", "ask_activity", true, [] },
        { "farmer-open", "qué haces", "ask_activity", true, [] },
        { "villager-open", "qué haces", "unknown", false, [] },
        { "smith-open", "me llamo ana", "ask_name", true, [] },
        { "smith-open", "dónde trabajas", "ask_activity", true, [] },
        { "farmer-open", "en qué trabaja usted", "ask_activity", true, [] },
        { "guide-open", "cómo hago una antorcha", "ask_craft", true, [] },
        { "guide-open", "cómo se hace una antorcha", "ask_craft", true, [] },
        { "guide-open", "quiero hacer una antorcha", "ask_craft", true, [] },
        { "guide-open", "qué necesito para una antorcha", "ask_craft", true, [] },
        { "villager-open", "cómo es la manzana", "ask_how", true, [] },
        { "villager-open", "dónde está la herrería", "ask_directions", true, [] },
        { "villager-open", "cómo te llamas", "unknown", false, [] },
        { "villager-open", "cómo se llama usted", "unknown", false, [] },
        { "villager-open", "qué compró luz", "state_action", true, [] },
        { "villager-open", "qué vas a hacer mañana", "state_action", true, [] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
