using System.Text.Json;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// T03 (D-game-27, G11): the world's data names things by the neutral ids of <c>game/nouns.json</c> (<c>wood</c>, <c>oven</c>), never by a target-language lemma
/// (<c>madera</c>, <c>horno</c>), so a German bundle maps <c>wood</c> to <c>Holz</c> with no change to the world. A string (a value or a property name) of a world file
/// that equals a lemma of the noun catalogue fails here. The language files (lessons, phrases, dialogue, checks, grammars, the NPCs' key words) keep their lemmas and are not
/// read. The allow-list names each string a world file keeps on purpose, with its reason.
/// </summary>
public sealed class WorldDataNeutralityTests
{
    // The world files of content/game-v1/game, relative to it: the folders are read whole.
    private static readonly string[] WorldFiles =
    [
        "recipes.json", "stations.json", "actions.json", "host-events.json", "needs.json", "tips.json", "scene.json", "session.json", "procedures.json", "controls.json",
    ];

    private static readonly string[] WorldFolders = ["objectives", "procedures"];

    // The strings a world file keeps that are a lemma, by file and string, each with the reason.
    private static readonly Dictionary<(string File, string Text), string> Allowed = new()
    {
        [("procedures/bread.json", "harina")] = "a step id: the locale keys its line by it (line.guide.lesson.baker.proc_harina.1), and the locale is not this milestone's; the step is not a noun",
        [("procedures/bread.json", "agua")] = "a step id: the locale keys its line by it (proc_agua), see harina",
        [("procedures/bread.json", "madera")] = "a step id: the locale keys its line by it (proc_madera), see harina",
        [("procedures/bread.json", "masa")] = "a step id: the locale keys its line by it (proc_masa), see harina",
    };

    [Fact]
    public void NoStringOfAWorldFileIsALemmaOfTheNounCatalogue()
    {
        var lemmas = Lemmas();
        Assert.True(lemmas.Count > 20, "the catalogue's lemmas were not read");

        var offences = new List<string>();
        var files = 0;
        foreach (var (relative, path) in Files())
        {
            files++;
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var text in Strings(document.RootElement).Where(text => lemmas.Contains(text) && !Allowed.ContainsKey((relative, text))))
            {
                offences.Add($"{relative} has the string \"{text}\", a lemma of game/nouns.json: name the noun by its id");
            }
        }

        Assert.True(files >= 12, "the world files were not found");
        Assert.Empty(offences.Distinct());
    }

    [Fact]
    public void EveryAllowedStringIsStillInItsFileWithAReason()
    {
        foreach (var ((file, text), reason) in Allowed)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason), $"{file} \"{text}\" has no reason");
            var path = Path.Combine(GameFolder(), file);
            Assert.True(File.Exists(path), $"{file} is not a world file");
            Assert.True(File.ReadAllText(path).Contains($"\"{text}\"", StringComparison.Ordinal), $"{file} no longer has \"{text}\"");
        }
    }

    [Fact]
    public void EveryNounHasAnIdThatIsNoLemma()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameFolder(), "nouns.json")));
        var nouns = document.RootElement.GetProperty("nouns").EnumerateArray().ToList();
        var ids = nouns.Select(static n => n.GetProperty("id").GetString()!).ToList();
        var lemmas = nouns.Select(static n => n.GetProperty("lemma").GetString()!).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(ids, lemmas.Contains);
    }

    private static HashSet<string> Lemmas()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameFolder(), "nouns.json")));
        var lemmas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var noun in document.RootElement.GetProperty("nouns").EnumerateArray())
        {
            _ = lemmas.Add(noun.GetProperty("lemma").GetString()!);
            if (noun.TryGetProperty("labels", out var labels))
            {
                foreach (var label in labels.EnumerateObject())
                {
                    _ = lemmas.Add(label.Value.GetString()!);
                }
            }
        }

        return lemmas;
    }

    // Every string of a JSON tree: the values, and the names of the properties (a price list or a map by noun names them as keys).
    private static IEnumerable<string> Strings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray().SelectMany(Strings))
                {
                    yield return item;
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var item in Strings(property.Value))
                    {
                        yield return item;
                    }
                }

                break;
        }
    }

    private static IEnumerable<(string Relative, string Path)> Files()
    {
        var folder = GameFolder();
        foreach (var file in WorldFiles.Where(file => File.Exists(Path.Combine(folder, file))))
        {
            yield return (file, Path.Combine(folder, file));
        }

        foreach (var sub in WorldFolders)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(folder, sub), "*.json").Order(StringComparer.Ordinal))
            {
                yield return (sub + "/" + Path.GetFileName(file), file);
            }
        }
    }

    private static string GameFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "content", "game-v1", "manifest.json")))
            {
                return Path.Combine(directory.FullName, "content", "game-v1", "game");
            }
        }

        throw new DirectoryNotFoundException("content/game-v1 was not found above the test output folder.");
    }
}
