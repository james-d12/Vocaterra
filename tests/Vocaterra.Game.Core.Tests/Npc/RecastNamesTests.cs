using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The NPCs' names in a correction line as the game shows it (village-plan V20, handover 9.3 D (d)): the core writes the learner's words in lower case, and Domingo's
/// recast of <c>buscaré madera para Luz</c> read <c>«buscaré madera para luz»</c>. A name gets the roster's spelling unless a determiner makes it the common noun.
/// </summary>
public sealed class RecastNamesTests
{
    private static GameWords Words => TestGame.Data.Words;

    private static IEnumerable<string> Names => TestGame.Data.Npcs.All.Select(static npc => npc.Name).OfType<string>();

    [Theory]
    [InlineData("Claro, «mañana buscaré madera para luz».", "Claro, «mañana buscaré madera para Luz».")]
    [InlineData("¡Eso es! «mar pescó en el río».", "¡Eso es! «Mar pescó en el río».")]
    [InlineData("«luz y domingo comieron pan»", "«Luz y Domingo comieron pan»")]
    [InlineData("«me gustó el pan de luz»", "«me gustó el pan de Luz»")]
    [InlineData("«hola, leon»", "«hola, León»")] // the roster's spelling, with its accent
    [InlineData("«la luz es bonita»", "«la luz es bonita»")] // a determiner: the common noun stays
    [InlineData("«el domingo fui al río»", "«el domingo fui al río»")]
    [InlineData("«fuimos al mar»", "«fuimos al mar»")]
    [InlineData("«veo la luna»", "«veo la luna»")]
    [InlineData("Claro, «quiero dos panes».", "Claro, «quiero dos panes».")]
    public void ANameIsSpeltAsTheRosterSpellsItUnlessADeterminerComesBefore(string line, string shown)
    {
        Assert.Equal(shown, RecastNames.Capitalise(line, Names, Words));
    }

    [Fact]
    public void TheNamesComeFromTheRosterNotAList()
    {
        Assert.Equal("«buscaré madera para luz»", RecastNames.Capitalise("«buscaré madera para luz»", [], Words)); // no roster, no name
        Assert.Equal("«buscaré madera para Sol»", RecastNames.Capitalise("«buscaré madera para sol»", ["Sol"], Words)); // another village's
    }

    // T02b: the determiners before a name are the file's (nameDeterminers): der, die and das are German, and a name after them is a noun.
    [Fact]
    public void TheDeterminersBeforeANameAreTheFilesOwn()
    {
        var german = WordsFile.WithList("nameDeterminers", "der", "die", "das");
        Assert.Equal("kaufe die luz", RecastNames.Capitalise("kaufe die luz", ["Luz"], german));
        Assert.Equal("kaufe Luz", RecastNames.Capitalise("kaufe luz", ["Luz"], german));
        Assert.Equal("kaufe die Luz", RecastNames.Capitalise("kaufe die luz", ["Luz"], Words)); // die is no Spanish determiner
    }
}
