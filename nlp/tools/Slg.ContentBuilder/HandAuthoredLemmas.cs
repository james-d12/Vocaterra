using System.Text;
using Slg.Content;
using Slg.Nlp.Lexicon;

namespace Slg.ContentBuilder;

/// <summary>The words of the hand-authored lexicon files an import defers to, with the names of those files.</summary>
/// <param name="FileNames">File names (without folders), in ordinal order.</param>
/// <param name="Lemmas">Every lemma the files define, NFC and lower case.</param>
/// <param name="FunctionWordForms">Every form of an article, pronoun, numeral, preposition, conjunction or adverb the files define, NFC and lower case.</param>
public sealed record HandAuthoredLemmas(IReadOnlyList<string> FileNames, IReadOnlySet<string> Lemmas, IReadOnlySet<string> FunctionWordForms)
{
    private static readonly HashSet<string> FunctionWordPartsOfSpeech = new(StringComparer.Ordinal)
    {
        LexiconSchema.Article,
        LexiconSchema.Pronoun,
        LexiconSchema.Numeral,
        LexiconSchema.Preposition,
        LexiconSchema.Conjunction,
        LexiconSchema.Adverb,
    };

    /// <summary>
    /// Reads the lemmas named by the value of <c>--hand-authored</c>: a comma-separated list of lexicon files, or a folder whose <c>*.json</c> files
    /// are all read except sidecars and files marked <c>wiktionary-import</c> (so the folder of a bundle can be named while an earlier import sits in it).
    /// </summary>
    /// <exception cref="FileNotFoundException">A named file or folder does not exist.</exception>
    /// <exception cref="FormatException">A file is not a valid lexicon.</exception>
    public static HandAuthoredLemmas Read(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var explicitFiles = new List<string>();
        var folderFiles = new List<string>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Directory.Exists(part))
            {
                folderFiles.AddRange(Directory.EnumerateFiles(part, "*.json")
                    .Where(static path => !LexiconSourceInfo.IsSidecarPath(ContentPaths.LexiconFolder + Path.GetFileName(path))));
            }
            else if (File.Exists(part))
            {
                explicitFiles.Add(part);
            }
            else
            {
                throw new FileNotFoundException($"Hand-authored lexicon not found: {part}", part);
            }
        }

        var names = new SortedSet<string>(StringComparer.Ordinal);
        var lemmas = new HashSet<string>(StringComparer.Ordinal);
        var functionForms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in explicitFiles.Concat(folderFiles))
        {
            var file = ReadLexicon(path);
            var isFolderFile = folderFiles.Contains(path);
            if (isFolderFile && string.Equals(file.GeneratedBy, KaikkiImporter.GeneratedBy, StringComparison.Ordinal))
            {
                continue;
            }

            names.Add(Path.GetFileName(path));
            foreach (var entry in file.Entries)
            {
                lemmas.Add(entry.Lemma.Normalize(NormalizationForm.FormC).ToLowerInvariant());
                if (FunctionWordPartsOfSpeech.Contains(entry.Pos))
                {
                    functionForms.Add(entry.Form.Normalize(NormalizationForm.FormC).ToLowerInvariant());
                }
            }
        }

        return new HandAuthoredLemmas([.. names], lemmas, functionForms);
    }

    private static LexiconFile ReadLexicon(string path)
    {
        try
        {
            return Lexicon.ReadFile(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or ArgumentException)
        {
            throw new FormatException($"{path}: {ex.Message}", ex);
        }
    }
}
