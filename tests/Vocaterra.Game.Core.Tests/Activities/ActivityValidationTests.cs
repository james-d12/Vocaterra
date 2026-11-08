using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The validator's rules for the procedures, the stations and the actions (W05, design 10.7): each rule has a passing and a failing case on a small
/// procedure built in the test, over the bundle's own nouns, NPCs, scene, lexicon and level lists. The shipped bundle has none of them yet.
/// </summary>
public sealed class ActivityValidationTests
{
    // The sheet's does rows, each with its Spanish, and the NPC's audio line the bundle has (shop_need.1 is in the speech index).
    private static readonly (string Id, string Spanish)[] DoesRows =
    [
        ("pon-la-madera", "Pon la madera en la mesa."),
        ("saca-el-pan", "Saca el pan de la puerta."),
        ("haz-la-madera", "Haz la madera."),
        ("haz-el-pan", "Haz el pan."),
        ("haz-pan", "Haz pan."),
    ];

    // The sheet: the does rows (their audio lines as given) and any extra rows the test adds.
    private static string Sheet(string audio = "[\"shop_need.1\"]", params string[] extra)
    {
        var rows = DoesRows.Select(row => $$"""{ "id": "{{row.Id}}", "says": "does", "spanish": "{{row.Spanish}}", "audioLineIds": {{audio}} }""")
            .Concat(extra);
        return "{ \"schemaVersion\": 1, \"phrases\": [" + string.Join(", ", rows) + "] }";
    }

    private static string Row(string id, string says, string spanish) =>
        $$"""{ "id": "{{id}}", "says": "{{says}}", "spanish": "{{spanish}}", "audioLineIds": ["shop_need.1"] }""";

    private const string Stations = """
        { "schemaVersion": 1, "stations": [
          { "id": "horno", "prop": "oven", "npc": "baker",
            "fixtures": [ { "noun": "mesa", "icon": "table" }, { "noun": "puerta", "icon": "door" } ] }
        ] }
        """;

    private const string Actions = """
        { "schemaVersion": 1, "actions": [
          { "use": "madera", "on": "mesa", "verbs": ["poner"] },
          { "use": "agua", "on": "puerta", "verbs": ["sacar", "abrir"] }
        ] }
        """;

    // The fixture procedure's steps: poner the wood on the table, and sacar the bread from the door with the hand.
    private const string DefaultSteps = """
        { "id": "madera", "row": "pon-la-madera", "do": "poner", "what": "madera", "where": "mesa" },
        { "id": "sacar", "row": "saca-el-pan", "do": "sacar", "what": "pan", "use": "agua", "from": "puerta" }
        """;

    private const string DefaultChunks = """
        { "id": "la-madera", "row": "haz-la-madera", "steps": ["madera"] },
        { "id": "el-pan", "row": "haz-el-pan", "steps": ["sacar"] }
        """;

    // The fixture procedure, A2 by default (the sacar is A2). Each part can be replaced, so one rule's breakage is the only difference.
    private static string Procedure(string steps = DefaultSteps, string chunks = DefaultChunks, string level = "a2", string makes = "bread",
        string uses = "{ \"item\": \"wood\", \"count\": 1 }", string station = "horno", string teacher = "baker", string goal = "haz-pan", string bonus = "")
    {
        var extra = bonus.Length == 0 ? string.Empty : ", \"bonus\": " + bonus;
        return $$"""
            { "schemaVersion": 1, "id": "pan", "station": "{{station}}", "teacher": "{{teacher}}", "level": "{{level}}",
              "makes": { "item": "{{makes}}", "count": 1{{extra}} }, "uses": [ {{uses}} ],
              "steps": [ {{steps}} ], "chunks": [ {{chunks}} ], "goal": { "row": "{{goal}}" } }
            """;
    }

    // The problems of one procedure with the given sheet, stations and actions (the bundle's own everything else).
    private static List<string> Problems(string procedure, string? sheet = null, string stations = Stations, string actions = Actions)
    {
        var text = sheet ?? Sheet();
        var data = TestGame.Data
            .WithStations(StationBook.Parse(stations))
            .WithActions(ActionTable.Parse(actions))
            .WithPhrases(PhraseSheet.Parse(text, LocaleFor(text)))
            .WithProcedures(ProcedureBook.Parse("""{ "schemaVersion": 1, "procedures": ["pan"] }""", new Dictionary<string, string> { ["pan"] = procedure }));
        return GameDataValidator.ActivityProblems(data).ToList();
    }

