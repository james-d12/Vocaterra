// The strings of the game's Spanish, as the grammar check reads them (docs/nlp/api.md, "Grammar check the strings"). The host owns this list
// because the game owns its files: the bundle's dialogue, prompts, answer sets and intents (read through the core's loader), the phrase sheet
// (content/PHRASES.md, with the learner-error examples), the Spanish side of checks.json, and the speech index. The output is
// one JSON object per line: id, text, kind (npc, learner or template) and, for an intent example or a learner-error example, errors.
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Host;

/// <summary>One string of the game's Spanish, as the grammar check's strings file names it.</summary>
/// <param name="Id">A stable id: the source file and the key inside it (for example <c>dialogue:guide.lesson.hello.1</c>).</param>
/// <param name="Text">The Spanish, in NFC.</param>
/// <param name="Kind">npc (authored speech), learner (what the learner says) or template (a line with <c>{slot}</c>s, rendered by the core).</param>
/// <param name="Errors">The expected error tags of a learner-error example or of an intent example (empty for "no error expected"); null for a plain string.</param>
public sealed record GrammarStringLine(string Id, string Text, string Kind, IReadOnlyList<string>? Errors);

/// <summary>Extracts the game's Spanish strings and writes them as the JSON Lines the core's <c>grammar --strings</c> reads.</summary>
public static class GrammarStringsExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex ErrorCell = new(@"^(?<text>.+?)\s*\((?<tags>[^()]+)\)$", RegexOptions.CultureInvariant);

    /// <summary>The learner-side kinds: a learner's line is capitalised by the core, an NPC's is checked as written.</summary>
    public const string Npc = "npc";

    /// <summary>The learner kind.</summary>
    public const string Learner = "learner";

    /// <summary>The template kind: a line with slots, rendered by the core's template engine.</summary>
    public const string Template = "template";

    /// <summary>
    /// Every Spanish string of the bundle in <paramref name="bundleDirectory"/> and of its game files (the phrase sheet and the speech index sit beside
    /// the bundle, in <c>content/</c>). The order is fixed: dialogue, prompts, answer sets, intents, phrases, checks.json, words.json, speech. The support language is
    /// not checked (the owner's decision), so the locale bundle is not read; before C03 the Spanish keys of english.json's glosses were listed too, and they
    /// repeated the dialogue's and the prompts' texts.
    /// A duplicate id is an error (the file would be refused by the core).
    /// </summary>
    public static IReadOnlyList<GrammarStringLine> Extract(GameData data, string bundleDirectory)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(bundleDirectory);
        var bundle = data.Bundle;
        var contentDirectory = Directory.GetParent(Path.GetFullPath(bundleDirectory))?.FullName
            ?? throw new InvalidOperationException($"{bundleDirectory} has no parent folder for the content sheets.");

        var lines = new List<GrammarStringLine>();
        AddDialogue(lines, bundle);
        AddPrompts(lines, bundle);
        AddAnswerSets(lines, bundle);
        AddIntents(lines, bundle);
        AddPhraseSheet(lines, Path.Combine(contentDirectory, "PHRASES.md"));
        AddChecks(lines, data);
        AddWords(lines, data);
        AddNpcs(lines, data);
        AddSpeechIndex(lines, Path.Combine(contentDirectory, "speech-index.json"));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            if (!seen.Add(line.Id))
            {
                throw new InvalidOperationException($"grammar strings: the id '{line.Id}' is given twice.");
            }
        }

        return lines;
    }

    /// <summary>The JSON Lines text of the strings: one object per line, in the order given, with a final newline.</summary>
    public static string ToJsonl(IEnumerable<GrammarStringLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.Append(JsonSerializer.Serialize(new JsonRecord(line.Id, line.Text, line.Kind, line.Errors), JsonOptions));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Extracts the strings and writes them to <paramref name="outputPath"/> (UTF-8, no byte order mark). Returns the lines written.</summary>
    public static IReadOnlyList<GrammarStringLine> Write(GameData data, string bundleDirectory, string outputPath)
    {
        var lines = Extract(data, bundleDirectory);
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, ToJsonl(lines), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return lines;
    }

    private static void AddDialogue(List<GrammarStringLine> lines, ContentBundle bundle)
    {
        foreach (var pool in bundle.Dialogue.Pools.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var variant in pool.Value.Variants)
            {
                lines.Add(Line("dialogue:" + variant.LineId, variant.Text, Npc));
            }
        }
    }

    private static void AddPrompts(List<GrammarStringLine> lines, ContentBundle bundle)
    {
        foreach (var prompt in bundle.Prompts.Values.Where(static prompt => prompt.Text is not null).OrderBy(static prompt => prompt.PromptId, StringComparer.Ordinal))
        {
            lines.Add(Line("prompts:" + prompt.PromptId, prompt.Text!, Npc));
        }
    }

    private static void AddAnswerSets(List<GrammarStringLine> lines, ContentBundle bundle)
    {
        foreach (var set in bundle.AnswerSets.Values.OrderBy(static set => set.AnswerSetId, StringComparer.Ordinal))
        {
            var file = "answer-sets/" + set.AnswerSetId;
            foreach (var answer in set.Answers)
            {
                lines.Add(Line(file + ":" + answer.AnswerId, answer.Canonical, Learner));
                for (var i = 0; i < answer.Aliases.Count; i++)
                {
                    lines.Add(Line(file + ":" + answer.AnswerId + ".alias" + (i + 1), answer.Aliases[i], Learner));
                }
            }
        }
    }

    private static void AddIntents(List<GrammarStringLine> lines, ContentBundle bundle)
    {
        foreach (var intent in bundle.Intents.Intents)
        {
            for (var i = 0; i < intent.ExampleSpecs.Count; i++)
            {
                // Examples count from zero in file order (confirm.ex5 is the sixth), as the research and the core's grammar notes name them. Every
                // example is an oracle example: an example with no tags is one that expects no error.
                var example = intent.ExampleSpecs[i];
                lines.Add(Line("intents:" + intent.Id.Value + ".ex" + i, example.Text, Learner, example.ExpectedErrors ?? []));
            }
        }
    }

    /// <summary>
    /// The phrase sheet: for each row, the phrase (learner or NPC, by the row's <c>says</c>), each accepted variant (learner) and the learner-error
    /// example with its tags. The card, the situation and the reviewer columns are not Spanish to check.
    /// </summary>
    private static void AddPhraseSheet(List<GrammarStringLine> lines, string sheetPath)
    {
        foreach (var row in ReadPhraseRows(sheetPath))
        {
            var kind = row.Says is "hears" or "does" ? Npc : Learner; // a does row is an NPC's instruction (W05)
            lines.Add(Line("PHRASES.md:" + row.Id + ".text", row.Spanish, kind));
            for (var i = 0; i < row.Variants.Count; i++)
            {
                lines.Add(Line("PHRASES.md:" + row.Id + ".variant" + (i + 1), row.Variants[i], Learner));
            }

            if (row.LearnerError is { } error)
            {
                var match = ErrorCell.Match(error);
                if (!match.Success)
                {
                    throw new InvalidDataException($"PHRASES.md, row {row.Id}: the learner error '{error}' is not 'text (tag, tag)'.");
                }

                var tags = match.Groups["tags"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                lines.Add(Line("PHRASES.md:" + row.Id + ".error", match.Groups["text"].Value, Learner, tags));
            }
        }
    }

    private static void AddChecks(List<GrammarStringLine> lines, GameData data)
    {
        // Only a comprehension item carries Spanish (the line the NPC says); a production item's Spanish is its phrase's, which the sheet holds.
        foreach (var item in data.Checks.Where(static item => item.Kind == CheckItemKind.Comprehension))
        {
            lines.Add(Line("checks.json:" + item.Id, item.Spanish, Npc));
        }
    }

    // The target-language words the game itself uses (game/words.json, content-plan C05): the guide's directions, the buttons, the headings, the pronouns
    // and the rest, which were constants in the code and so went unchecked. The letters of the typing help are not words, so they are left out.
    private static void AddWords(List<GrammarStringLine> lines, GameData data)
    {
        foreach (var (path, text) in data.Words.All())
        {
            lines.Add(Line("words.json:" + path, text, Npc));
        }
    }

    // The NPCs' labels and names (game/npcs/<id>.json, C08), which were constants in the code, and their key words (schema 2, V01: they were words.json's).
    private static void AddNpcs(List<GrammarStringLine> lines, GameData data)
    {
        foreach (var npc in data.Npcs.All)
        {
            lines.Add(Line("npcs/" + npc.Id + ".json:label", npc.Label, Npc));
            if (npc.Name is { } name)
            {
                lines.Add(Line("npcs/" + npc.Id + ".json:name", name, Npc));
            }

            for (var i = 0; i < npc.KeyWords.Count; i++)
            {
                lines.Add(Line("npcs/" + npc.Id + ".json:keyWords[" + i.ToString(CultureInfo.InvariantCulture) + "]", npc.KeyWords[i], Npc));
            }
        }
    }

    /// <summary>
    /// The speech index holds the text of every pre-rendered line (speech/<id>.wav). A text already extracted from the bundle or the sheets is not
    /// repeated; a speech text they do not cover is added once, under its first file name.
    /// </summary>
    private static void AddSpeechIndex(List<GrammarStringLine> lines, string indexPath)
    {
        var known = lines.Select(static line => line.Text).ToHashSet(StringComparer.Ordinal);
        var document = ReadJson(indexPath);
        foreach (var file in document.RootElement.GetProperty("Files").EnumerateObject().OrderBy(static file => file.Name, StringComparer.Ordinal))
        {
            var text = file.Value.GetProperty("Text").GetString()!.Normalize(NormalizationForm.FormC);
            if (known.Add(text))
            {
                var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file.Name));
                lines.Add(Line("speech:" + name, text, Npc));
            }
        }
    }

    private static GrammarStringLine Line(string id, string text, string kind, IReadOnlyList<string>? errors = null)
    {
        var nfc = text.Normalize(NormalizationForm.FormC);
        var resolvedKind = nfc.Contains('{', StringComparison.Ordinal) ? Template : kind;
        return new GrammarStringLine(id, nfc, resolvedKind, errors);
    }

    private static JsonDocument ReadJson(string path)
    {
        return JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private static IReadOnlyList<PhraseSheetRow> ReadPhraseRows(string sheetPath)
    {
        var rows = new List<PhraseSheetRow>();
        var inTable = false;
        foreach (var line in File.ReadAllLines(sheetPath, Encoding.UTF8))
        {
            if (!line.StartsWith('|'))
            {
                if (inTable && rows.Count > 0)
                {
                    break;
                }

                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(static c => c.Trim()).ToArray();
            if (cells[0] == "id")
            {
                inTable = true;
                continue;
            }

            if (!inTable || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            if (cells.Length != 13)
            {
                throw new InvalidDataException($"PHRASES.md: a row of {cells.Length} cells, not 13: {cells[0]}.");
            }

            rows.Add(new PhraseSheetRow(
                cells[0],
                cells[1],
                cells[2],
                Variants(cells[6]),
                cells[7] == "-" ? null : cells[7]));
        }

        return rows;
    }

    private static IReadOnlyList<string> Variants(string cell)
    {
        return cell == "-" ? [] : cell.Split(" ; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed record PhraseSheetRow(string Id, string Says, string Spanish, IReadOnlyList<string> Variants, string? LearnerError);

    private sealed record JsonRecord(string Id, string Text, string Kind, IReadOnlyList<string>? Errors);
}
