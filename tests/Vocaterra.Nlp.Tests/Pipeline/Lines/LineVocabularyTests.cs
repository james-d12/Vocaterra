namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): near misses and unknown words.</summary>
public class LineVocabularyTests
{
    // Near misses and unknown words: the word the line should have had, and a typed n for ñ.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "farmer-open", "¿cuántos son todo?", "ask_total", true, ["vocab.wrong", "noun.number=cuánto", "conj.person=es"] },
        { "villager-open", "tengo diez anos", "unknown", false, ["vocab.unknown"] },
        { "lesson-market", "compré three manzanas", "state_action", true, ["vocab.unknown=tres"] },
        { "lesson-will-go", "mañana iré al glorp", "state_action", true, ["vocab.unknown=río"] },
        { "lesson-will-go", "glorp iré al río", "state_action", false, ["vocab.unknown"] },
        { "lesson-market", "compré tres glorps", "unknown", false, ["vocab.unknown"] },
        { "villager-open", "me llamo ana", "unknown", false, ["vocab.unknown"] },
        { "villager-open", "hola me llamo marta", "greet", false, ["vocab.unknown"] },
        { "lesson-will-go", "pasado mañana iré al río con Mar", "state_action", true, ["vocab.wrong"] },
        { "lesson-what", "cuál es esto", "ask_what", true, ["vocab.wrong"] },
        { "farmer-open", "mi amigo se llama pedro", "ask_name", false, ["vocab.unknown"] },
        { "lesson-name", "me llamo pedro", "unknown", false, ["vocab.unknown"] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
