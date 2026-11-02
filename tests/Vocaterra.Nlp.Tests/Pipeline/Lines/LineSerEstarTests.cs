namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): ser against estar.</summary>
public class LineSerEstarTests
{
    // Ser and estar: the verb for place and for state; the wrong one keeps ser_estar.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "villager-open", "la tienda es cerca del río", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "son cerca", "unknown", false, ["ser_estar=están"] },
        { "villager-open", "son lejos", "unknown", false, ["ser_estar=están"] },
        { "villager-open", "la tienda es en el pueblo", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "la herrería es en la plaza", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "mi casa es en el pueblo", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "el pan es en la tienda", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "ella es en la tienda", "unknown", false, ["ser_estar=está"] },
        { "villager-open", "la tienda era en el pueblo", "unknown", false, ["ser_estar=estaba"] },
        { "villager-open", "yo soy en casa", "unknown", false, ["ser_estar=estoy"] },
        { "villager-open", "los niños son en el río", "unknown", false, ["ser_estar=están"] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
