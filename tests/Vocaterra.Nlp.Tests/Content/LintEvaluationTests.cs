using System.Text.Json.Nodes;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// The lint reads each text once, matches each grammar over it once and completes its analysis once per winning grammar (M60). These tests prove that what it gives in
/// every prompt is what the analyzer gives afresh, so a cache keyed by the text alone, one that hands a text another grammar's match, or one that lets a name's cleared
/// readings leak into another prompt fails them.
/// </summary>
public class LintEvaluationTests
{
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    [Fact]
    public void ATextIsAnalyzedWithTheGrammarThatWinsInEachPromptWhateverOrderThePromptsComeIn()
    {
        // "necesito agua" is request_item's and state_want's (each has its own necesitar grammar), and greet's grammars do not match it at all.
        var bundle = TestSupport.Bundle;
        var context = new LintContext(bundle, LintOptions.Default);
        string[][] allowLists = [["request_item"], ["state_want"], ["greet"], ["request_item", "state_want"], ["greet", "state_want"], ["request_item"], ["greet"]];

        var winners = new List<string?>();
        foreach (var allowed in allowLists)
        {
            var prompt = context.Only(allowed);
            var (analysis, resolution) = Fresh(bundle, "necesito agua", prompt);

            var evaluated = context.Evaluate("necesito agua", prompt);
            Assert.Equal(analysis, evaluated.Analysis);
            Assert.Equal(resolution, evaluated.Resolution);
            Assert.Equal(analysis, context.Analyze(Normalize(bundle, "necesito agua"), prompt));
            winners.Add(analysis.MatchedGrammarId);
        }

        // The prompts must really give the text different winners, and no winner at all, or the test proves nothing.
        Assert.Contains(null, winners);
        Assert.True(winners.Where(static id => id is not null).Distinct(StringComparer.Ordinal).Count() >= 2, string.Join(" | ", winners));
    }

    [Fact]
    public void AWordThatOnePromptBindsAsANameKeepsItsReadingsInAnother()
    {
        // The completion drops a name's readings from its own list of tokens; the readings of the text, which every prompt shares, must keep them.
        using var copy = new TempBundle(rehash: false);
        var intents = JsonNode.Parse(copy.Read("intents/intents.json"))!.AsObject();
        intents["intents"]!.AsArray().Add(JsonNode.Parse(
            "{\"intentId\":\"fx_name\",\"requiredSlots\":[\"name\"],\"slotGrammars\":[\"me [VERB:llamar] [WORD:name]\"],\"examples\":[\"me llamo agua\"]}"));
        copy.Write("intents/intents.json", intents.ToJsonString());
        var bundle = SpanishContent.Load(copy.Directory, new ContentLoadOptions { VerifyHashes = false });
        var context = new LintContext(bundle, LintOptions.Default);

        var named = context.Evaluate("me llamo agua", context.Only("fx_name")).Analysis;
        var unnamed = context.Evaluate("me llamo agua", context.Only("greet")).Analysis;

        Assert.Equal(Fresh(bundle, "me llamo agua", context.Only("fx_name")).Analysis, named);
        Assert.Equal(Fresh(bundle, "me llamo agua", context.Only("greet")).Analysis, unnamed);
        Assert.Empty(named.Tokens[2].Readings);
        Assert.NotEmpty(unnamed.Tokens[2].Readings);
    }

    [Fact]
    public void EveryExampleIsAnalyzedAsTheAnalyzerDoesInItsOwnPromptAndInEveryPromptThatAllowsItsIntent()
    {
        var bundle = TestSupport.Bundle;
        var context = new LintContext(bundle, LintOptions.Default);
        var analyzer = Analyzer(bundle);
        var count = 0;
        foreach (var intent in bundle.Intents.Intents)
        {
            List<PromptSpec> prompts = [context.Only(intent.Id.Value), .. context.PromptsAllowing(intent.Id.Value)];
            foreach (var example in intent.ExampleSpecs)
            {
                var normalized = Normalize(bundle, example.Text);
                foreach (var prompt in prompts)
                {
                    var expected = analyzer.Analyze(normalized, PromptGrammars.For(bundle.Intents, prompt));
                    Assert.Equal(expected, context.Evaluate(example.Text, prompt).Analysis);
                    Assert.Equal(expected, context.Analyze(normalized, prompt));
                    count++;
                }
            }
        }

        Assert.True(count > bundle.Intents.Intents.Count, $"only {count} analyses were compared");
    }

    private static LinguisticAnalyzer Analyzer(ContentBundle bundle)
    {
        return new LinguisticAnalyzer(bundle.Lexicon, bundle.LanguageMorphology, bundle.Language, bundle.AnalysisRules);
    }

    private static NormalizedText Normalize(ContentBundle bundle, string text)
    {
        return bundle.Language.Normalizer.Normalize(text, KeepArticles);
    }

    // The analysis and resolution of the text in the prompt without any cache: what the lint did for every evaluation before M60.
    private static (AnalysisResult Analysis, IntentResolution Resolution) Fresh(ContentBundle bundle, string text, PromptSpec prompt)
    {
        var normalized = Normalize(bundle, text);
        var analysis = Analyzer(bundle).Analyze(normalized, PromptGrammars.For(bundle.Intents, prompt));
        return (analysis, new IntentResolver(bundle.Intents, bundle.Language, null, null, bundle.AnalysisRules).Resolve(normalized, analysis, prompt));
    }
}
