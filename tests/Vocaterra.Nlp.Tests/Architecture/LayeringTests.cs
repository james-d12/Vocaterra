using Fixtures = Vocaterra.Nlp.Core.Fixtures;

namespace Vocaterra.Nlp.Tests.Architecture;

/// <summary>
/// The engine is one assembly, so the compiler no longer stops a stage from using a stage it may not use. These tests check the layering of
/// <see cref="LayerRules"/> over the engine assembly, and prove that the check reports a violation (ADR 0012).
/// </summary>
public class LayeringTests
{
    private static readonly System.Reflection.Assembly Engine = typeof(AssemblyMarker).Assembly;

    /// <summary>Each stage's rule, one case per stage (Pipeline has none, because every other rule forbids it).</summary>
    public static IEnumerable<object[]> StageRules() =>
        LayerRules.Rules().Select(rule => new object[] { rule.Source, rule.Forbidden });

    [Theory]
    [MemberData(nameof(StageRules))]
    public void StageDoesNotDependOnStagesItMayNotUse(string source, string[] forbidden)
    {
        var violations = LayerRules.Violations(Engine, source, forbidden);

        Assert.True(
            violations.Count == 0,
            $"{source} must not depend on {string.Join(", ", forbidden)}; these types do: {string.Join(", ", violations)}");
    }

    [Fact]
    public void NothingButPipelineDependsOnPipeline()
    {
        var violations = LayerRules.Stages
            .Where(stage => stage != "Pipeline")
            .SelectMany(stage => LayerRules.Violations(Engine, LayerRules.Root + "." + stage, [LayerRules.Root + ".Pipeline"]))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryStageHasTypesInTheEngineAssembly()
    {
        // A rule over an empty namespace passes for nothing, so each stage must match at least one type.
        foreach (var stage in LayerRules.Stages)
        {
            Assert.True(LayerRules.TypeCount(Engine, LayerRules.Root + "." + stage) > 0, $"no type resides in {LayerRules.Root}.{stage}");
        }
    }

    [Fact]
    public void TheLanguagePacksHaveTypesAndARuleOfTheirOwn()
    {
        Assert.True(LayerRules.TypeCount(Engine, LayerRules.PacksNamespace) > 0, $"no type resides in {LayerRules.PacksNamespace}");
        Assert.Contains(LayerRules.Rules(), rule => rule.Source == LayerRules.PacksNamespace);
    }

    [Fact]
    public void AViolationIsReportedThroughTheSameRule()
    {
        // The fixture is a core-like type in the test assembly that depends on the pipeline. The same check must report it.
        var fixtures = typeof(Fixtures.CoreDependsOnPipeline).Assembly;

        var violations = LayerRules.Violations(fixtures, typeof(Fixtures.CoreDependsOnPipeline).Namespace!, [LayerRules.Root + ".Pipeline"]);

        Assert.Contains(typeof(Fixtures.CoreDependsOnPipeline).FullName!, violations);
    }

    [Fact]
    public void ACleanFixtureIsNotReported()
    {
        // The same namespace, with a type that depends on nothing in the pipeline, must not be reported: the check bites only on the dependency.
        var fixtures = typeof(Fixtures.CoreIsSelfContained).Assembly;

        var violations = LayerRules.Violations(fixtures, typeof(Fixtures.CoreIsSelfContained).Namespace!, [LayerRules.Root + ".Pipeline"]);

        Assert.DoesNotContain(typeof(Fixtures.CoreIsSelfContained).FullName!, violations);
    }
}
