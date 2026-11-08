namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): near misses and unknown words.</summary>
public class LineVocabularyTests
{
    // Near misses and unknown words: the word the line should have had, and a typed n for ñ.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "farmer-open", "¿cuántos son todo?", "ask_total", true, ["noun.number=cuánto", "conj.person=es"] }, // M75: no answer phrase is one slip away; the grammar errors name the words
        { "villager-open", "tengo diez anos", "unknown", false, ["vocab.unknown"] },
        { "lesson-market", "compré three manzanas", "state_action", true, ["vocab.unknown=tres"] },
        { "lesson-will-go", "mañana iré al glorp", "state_action", true, ["vocab.unknown=río"] },
        { "lesson-will-go", "glorp iré al río", "state_action", false, ["vocab.unknown"] },
        { "lesson-market", "compré tres glorps", "unknown", false, ["vocab.unknown"] },
        { "villager-open", "me llamo ana", "unknown", false, [] }, // M78: a name after me llamo is no unknown word, at any prompt
        { "villager-open", "hola me llamo marta", "greet", true, [] },
        { "villager-open", "Pedro", "unknown", false, [] }, // M78: a lone capitalised word of a free line is a name
        { "villager-open", "soy pedro", "unknown", false, [] },
        { "villager-open", "hola, soy pedro", "greet", true, [] },
        { "villager-open", "hola pedro", "greet", true, [] },
        { "smith-open", "buenos días pedro", "unknown", false, [] }, // no intent takes a name after buenos días, but it is no unknown word
        { "villager-open", "quiero pedro", "unknown", false, ["vocab.unknown"] }, // M78: an unknown word anywhere else stays unknown
        { "villager-open", "hace pedro", "unknown", false, ["vocab.unknown"] },
        { "lesson-will-go", "pasado mañana iré al río con Mar", "state_action", true, [] }, // M75: correct Spanish, a word added to the answer: no near miss
        { "lesson-what", "cuál es esto", "ask_what", true, ["vocab.wrong=qué"] }, // M75: the near miss names its word
        { "farmer-open", "mi amigo se llama pedro", "ask_name", false, ["vocab.unknown"] },
        { "lesson-name", "me llamo pedro", "unknown", false, [] }, // M78: a name after me llamo is no unknown word
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
