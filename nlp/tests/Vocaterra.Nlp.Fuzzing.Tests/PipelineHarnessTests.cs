using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>Guards the builders stage 2 oracles rely on, so the harness cannot rot between stages.</summary>
public sealed class PipelineHarnessTests
{
    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void TheHarnessLoadsTheCommittedBundleWithVerifiedHashes()
    {
        Assert.Equal(Harness.Bundle.Manifest.BundleHash, Harness.Bundle.ContentHash);
        Assert.NotEmpty(Harness.Bundle.Prompts);
        Assert.NotEmpty(Harness.Bundle.Dialogue.Pools);
        Assert.NotEmpty(Harness.Grammars.Grammars);
    }

    [Fact]
    public void TheScriptedPipelineEvaluatesTheWorkedExample()
    {
        var prompt = Harness.Prompt("offer-items-open");
        var pipeline = Harness.NewPipeline("yo tiene dos espada", prompt.Mode, phonemes: true);

        var result = pipeline.Evaluate(PipelineHarness.Clip(), prompt, Harness.NewContext("blacksmith", "trading"));

        Assert.Equal("offer_item", result.Verdict.Intent.Value);
        Assert.Equal([ErrorTags.ConjPerson, ErrorTags.NounNumber], result.Verdict.Errors.Select(static e => e.Tag).ToArray());
        Assert.NotEmpty(result.Verdict.Pronunciation);
        Assert.Equal(RecognitionMode.Open, prompt.Mode);
    }

    [Fact]
    public void AFreshAnalyzerAndResolverAgreeWithTheSharedOnes()
    {
        var normalized = PipelineHarness.Normalize("yo tiene dos espada");
        var shared = Harness.Analyzer.Analyze(normalized, Harness.Bundle.Intents.Grammars);
        var fresh = Harness.NewAnalyzer().Analyze(normalized, Harness.Bundle.Intents.Grammars);

        Assert.Equal(shared, fresh);
        Assert.Equal(
            Harness.NewResolver(embeddings: true).Resolve(normalized, shared, Harness.Prompt("offer-items-open")),
            Harness.NewResolver(embeddings: true).Resolve(normalized, fresh, Harness.Prompt("offer-items-open")));
    }
}