    // The locale of the sheet's rows: each row's meaning and card, which the phrase sheet asks for.
    private static LocaleText LocaleFor(string sheet)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in Regex.Matches(sheet, "\"id\": \"([a-z-]+)\"").Select(static m => m.Groups[1].Value).Distinct(StringComparer.Ordinal))
        {
            strings[PhraseSheet.LocaleId(id, "meaning")] = "meaning of " + id;
            strings[PhraseSheet.LocaleId(id, "card")] = "card of " + id;
        }

        return TestGame.Locale.With(strings);
    }

    private static void Expect(string procedure, string message, string? sheet = null, string stations = Stations, string actions = Actions)
    {
        var problems = Problems(procedure, sheet, stations, actions);
        Assert.True(problems.Any(p => p.Contains(message, StringComparison.Ordinal)), $"No problem says '{message}'. Problems:\n  " + string.Join("\n  ", problems));
    }

    [Fact]
    public void TheFixtureProcedureHoldsTogether()
    {
        Assert.Empty(Problems(Procedure()));
    }

    [Fact]
    public void TheShippedBundleHasNoActivityProblems()
    {
        Assert.Empty(GameDataValidator.ActivityProblems(TestGame.BreadData));
        Assert.Empty(GameDataValidator.Validate(TestGame.BreadData));
    }

    [Fact]
    public void AStationsPropIsAPropOfTheScene()
    {
        Expect(Procedure(), "the prop 'nowhere'", stations: Stations.Replace("\"oven\"", "\"nowhere\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AStationsNpcIsAnNpc()
    {
        Expect(Procedure(), "the NPC 'nobody'", stations: Stations.Replace("\"baker\"", "\"nobody\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AStationsFixtureIsACatalogueNoun()
    {
        Expect(Procedure(), "the fixture 'queso', which is not a catalogue noun", stations: Stations.Replace("\"mesa\"", "\"queso\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AVerbIsInTheLexiconWithItsTuImperative()
    {
        Assert.Empty(Problems(Procedure(), actions: Actions));
        Expect(Procedure(), "the verb 'xyzzy' is not in the lexicon", actions: Actions.Replace("\"poner\"", "\"xyzzy\"", StringComparison.Ordinal));
        Expect(Procedure(), "the verb 'madera' has no tú imperative", actions: Actions.Replace("\"poner\"", "\"madera\"", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLexiconRuleReadsEachVerbByItsInfinitive()
    {
        // sacar is looked up as its infinitive: its tu imperative (saca) is found from the entry, so the actions pass; the A1 level check's
        // lemma 'saca' is the other rule's and never reaches this one.
        Assert.Empty(Problems(Procedure(), actions: Actions));
        var unknown = Problems(Procedure(), actions: Actions.Replace("\"sacar\", \"abrir\"", "\"sacar\", \"abrir\", \"xyzzy\"", StringComparison.Ordinal));
        Assert.Contains(unknown, p => p.Contains("the verb 'xyzzy' is not in the lexicon", StringComparison.Ordinal));
        // A verb whose entries have no tu imperative: madera is a noun in the lexicon, with no imperative of any person.
        var noImperative = Problems(Procedure(), actions: Actions.Replace("\"poner\"", "\"madera\"", StringComparison.Ordinal));
        Assert.Contains(noImperative, p => p.Contains("the verb 'madera' has no tú imperative", StringComparison.Ordinal));
        Assert.DoesNotContain(noImperative, p => p.Contains("the verb 'sacar'", StringComparison.Ordinal));
    }

    [Fact]
    public void AProcedureNamesItsStationTeacherAndItems()
    {
        Expect(Procedure(station: "molino"), "the station 'molino' is not one of the stations");
        Expect(Procedure(teacher: "nobody"), "the teacher 'nobody' is not one of the NPCs");
        Expect(Procedure(makes: "cheese"), "makes the item 'cheese', which is not an item");
        Expect(Procedure(uses: "{ \"item\": \"cheese\", \"count\": 1 }"), "uses the item 'cheese', which is not an item");
        Expect(Procedure(uses: "{ \"item\": \"water-bottle\", \"count\": 1, \"leaves\": \"jar\" }"), "leaves the item 'jar', which is not an item");
    }

    [Fact]
    public void ABonusNeedsNoMoreStepsThanTheProcedureHas()
    {
        Assert.Empty(Problems(Procedure(bonus: "{ \"firstTimeSteps\": 2, \"count\": 1 }")));
        Expect(Procedure(bonus: "{ \"firstTimeSteps\": 5, \"count\": 1 }"), "needs 5 steps right first time, and the procedure has 2 steps");
    }

    [Fact]
    public void EachStepsNounIsACatalogueNounAndItsTargetsAreFixturesOfTheStation()
    {
        Expect(Procedure(steps: DefaultSteps.Replace("\"what\": \"madera\"", "\"what\": \"queso\"", StringComparison.Ordinal)),
            "acts on 'queso' (what), which is not a catalogue noun");
        Expect(Procedure(steps: DefaultSteps.Replace("\"where\": \"mesa\"", "\"where\": \"agua\"", StringComparison.Ordinal)),
            "where 'agua' is not a fixture of the station 'horno'");
        Expect(Procedure(steps: DefaultSteps.Replace("\"from\": \"puerta\"", "\"from\": \"madera\"", StringComparison.Ordinal)),
            "from 'madera' is not a fixture of the station 'horno'");
        Expect(Procedure(steps: DefaultSteps.Replace("\"what\": \"madera\", \"where\": \"mesa\"", "\"what\": \"madera\", \"where\": \"mesa\", \"use\": \"queso\"", StringComparison.Ordinal)),
            "acts with 'queso', which is not a catalogue noun");
    }

    [Fact]
    public void EachStepsVerbIsListedForItsPair()
    {
        Expect(Procedure(steps: DefaultSteps.Replace("\"do\": \"poner\"", "\"do\": \"abrir\"", StringComparison.Ordinal)),
            "does 'abrir' with 'madera' on 'mesa', and game/actions.json lists only poner for it");
        Expect(Procedure(steps: DefaultSteps.Replace("\"where\": \"mesa\"", "\"where\": \"puerta\"", StringComparison.Ordinal)),
            "does 'poner' with 'madera' on 'puerta', and game/actions.json lists no verb for that pair");
    }

    [Fact]
    public void EachStepsRowIsADoesRowWhoseLinesAreInTheSpeechIndex()
    {
        Expect(Procedure(steps: DefaultSteps.Replace("\"pon-la-madera\"", "\"ayuda\"", StringComparison.Ordinal)),
            "names the row 'ayuda', which is not a row of the phrase sheet");
        Expect(Procedure(steps: DefaultSteps.Replace("\"pon-la-madera\"", "\"hola-test\"", StringComparison.Ordinal)),
            "names the row 'hola-test', which is a 'hears' row, not a does row", Sheet("[\"shop_need.1\"]", Row("hola-test", "hears", "Hola.")));
        Expect(Procedure(), "whose audio line 'nowhere.9' is not in the speech index", Sheet("[\"nowhere.9\"]"));
        Expect(Procedure(), "names the does row 'pon-la-madera', which names no audio line", Sheet("[]"));
    }

    [Fact]
    public void ChunksNameTheirStepsContiguouslyAndInNoTwoChunks()
    {
        const string three = """
            { "id": "a", "row": "pon-la-madera", "do": "poner", "what": "madera", "where": "mesa" },
            { "id": "b", "row": "pon-la-madera", "do": "poner", "what": "madera", "where": "mesa" },
            { "id": "c", "row": "pon-la-madera", "do": "poner", "what": "madera", "where": "mesa" }
            """;
        Assert.Empty(Problems(Procedure(steps: three, chunks: "{ \"id\": \"x\", \"row\": \"haz-la-madera\", \"steps\": [\"a\", \"b\"] }, { \"id\": \"y\", \"row\": \"haz-el-pan\", \"steps\": [\"c\"] }")));
        Expect(Procedure(steps: three, chunks: "{ \"id\": \"x\", \"row\": \"haz-la-madera\", \"steps\": [\"a\", \"c\"] }"), "does not hold contiguous steps");
        Expect(Procedure(steps: three, chunks: "{ \"id\": \"x\", \"row\": \"haz-la-madera\", \"steps\": [\"a\", \"b\"] }, { \"id\": \"y\", \"row\": \"haz-el-pan\", \"steps\": [\"b\"] }"),
            "the step 'b' belongs to two chunks");
        Expect(Procedure(chunks: "{ \"id\": \"x\", \"row\": \"haz-la-madera\", \"steps\": [\"nope\"] }"), "names the step 'nope', which the procedure does not have");
        Expect(Procedure(chunks: "{ \"id\": \"x\", \"row\": \"pon-la-madera-no\", \"steps\": [\"madera\"] }"), "names the row 'pon-la-madera-no', which is not a row of the phrase sheet");
    }

    [Fact]
    public void TheGoalIsADoesRow()
    {
        Expect(Procedure(goal: "haz-pan-no"), "the goal names the row 'haz-pan-no', which is not a row of the phrase sheet");
    }

    [Fact]
    public void AProceduresSpanishKeepsToItsLevel()
    {
        // The fixture's Spanish is A1 except sacar (A2 only): the A2 procedure is clean, the A1 one is held to its list.
        Assert.Empty(Problems(Procedure(level: "a2")));
        Expect(Procedure(level: "a1"), "says “Saca el pan de la puerta.” (saca-el-pan), and 'saca' is above A1");
    }
}
