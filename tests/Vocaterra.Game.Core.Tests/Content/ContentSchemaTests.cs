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
    public async Task AStepTypeRequiresItsOwnFields()
    {
        // The lessons schema says what each step type needs with if/then: a listen step without its options is refused, and so is a do step without its event.
        var schema = await JsonSchema.FromFileAsync(Path.Combine(SchemaFolder, "lessons.schema.json"));
        static string Lesson(string step) =>
            $$"""{ "schemaVersion": 1, "lessons": [{ "id": "a", "npc": "guide", "title": "A", "english": "A", "steps": [{{step}}] }] }""";
        Assert.Empty(schema.Validate(Lesson("""{ "id": "s", "type": "listen", "english": "E", "line": "phrase.hola", "translation": "T", "options": ["a", "b"], "correct": 0 }""")));
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "listen", "english": "E", "line": "phrase.hola", "translation": "T" }""")));
        Assert.NotEmpty(schema.Validate(Lesson("""{ "id": "s", "type": "do", "english": "E" }""")));
    }

    [Fact]
    public void TheItemAndBlockNamesOfTheNounSchemaAreTheGamesTables()
    {
        // The schema lists the stable names for the editors' completion; the code's tables are the authority.
        var definitions = ReadJson(Path.Combine(SchemaFolder, "nouns.schema.json"))["definitions"]!;
        Assert.Equal(ItemDefinition.All.Select(static d => d.Name), Names(definitions["item"]!));
        Assert.Equal(BlockDefinition.All.Select(static d => d.Name), Names(definitions["block"]!));
    }

    // Schemas written before the files that use them (the locale files arrive with C03).
    private static readonly HashSet<string> Pending = new(StringComparer.Ordinal) { "locale.schema.json", "locales.schema.json" };

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
