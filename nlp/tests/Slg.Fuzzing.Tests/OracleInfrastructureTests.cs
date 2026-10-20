using Slg.Core.Verdicts;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Slg.Response.Templates;
using Xunit.Sdk;
using SlotGrammarType = Slg.Nlp.SlotGrammar.SlotGrammar;

namespace Slg.Fuzzing.Tests;

/// <summary>Plain tests of the fuzz suite's own machinery, so a broken helper cannot turn an oracle into a silent pass.</summary>
public sealed class OracleInfrastructureTests
{
    [Fact]
    public void FirstDifference_NamesTheJsonPath()
    {
        Assert.Equal("$.errors[1].span.start", Determinism.FirstDifference(
            """{"errors":[{"span":{"start":1}},{"span":{"start":2}}]}""",
            """{"errors":[{"span":{"start":1}},{"span":{"start":3}}]}"""));
        Assert.Equal("$.errors.length", Determinism.FirstDifference("""{"errors":[1]}""", """{"errors":[1,2]}"""));
        Assert.Equal("(identical)", Determinism.FirstDifference("""{"a":1}""", """{"a":1}"""));
    }

    [Fact]
    public void AssertSame_FailsWithTheInputAndThePath_WhenTwoRunsDiffer()
    {
        var calls = 0;
        var failure = Assert.Throws<XunitException>(() =>
            Determinism.AssertSame("OX", "counter", "the input", () => new Counter(calls++), () => new Counter(calls++)));

        Assert.Contains("[OX] counter is not deterministic", failure.Message, StringComparison.Ordinal);
        Assert.Contains("$.value", failure.Message, StringComparison.Ordinal);
        Assert.Contains("\"the input\"", failure.Message, StringComparison.Ordinal);
        Determinism.AssertSame("OX", "constant", "x", () => new Counter(7), () => new Counter(7));
    }

    [Fact]
    public void Describe_RendersStringsAsJsonLiterals_AndFallsBackForValuesCanonicalJsonRefuses()
    {
        Assert.Equal("\"a\\nb ñ\"", Determinism.Describe("a\nb ñ"));
        Assert.Contains("\"value\":3", Determinism.Describe(new Counter(3)), StringComparison.Ordinal);
        Assert.Contains("NaN", Determinism.Describe(new Counter(double.NaN)), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAMissingSlotIsTolerated()
    {
        var slots = SlotMap.From([new KeyValuePair<string, string>("item", "espada")]);

        Assert.True(KnownTolerances.IsMissingSlot(new TemplateException("qty", "Unknown slot 'qty'."), slots));
        Assert.False(KnownTolerances.IsMissingSlot(new TemplateException("item", "Unknown slot 'item'."), slots));
        Assert.False(KnownTolerances.IsMissingSlot(new TemplateException("{item:bogus}", "Unknown filter."), slots));
    }

    [Fact]
    public void EveryCatalogPatternAndSkeletonRendersBackToItself()
    {
        var patterns = PipelineHarness.Shared.Grammars.Grammars.Select(static g => g.Id).Concat(PatternGen.Skeletons);
        foreach (var text in patterns)
        {
            var tree = GrammarPattern.Parse(text);
            var compiled = SlotGrammarType.Parse(text);
            Assert.Equal(compiled.Pattern, tree.Render());
            Assert.Equal(tree.Render(), GrammarPattern.Parse(tree.Render()).Render());
        }
    }

    [Fact]
    public void SlotAmbiguityIsDetectedForCompetingOptionalTokens()
    {
        static IEnumerable<string> None(string word) => [];

        Assert.True(GrammarPattern.Parse("[NOUN:a]? [NOUN:b]?").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("[ADJ]? [ADJ:x]?").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("[DET]? [ADJ:x]").HasAmbiguousSlots(None));
        Assert.True(GrammarPattern.Parse("isla? [NOUN:x]").HasAmbiguousSlots(static _ => ["NOUN"]));
        Assert.False(GrammarPattern.Parse("[VERB:tener:verb] [NUM:qty] [NOUN:item]").HasAmbiguousSlots(None));
        Assert.False(GrammarPattern.Parse("[VERB] [ART] [NOUN:first] [PREP] [ART] [NOUN:second]").HasAmbiguousSlots(None));
    }

    [Fact]
    public void NoNoise_LeavesTheTextAlone_AndSafeNoiseNeverChangesTheNormalizedText()
    {
        Assert.Equal("yo tengo dos espadas", TextNoise.None.Apply("yo tengo dos espadas"));
        var noisy = new TextNoise(CaseMode: 4, AccentMode: 0, Nfd: true, Punctuation: 3, Spaces: 3, NumberMode: 0, AdversarialIndex: -1, Seed: 5).Apply("yo tengo dos espadas");

        Assert.NotEqual("yo tengo dos espadas", noisy);
        Assert.Equal("yo tengo dos espadas", PipelineHarness.Normalize(noisy).Text.Replace("2", "dos", StringComparison.Ordinal));
    }

    public sealed record Counter(double Value);
}
