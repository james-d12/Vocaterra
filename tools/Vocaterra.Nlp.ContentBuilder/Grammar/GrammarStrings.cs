using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// Collects the Spanish strings the grammar check reads: from a bundle (dialogue lines, prompts, answer sets, intent examples) and from a host's JSONL
/// file (one string per line, see <c>docs/nlp/api.md</c>). Dialogue lines with slots are rendered with the core's own <see cref="TemplateEngine"/> from a
/// fixed set of sampled fillings, so a template that produces an agreement error is found in the text it produces.
/// </summary>
internal static class GrammarStrings
{
    /// <summary>The fillings every slot template is rendered with: two nouns (masculine, feminine), two numbers (singular, plural) and the verbs.</summary>
    private const int FillingCount = 4;

    private static readonly string[] Masculine = ["libro"];
    private static readonly string[] Feminine = ["espada"];

    /// <summary>The strings of the bundle, in a fixed order: dialogue pools by id, prompts, answer sets, intent examples.</summary>
    public static List<GrammarItem> FromBundle(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var items = new List<GrammarItem>();
        foreach (var pool in bundle.Dialogue.Pools.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var variant in pool.Value.Variants)
            {
                AddTemplate(items, bundle.TemplateEngine, ContentPaths.Dialogue, variant.LineId, variant.Text);
            }
        }

        foreach (var prompt in bundle.Prompts.Values.Where(static prompt => prompt.Text is not null).OrderBy(static prompt => prompt.PromptId, StringComparer.Ordinal))
        {
            items.Add(new GrammarItem("prompt." + prompt.PromptId, ContentPaths.Prompts, prompt.Text!, GrammarKind.Npc, string.Empty, null));
        }

        foreach (var set in bundle.AnswerSets.Values.OrderBy(static set => set.AnswerSetId, StringComparer.Ordinal))
        {
            var path = ContentPaths.AnswerSetFolder + set.AnswerSetId + ".json";
            foreach (var answer in set.Answers)
            {
                items.Add(new GrammarItem(answer.AnswerId, path, answer.Canonical, GrammarKind.Learner, string.Empty, null));
                for (var i = 0; i < answer.Aliases.Count; i++)
                {
                    items.Add(new GrammarItem(answer.AnswerId + ".alias" + (i + 1), path, answer.Aliases[i], GrammarKind.Learner, string.Empty, null));
                }
            }
        }

        foreach (var intent in bundle.Intents.Intents)
        {
            for (var i = 0; i < intent.ExampleSpecs.Count; i++)
            {
                // Ids count examples from zero in file order (confirm.ex5 is the sixth example), as the research and the lint notes name them.
                var example = intent.ExampleSpecs[i];
                items.Add(new GrammarItem(intent.Id.Value + ".ex" + i, ContentPaths.Intents, example.Text, GrammarKind.Learner, string.Empty, example.ExpectedErrors ?? [], Example: true));
            }
        }

