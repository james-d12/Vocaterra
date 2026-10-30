using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>
/// The JSON Schemas in <c>content/schema</c> (CONTENT_PLAN 3.7, C01): every game content file names its schema in <c>$schema</c> and validates against
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
        Assert.Empty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "lemma": "pan", "items": ["bread"] }] }"""));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "lemma": "pan", "itmes": ["bread"] }] }"""));
        Assert.NotEmpty(schema.Validate("""{ "schemaVersion": 1, "nouns": [{ "lemma": "pan", "items": ["loaf"] }] }"""));
    }

    [Fact]
    public async Task AStepTypeRequiresItsOwnFieldsAndCarriesNoSupportLanguage()
    {
        // The lessons schema says what each step type needs (anyOf with not: NJsonSchema does not apply if/then): a listen step without its answer is refused,
        // and so is a do step without its event. A step's text belongs to the locale since C03, so a step that carries "english" is refused too.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lesson.schema.json"));
        static string Lesson(string step) =>
            $$"""{ "schemaVersion": 1, "id": "a", "npc": "guide", "steps": [{{step}}] }""";
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
    public void TheItemAndBlockNamesOfTheNounSchemaAreTheGamesTables()
    {
        // The schema lists the stable names for the editors' completion; the code's tables are the authority.
        var definitions = ReadJson(Path.Combine(SchemaFolder, "nouns.schema.json"))["definitions"]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(definitions["item"]!));
        Assert.Equal(BlockDefinition.All.Select(static d => d.Name), Names(definitions["block"]!));
        var give = ReadJson(Path.Combine(SchemaFolder, "host-events.schema.json"))["properties"]!["events"]!["items"]!["properties"]!["action"]!["oneOf"]![0]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(give["properties"]!["item"]!)); // C08: a host event gives an item by the same names
    }

    // Schemas written before the files that use them (none at present).
    private static readonly HashSet<string> Pending = new(StringComparer.Ordinal);

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
