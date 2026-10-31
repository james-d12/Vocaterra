using System.Globalization;
using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The tests' own lessons and check items keep their support-language text inline, as one readable fixture (<c>"english": "..."</c>). The game's files
/// carry ids only since C03 (the text is the locale's), so a fixture is split before it is parsed: the text goes into locale strings under the ids the
/// game asks for, and the rest is the file the loader reads.
/// </summary>
internal static class LocaleFixture
{
    private static readonly (string Field, string Key)[] LessonFields = [("title", "title"), ("english", "text"), ("summary", "summary"), ("objective", "objective")];

    private static readonly (string Field, string Key)[] StepFields =
    [
        ("english", "text"), ("translation", "translation"), ("help", "help"), ("retry", "retry"), ("personRetry", "personRetry"), ("tenseRetry", "tenseRetry"),
        ("presentNote", "presentNote"), ("futureNote", "futureNote"), ("hud", "hud"),
    ];

    /// <summary>A lessons file with its text inline, split into the file the loader reads and the locale strings of its text.</summary>
    /// <param name="json">The fixture.</param>
    public static (string Json, Dictionary<string, string> Strings) SplitLessons(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var lesson in root["lessons"]!.AsArray().Select(static node => node!.AsObject()))
        {
            var id = lesson["id"]!.GetValue<string>();
            foreach (var (field, key) in LessonFields)
            {
                Move(lesson, field, LessonBook.LessonId(id, key), strings);
            }

            foreach (var step in lesson["steps"]!.AsArray().Select(static node => node!.AsObject()))
            {
                var stepId = step["id"]?.GetValue<string>() ?? string.Empty;
                foreach (var (field, key) in StepFields)
                {
                    Move(step, field, LessonBook.StepId(id, stepId, key), strings);
                }

                if (step["options"] is JsonArray options)
                {
                    for (var i = 0; i < options.Count; i++)
                    {
                        if (options[i]?.GetValue<string>() is { Length: > 0 } option)
                        {
                            strings[LessonBook.StepId(id, stepId, "option." + (i + 1).ToString(CultureInfo.InvariantCulture))] = option;
                        }
                    }

                    step.Remove("options");
                }
            }
        }

        return (root.ToJsonString(), strings);
    }

    /// <summary>
    /// <see cref="SplitLessons"/> for a fixture that may be malformed on purpose: false when it is not a lessons object the splitter can read, so the
    /// test hands the text to the loader as it is and the loader names the problem.
    /// </summary>
    public static bool TrySplitLessons(string json, out string file, out Dictionary<string, string> strings)
    {
        try
        {
            (file, strings) = SplitLessons(json);
            return true;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NullReferenceException or FormatException)
        {
            (file, strings) = (json, new Dictionary<string, string>(StringComparer.Ordinal));
            return false;
        }
    }

    /// <summary>A check battery with its text inline, split into the file the loader reads and the locale strings of its text.</summary>
    /// <param name="json">The fixture.</param>
    public static (string Json, Dictionary<string, string> Strings) SplitChecks(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in root["items"]!.AsArray().Select(static node => node!.AsObject()))
        {
            var id = item["id"]?.GetValue<string>() ?? string.Empty;
            Move(item, "question", CheckBank.LocaleId(id, "question"), strings);
            Move(item, "situation", CheckBank.LocaleId(id, "situation"), strings);
            if (item["options"] is JsonArray options)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] is JsonObject option)
                    {
                        Move(option, "label", CheckBank.LocaleId(id, "option." + (i + 1).ToString(CultureInfo.InvariantCulture)), strings);
                    }
                }
            }
        }

        return (root.ToJsonString(), strings);
    }

    private static void Move(JsonObject owner, string field, string id, Dictionary<string, string> strings)
    {
        if (owner[field]?.GetValue<string>() is { } text)
        {
            if (text.Length > 0)
            {
                strings[id] = text;
            }

            owner.Remove(field);
        }
    }
}
