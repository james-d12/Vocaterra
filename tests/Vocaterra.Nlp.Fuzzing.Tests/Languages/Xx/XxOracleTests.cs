using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Embeddings;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Response.Speech;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// The neutral oracles O1, O2, O7 and O14 run on the toy language (M35): the same bodies the Spanish classes run, on the toy pack and the three-word
/// bundle, through the pipeline. They must pass with no Spanish code behind them; the coverage facts say the generated inputs reached the toy
/// language's own paths. The toy registry holds no Spanish pack (see <see cref="XxBundleTests"/>).
/// </summary>
public sealed class XxOracleTests(ITestOutputHelper output)
{
    private static FuzzLanguage Toy => XxFuzzLanguage.Instance;

    [Fact]
    public void O1_TheNormalizerIsTotalIdempotentAndWellFormed_OnTheToyLanguage()
    {
        var diagnostics = LanguageOracles.Normalizer(Toy, output);
        Assert.True(diagnostics.Get("input:lone-surrogate") > 0);
    }

    [Fact]
    public void O2_TheAnalyzerIsDeterministicAndWellFormed_OnTheToyLanguage()
    {
        var diagnostics = LanguageOracles.Analyzer(Toy, output);

        // The toy inputs reach the toy language's own error tag (an unknown word) and a matched grammar.
        Assert.True(diagnostics.Get($"tag:{ErrorTags.VocabUnknown}") > 0, $"No toy input produced {ErrorTags.VocabUnknown}.\n{diagnostics.Report()}");
        Assert.True(diagnostics.Get("grammar:matched") > 0, $"No toy input matched a grammar.\n{diagnostics.Report()}");
    }

    [Fact]
    public void O7_TheResolverRespectsItsLadderAndTheAllowedIntents_OnTheToyLanguage()
    {
        var diagnostics = LanguageOracles.Resolver(Toy, output);
        Assert.True(diagnostics.Get("rung:Grammar") > 0 && diagnostics.Get("rung:Unresolved") > 0, "The grammar and unresolved rungs must be reached.\n" + diagnostics.Report());
    }

    [Fact]
    public void O14_ThePipelineNeverThrowsIsDeterministicAgreesWithTheStagesAndReplaysClean_OnTheToyLanguage()
    {
        var diagnostics = LanguageOracles.Pipeline(Toy, output);
        Assert.True(diagnostics.Get("verdict:communicated") > 0 && diagnostics.Get("verdict:not-communicated") > 0, "Both verdicts must be reached.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("verdict:errors") > 0, "A toy verdict must carry an error.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("repair:step") > 0, "The toy repair ladder must be reached.\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("mode:Constrained") > 0 && diagnostics.Get("mode:Open") > 0, "Both recognition modes must be reached.\n" + diagnostics.Report());
    }

    [Fact]
    public void TheToyRunCarriesNoSpanishTagOrText()
    {
        // A run over the toy inputs: every error tag is one of the toy pack's, and every line the agent speaks is toy text (no inverted mark, no
        // Spanish accent, no ñ), so no Spanish code or word reached a result.
        var bundle = XxBundle.LoadInTemp();
        var prompt = bundle.Prompts[XxBundle.PromptId];
        var pack = bundle.Language;
        var forbidden = new[] { '¿', '¡', 'ñ', 'á', 'é', 'í', 'ó', 'ú', 'ü' };
        var transcripts = new[] { "ka", "mo ka", "ti", "mo", "nu ko", "xyz qq", "Ka!", "ti mo ka" };
        foreach (var transcript in transcripts)
        {
            var result = RunOnce(bundle, prompt, transcript);
            foreach (var error in result.Verdict.Errors)
            {
                Assert.True(pack.ErrorTags.IsKnown(error.Tag), $"'{error.Tag}' is not a toy error tag.");
            }

            foreach (var line in result.Response.Lines)
            {
                Assert.False(forbidden.Any(line.Text.Contains), $"The toy line '{line.Text}' carries a Spanish mark.");
            }
        }

        // The toy bundle has no Spanish morphology: the Spanish generator is never the bundle's (ContentBundle.Morphology refuses).
        Assert.Throws<NotSupportedException>(() => bundle.Morphology);
    }

    // One utterance of the toy bundle, scripted to hear the transcript, in the context of the toy agent.
    private static PipelineResult RunOnce(ContentBundle bundle, PromptSpec prompt, string transcript)
    {
        var asr = new AsrResult(transcript, [new AsrHypothesis(transcript, 1.0)], [], 1.0, "scripted-toy", prompt.Mode);
        var services = new PipelineServices(
            new ScriptedWordRecognizer(asr), null, new FakeEmbeddingModel(), new SilentSynthesizer(), bundle);
        var context = ConversationContext.Create(
            42,
            "guide",
            new AgentState("guide", "idle", 0),
            new StoredTimestamp(PipelineHarness.FixedNow),
            LearnerModel.New(),
            null,
            null,
            null);
        return new UtterancePipeline(services).Evaluate(PipelineHarness.Clip(), prompt, context);
    }
}
