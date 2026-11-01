using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Language.Checks;

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
/// <param name="Label">The support-language label the learner reads (<c>The shopkeeper offers you bread.</c>; <c>check.&lt;id&gt;.option.&lt;n&gt;</c> in the locale).</param>
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
/// <param name="Question">The support-language question of a comprehension item (<c>You hear this. What does it mean?</c>); empty for production.</param>
/// <param name="Line">The speech index id of the line the learner hears (<c>phrase.pedir-agua</c>, <c>prompt.stall-need</c>); empty for production.</param>
/// <param name="Spanish">The Spanish text of that line, for the panel; empty for production.</param>
/// <param name="Options">The three options of a comprehension item, in the bundle's order; empty for production.</param>
/// <param name="Correct">The index in <see cref="Options"/> that is right (bundle order).</param>
/// <param name="Situation">The support-language situation of a production item; empty for comprehension.</param>
/// <param name="Prompt">The prompt a production item is graded at (<c>stall-need</c>); empty for comprehension.</param>
/// <param name="Intent">The intent a production item must resolve to (<c>request_item</c>); empty for comprehension.</param>
/// <param name="Lesson">The lesson the item belongs to (VILLAGE_PLAN V07), or <see langword="null"/> for an item of no lesson (the free run's stall and villager).</param>
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
    string Intent,
    string? Lesson = null);

