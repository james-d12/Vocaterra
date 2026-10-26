using System.Reflection;
using NetArchTest.Rules;

namespace Vocaterra.Nlp.Tests.Architecture;

/// <summary>
/// The layering of the language core, as it was between its eleven projects (research 2026-10-26 section 5.1, read from the csproj files), and
/// the one check that applies a rule to an assembly. The engine is one assembly now (ADR 0012); its folders are the former projects.
/// </summary>
public static class LayerRules
{
    /// <summary>The namespace of the engine's root, and the prefix of every stage.</summary>
    public const string Root = "Vocaterra.Nlp";

    /// <summary>The engine's stages, in pipeline order: one folder and one namespace each.</summary>
    public static readonly string[] Stages =
    [
        "Core", "Audio", "Asr", "Phonology", "Pronunciation", "Language", "Evaluation", "Decision", "Response", "Content", "Pipeline",
    ];

    /// <summary>
    /// What each stage may depend on, besides itself. Core depends on nothing. Content may depend on everything except Pipeline, and
    /// Pipeline on everything, so neither has an entry that forbids anything beyond the rules below.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> MayDependOn = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Core"] = [],
        ["Audio"] = ["Core"],
        ["Asr"] = ["Core"],
        ["Phonology"] = ["Core"],
        ["Pronunciation"] = ["Core", "Phonology"],
        ["Language"] = ["Core"],
        ["Evaluation"] = ["Core"],
        ["Decision"] = ["Core"],
        ["Response"] = ["Core", "Asr", "Language", "Phonology"],
        ["Content"] = ["Core", "Audio", "Asr", "Phonology", "Pronunciation", "Language", "Evaluation", "Decision", "Response"],
        ["Pipeline"] = Stages,
    };

    /// <summary>
    /// One rule per stage except Pipeline: the stage's namespace must not depend on any stage it is not allowed to use. Nothing but Pipeline
    /// therefore depends on Pipeline.
    /// </summary>
    public static IEnumerable<(string Source, string[] Forbidden)> Rules()
    {
        foreach (var stage in Stages.Where(stage => stage != "Pipeline"))
        {
            var allowed = MayDependOn[stage];
            var forbidden = Stages
                .Where(other => other != stage && !allowed.Contains(other, StringComparer.Ordinal))
                .Select(other => Root + "." + other)
                .ToArray();
            yield return (Root + "." + stage, forbidden);
        }
    }

    /// <summary>
    /// The types of <paramref name="assembly"/> that reside in <paramref name="sourceNamespace"/> (or a namespace under it) and have a
    /// dependency on one of <paramref name="forbiddenNamespaces"/>. Empty when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> Violations(Assembly assembly, string sourceNamespace, IEnumerable<string> forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith(sourceNamespace)
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces.ToArray())
            .GetResult();
        return result.FailingTypeNames?.ToList() ?? [];
    }

    /// <summary>The number of types of <paramref name="assembly"/> that reside in <paramref name="sourceNamespace"/> (or under it).</summary>
    public static int TypeCount(Assembly assembly, string sourceNamespace) =>
        Types.InAssembly(assembly).That().ResideInNamespaceStartingWith(sourceNamespace).GetTypes().Count();
}
