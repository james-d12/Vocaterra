using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The loaders of the procedures, the stations and the actions (W05, design 10.7): each reads its file's text into plain values, and refuses a malformed one
/// with the file's name. The bundle has none yet, so it loads empty.
/// </summary>
public sealed class ActivityBooksTests
{
    // The design's example (design 10.7, game/procedures/pan.json), verbatim: its items are the design's, and flour is not yet an item (W12); the loader does not check names.
    private const string Pan = """
        {
          "$schema": "../../../schema/procedure.schema.json",
          "schemaVersion": 1,
          "id": "pan",
          "station": "horno",
          "teacher": "baker",
          "level": "a2",
          "makes": { "item": "bread", "count": 2, "bonus": { "firstTimeSteps": 5, "count": 1 } },
          "uses": [
            { "item": "flour", "count": 1 },
            { "item": "water-bottle", "count": 1, "leaves": "bottle" },
            { "item": "wood", "count": 1 }
          ],
          "steps": [
            { "id": "harina",  "row": "pon-la-harina", "do": "poner",   "what": "harina", "where": "bol", "anyOrder": "bol" },
            { "id": "agua",    "row": "echa-el-agua",  "do": "echar",   "what": "agua",   "where": "bol", "anyOrder": "bol" },
            { "id": "mezclar", "row": "mezcla",        "do": "mezclar", "what": "bol",    "with": "cuchara" },
            { "id": "madera",  "row": "pon-la-madera", "do": "poner",   "what": "madera", "where": "horno" },
            { "id": "masa",    "row": "pon-la-masa",   "do": "poner",   "what": "masa",   "where": "horno" },
            { "id": "sacar",   "row": "saca-el-pan",   "do": "sacar",   "what": "pan",    "from": "horno" }
          ],
          "chunks": [
            { "id": "la-masa",  "row": "haz-la-masa",      "steps": ["harina", "agua", "mezclar"] },
            { "id": "el-horno", "row": "haz-el-pan-horno", "steps": ["madera", "masa", "sacar"] }
          ],
          "goal": { "row": "haz-pan" }
        }
        """;

    // One step, in a procedure of its own (the rest of the file is the design's).
    private static string WithStep(string step) =>
        $$"""{ "schemaVersion": 1, "id": "t", "station": "horno", "teacher": "baker", "level": "a1", "makes": { "item": "bread", "count": 1 }, "uses": [], "steps": [ {{step}} ], "chunks": [], "goal": { "row": "haz-pan" } }""";

    [Fact]
    public void TheDesignsPanExampleLoadsIntoItsValues()
    {
        var pan = ProcedureBook.ParseProcedure(Pan, "pan");

        Assert.Equal("horno", pan.Station);
        Assert.Equal("baker", pan.Teacher);
        Assert.Equal("a2", pan.Level);
        Assert.Equal(new ProcedureMakes("bread", 2, new ProcedureBonus(5, 1)), pan.Makes);
        Assert.Equal(new[] { new ProcedureUse("flour", 1, null), new ProcedureUse("water-bottle", 1, "bottle"), new ProcedureUse("wood", 1, null) }, pan.Uses);
        Assert.Equal(6, pan.Steps.Count);
        Assert.Equal("bol", pan.Steps[0].AnyOrder);
        Assert.Equal(new ProcedureStep("mezclar", "mezcla", "mezclar", "bol", null, null, null, "cuchara", null), pan.Steps[2]);
        Assert.Equal(["la-masa", "el-horno"], pan.Chunks.Select(static c => c.Id));
        Assert.Equal(["harina", "agua", "mezclar"], pan.Chunks[0].Steps);
        Assert.Equal("haz-pan", pan.Goal.Row);
    }

    [Fact]
    public void AStepsActionIsItsUseAndItsFixture()
    {
        var pan = ProcedureBook.ParseProcedure(Pan, "pan");

        Assert.Equal(("harina", "bol"), (pan.Steps[0].ActUse, pan.Steps[0].ActOn));
        Assert.Equal(("cuchara", "bol"), (pan.Steps[2].ActUse, pan.Steps[2].ActOn)); // with: the tool acts on what
        Assert.Equal(("pan", "horno"), (pan.Steps[5].ActUse, pan.Steps[5].ActOn)); // as written; a step that takes its hand names use: mano
        Assert.Equal(("mano", "horno"), (ProcedureBook.ParseProcedure(WithStep("""{ "id": "s", "row": "r", "do": "sacar", "what": "pan", "use": "mano", "from": "horno" }""")).Steps[0].ActUse, "horno"));
    }

