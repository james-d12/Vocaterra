namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;

/// <summary>
/// M95: the polite phrase and the ladder. A line that ends in <c>por favor</c> is still read over the whole catalog: a grammar of an intent the prompt does not
/// allow that outranks the allowed grammar takes the line, whatever the phrase binds. The intents and grammars are those of <c>content/game-v1</c>.
/// </summary>
public class IntentResolverPoliteLadderTests
{
    private static readonly string[] LuzWill = ["ask_help", "state_action", "dont_know"];

    private static IntentResolution Resolve(string text, params string[] allowed)
    {
        var bundle = Vocaterra.Nlp.Pipeline.Tests.TestSupport.Bundle;
        var normalized = bundle.Language.Normalizer.Normalize(text, IntentFixtures.KeepArticles);
        var prompt = new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers", allowed, null, []);
        var grammars = bundle.Intents.Intents.Where(i => allowed.Length == 0 || allowed.Contains(i.Id.Value)).SelectMany(static i => i.Grammars).ToArray();
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, bundle.Language, bundle.AnalysisRules).Analyze(normalized, grammars);
        return new IntentResolver(bundle.Intents, bundle.Language, null, null, bundle.AnalysisRules).Resolve(normalized, analysis, prompt);
    }

    private static IntentResolution OffPrompt(string intent) =>
        new(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0, new IntentId(intent));

    [Fact]
    public void TheFuzzSeed6_6oGJLVh3o1_AnOutsideGrammarOutranksTheAllowedOneBeforeThePolitePhrase()
    {
        // offer_item's (yo)? (te|le)? vendo ... [NOUN:item] outranks state_action's [VERB:vender] ... [NOUN:item], which the prompt allows. request_item's
        // grammar beats both in the rung's order (four tokens) but names fewer of the words than the allowed one, so it must not stand in offer_item's way.
        Assert.Equal(OffPrompt("offer_item"), Resolve("yo vendo partos por favor", LuzWill));
    }

    [Fact]
    public void TheSameLineWithoutThePolitePhraseIsTakenTheSameWay() =>
        Assert.Equal(OffPrompt("offer_item"), Resolve("yo vendo partos", LuzWill));

    [Fact]
    public void ThePolitePhraseLeadingTheLineIsNotOneOfSpanishPolitePositionsAndTakesNothing() =>
        // Spanish allows the phrase only at the end: led by it, no grammar of the catalog matches, and the line stays unresolved, off no prompt.
        Assert.Equal(new IntentResolution(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0), Resolve("por favor vendo partos", LuzWill));

    [Fact]
    public void AnAllowedGrammarThatOutranksKeepsTheLine()
    {
        var resolution = Resolve("yo vendo partos por favor", "offer_item", "state_action");
        Assert.Equal(new IntentId("offer_item"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
    }

    [Fact]
    public void ThePolitePhraseOnAnotherIntentKeepsItsOwnGrammar()
    {
        // Nothing outside the prompt outranks an allowed ask_help line with the polite phrase.
        Assert.Equal(new IntentId("ask_help"), Resolve("ayuda por favor", LuzWill).Intent);
    }

    [Fact]
    public void WithEveryIntentAllowedTheAnalysisOfThePromptIsReadByTheSameRule()
    {
        // O7's second check: the off-prompt intent is the intent the resolver gives the same analysis with every intent allowed.
        var bundle = Vocaterra.Nlp.Pipeline.Tests.TestSupport.Bundle;
        var normalized = bundle.Language.Normalizer.Normalize("yo vendo partos por favor", IntentFixtures.KeepArticles);
        var grammars = bundle.Intents.Intents.Where(i => LuzWill.Contains(i.Id.Value)).SelectMany(static i => i.Grammars).ToArray();
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, bundle.Language, bundle.AnalysisRules).Analyze(normalized, grammars);
        var prompt = new PromptSpec("p1", RecognitionMode.Constrained, Difficulty.Beginner, "answers", [], null, []);

        var resolution = new IntentResolver(bundle.Intents, bundle.Language, null, null, bundle.AnalysisRules).Resolve(normalized, analysis, prompt);

        Assert.Equal(new IntentId("offer_item"), resolution.Intent);
        Assert.Equal(IntentRung.Grammar, resolution.Rung);
    }
}
