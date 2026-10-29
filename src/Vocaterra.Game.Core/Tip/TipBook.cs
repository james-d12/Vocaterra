using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tip;

/// <summary>
/// The first-time tips and the controls help (S16): when each tip comes up and the order of the controls are game data (<c>game/tips.json</c>,
/// <c>game/controls.json</c>); what they say is the support language's (<c>tip.&lt;id&gt;</c>, <c>control.&lt;id&gt;.keys</c> and <c>.text</c>; CONTENT_PLAN C03).
/// </summary>
public static class TipBook
{
    /// <summary>The tips' file inside the bundle folder.</summary>
    public const string TipsPath = "game/tips.json";

    /// <summary>The controls' file inside the bundle folder.</summary>
    public const string ControlsPath = "game/controls.json";

    /// <summary>The longest a tip may wait after its trigger, in seconds.</summary>
    public const int MaxDelaySeconds = 300;

    /// <summary>The triggers a tip may name: <c>start</c>, <c>aimed_npc</c> and every world event.</summary>
    public static IReadOnlyList<string> TipTriggers { get; } = ["start", "aimed_npc", .. WorldEvents.All];

    /// <summary>Reads the tips from the bundle's verified host file and their text from the locale.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="locale">The support language.</param>
    /// <exception cref="InvalidDataException">The file is missing or malformed, a trigger is unknown, or a tip's text is not in the locale.</exception>
    public static IReadOnlyList<TipSpec> LoadTips(ContentBundle bundle, LocaleText locale) => ParseTips(GameData.HostText(bundle, TipsPath), locale);

    /// <summary>Reads the controls help from the bundle's verified host file and its text from the locale.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="locale">The support language.</param>
    /// <exception cref="InvalidDataException">The file is missing or malformed, or a line's text is not in the locale.</exception>
    public static IReadOnlyList<ControlHelp> LoadControls(ContentBundle bundle, LocaleText locale) => ParseControls(GameData.HostText(bundle, ControlsPath), locale);

    /// <summary>Builds the tips from the file's text: <c>{ "schemaVersion": 1, "tips": [ { "id", "when", "delay"? } ] }</c>.</summary>
    /// <param name="json">The contents of <c>game/tips.json</c>.</param>
    /// <param name="locale">The support language.</param>
    /// <exception cref="InvalidDataException">The text is malformed, a trigger is unknown, a delay is out of range, an id is given twice, or a tip's text is not in the locale.</exception>
    public static IReadOnlyList<TipSpec> ParseTips(string json, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(locale);
        return Read(TipsPath, json, "tips", element =>
        {
            var id = Text(TipsPath, element, "id");
            var when = Text(TipsPath, element, "when");
            var delay = element.TryGetProperty("delay", out var delayElement) ? delayElement.GetInt32() : 0;
            if (delay is < 0 or > MaxDelaySeconds)
            {
                throw new InvalidDataException($"{TipsPath}: the tip '{id}' waits {delay.ToString(CultureInfo.InvariantCulture)} seconds; it must be 0 to {MaxDelaySeconds.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (!TipTriggers.Contains(when, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{TipsPath}: the tip '{id}' has the trigger '{when}', which is neither 'start', 'aimed_npc' nor a world event.");
            }

            return new TipSpec(id, when, Required(locale, TipsPath, "tip." + id), delay);
        });
    }

    /// <summary>Builds the controls help from the file's text: <c>{ "schemaVersion": 1, "controls": [ { "id" } ] }</c>.</summary>
    /// <param name="json">The contents of <c>game/controls.json</c>.</param>
    /// <param name="locale">The support language.</param>
    /// <exception cref="InvalidDataException">The text is malformed, an id is given twice, or a line's text is not in the locale.</exception>
    public static IReadOnlyList<ControlHelp> ParseControls(string json, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(locale);
        return Read(ControlsPath, json, "controls", element =>
        {
            var id = Text(ControlsPath, element, "id");
            return new ControlHelp(id, Required(locale, ControlsPath, "control." + id + ".keys"), Required(locale, ControlsPath, "control." + id + ".text"));
        });
    }

    private static List<T> Read<T>(string path, string json, string section, Func<JsonElement, T> item)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{path} must have schemaVersion 1.");
            }

            var items = new List<T>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty(section).EnumerateArray())
            {
                if (!ids.Add(Text(path, element, "id")))
                {
                    throw new InvalidDataException($"{path}: {section} has the id '{Text(path, element, "id")}' twice.");
                }

                items.Add(item(element));
            }

            return items;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{path} is malformed: {ex.Message}", ex);
        }
    }

    private static string Required(LocaleText locale, string path, string id) =>
        locale.Find(id) ?? throw new InvalidDataException($"{path}: '{id}' is not in the locale {locale.Code}.");

    private static string Text(string path, JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{path}: the text '{name}' is missing or empty.");
}
