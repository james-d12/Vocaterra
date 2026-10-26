using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Nlp.Core.Fixtures;

/// <summary>
/// A deliberate layering violation, kept on purpose: a core-like type that depends on the pipeline (a stage above it). LayeringTests must
/// report it. It is not used by the engine or by any other test.
/// </summary>
public static class CoreDependsOnPipeline
{
    /// <summary>The name of the pipeline type this fixture depends on, taken from its signature.</summary>
    public static string Describe(UtterancePipeline pipeline) => pipeline.GetType().Name;
}

/// <summary>A core-like type that depends on nothing above it. LayeringTests must not report it.</summary>
public static class CoreIsSelfContained
{
    /// <summary>A constant that needs no other stage.</summary>
    public static int Answer() => 42;
}
