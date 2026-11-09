using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>
/// The JSON Schemas in <c>content/schema</c> (content-plan 3.7, C01): every game content file names its schema in <c>$schema</c> and validates against
/// it, every schema is used, and the schemas keep in step with the code's tables. The schemas serve editors (Rider and VS Code validate and complete
/// as you type); the loaders stay the authority, and this test keeps the two in step.
/// </summary>
public sealed class ContentSchemaTests
{
    // The folders whose JSON files are the game's own content (the core's files of content/game-v1 keep their own formats and carry no $schema).
    private static readonly string[] GameFolders = [Path.Combine("content", "game-v1", "game"), Path.Combine("content", "locale")];

    public static TheoryData<string> GameFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in EnumerateGameFiles())
        {
            data.Add(Path.GetRelativePath(Repo.Root, file).Replace('\\', '/'));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GameFiles))]
    public async Task EveryGameFileNamesItsSchemaAndValidatesAgainstIt(string relative)
    {
        var path = Repo.Path(relative);
        var json = ReadJson(path);
        var reference = json["$schema"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(reference), $"{relative} names no $schema.");
        var schemaPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference));
        Assert.True(File.Exists(schemaPath), $"{relative} names the schema {reference}, which does not exist.");
        Assert.StartsWith(SchemaFolder, schemaPath, StringComparison.Ordinal);

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        var errors = schema.Validate(Plain(json));
        Assert.True(errors.Count == 0, $"{relative} does not match {reference}:\n" + string.Join("\n", errors.Select(static e => $"  {e.Path}: {e.Kind}")));
    }

    [Fact]
    public void EverySchemaIsDraft07AndUsedByAFile()
    {
        var used = EnumerateGameFiles()
            .Select(file => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, ReadJson(file)["$schema"]?.GetValue<string>() ?? string.Empty)))
            .ToHashSet(StringComparer.Ordinal);
        var schemas = Directory.GetFiles(SchemaFolder, "*.schema.json").Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(schemas);
        foreach (var schema in schemas)
        {
            Assert.Equal("http://json-schema.org/draft-07/schema#", ReadJson(schema)["$schema"]?.GetValue<string>());
            Assert.True(used.Contains(schema) || Pending.Contains(Path.GetFileName(schema)), $"No content file uses {Path.GetFileName(schema)}.");
        }
    }

    [Fact]
    public async Task ASchemaRejectsAMisspelledFieldAndAMissingOne()
    {
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "nouns.schema.json"));
        Assert.Empty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "id": "bread", "lemma": "pan", "items": ["bread"] }] }"""));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "id": "bread", "lemma": "pan", "itmes": ["bread"] }] }"""));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "id": "bread", "lemma": "pan", "items": ["loaf"] }] }"""));
    }

    [Fact]
    public async Task ACheckItemNamesItsLessonInSchemaTwo()
    {
        // village-plan V07: every item names the lesson it belongs to, or null for none; the battery may say its core sessions.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "checks.schema.json"));
        static string Checks(string version, string lesson, string core = "") =>
            $$"""{ "schemaVersion": {{version}}, {{core}} "items": [ { "id": "say.x", "kind": "production", "phrase": "pedir-agua", {{lesson}} "prompt": "stall-need", "intent": "request_item" } ] }""";

        Assert.Empty(schema.Validate(Checks("2", "\"lesson\": \"first-words\",", "\"coreSessions\": 3,")));
        Assert.Empty(schema.Validate(Checks("2", "\"lesson\": null,")));
        Assert.NotEmpty(schema.Validate(Checks("2", string.Empty))); // no lesson
        Assert.NotEmpty(schema.Validate(Checks("1", "\"lesson\": null,"))); // the schema before V07
        Assert.NotEmpty(schema.Validate(Checks("2", "\"lesson\": null,", "\"coreSessions\": 0,")));
    }

    [Fact]
    public async Task AStepTypeRequiresItsOwnFieldsAndCarriesNoSupportLanguage()
    {
        // The lessons schema says what each step type needs (anyOf with not: NJsonSchema does not apply if/then): a listen step without its answer is refused,
        // and so is a do step without its event. A step's text belongs to the locale since C03, so a step that carries "english" is refused too.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lesson.schema.json"));
        static string Lesson(string step) =>
            $$"""{ "schemaVersion": 2, "id": "a", "npc": "guide", "level": "a1", "steps": [{{step}}] }""";
        Assert.Empty(schema.Validate(Lesson("""{ "id": "s", "type": "listen", "line": "phrase.hola", "correct": 0 }""")));
        Assert.Empty(schema.Validate(Lesson("""{ "id": "s", "type": "do", "trigger": { "event": "item_picked_up", "detail": "fruta" } }""")));
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "listen", "line": "phrase.hola" }""")));
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "do" }""")));
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "do", "event": "item_picked_up" }"""))); // C06: the event is the trigger's
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "explain", "english": "E" }""")));
    }

    [Fact]
    public async Task AnNpcFileOfSchemaTwoNamesItsPromptsOpeningAndVoice()
    {
        // V01: game/npcs/<id>.json says what the NPC does that was code (the prompts by state, the opening, the voice); a file of schema 1 lacks them.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "npc.schema.json"));
        static string Npc(string schemaVersion = "2", string prompts = "\"prompts\": { \"idle\": \"fisher-open\" },", string opening = "{ \"event\": \"fisher.greet\", \"when\": \"first\" }", string voice = "{ \"speaker\": 53, \"name\": \"em_santa\", \"speed\": 0.9 }") =>
            $$"""{ "schemaVersion": {{schemaVersion}}, "id": "fisher", "label": "Pescador", "figure": "fisher", {{prompts}} "opening": {{opening}}, "voice": {{voice}} }""";
        Assert.Empty(schema.Validate(Npc()));
        Assert.Empty(schema.Validate(Npc(opening: "{ \"when\": \"never\" }", voice: "{ \"speaker\": 28, \"name\": \"ef_dora\" }")));
        Assert.NotEmpty(schema.Validate(Npc(schemaVersion: "1")));
        Assert.NotEmpty(schema.Validate(Npc(prompts: string.Empty)));
        Assert.NotEmpty(schema.Validate(Npc(prompts: "\"prompts\": {},")));
        Assert.NotEmpty(schema.Validate(Npc(opening: "{ \"event\": \"fisher.greet\", \"when\": \"sometimes\" }")));
        Assert.NotEmpty(schema.Validate(Npc(voice: "{ \"speaker\": 54, \"name\": \"em_santa\" }")));
        Assert.NotEmpty(schema.Validate(Npc(voice: "{ \"speaker\": 53 }")));
        Assert.NotEmpty(schema.Validate(Npc(voice: "{ \"speaker\": 53, \"name\": \"em_santa\", \"pitch\": 2 }")));
    }

    [Fact]
    public async Task AHostEventsFileOfSchemaTwoGivesACountAndPointsAtPlaces()
    {
        // V02: give_item takes a count (a number, or qty: the number the learner said), and the beacon goes to a place of the scene or to the place a slot names.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "host-events.schema.json"));
        static string File(string action, string version = "2") =>
            $$"""{ "schemaVersion": {{version}}, "events": [ { "npc": "villager", "event": "villager.do", "action": {{action}} } ] }""";
        Assert.Empty(schema.Validate(File("""{ "kind": "give_item", "item": "bread", "count": 2 }""")));
        Assert.Empty(schema.Validate(File("""{ "kind": "give_item", "item": "bread", "count": "qty" }""")));
        Assert.Empty(schema.Validate(File("""{ "kind": "point_at_place", "place": "stall" }""")));
        Assert.Empty(schema.Validate(File("""{ "kind": "point_at_slot", "slot": "place" }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "give_item", "item": "bread" }""", version: "1")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "give_item", "item": "bread", "count": 0 }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "give_item", "item": "bread", "count": "two" }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "point_at_place", "noun": "río" }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "point_at_slot" }""")));

        // V03: take_item and sell.
        Assert.Empty(schema.Validate(File("""{ "kind": "take_item", "item": "coin", "count": 3 }""")));
        Assert.Empty(schema.Validate(File("""{ "kind": "sell", "item": "bread", "price": 2 }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "sell", "item": "bread" }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "sell", "item": "bread", "price": 0 }""")));
        Assert.NotEmpty(schema.Validate(File("""{ "kind": "sell", "item": "bread", "price": 2, "count": 2 }""")));
    }

    [Fact]
    public async Task AnOrderNamesItsNpcAndGoodsAndHasNoStepsOfItsOwn()
    {
        // V03: an objective with deliverTo and deliver (and optionally session) has no title and no steps; any other objective has steps and no goods.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "objective.schema.json"));
        const string Order = """{ "schemaVersion": 1, "id": "oven-wood", "session": 5, "deliverTo": "baker", "deliver": [ { "item": "wood", "count": 5 } ] }""";
        Assert.Empty(schema.Validate(Order));
        Assert.Empty(schema.Validate("""{ "schemaVersion": 1, "id": "a", "session": 2, "steps": [ { "id": "s", "target": 1, "trigger": { "event": "fire_lit" } } ] }"""));
        Assert.NotEmpty(schema.Validate(Order.Replace("\"session\": 5", "\"session\": 0", StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate(Order.Replace(", \"deliver\": [ { \"item\": \"wood\", \"count\": 5 } ]", string.Empty, StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate(Order.Replace("\"wood\"", "\"madera\"", StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate(Order.Replace("\"session\": 5,", "\"title\": \"Horno\",", StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate(Order.Replace("\"session\": 5,", "\"steps\": [ { \"id\": \"s\", \"target\": 1, \"trigger\": { \"event\": \"fire_lit\" } } ],", StringComparison.Ordinal)));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 1, "id": "a" }"""));
    }

    [Fact]
    public async Task ADoStepMayPointAtAPlaceOfTheScene()
    {
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lesson.schema.json"));
        Assert.Empty(schema.Validate("""{ "schemaVersion": 2, "id": "a", "npc": "villager", "level": "a1", "steps": [{ "id": "s", "type": "do", "pointPlace": "stall", "trigger": { "event": "beacon_reached", "detail": "stall" } }] }"""));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 2, "id": "a", "npc": "villager", "level": "a1", "steps": [{ "id": "s", "type": "do", "pointPlace": "", "trigger": { "event": "beacon_reached" } }] }"""));
    }

    [Fact]
    public async Task ARepeatOrAnswerStepMayExpectTheSimpleFuture()
    {
        // V16: a step's tense may be fut (the validator holds it to an a2 lesson); the enum is wider, so a file of schema 2 stays valid and the version is unchanged.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lesson.schema.json"));
        static string Lesson(string tense) =>
            $$"""{ "schemaVersion": 2, "id": "a", "npc": "fisher", "level": "a2", "steps": [{ "id": "s", "type": "repeat", "phrase": "voy-a-comprar-pan", "tense": "{{tense}}" }] }""";
        foreach (var tense in new[] { "pres", "pret", "ir-a", "fut" })
        {
            Assert.Empty(schema.Validate(Lesson(tense)));
        }

        Assert.NotEmpty(schema.Validate(Lesson("future")));
        Assert.NotEmpty(schema.Validate(Lesson("cond")));
    }

    [Fact]
    public async Task ALessonOfSchemaTwoDeclaresItsLevel()
    {
        // V08: every lesson declares the level it keeps to, a1 or a2 (the validator holds its Spanish and its tenses to it); a file of schema 1 has none.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lesson.schema.json"));
        static string Lesson(string level, string version = "2") =>
            $$"""{ "schemaVersion": {{version}}, "id": "a", "npc": "guide", {{level}} "steps": [{ "id": "s", "type": "explain" }] }""";
        Assert.Empty(schema.Validate(Lesson("\"level\": \"a1\",")));
        Assert.Empty(schema.Validate(Lesson("\"level\": \"a2\",")));
        Assert.NotEmpty(schema.Validate(Lesson(string.Empty))); // no level
        Assert.NotEmpty(schema.Validate(Lesson("\"level\": \"b1\",")));
        Assert.NotEmpty(schema.Validate(Lesson("\"level\": \"A1\",")));
        Assert.NotEmpty(schema.Validate(Lesson("\"level\": \"a1\",", version: "1"))); // the schema before V08
    }

    [Fact]
    public void TheItemAndBlockNamesOfTheNounSchemaAreTheGamesTables()
    {
        // The schema lists the stable names for the editors' completion; the code's tables are the authority.
        var definitions = ReadJson(Path.Combine(SchemaFolder, "nouns.schema.json"))["definitions"]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(definitions["item"]!));
        Assert.Equal(BlockDefinition.All.Select(static d => d.Name), Names(definitions["block"]!));
        var hostEvents = ReadJson(Path.Combine(SchemaFolder, "host-events.schema.json"))["definitions"]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(hostEvents["item"]!)); // C08: a host event gives an item by the same names
        var objective = ReadJson(Path.Combine(SchemaFolder, "objective.schema.json"))["definitions"]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(objective["item"]!)); // V03: and an order asks for them by the same names
    }

    // Schemas written before the files that use them: procedure.schema.json is the shape of game/procedures/<id>.json, which W10 adds (W05 has none).
    private static readonly HashSet<string> Pending = new(StringComparer.Ordinal) { "procedure.schema.json" };

    private static string SchemaFolder => Repo.Path("content", "schema") + Path.DirectorySeparatorChar;

    private static IEnumerable<string> EnumerateGameFiles() =>
        GameFolders
            .Select(folder => Repo.Path(folder))
            .Where(Directory.Exists)
            .SelectMany(static folder => Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
            .Where(static file => !string.Equals(Path.GetFileName(file), "manifest.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    // game/scene.json has // comments (JSONC); they are skipped here as the scene's loader skips them.
    private static JsonNode ReadJson(string path) =>
        JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
        ?? throw new InvalidDataException($"{path} is empty.");

    private static string Plain(JsonNode json) => json.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static IEnumerable<string> Names(JsonNode definition) => definition["enum"]!.AsArray().Select(static n => n!.GetValue<string>());
}