/// <summary>
/// The check battery's items, read from the bundle's host file <c>game/checks.json</c> (hashed, listed in <c>provenance.json</c>), in the file's order. Every
/// item's shape is checked when it is read; its phrase, line, prompt and lesson are checked against the other files by <see cref="Content.GameDataValidator"/>,
/// so a bad item is a load error, not a surprise in a playtest.
/// </summary>
/// <remarks>
/// <b>By lesson</b> (VILLAGE_PLAN V07, schema 2). Each item names the lesson it belongs to, and a battery asks the items of the lessons the play session has
/// unlocked (<see cref="AskedIn"/>), so a new lesson's items join the battery from the session it opens in and the battery does not grow with the bundle. The
/// file's <see cref="CoreSessions"/> are asked in every battery from the first session on: the lessons of sessions 1 to 3 are the pilot's battery of 31 items
/// (<c>docs/game/PLAYTEST.md</c>), which the pilot asks whole in its first session and at the recall in its second.
/// </remarks>
public sealed class CheckBank : IReadOnlyList<CheckItem>
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/checks.json";

    /// <summary>The file's schema version: 2 since VILLAGE_PLAN V07 (an item's <c>lesson</c>, the battery's <c>coreSessions</c>).</summary>
    public const int SchemaVersion = 2;

    private readonly IReadOnlyList<CheckItem> _items;

    /// <summary>A battery of these items.</summary>
    /// <param name="items">The items, in the file's order.</param>
    /// <param name="coreSessions">The play sessions whose lessons' items every battery asks (1: only the first session's).</param>
    public CheckBank(IReadOnlyList<CheckItem> items, int coreSessions = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(coreSessions, 1);
        _items = items;
        CoreSessions = coreSessions;
    }

    /// <summary>The play sessions, from the first, whose lessons' items every battery asks: the file's <c>coreSessions</c> (3, the pilot's battery).</summary>
    public int CoreSessions { get; }

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public CheckItem this[int index] => _items[index];

    /// <inheritdoc />
    public IEnumerator<CheckItem> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The items a battery asks in a play session, in the file's order: those of no lesson, and those of a lesson that opens in a core session or in the play
    /// session or before it (<c>lesson.Session &lt;= max(playSession, CoreSessions)</c>). An item whose lesson the bundle lacks is asked (the validator names it).
    /// </summary>
    /// <param name="playSession">The play session the battery runs in (the pre- and post-check's own, the recall's after the load).</param>
    /// <param name="lessons">The lessons, for the session each opens in.</param>
    public IReadOnlyList<CheckItem> AskedIn(int playSession, LessonBook lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        var unlockedUpTo = Math.Max(playSession, CoreSessions);
        return _items.Where(item => item.Lesson is not { } id || lessons.Find(id) is not { } lesson || lesson.Session <= unlockedUpTo).ToList();
    }

    /// <summary>Reads the battery from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="locale">The support language, which holds the items' questions, options and situations (<c>check.&lt;id&gt;.*</c>).</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or an item is malformed or lacks its text in the locale.</exception>
    public static CheckBank Load(ContentBundle bundle, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span), locale);
    }

    /// <summary>The locale id of an item's support-language text: <c>check.&lt;id&gt;.&lt;field&gt;</c> (question, option.&lt;n&gt;, situation).</summary>
    /// <param name="id">The item's id.</param>
    /// <param name="field">The field.</param>
    public static string LocaleId(string id, string field) => "check." + id + "." + field;

    /// <summary>Builds the battery from the file's text and the support language.</summary>
    /// <param name="json">The contents of <c>game/checks.json</c>.</param>
    /// <param name="locale">The support language, which holds the items' questions, options and situations.</param>
    /// <exception cref="InvalidDataException">The text is malformed or an item does not hold together.</exception>
    public static CheckBank Parse(string json, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(locale);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"checks.json: schemaVersion must be {SchemaVersion}."));
            }

            var coreSessions = root.TryGetProperty("coreSessions", out var core) ? core.GetInt32() : 1;
            if (coreSessions < 1)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"checks.json: coreSessions is {coreSessions}; sessions start at 1."));
            }

            var items = new List<CheckItem>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("items").EnumerateArray())
            {
                var item = ParseItem(element, locale);
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

            return new CheckBank(items, coreSessions);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"checks.json is malformed: {ex.Message}", ex);
        }
    }

    private static CheckItem ParseItem(JsonElement element, LocaleText locale)
    {
        var id = element.GetProperty("id").GetString() ?? string.Empty;
        var phrase = element.GetProperty("phrase").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phrase))
        {
            throw new InvalidDataException($"checks.json: item '{id}' names no phrase.");
        }

        // Every item says which lesson it belongs to, null for none (VILLAGE_PLAN V07): an item that leaves it out was not looked at.
        if (!element.TryGetProperty("lesson", out var lessonElement) || lessonElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            throw new InvalidDataException($"checks.json: item '{id}' names no lesson (\"lesson\": a lesson's id, or null for an item of no lesson).");
        }

        var lesson = lessonElement.ValueKind == JsonValueKind.String ? lessonElement.GetString() : null;
        var kind = element.GetProperty("kind").GetString();
        switch (kind)
        {
            case "comprehension":
                var line = element.GetProperty("line").GetString() ?? string.Empty;
                // The options' labels are the locale's (check.<id>.option.<n>); the file keeps the icons, one entry per option.
                var options = element.GetProperty("options").EnumerateArray()
                    .Select((o, i) => new CheckOption(
                        locale.Find(LocaleId(id, "option." + (i + 1).ToString(CultureInfo.InvariantCulture))) ?? string.Empty,
                        o.TryGetProperty("icon", out var icon) ? icon.GetString() : null))
                    .ToList();
                var correct = element.GetProperty("correct").GetInt32();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(line) || options.Count != 3 || options.Any(static o => o.Label.Length == 0))
                {
                    throw new InvalidDataException($"checks.json: comprehension item '{id}' needs an id, a line, and three options labelled in the locale ({LocaleId(id, "option.<n>")}).");
                }

                if (correct < 0 || correct >= options.Count)
                {
                    throw new InvalidDataException($"checks.json: comprehension item '{id}' has its correct option at {correct}, outside 0 to {options.Count - 1}.");
                }

                return new CheckItem(
                    id,
                    CheckItemKind.Comprehension,
                    phrase,
                    locale.Find(LocaleId(id, "question")) ?? throw new InvalidDataException($"checks.json: comprehension item '{id}' has no question in the locale {locale.Code} ({LocaleId(id, "question")})."),
                    line,
                    element.GetProperty("spanish").GetString() ?? string.Empty,
                    options,
                    correct,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    lesson);
            case "production":
                var prompt = element.GetProperty("prompt").GetString() ?? string.Empty;
                var intent = element.GetProperty("intent").GetString() ?? string.Empty;
                var situation = locale.Find(LocaleId(id, "situation")) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(situation) || string.IsNullOrWhiteSpace(intent))
                {
                    throw new InvalidDataException($"checks.json: production item '{id}' needs an id, an intent and a situation in the locale ({LocaleId(id, "situation")}).");
                }

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    throw new InvalidDataException($"checks.json: production item '{id}' names no prompt.");
                }

                return new CheckItem(id, CheckItemKind.Production, phrase, string.Empty, string.Empty, string.Empty, [], -1, situation, prompt, intent, lesson);
            default:
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"checks.json: item '{id}' has the kind '{kind}', which is neither comprehension nor production."));
        }
    }
}