    [Fact]
    public void AProcedureNeedsOneTargetPerStepAndItsOwnId()
    {
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(WithStep("""{ "id": "s", "row": "r", "do": "poner", "what": "pan" }""")));
        var both = Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(WithStep("""{ "id": "s", "row": "r", "do": "poner", "what": "pan", "where": "a", "from": "b" }""")));
        Assert.Contains("exactly one of where, from and with", both.Message, StringComparison.Ordinal);
        var other = Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(Pan, "bread"));
        Assert.Contains("'bread'", other.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(Pan.Replace("\"level\": \"a2\"", "\"level\": \"b1\"", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(Pan.Replace("\"count\": 2,", "\"count\": 0,", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseProcedure(Pan.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheIndexListsTheProceduresAndEachFileIsReadByItsId()
    {
        const string index = """{ "schemaVersion": 1, "procedures": ["pan"] }""";
        var book = ProcedureBook.Parse(index, new Dictionary<string, string> { ["pan"] = Pan });

        Assert.Equal(["pan"], book.Procedures.Select(static p => p.Id));
        Assert.Equal("horno", book.Find("pan")!.Station);
        Assert.Single(book.AtStation("horno"));
        Assert.Empty(book.AtStation("forno"));
        Assert.Null(book.Find("tortilla"));

        Assert.Throws<InvalidDataException>(() => ProcedureBook.Parse(index, new Dictionary<string, string>())); // listed, but no file
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseIndex("""{ "schemaVersion": 1, "procedures": ["pan", "pan"] }"""));
        Assert.Throws<InvalidDataException>(() => ProcedureBook.ParseIndex("""{ "schemaVersion": 2, "procedures": [] }"""));
    }

    [Fact]
    public void TheStationsFileHoldsEachStationWithItsFixtures()
    {
        var book = StationBook.Parse("""
            { "schemaVersion": 1, "stations": [
              { "id": "horno", "prop": "oven", "npc": "baker",
                "fixtures": [ { "noun": "bol", "icon": "bowl" }, { "noun": "cuchara", "icon": "spoon" }, { "noun": "horno", "icon": "oven" } ] }
            ] }
            """);

        var horno = book.Find("horno")!;
        Assert.Equal(("oven", "baker"), (horno.Prop, horno.Npc));
        Assert.Equal(["bowl", "spoon", "oven"], horno.Fixtures.Select(static f => f.Icon));
        Assert.True(horno.HasFixture("cuchara"));
        Assert.False(horno.HasFixture("harina")); // an item held is not a fixture
        Assert.Null(book.Find("molino"));
        Assert.Throws<InvalidDataException>(() => StationBook.Parse("""{ "schemaVersion": 1, "stations": [ { "id": "a", "prop": "p", "npc": "n", "fixtures": [ { "noun": "bol", "icon": "bowl" }, { "noun": "bol", "icon": "bowl" } ] } ] }"""));
        Assert.Throws<InvalidDataException>(() => StationBook.Parse("""{ "schemaVersion": 1, "stations": [ { "id": "a", "prop": "p", "npc": "n", "fixtures": [] }, { "id": "a", "prop": "p", "npc": "n", "fixtures": [] } ] }"""));
    }

    [Fact]
    public void TheActionsFileGivesTheVerbsOfEachPair()
    {
        var table = ActionTable.Parse("""
            { "schemaVersion": 1, "actions": [
              { "use": "harina", "on": "bol", "verbs": ["poner"] },
              { "use": "mano", "on": "horno", "verbs": ["sacar", "abrir"] }
            ] }
            """);

        Assert.Equal(["poner"], table.VerbsFor("harina", "bol"));
        Assert.Equal(["sacar", "abrir"], table.VerbsFor("mano", "horno"));
        Assert.Empty(table.VerbsFor("harina", "horno"));
        Assert.Equal(["poner", "sacar", "abrir"], table.Verbs);
        Assert.Throws<InvalidDataException>(() => ActionTable.Parse("""{ "schemaVersion": 1, "actions": [ { "use": "a", "on": "b", "verbs": ["x"] }, { "use": "a", "on": "b", "verbs": ["y"] } ] }"""));
        Assert.Throws<InvalidDataException>(() => ActionTable.Parse("""{ "schemaVersion": 1, "actions": [ { "use": "a", "on": "b", "verbs": ["x", "x"] } ] }"""));
    }

    [Fact]
    public void TheBundleHasTheBreadProcedureItsStationAndItsSixActions()
    {
        // W05 added the books and the empty files; W10's bread fills them (the tests that predate it read TestGame.Data, the bundle without its procedure).
        var data = TestGame.BreadData;

        Assert.Equal(["bread"], data.Procedures.Procedures.Select(static p => p.Id)); // T03: the ids are neutral, the nouns of the books are read through nouns.json
        Assert.Equal(["oven"], data.Stations.Stations.Select(static s => s.Id));
        Assert.Equal("oven", data.Procedures.Find("bread")!.Station);
        Assert.Equal(["bol", "cuchara", "horno"], data.Stations.Find("oven")!.Fixtures.Select(static f => f.Noun)); // the lemmas of ids bowl, spoon and oven
        Assert.Equal(6, data.Actions.Rules.Count);
        Assert.Equal(["sacar", "abrir"], data.Actions.VerbsFor("mano", "horno"));
        Assert.Empty(TestGame.Data.Procedures.Procedures);
    }

    [Fact]
    public void TheFilesNameTheNounsByNeutralIdsAndTheLoadedBooksByTheirLemmas()
    {
        var stations = StationBook.Parse(GameData.HostText(TestGame.Bundle, StationBook.RelativePath));
        var actions = ActionTable.Parse(GameData.HostText(TestGame.Bundle, ActionTable.RelativePath));

        Assert.Equal(["bowl", "spoon", "oven"], stations.Find("oven")!.Fixtures.Select(static f => f.Noun));
        Assert.Equal(["sacar", "abrir"], actions.VerbsFor("hand", "oven"));
        Assert.Empty(actions.VerbsFor("mano", "horno"));

        var inLanguage = stations.InLanguage(TestGame.Nouns);
        Assert.Equal(["bol", "cuchara", "horno"], inLanguage.Find("oven")!.Fixtures.Select(static f => f.Noun));
        Assert.Equal(["sacar", "abrir"], actions.InLanguage(TestGame.Nouns).VerbsFor("mano", "horno"));
        Assert.Equal("mano", StationScreen.HandNoun(TestGame.NewSession()));
    }
}
