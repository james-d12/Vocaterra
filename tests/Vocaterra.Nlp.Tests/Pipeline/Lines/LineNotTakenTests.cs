namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): lines a prompt does not take.</summary>
public class LineNotTakenTests
{
    // Lines a prompt does not take (off-prompt): correct Spanish with no error, not communicated, an unknown intent.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "farmer-open", "¿puede usted vender manzanas?", "unknown", false, [] },
        { "farmer-open", "puede usted vender manzanas", "unknown", false, [] },
        { "smith-open", "¿pueden ustedes venir mañana?", "unknown", false, [] },
        { "farmer-open", "¿puede usted pagar con fruta?", "unknown", false, [] },
        { "smith-open", "debo dinero", "unknown", false, [] },
        { "baker-open", "es para ti", "unknown", false, [] },
        { "lesson-want", "el río es grande", "unknown", false, [] },
        { "lesson-luz-bought", "el pan es grande", "unknown", false, [] },
        { "farmer-open", "¿cuántas manzanas quieres?", "unknown", false, [] },
        { "farmer-open", "¿cuántos panes tienes?", "unknown", false, [] },
        { "lesson-like-apples", "me encantan las manzanas rojas", "unknown", false, [] },
        { "smith-open", "hace frío", "state_weather", true, [] },
        { "farmer-open", "hace frío", "state_weather", true, [] },
        { "baker-open", "hace frío", "state_weather", true, [] },
                { "farmer-open", "hoy hace buen tiempo", "state_weather", true, [] },
        { "baker-open", "me gusta mucho el pan", "unknown", false, [] },
        { "fisher-open", "el pozo está en el pueblo", "unknown", false, [] },
        { "fisher-open", "el río es muy bonito", "unknown", false, [] },
        { "farmer-open", "cuántas manzanas quieres", "unknown", false, [] },
                { "smith-open", "hoy hace buen tiempo", "state_weather", true, [] },
        { "baker-open", "dos panes son caros", "unknown", false, [] },
        { "baker-open", "los panes son caros", "unknown", false, [] },
        { "baker-open", "tres manzanas son caras", "unknown", false, [] },
        { "baker-open", "el pan es caro", "unknown", false, [] },
        { "baker-open", "puede usted vender pan", "unknown", false, [] },
        { "baker-open", "me gusta el pan", "unknown", false, [] },
        { "baker-open", "me gustan los panes", "unknown", false, [] },
        { "baker-open", "el pan es muy bueno", "unknown", false, [] },
        { "fisher-open", "el mar es grande", "unknown", false, [] },
        { "fisher-open", "el agua está fría", "unknown", false, [] },
        { "fisher-open", "el agua es buena", "unknown", false, [] },
        { "smith-open", "el día es bonito", "unknown", false, [] },
        { "smith-open", "el mapa es grande", "unknown", false, [] },
        { "smith-open", "la mano es grande", "unknown", false, [] },
        { "farmer-open", "son quince monedas", "unknown", false, [] },
        { "farmer-open", "el pan está caro", "unknown", false, [] },
        { "smith-open", "usted hace hachas muy buenas", "unknown", false, [] }, // M78: a compliment, describe, which smith-open does not allow
    };

    // M78: statements about what one does, which state_action takes where the prompt allows it (it was unknown, off every prompt: no grammar took them).
    public static TheoryData<string, string, string, bool, string[]> ActivityRows => new()
    {
        { "smith-open", "ustedes trabajan mucho", "state_action", true, [] },
        { "smith-open", "yo trabajo mucho", "state_action", true, [] },
        { "smith-open", "trabajas mucho", "state_action", true, [] },
        { "fisher-open", "tú pescas mucho", "state_action", true, [] },
        { "fisher-open", "pescas mucho", "state_action", true, [] },
        { "fisher-open", "usted pesca poco", "state_action", true, [] },
        { "fisher-open", "tú pesca mucho", "state_action", true, ["conj.person=pescas"] },
        { "farmer-open", "la tienda está en el pueblo", "describe", true, [] }, // M78: a location statement is describe
    };

    [Theory]
    [MemberData(nameof(ActivityRows))]
    public void AStatementAboutWhatOneDoesIsUnderstoodWhereThePromptTakesIt(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