        return items;
    }

    /// <summary>
    /// Reads a host's JSONL strings: one object per line with <c>id</c>, <c>text</c> and <c>kind</c> (<c>npc</c>, <c>learner</c> or <c>template</c>), and an
    /// optional <c>errors</c> array of expected error tags. Blank lines are skipped; a duplicate id, an unknown kind or a bad line is an error.
    /// </summary>
    public static List<GrammarItem> FromJsonl(string path, TemplateEngine engine)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(engine);
        var name = Path.GetFileName(path);
        var items = new List<GrammarItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            var line = lines[lineNumber - 1];
            if (line.Trim().Length == 0)
            {
                continue;
            }

            var where = name + ":" + lineNumber;
            var entry = ParseLine(line, where);
            if (!seen.Add(entry.Id))
            {
                throw new GrammarInputException($"{where}: the id '{entry.Id}' is given twice.");
            }

            if (entry.Kind == "template")
            {
                AddTemplate(items, engine, name, entry.Id, entry.Text, entry.Errors);
                continue;
            }

            var kind = entry.Kind == "learner" ? GrammarKind.Learner : GrammarKind.Npc;
            // A line with an errors array is a learner example (an empty array means "no errors expected"); one without it is a plain string.
            items.Add(new GrammarItem(entry.Id, name, entry.Text, kind, string.Empty, entry.Errors, Example: entry.Errors is not null));
        }

        return items;
    }

    /// <summary>
    /// Capitalises the first letter of a learner-side string, skipping spaces, opening question and exclamation marks and opening quotes, so
    /// LanguageTool's sentence-start rule does not fire on every lower-case answer. Other text is returned unchanged.
    /// </summary>
    public static string Capitalise(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c) || c is '¿' or '¡' or '"' or '“' or '«' or '(')
            {
                continue;
            }

            if (!char.IsLetter(c) || char.IsUpper(c))
            {
                return text;
            }

            return string.Concat(text.AsSpan(0, i), char.ToUpperInvariant(c).ToString(), text.AsSpan(i + 1));
        }

        return text;
    }

    /// <summary>
    /// The fillings a template is rendered with, as (slots, description) pairs in a fixed order. A template with no slot has one filling (no
    /// description). Otherwise filling k uses the masculine noun for even k and the feminine for odd k, the singular for k 0 and 1 and the plural for
    /// 2 and 3, and the verb tener for even k and ser for odd k; the reserved feedback slots get the sample texts the loader uses.
    /// </summary>
    public static IReadOnlyList<(SlotMap Slots, string Description)> Fillings(DialogueTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var names = TemplateEngine.ReferencedSlots(template);
        if (names.Count == 0)
        {
            return [(SlotMap.Empty, string.Empty)];
        }

        var kinds = TemplateEngine.SlotKinds(template);
        var result = new List<(SlotMap, string)>(FillingCount);
        for (var k = 0; k < FillingCount; k++)
        {
            var pairs = names.Select(name => new KeyValuePair<string, string>(name, ValueFor(name, kinds, k))).ToList();
            var description = string.Join(" ", pairs.Select(static pair => pair.Key + "=" + pair.Value));
            result.Add((SlotMap.From(pairs), description));
        }

        return result;
    }

    private static string ValueFor(string name, IReadOnlyDictionary<string, TemplateSlotKind> kinds, int k)
    {
        switch (name)
        {
            case "tag":
                return "gender.agree";
            case "expected":
                return "tengo";
            case "actual":
                return "tiene";
            case "recast":
                return "yo tengo dos espadas";
        }

        var kind = kinds.GetValueOrDefault(name, TemplateSlotKind.Text);
        if (kind == TemplateSlotKind.Number || name is "qty" or "count" or "number" or "amount" or "price")
        {
            return k < 2 ? "1" : "2";
        }

        if (kind == TemplateSlotKind.Verb || name is "verb" or "action")
        {
            return k % 2 == 0 ? "tener" : "ser";
        }

        return k % 2 == 0 ? Masculine[0] : Feminine[0];
    }

    private static void AddTemplate(List<GrammarItem> items, TemplateEngine engine, string path, string id, string text, IReadOnlyList<string>? errors = null)
    {
        var template = new DialogueTemplate(id, text);
        var rendered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (slots, description) in Fillings(template))
        {
            string output;
            try
            {
                output = engine.Render(template, slots);
            }
            catch (TemplateException ex)
            {
                throw new GrammarInputException($"{id}: the template does not render ({ex.Message})" + (description.Length == 0 ? string.Empty : $" with {description}"));
            }

            if (rendered.Add(output))
            {
                items.Add(new GrammarItem(id, path, output, GrammarKind.Npc, description, errors));
            }
        }
    }

    private static JsonLine ParseLine(string line, string where)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            throw new GrammarInputException($"{where}: not JSON ({ex.Message})");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new GrammarInputException($"{where}: each line must be a JSON object.");
            }

            var id = RequiredString(root, "id", where);
            var text = RequiredString(root, "text", where);
            var kind = RequiredString(root, "kind", where);
            if (kind is not ("npc" or "learner" or "template"))
            {
                throw new GrammarInputException($"{where}: kind must be npc, learner or template, not '{kind}'.");
            }

            List<string>? errors = null;
            if (root.TryGetProperty("errors", out var errorsElement))
            {
                if (errorsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new GrammarInputException($"{where}: errors must be an array of tags.");
                }

                errors = [];
                foreach (var tag in errorsElement.EnumerateArray())
                {
                    if (tag.ValueKind != JsonValueKind.String || tag.GetString() is not { Length: > 0 } value)
                    {
                        throw new GrammarInputException($"{where}: every error tag must be a non-empty string.");
                    }

                    errors.Add(value);
                }
            }

            return new JsonLine(id, text, kind, errors);
        }
    }

    private static string RequiredString(JsonElement root, string name, string where)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } text)
        {
            throw new GrammarInputException($"{where}: '{name}' must be a non-empty string.");
        }

        return text;
    }

    private sealed record JsonLine(string Id, string Text, string Kind, IReadOnlyList<string>? Errors);
}
