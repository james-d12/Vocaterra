using Slg.Core.Prompts;
using Slg.Decision.Feedback;
using Slg.Decision.Npc;
using Slg.Evaluation;
using Slg.Nlp.Intent;
using Slg.Nlp.Matching;
using Slg.Nlp.Morphology;
using Slg.Response.Effects;
using Slg.Response.Templates;
using NlpLexicon = Slg.Nlp.Lexicon.Lexicon;

namespace Slg.Content;

/// <summary>A loaded, hash-verified and cross-checked content bundle exposing every catalog as a typed object; produced by <see cref="ContentLoader"/>.</summary>
public sealed class ContentBundle
{
    internal ContentBundle(
        ContentManifest manifest,
        ContentProvenance provenance,
        NlpLexicon lexicon,
        IReadOnlyDictionary<string, AnswerSet> answerSets,
        IntentCatalog intents,
        IReadOnlyDictionary<string, PromptSpec> prompts,
        IReadOnlyDictionary<string, NpcStateMachine> npcs,
        DialogueCatalog dialogue,
        FeedbackPolicy feedbackPolicy,
        EvaluationConfig evaluationConfig,
        EffectTable effects,
        IReadOnlyDictionary<string, IReadOnlySet<string>> cefr)
    {
        Manifest = manifest;
        Provenance = provenance;
        Lexicon = lexicon;
        Morphology = new MorphologyGenerator(lexicon);
        TemplateEngine = new TemplateEngine(lexicon, Morphology);
        AnswerSets = answerSets;
        Intents = intents;
        Prompts = prompts;
        Npcs = npcs;
        Dialogue = dialogue;
        FeedbackPolicy = feedbackPolicy;
        EvaluationConfig = evaluationConfig;
        Effects = effects;
        Cefr = cefr;
    }

    /// <summary>The manifest the bundle was verified against.</summary>
    public ContentManifest Manifest { get; }

    /// <summary>Who generated and approved each file.</summary>
    public ContentProvenance Provenance { get; }

    /// <summary>The bundle hash from the manifest; goes into every <c>VersionStamp.ContentHash</c>.</summary>
    public string ContentHash => Manifest.BundleHash;

    /// <summary>All <c>lexicon/*.json</c> files merged into one lexicon.</summary>
    public NlpLexicon Lexicon { get; }

    /// <summary>A morphology generator over <see cref="Lexicon"/>.</summary>
    public MorphologyGenerator Morphology { get; }

    /// <summary>A template engine over <see cref="Lexicon"/> and <see cref="Morphology"/>; use it for the response planner so templates inflect with bundle words.</summary>
    public TemplateEngine TemplateEngine { get; }

    /// <summary>The answer sets by id, enumerating in ordinal id order.</summary>
    public IReadOnlyDictionary<string, AnswerSet> AnswerSets { get; }

    /// <summary>The intent catalog.</summary>
    public IntentCatalog Intents { get; }

    /// <summary>The prompts by id, enumerating in ordinal id order.</summary>
    public IReadOnlyDictionary<string, PromptSpec> Prompts { get; }

    /// <summary>The NPC state machines by NPC id, enumerating in ordinal id order.</summary>
    public IReadOnlyDictionary<string, NpcStateMachine> Npcs { get; }

    /// <summary>The dialogue catalog (dialogue intent to variant pool).</summary>
    public DialogueCatalog Dialogue { get; }

    /// <summary>The feedback rule table.</summary>
    public FeedbackPolicy FeedbackPolicy { get; }

    /// <summary>The evaluation weights and thresholds per difficulty.</summary>
    public EvaluationConfig EvaluationConfig { get; }

    /// <summary>The game-effect table.</summary>
    public EffectTable Effects { get; }

    /// <summary>The CEFR lemma lists by lower-case level (<c>a1</c>, <c>a2</c>); each set enumerates in ordinal order and lists only that level's own lemmas.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Cefr { get; }
}
