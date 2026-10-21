namespace Vocaterra.Nlp.Content;

/// <summary>The fixed file and folder names of a content bundle (all paths use '/' separators, relative to the bundle directory).</summary>
public static class ContentPaths
{
    /// <summary>The manifest file; it lists every other file and is never listed itself.</summary>
    public const string Manifest = "manifest.json";

    /// <summary>The provenance file.</summary>
    public const string Provenance = "provenance.json";

    /// <summary>The attribution and license notice.</summary>
    public const string Attribution = "ATTRIBUTION.md";

    /// <summary>Folder of lexicon files (every <c>*.json</c> file is merged into one lexicon).</summary>
    public const string LexiconFolder = "lexicon/";

    /// <summary>Folder of answer sets (one <c>&lt;answerSetId&gt;.json</c> per set).</summary>
    public const string AnswerSetFolder = "answer-sets/";

    /// <summary>The intent catalog.</summary>
    public const string Intents = "intents/intents.json";

    /// <summary>The prompt list.</summary>
    public const string Prompts = "prompts/prompts.json";

    /// <summary>Folder of agent definitions (one <c>&lt;agentId&gt;.json</c> per agent).</summary>
    public const string AgentFolder = "agents/";

    /// <summary>The dialogue catalog.</summary>
    public const string Dialogue = "dialogue/dialogue.json";

    /// <summary>The feedback policy.</summary>
    public const string FeedbackPolicy = "feedback/feedback-policy.json";

    /// <summary>The evaluation configuration.</summary>
    public const string EvaluationConfig = "evaluation/evaluation-config.json";

    /// <summary>The effect table.</summary>
    public const string Effects = "effects/effects.json";

    /// <summary>Folder of CEFR lemma lists (<c>a1.txt</c>, <c>a2.txt</c>: one lemma per line).</summary>
    public const string CefrFolder = "cefr/";

    /// <summary>
    /// True when the loader parses or validates the file, so that it is content the loader owns. Every other file listed in the manifest is a
    /// host-owned file (see <see cref="ContentBundle.TryReadHostFile"/>).
    /// </summary>
    internal static bool IsLoaderOwned(string path)
    {
        return path is Provenance or Attribution or Intents or Prompts or Dialogue or FeedbackPolicy or EvaluationConfig or Effects
            || (path.StartsWith(LexiconFolder, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            || (path.StartsWith(AnswerSetFolder, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            || (path.StartsWith(AgentFolder, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            || (path.StartsWith(CefrFolder, StringComparison.Ordinal) && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when the path has the form a host file path must have: relative, with '/' separators only, no empty, '.' or '..' segment, and no drive
    /// letter or leading slash. It does not check that the file exists.
    /// </summary>
    internal static bool IsWellFormedHostPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path[0] == '/')
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        return true;
    }
}
