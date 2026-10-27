using System.Text;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// The three-word bundle of the toy language, <c>xx</c>: a lexicon of three words (<c>ka</c>, <c>mo</c>, <c>ti</c>), two intents, one prompt, an answer
/// set, a dialogue pool set and an effect table. It is built in memory, written to a temporary directory at test time, hashed with the manifest's
/// <c>language</c> field, and loaded through <see cref="ContentLoader"/> with a registry that holds <see cref="XxLanguagePack"/> and no Spanish.
/// Nothing is stored under a <c>Data/</c> folder of any test project.
/// </summary>
public static class XxBundle
{
    /// <summary>The bundle id written to the manifest.</summary>
    public const string BundleId = "xx-toy";

    /// <summary>The prompt the oracles run with.</summary>
    public const string PromptId = "xx-open";

    /// <summary>The registry the toy bundle is loaded with: the toy pack only, so <c>es</c> is not registered.</summary>
    public static LanguagePacks Registry { get; } = LanguagePacks.Of(XxLanguagePack.Instance);

    /// <summary>The bundle's files as relative path to text, in path order.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["ATTRIBUTION.md"] = "# Attribution\n\nThe toy language xx is hand-written for the fuzz tests of Vocaterra. It has no source material.\n",
        ["agents/guide.json"] = """
            {
              "agentId": "guide",
              "initialState": "idle",
              "variantPools": [ { "id": "xx_take", "count": 1 }, { "id": "not_understood", "count": 1 } ],
              "states": [
                {
                  "id": "idle",
                  "transitions": [
                    { "on": { "intent": "take", "communicated": true }, "to": "idle", "actions": [ { "dialogueIntent": "xx_take", "variantPoolId": "xx_take" } ] },
                    { "on": { "intent": "greet", "communicated": true }, "to": "idle", "actions": [ { "dialogueIntent": "xx_take", "variantPoolId": "xx_take" } ] },
                    { "on": { "communicated": false }, "to": "idle", "actions": [ { "dialogueIntent": "not_understood", "variantPoolId": "not_understood" } ] }
                  ]
                }
              ]
            }
            """,
        ["answer-sets/xx-words.json"] = """
            {
              "answerSetId": "xx-words",
              "answers": [
                { "answerId": "ka", "canonical": "ka", "aliases": ["ka"] },
                { "answerId": "mo-ka", "canonical": "mo ka", "aliases": ["mo ka", "mo"] },
                { "answerId": "ti", "canonical": "ti", "aliases": ["ti"] }
              ]
            }
            """,
        ["dialogue/dialogue.json"] = """
            {
              "pools": {
                "feedback.explicit_correction": { "poolId": "feedback.explicit_correction", "variants": [ { "lineId": "feedback.explicit_correction.1", "text": "Say {expected}, not {actual}." } ] },
                "feedback.hint": { "poolId": "feedback.hint", "variants": [ { "lineId": "feedback.hint.1", "text": "Close. Try again." } ] },
                "not_understood": { "poolId": "not_understood", "variants": [ { "lineId": "not_understood.1", "text": "I did not get that." } ] },
                "xx_take": { "poolId": "xx_take", "variants": [ { "lineId": "xx_take.1", "text": "Ka." } ] }
              }
            }
            """,
        ["effects/effects.json"] = """
            {
              "effects": []
            }
            """,
        ["evaluation/evaluation-config.json"] = """
            {
              "beginner": {
                "errorWeights": { "vocab.unknown": 0.4, "vocab.wrong": 0.6, "xx.word_order": 0.2 },
                "communicatedConfidenceThreshold": 0.6,
                "correctnessThreshold": 0.7,
                "pronunciationThreshold": 0.5,
                "epsilon": 0.02,
                "unknownTagWeight": 0.3
              },
              "intermediate": {
                "errorWeights": { "vocab.unknown": 0.4, "vocab.wrong": 0.6, "xx.word_order": 0.2 },
                "communicatedConfidenceThreshold": 0.6,
                "correctnessThreshold": 0.8,
                "pronunciationThreshold": 0.6,
                "epsilon": 0.02,
                "unknownTagWeight": 0.3
              },
              "advanced": {
                "errorWeights": { "vocab.unknown": 0.4, "vocab.wrong": 0.6, "xx.word_order": 0.2 },
                "communicatedConfidenceThreshold": 0.6,
                "correctnessThreshold": 0.9,
                "pronunciationThreshold": 0.7,
                "epsilon": 0.02,
                "unknownTagWeight": 0.3
              }
            }
            """,
        ["feedback/feedback-policy.json"] = """
            {
              "rules": [
                { "tagPattern": "*", "minRepeat": 1, "maxRepeat": 1, "difficulty": null, "type": "Recast" },
                { "tagPattern": "*", "minRepeat": 2, "maxRepeat": 2147483647, "difficulty": null, "type": "Hint" }
              ],
              "notCommunicatedType": "SlowRepeat"
            }
            """,
        ["intents/intents.json"] = """
            {
              "schemaVersion": 1,
              "intents": [
                {
                  "intentId": "take",
                  "requiredSlots": [],
                  "slotGrammars": [ "mo [NOUN]", "ka" ],
                  "keywordRules": [ { "lemmas": ["ka"], "requiresSlots": [] } ],
                  "examples": [ "mo ka", "ka" ]
                },
                {
                  "intentId": "greet",
                  "requiredSlots": [],
                  "slotGrammars": [ "ti" ],
                  "keywordRules": [ { "lemmas": ["ti"], "requiresSlots": [] } ],
                  "examples": [ "ti" ]
                }
              ]
            }
            """,
        ["lexicon/words.json"] = """
            {
              "schemaVersion": 1,
              "generated_by": "hand-authored",
              "entries": [
                { "form": "ka", "lemma": "ka", "pos": "noun", "features": {} },
                { "form": "mo", "lemma": "mo", "pos": "verb", "features": {} },
                { "form": "ti", "lemma": "ti", "pos": "noun", "features": {} }
              ]
            }
            """,
        ["prompts/prompts.json"] = """
            [
              {
                "promptId": "xx-open",
                "mode": "Open",
                "difficulty": "Beginner",
                "answerSetId": "xx-words",
                "allowedIntentIds": ["take", "greet"],
                "agentId": "guide",
                "slotGrammarIds": [],
                "text": "Ka?"
              },
              {
                "promptId": "xx-choose",
                "mode": "Constrained",
                "difficulty": "Intermediate",
                "answerSetId": "xx-words",
                "allowedIntentIds": ["take"],
                "agentId": "guide",
                "slotGrammarIds": [],
                "text": "Ka or mo ka?"
              }
            ]
            """,
    };

    /// <summary>The provenance items of the files, the same for every file (hand-authored, unapproved).</summary>
    private static string Provenance()
    {
        var items = Files.Keys.Select(path => $"{{ \"path\": \"{path}\", \"generatedBy\": \"hand-authored\", \"approvedBy\": null, \"approvedOn\": null }}");
        return "{\n  \"items\": [\n    " + string.Join(",\n    ", items) + "\n  ]\n}\n";
    }

    /// <summary>
    /// Writes the bundle into <paramref name="directory"/> (created if needed): the files, the provenance and a manifest that names the language
    /// <c>xx</c>, so its bundle hash folds the language in.
    /// </summary>
    public static void WriteTo(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var all = new SortedDictionary<string, string>(StringComparer.Ordinal) { [ContentPaths.Provenance] = Provenance() };
        foreach (var (path, text) in Files)
        {
            all[path] = text;
        }

        foreach (var (path, text) in all)
        {
            var full = Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));
        }

        var manifest = ContentManifest.Compute(directory, bundleId: BundleId, contentVersion: "1.0.0", language: XxLanguagePack.Id);
        File.WriteAllText(Path.Combine(directory, ContentPaths.Manifest), manifest.ToJson(), new UTF8Encoding(false));
    }

    /// <summary>
    /// Writes the bundle into <paramref name="directory"/>, then loads it with the hashes verified and the toy registry. The directory is left in place
    /// (the loaded bundle holds its contents in memory); <see cref="LoadInTemp"/> writes to a temporary directory and removes it.
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    public static ContentBundle Load(string directory)
    {
        WriteTo(directory);
        return ContentLoader.Load(directory, new ContentLoadOptions { VerifyHashes = true, LanguagePacks = Registry });
    }

    /// <summary>Writes the bundle to a fresh temporary directory, loads it and deletes the directory again.</summary>
    public static ContentBundle LoadInTemp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vocaterra-xx-" + Guid.NewGuid().ToString("N"));
        try
        {
            return Load(directory);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temporary directory must not fail a test.
            }
        }
    }
}
