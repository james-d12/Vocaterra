using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// A three-step procedure built in the test (W06; the bundle has none): wood on the table and water on the table in either order (a group), then the bread out of the oven;
/// two chunks and a goal; it uses a wood and a water-bottle (which leaves a bottle) and makes bread, with a bonus loaf when all three steps are right the first time.
/// </summary>
internal static class ProcedureRig
{
    public const string Pan = """
        { "schemaVersion": 1, "id": "pan", "station": "horno", "teacher": "baker", "level": "a2",
          "makes": { "item": "bread", "count": 2, "bonus": { "firstTimeSteps": 3, "count": 1 } },
          "uses": [ { "item": "wood", "count": 1 }, { "item": "water-bottle", "count": 1, "leaves": "bottle" } ],
          "steps": [
            { "id": "madera", "row": "pon-la-madera", "do": "poner", "what": "madera", "where": "mesa", "anyOrder": "mesa" },
            { "id": "agua", "row": "echa-el-agua", "do": "echar", "what": "agua", "where": "mesa", "anyOrder": "mesa" },
            { "id": "sacar", "row": "saca-el-pan", "do": "sacar", "what": "pan", "use": "mano", "from": "horno" } ],
          "chunks": [
            { "id": "la-masa", "row": "haz-la-masa", "steps": ["madera", "agua"] },
            { "id": "el-pan", "row": "haz-el-pan", "steps": ["sacar"] } ],
          "goal": { "row": "haz-pan" } }
        """;

    public const string Stations = """{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [ { "noun": "mesa", "icon": "table" }, { "noun": "horno", "icon": "oven" } ] } ] }""";

    public static readonly string[] Rows = ["pon-la-madera", "echa-el-agua", "saca-el-pan", "haz-la-masa", "haz-el-pan", "haz-pan"];

    public static ProcedureBook Book { get; } = ProcedureBook.Parse("""{ "schemaVersion": 1, "procedures": ["pan"] }""", new Dictionary<string, string> { ["pan"] = Pan });

    public static StationBook StationBook { get; } = StationBook.Parse(Stations);

    public static ProcedureDefinition Definition => Book.Find("pan")!;

    public static PhraseSheet Sheet()
    {
        var text = "{ \"schemaVersion\": 1, \"phrases\": [" + string.Join(", ", Rows.Select(static r => $$"""{ "id": "{{r}}", "says": "does", "spanish": "{{r}}", "audioLineIds": ["shop_need.1"] }""")) + "] }";
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in Regex.Matches(text, "\"id\": \"([a-z-]+)\"").Select(static m => m.Groups[1].Value))
        {
            strings[PhraseSheet.LocaleId(id, "meaning")] = "meaning of " + id;
            strings[PhraseSheet.LocaleId(id, "card")] = "card of " + id;
        }

        return PhraseSheet.Parse(text, TestGame.Locale.With(strings));
    }

    /// <summary>The game's content with the test procedure, its station and its instruction rows.</summary>
    public static GameData Data() => TestGame.Data.WithPhrases(Sheet()).WithStations(StationBook).WithProcedures(Book);

    /// <summary>A bag with what one run uses.</summary>
    public static Inventory Bag()
    {
        var bag = new Inventory();
        _ = bag.Add(ItemId.Wood, 1);
        _ = bag.Add(ItemId.WaterBottle, 1);
        return bag;
    }

    /// <summary>The act of a step: what is held, where, and the verb.</summary>
    public static (string Use, string On, string Verb) ActOf(string stepId)
    {
        var step = Definition.Steps.First(s => s.Id == stepId);
        return (step.ActUse, step.ActOn, step.Verb);
    }
}
