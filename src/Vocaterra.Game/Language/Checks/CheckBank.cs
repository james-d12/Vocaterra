using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Language.Phrases;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Language.Checks;

/// <summary>What a check item asks of the learner: to understand a line (<see cref="Comprehension"/>) or to say one (<see cref="Production"/>).</summary>
public enum CheckItemKind
{
    /// <summary>The learner hears a Spanish line and picks what it means among three options (a recognition measure, never production).</summary>
    Comprehension,

    /// <summary>The learner reads an English situation and says the phrase in Spanish (typed or spoken), graded by the core.</summary>
    Production,
}

/// <summary>The wire names of the check kinds and item kinds, for the session log and the report.</summary>
public static class CheckNames
{
    /// <summary>The wire name of an item kind: <c>comprehension</c> or <c>production</c>.</summary>
    public static string Of(CheckItemKind kind) => kind switch
    {
        CheckItemKind.Comprehension => "comprehension",
        CheckItemKind.Production => "production",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown check item kind."),
    };
}

/// <summary>One option of a comprehension item: its English label and, when it names an item, the item's icon (an <c>ItemId</c> name such as <c>Bread</c>).</summary>
/// <param name="Label">The English label the learner reads (<c>The shopkeeper offers you bread.</c>).</param>
/// <param name="Icon">The name of the <c>ItemId</c> drawn beside the label, or <see langword="null"/>.</param>
public sealed record CheckOption(string Label, string? Icon = null);

/// <summary>
/// One item of the check battery (<c>game/checks.json</c>, S11). A comprehension item has <see cref="Line"/> (the speech index id of the
/// pre-rendered line), <see cref="Spanish"/>, <see cref="Options"/> and the <see cref="Correct"/> option's index in the bundle's order. A
/// production item has the <see cref="Situation"/> the learner reads, the <see cref="Prompt"/> it is graded at and the <see cref="Intent"/> the
/// row's phrase must resolve to.
/// </summary>
/// <param name="Id">The item's id (<c>hear.pedir-agua</c> or <c>say.pedir-agua</c>).</param>
/// <param name="Kind">Comprehension or production.</param>
/// <param name="Phrase">The phrase sheet row the item is about (<c>pedir-agua</c>).</param>
/// <param name="Question">The English question of a comprehension item (<c>You hear this. What does it mean?</c>); empty for production.</param>
/// <param name="Line">The speech index id of the line the learner hears (<c>phrase.pedir-agua</c>, <c>prompt.stall-need</c>); empty for production.</param>
/// <param name="Spanish">The Spanish text of that line, for the panel; empty for production.</param>
/// <param name="Options">The three options of a comprehension item, in the bundle's order; empty for production.</param>
/// <param name="Correct">The index in <see cref="Options"/> that is right (bundle order).</param>
/// <param name="Situation">The English situation of a production item; empty for comprehension.</param>
/// <param name="Prompt">The prompt a production item is graded at (<c>stall-need</c>); empty for comprehension.</param>
/// <param name="Intent">The intent a production item must resolve to (<c>request_item</c>); empty for comprehension.</param>
public sealed record CheckItem(
    string Id,
    CheckItemKind Kind,
    string Phrase,
    string Question,
    string Line,
    string Spanish,
    IReadOnlyList<CheckOption> Options,
    int Correct,
    string Situation,
    string Prompt,
    string Intent);

/// <summary>
/// The check battery's items, read from the bundle's host file <c>game/checks.json</c> (hashed, listed in <c>provenance.json</c>). Every item is
/// checked against the phrase sheet and the bundle's prompts when it is read, so a bad item is a load error, not a surprise in a playtest.
/// </summary>
public static class CheckBank
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/checks.json";

    /// <summary>Reads the battery from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or an item is malformed or refers to a missing phrase or prompt.</exception>
    public static IReadOnlyList<CheckItem> Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span), PhraseSheet.Load(bundle), bundle.Prompts.Keys.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Builds the battery from the file's text.</summary>
    /// <param name="json">The contents of <c>game/checks.json</c>.</param>
    /// <param name="sheet">The phrase sheet the items refer to.</param>
    /// <param name="prompts">The bundle's prompt ids.</param>
    /// <exception cref="InvalidDataException">The text is malformed or an item does not hold together.</exception>
    public static IReadOnlyList<CheckItem> Parse(string json, PhraseSheet sheet, IReadOnlySet<string> prompts)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(prompts);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException("checks.json: schemaVersion must be 1.");
            }

            var items = new List<CheckItem>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("items").EnumerateArray())
            {
                var item = ParseItem(element, sheet, prompts);
                if (!ids.Add(item.Id))
                {
                    throw new InvalidDataException($"checks.json: the item id '{item.Id}' is used twice.");
                }

                items.Add(item);
            }

            if (items.Count == 0)
            {
                throw new InvalidDataException("checks.json: the battery has no items.");
            }

            return items;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"checks.json is malformed: {ex.Message}", ex);
        }
    }

    private static CheckItem ParseItem(JsonElement element, PhraseSheet sheet, IReadOnlySet<string> prompts)
    {
        var id = element.GetProperty("id").GetString() ?? string.Empty;
        var phrase = element.GetProperty("phrase").GetString() ?? string.Empty;
        var row = sheet.Find(phrase) ?? throw new InvalidDataException($"checks.json: item '{id}' names the phrase '{phrase}', which is not in the phrase sheet.");
        var kind = element.GetProperty("kind").GetString();
        switch (kind)
        {
            case "comprehension":
                var line = element.GetProperty("line").GetString() ?? string.Empty;
                var options = element.GetProperty("options").EnumerateArray()
                    .Select(static o => new CheckOption(o.GetProperty("label").GetString() ?? string.Empty, o.TryGetProperty("icon", out var icon) ? icon.GetString() : null))
                    .ToList();
                var correct = element.GetProperty("correct").GetInt32();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(line) || options.Count != 3 || options.Any(static o => o.Label.Length == 0))
                {
                    throw new InvalidDataException($"checks.json: comprehension item '{id}' needs an id, a line, and three labelled options.");
                }

                if (correct < 0 || correct >= options.Count)
                {
                    throw new InvalidDataException($"checks.json: comprehension item '{id}' has its correct option at {correct}, outside 0 to {options.Count - 1}.");
                }

                return new CheckItem(
                    id,
                    CheckItemKind.Comprehension,
                    row.Id,
                    element.GetProperty("question").GetString() ?? string.Empty,
                    line,
                    element.GetProperty("spanish").GetString() ?? string.Empty,
                    options,
                    correct,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            case "production":
                var prompt = element.GetProperty("prompt").GetString() ?? string.Empty;
                var intent = element.GetProperty("intent").GetString() ?? string.Empty;
                var situation = element.GetProperty("situation").GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(situation) || string.IsNullOrWhiteSpace(intent))
                {
                    throw new InvalidDataException($"checks.json: production item '{id}' needs an id, a situation and an intent.");
                }

                if (!prompts.Contains(prompt))
                {
                    throw new InvalidDataException($"checks.json: production item '{id}' is graded at the prompt '{prompt}', which the bundle does not have.");
                }

                if (!row.IsLearnerPhrase)
                {
                    throw new InvalidDataException($"checks.json: production item '{id}' names '{phrase}', a phrase the learner does not say.");
                }

                return new CheckItem(id, CheckItemKind.Production, row.Id, string.Empty, string.Empty, string.Empty, [], -1, situation, prompt, intent);
            default:
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"checks.json: item '{id}' has the kind '{kind}', which is neither comprehension nor production."));
        }
    }
}
