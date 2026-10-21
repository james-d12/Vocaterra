namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Normalization;

/// <summary>Shared catalog, analyzer and helpers for the intent tests.</summary>
internal static class IntentFixtures
{
    public const string CatalogJson = """
        {
          "schemaVersion": 1,
          "intents": [
            {
              "intentId": "offer_item",
              "requiredSlots": ["qty", "item"],
              "slotGrammars": ["tengo [NUM:qty] [NOUN:item]", "[PRON]? [VERB:tener] [NUM:qty] [NOUN:item]"],
              "keywordRules": [],
              "examples": ["tengo dos espadas", "tengo tres escudos"]
            },
            {
              "intentId": "buy_item",
              "requiredSlots": ["item"],
              "slotGrammars": [],
              "keywordRules": [
                { "lemmas": ["comprar"], "requiresSlots": ["item"], "slotFromNoun": "item" }
              ],
              "examples": ["quiero comprar una espada", "compro una espada", "necesito una espada"]
            },
            {
              "intentId": "greet",
              "requiredSlots": [],
              "slotGrammars": [],
              "keywordRules": [],
              "examples": ["hola buenos días", "buenas tardes", "hola amigo"]
            },
            {
              "intentId": "ask_price",
              "requiredSlots": ["item"],
              "slotGrammars": [],
              "keywordRules": [
                { "lemmas": ["costar"], "requiresSlots": [], "slotFromNoun": "item" }
              ],
              "examples": ["cuánto cuesta la espada", "cuál es el precio de la espada"]
            }
          ]
        }
        """;

    public static NormalizationOptions KeepArticles { get; } = new() { ConvertIndefiniteArticles = false };

    public static IntentCatalog Catalog { get; } = IntentCatalog.Load(CatalogJson);

    public static LinguisticAnalyzer Analyzer { get; } = new(Vocaterra.Nlp.Language.Lexicon.Lexicon.Seed);

    public static PromptSpec Prompt(params string[] allowedIntents)
    {
        return new PromptSpec("p1", RecognitionMode.Open, Difficulty.Beginner, "answers", allowedIntents, null, []);
    }

    public static NormalizedText Normalize(string text)
    {
        return TextNormalizer.Normalize(text, KeepArticles);
    }

    /// <summary>Analyzes the text with the given grammars (none by default, as when the pipeline passed no grammars to the analyzer).</summary>
    public static AnalysisResult Analyze(string text, bool withCatalogGrammars = false)
    {
        var normalized = Normalize(text);
        return withCatalogGrammars ? Analyzer.Analyze(normalized, Catalog.Grammars) : Analyzer.Analyze(normalized);
    }

    public static IntentResolution Resolve(
        string text,
        IEmbeddingModel? embeddings = null,
        bool withCatalogGrammars = false,
        IntentResolverOptions? options = null,
        params string[] allowedIntents)
    {
        var resolver = new IntentResolver(Catalog, embeddings, options);
        return resolver.Resolve(Normalize(text), Analyze(text, withCatalogGrammars), Prompt(allowedIntents));
    }
}
