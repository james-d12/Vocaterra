namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.SlotGrammar;

/// <summary>
/// M74: the grammar rung reads a line over the whole catalog before the prompt narrows it. A grammar of an intent the prompt does not allow takes the line from the
/// best allowed grammar when it beats it in the rung's order and names more of the line's words (a literal word, or a verb of a named lemma); the line is then
/// unknown at the prompt, with that intent off the prompt. The words are those of <c>content/game-v1</c>'s lexicon, the catalogs are written here.
/// </summary>
public class IntentResolverCatalogReadingTests
{
    private const string Catalog = """
        { "schemaVersion": 1, "intents": [
          { "intentId": "action", "slotGrammars": ["[VERB:hacer] [ART]? [NOUN:item]", "[VERB:vender] [NOUN:item]", "[VERB:necesitar] [NOUN:item]"],
            "keywordRules": [ { "lemmas": ["qué", "hacer"] }, { "lemmas": ["comprar"], "requiresSlots": ["item"], "slotFromNoun": "item" } ] },
          { "intentId": "offer", "slotGrammars": ["vendo [NOUN:item]"] },
          { "intentId": "request", "slotGrammars": ["[VERB:necesitar] [NOUN:item] (por favor)?"] },
          { "intentId": "weather", "slotGrammars": ["(hoy)? [VERB:hacer] (calor|sol|buen tiempo):weather", "qué tiempo [VERB:hacer]"] } ] }
        """;

    private static readonly IntentCatalog Intents = IntentCatalog.Load(Catalog);

