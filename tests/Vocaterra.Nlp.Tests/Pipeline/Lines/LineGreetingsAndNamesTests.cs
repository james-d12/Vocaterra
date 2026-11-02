namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): greetings and names.</summary>
public class LineGreetingsAndNamesTests
{
    // Greetings and names: correct lines stay clean; a name or a greeting the core alone does not take out of the line (the host does) is the host cases of the same topic.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "baker-open", "hola luz quiero dos panes", "greet", true, [] },
        { "farmer-open", "puedo pagar con fruta y con pan", "unknown", false, [] },
        { "smith-open", "me llamo domingo", "ask_name", true, [] },
        { "baker-open", "buenos días, quiero un pan", "unknown", false, [] },
        { "villager-open", "¿cómo está usted?", "greet", true, [] },
        { "villager-open", "¿cómo estáis vosotros?", "greet", true, [] },
        { "villager-open", "buenas tardes", "greet", true, [] },
        { "villager-open", "hola buenas tardes", "greet", true, [] },
        { "villager-open", "buenos días", "greet", true, [] },
        { "villager-open", "buenas noches", "greet", true, [] },
        { "villager-open", "buenos días senor", "unknown", false, [] },
        { "villager-open", "buenos días, señor", "unknown", false, [] },
        { "farmer-open", "buenos días", "greet", true, [] },
        { "baker-open", "buenas tardes", "greet", true, [] },
        { "fisher-open", "cómo está usted", "greet", true, [] },
        { "lesson-hello", "buenos días", "greet", true, [] },
        { "villager-open", "cómo está usted", "greet", true, [] },
        { "villager-open", "cómo estás", "greet", true, [] },
        { "smith-open", "cómo está usted", "greet", true, [] },
        { "baker-open", "cómo estás", "greet", true, [] },
        { "lesson-hello", "cómo estás", "greet", true, [] },
        { "villager-open", "hola", "greet", true, [] },
        { "baker-open", "luz quiero pan", "unknown", false, [] },
        { "baker-open", "buenos días luz", "unknown", false, [] },
        { "baker-open", "luz hará pan", "state_action", true, [] },
        { "baker-open", "luz y mar compraron pan", "state_action", true, [] },
        { "baker-open", "ayer compré pan para luz", "state_action", true, [] },
        { "baker-open", "gracias luz", "thank", true, [] },
        { "fisher-open", "mar pescaste en el río", "state_action", true, [] },
        { "fisher-open", "hola mar", "greet", true, [] },
        { "fisher-open", "mar está en el río", "unknown", false, [] },
        { "fisher-open", "mañana vendrá mar", "unknown", false, [] },
        { "smith-open", "domingo trabaja en la herrería", "state_action", true, [] },
        { "smith-open", "domingo tengo madera", "state_have", true, [] },
        { "smith-open", "qué hace domingo", "ask_activity", true, [] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
