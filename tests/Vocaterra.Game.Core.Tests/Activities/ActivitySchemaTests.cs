using NJsonSchema;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Activities;

/// <summary>
/// The schemas of the procedures, the stations and the actions (W05, design 10.7): each accepts its design shape and refuses what the loaders refuse. The
/// procedure schema refers to the nouns schema's item names, so the design's flour (not an item) is refused by the schema too.
/// </summary>
public sealed class ActivitySchemaTests
{
    private static string Schema(string name) => Path.Combine(Repo.Path("content", "schema"), name);

    // The design's pan example with its items as the game names them: flour is not one yet (W12), so the test's pan uses wood for it.
    private const string Pan = """
        {
          "$schema": "../../../schema/procedure.schema.json", "schemaVersion": 1, "id": "pan", "station": "horno", "teacher": "baker", "level": "a2",
          "makes": { "item": "bread", "count": 2, "bonus": { "firstTimeSteps": 5, "count": 1 } },
          "uses": [ { "item": "wood", "count": 1 }, { "item": "water-bottle", "count": 1, "leaves": "bottle" } ],
          "steps": [
            { "id": "harina", "row": "pon-la-harina", "do": "poner", "what": "harina", "where": "bol", "anyOrder": "bol" },
            { "id": "mezclar", "row": "mezcla", "do": "mezclar", "what": "bol", "with": "cuchara" },
            { "id": "sacar", "row": "saca-el-pan", "do": "sacar", "what": "pan", "use": "mano", "from": "horno" }
          ],
          "chunks": [ { "id": "la-masa", "row": "haz-la-masa", "steps": ["harina", "mezclar"] } ],
          "goal": { "row": "haz-pan" }
        }
        """;

    [Fact]
    public async Task TheProcedureSchemaAcceptsADesignShapedProcedureAndRefusesItsBreakages()
    {
        var schema = await JsonSchema.FromFileAsync(Schema("procedure.schema.json"));

        Assert.Empty(schema.Validate(Pan));
        Assert.NotEmpty(schema.Validate(Pan.Replace("\"item\": \"wood\"", "\"item\": \"Hard Cheese\"", StringComparison.Ordinal))); // not shaped like an item key (whether a key names an item is the content check's, I01)
        Assert.NotEmpty(schema.Validate(Pan.Replace("\"where\": \"bol\", \"anyOrder\"", "\"where\": \"bol\", \"from\": \"horno\", \"anyOrder\"", StringComparison.Ordinal))); // two targets
        Assert.NotEmpty(schema.Validate(Pan.Replace("\"where\": \"bol\", \"anyOrder\"", "\"anyOrder\"", StringComparison.Ordinal))); // no target
        Assert.NotEmpty(schema.Validate(Pan.Replace("\"level\": \"a2\"", "\"level\": \"b1\"", StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate(Pan.Replace("\"goal\": { \"row\": \"haz-pan\" }", "\"goal\": { \"row\": \"haz-pan\" }, \"extra\": 1", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TheIndexStationsAndActionSchemasAcceptTheirShapes()
    {
        var index = await JsonSchema.FromFileAsync(Schema("procedures.schema.json"));
        Assert.Empty(index.Validate("""{ "schemaVersion": 1, "procedures": ["pan"] }"""));
        Assert.NotEmpty(index.Validate("""{ "schemaVersion": 1, "procedures": ["Pan"] }"""));

        var stations = await JsonSchema.FromFileAsync(Schema("stations.schema.json"));
        Assert.Empty(stations.Validate("""{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [ { "noun": "bol", "icon": "bowl" } ] } ] }"""));
        Assert.NotEmpty(stations.Validate("""{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [ { "noun": "bol" } ] } ] }"""));
        Assert.NotEmpty(stations.Validate("""{ "schemaVersion": 1, "stations": [ { "id": "horno", "prop": "oven", "npc": "baker", "fixtures": [], "items": [] } ] }"""));

        var actions = await JsonSchema.FromFileAsync(Schema("actions.schema.json"));
        Assert.Empty(actions.Validate("""{ "schemaVersion": 1, "actions": [ { "use": "mano", "on": "horno", "verbs": ["sacar", "abrir"] } ] }"""));
        Assert.NotEmpty(actions.Validate("""{ "schemaVersion": 1, "actions": [ { "use": "mano", "on": "horno", "verbs": [] } ] }"""));
        Assert.NotEmpty(actions.Validate("""{ "schemaVersion": 1, "actions": [ { "use": "mano", "verbs": ["sacar"] } ] }"""));
    }
}
