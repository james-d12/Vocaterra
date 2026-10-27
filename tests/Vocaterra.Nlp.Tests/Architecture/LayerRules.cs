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

    /// <summary>The namespace of the Spanish pack project (<c>src/Vocaterra.Nlp.Es</c>, M53): no engine type may depend on it, and the engine assembly does not reference it.</summary>
    public const string SpanishPackNamespace = Root + ".Es";

    /// <summary>The assembly name of the Spanish pack project.</summary>
    public const string SpanishPackAssembly = "Vocaterra.Nlp.Es";

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
    /// The language pack contracts (<c>Vocaterra.Nlp.Language.Packs</c>, the interfaces and the registry) sit in the Language folder but use types of other
    /// stages, so they have a rule of their own and the Language rule does not cover them. They may use Core, Language and Phonology, nothing else. Not Response: the
    /// punctuation fixer and the number agreement moved into the packs in M33 (ADR 0011, point 9), so Response may use the packs and the packs do
    /// not use Response. There is no cycle between the two stages.
    /// </summary>
    public const string PacksNamespace = Root + ".Language.Packs";

    private static readonly string[] PacksMayDependOn = ["Core", "Language", "Phonology"];

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

        yield return (
            PacksNamespace,
            Stages.Where(other => !PacksMayDependOn.Contains(other, StringComparer.Ordinal)).Select(other => Root + "." + other).ToArray());
    }

    /// <summary>
    /// The types of <paramref name="assembly"/> that reside in <paramref name="sourceNamespace"/> (or a namespace under it) and have a
    /// dependency on one of <paramref name="forbiddenNamespaces"/>. Empty when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> Violations(Assembly assembly, string sourceNamespace, IEnumerable<string> forbiddenNamespaces)
    {
        var types = Types.InAssembly(assembly).That().ResideInNamespaceStartingWith(sourceNamespace);
        if (!string.Equals(sourceNamespace, PacksNamespace, StringComparison.Ordinal))
        {
            // The packs have a rule of their own (see PacksNamespace).
            types = types.And().DoNotResideInNamespaceStartingWith(PacksNamespace);
        }

        var result = types
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces.ToArray())
            .GetResult();
        return result.FailingTypeNames?.ToList() ?? [];
    }

    /// <summary>The number of types of <paramref name="assembly"/> that reside in <paramref name="sourceNamespace"/> (or under it).</summary>
    public static int TypeCount(Assembly assembly, string sourceNamespace) =>
        Types.InAssembly(assembly).That().ResideInNamespaceStartingWith(sourceNamespace).GetTypes().Count();
}
