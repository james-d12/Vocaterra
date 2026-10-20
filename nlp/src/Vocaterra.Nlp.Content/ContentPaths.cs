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

    /// <summary>Folder of NPC definitions (one <c>&lt;npcId&gt;.json</c> per NPC).</summary>
    public const string NpcFolder = "npcs/";

    /// <summary>The dialogue catalog.</summary>
    public const string Dialogue = "dialogue/dialogue.json";

    /// <summary>The feedback policy.</summary>
    public const string FeedbackPolicy = "feedback/feedback-policy.json";

    /// <summary>The evaluation configuration.</summary>
    public const string EvaluationConfig = "evaluation/evaluation-config.json";

    /// <summary>The game-effect table.</summary>
    public const string Effects = "effects/effects.json";

    /// <summary>Folder of CEFR lemma lists (<c>a1.txt</c>, <c>a2.txt</c>: one lemma per line).</summary>
    public const string CefrFolder = "cefr/";
}