    private static IntentResolution Resolve(string text, params string[] allowed)
    {
        var bundle = Vocaterra.Nlp.Pipeline.Tests.TestSupport.Bundle;
        var normalized = bundle.Language.Normalizer.Normalize(text, IntentFixtures.KeepArticles);
        var prompt = new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers", allowed, null, []);

        // The analyzer is given the allowed intents' grammars, as the pipeline gives it (every grammar when every intent is allowed).
        var grammars = Intents.Intents.Where(i => allowed.Length == 0 || allowed.Contains(i.Id.Value)).SelectMany(static i => i.Grammars).ToArray();
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, bundle.Language, bundle.AnalysisRules).Analyze(normalized, grammars);
        return new IntentResolver(Intents, bundle.Language, null, null, bundle.AnalysisRules).Resolve(normalized, analysis, prompt);
    }

    private static IntentResolution OffPrompt(string intent) =>
        new(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0, new IntentId(intent));

    [Theory]
    [InlineData("hace calor")]
    [InlineData("hoy hace sol")]
    public void AGrammarThatNamesAWordTheAllowedGrammarReadsOnlyAsAClassTakesTheLine(string text)
    {
        // [VERB:hacer] [ART]? [NOUN:item] reads calor as a noun; the weather's grammar names it.
        Assert.Equal(OffPrompt("weather"), Resolve(text, "action"));
        Assert.Equal(new IntentId("weather"), Resolve(text, "action", "weather").Intent);
        Assert.Equal(new IntentId("weather"), Resolve(text).Intent);
    }

    [Fact]
    public void TheIntentThatTakesTheLineFillsItsSlots()
    {
        var resolution = Resolve("hoy hace buen tiempo", "action", "weather");

        Assert.Equal(new IntentResolution(new IntentId("weather"), SlotMap.From([new("weather", "buen tiempo")]), IntentRung.Grammar, 1.0, 1.0), resolution);
    }

    [Theory]
    [InlineData("hago pan", "item=pan")]
    [InlineData("hace una espada", "item=espada")]
    public void AnActionTheWeatherDoesNotNameStaysTheAllowedIntents(string text, string slot)
    {
        foreach (var allowed in new[] { new[] { "action" }, ["action", "weather"], [] })
        {
            var resolution = Resolve(text, allowed);
            Assert.Equal(new IntentId("action"), resolution.Intent);
            Assert.Equal(slot, string.Join(",", resolution.Slots.Select(static s => s.Key + "=" + s.Value)));
        }
    }

    [Fact]
    public void BeatingTheAllowedGrammarWithoutNamingMoreOfTheWordsIsNotEnough()
    {
        // (por favor)? consumes the words the polite phrase every grammar allows reads too, and vendo names the verb [VERB:vender] names.
        Assert.Equal(new IntentId("action"), Resolve("necesito pan por favor", "action").Intent);
        Assert.Equal(new IntentId("action"), Resolve("vendo pan", "action").Intent);
        Assert.Equal(new IntentId("request"), Resolve("necesito pan por favor").Intent);
    }

    [Fact]
    public void ALineNoAllowedGrammarMatchesGoesDownTheLadderBeforeTheCatalogIsAsked()
    {
        // The keyword rule of qué with hacer still reads qué tiempo hace as the action where the weather is not allowed (a grammar outside the prompt takes a line
        // only from an allowed grammar; letting it pre-empt the allowed keyword rules took sí, gracias from thank at a thanks lesson). Where the weather is allowed,
        // its grammar comes first; where nothing allowed reads the line, it is the weather off the prompt.
        var rule = Resolve("qué tiempo hace", "action");
        Assert.Equal((new IntentId("action"), IntentRung.Rules), (rule.Intent, rule.Rung));
        Assert.Equal(new IntentId("weather"), Resolve("qué tiempo hace", "action", "weather").Intent);
        Assert.Equal(OffPrompt("weather"), Resolve("qué tiempo hace", "offer"));
        Assert.Equal(OffPrompt("weather"), Resolve("hace sol", "offer"));
    }

    [Fact]
    public void WithEveryIntentAllowedTheLineIsReadOverTheWholeCatalogWhateverGrammarsTheAnalyzerWasGiven()
    {
        // The analysis made for a prompt that allows only the action matched its grammar; with every intent allowed, the weather's grammar, which the analyzer was
        // not given, reads the line better, and it is the off-prompt intent of the first prompt (M72's rule: the intent with every intent allowed).
        var bundle = Vocaterra.Nlp.Pipeline.Tests.TestSupport.Bundle;
        var normalized = bundle.Language.Normalizer.Normalize("hace calor", IntentFixtures.KeepArticles);
        var action = Intents.Find("action")!;
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, bundle.Language, bundle.AnalysisRules).Analyze(normalized, action.Grammars);
        Assert.Equal("[VERB:hacer] [ART]? [NOUN:item]", analysis.MatchedGrammarId);
        var resolver = new IntentResolver(Intents, bundle.Language, null, null, bundle.AnalysisRules);

        var everyIntent = resolver.Resolve(normalized, analysis, new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers", [], null, []));
        var onlyAction = resolver.Resolve(normalized, analysis, new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers", ["action"], null, []));

        Assert.Equal(new IntentId("weather"), everyIntent.Intent);
        Assert.Equal(SlotMap.From([new("weather", "calor")]), everyIntent.Slots);
        Assert.Equal(everyIntent.Intent, onlyAction.OffPromptIntent);
    }

    [Theory]
    [InlineData("hace dos días compré pan")]
    [InlineData("hace un rato compré pan")]
    [InlineData("compré pan hace dos días")]
    public void TheNounOfAPhraseOfElapsedTimeIsNotTheRulesItem(string text)
    {
        // M74 (found with the weather lines): hace dos días is "two days ago", hace the preposition (M59); the keyword rule of comprar took días as the item.
        // No weather grammar takes the line, which is the past action it says, as compré pan is.
        var resolution = Resolve(text, "action");

        Assert.Equal((new IntentId("action"), IntentRung.Rules), (resolution.Intent, resolution.Rung));
        Assert.Equal(SlotMap.From([new("item", "pan")]), resolution.Slots);
        var atWeather = Resolve(text, "weather");
        Assert.Equal(IntentId.Unknown, atWeather.Intent);
        Assert.Equal(new IntentId("action"), atWeather.OffPromptIntent);
    }

    [Fact]
    public void TheCatalogReadingIsTheSameAcrossCallsAndInstances()
    {
        var first = Resolve("hace calor", "action");
        Assert.Equal(first, Resolve("hace calor", "action"));
        Assert.Equal(first, Resolve("hace calor", "offer", "action", "request"));
    }

    [Fact]
    public void AGroupSlotIsDeclaredAsANounSlotForTheEmbeddingRung()
    {
        var weather = Intents.Find("weather")!;

        Assert.Contains("weather", weather.DeclaredSlots.Select(static s => s.Name));
        Assert.Equal("(hoy)? [VERB:hacer] (calor|sol|buen tiempo):weather", SlotGrammar.Parse(weather.Grammars[0].Pattern).Pattern);
    }
}
