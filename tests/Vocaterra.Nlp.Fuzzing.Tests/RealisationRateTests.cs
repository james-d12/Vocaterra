using System.Globalization;
using System.Text;
using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M98 (the codebase review's T3), the realisation rate. The realiser holds many re-roll rules, and a recipe it cannot realise after every attempt is skipped
/// (<c>skip:</c> counters): each new rule narrows what O3, O4 and O5 test with nothing to show it, and <see cref="SkipBudgetTests"/> bounds the share of each rejected
/// <em>draw</em>, not the share of recipes that give no sentence at all. This test draws <see cref="Recipes"/> recipes of O3's stream from fixed seeds (one case per
/// seed: CsCheck seeds only the first case of a run), counts the recipes realised and the skips by reason for each pattern family (<c>catalog</c>, <c>skeleton</c>,
/// <c>random</c>: <see cref="SentenceRecipe.Source"/>), prints them, and pins a floor on the realisation rate of each family and of all. A change that makes the generator
/// avoid much more of a family fails here. Lower a floor never; a change that raises a rate may raise its floor.
/// </summary>
public sealed class RealisationRateTests(ITestOutputHelper output)
{
    private const int Recipes = FuzzOptions.GrammarDefault;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // The floors: the measured rate on the fixed seeds (README, "The realisation rate") minus a margin of about five points, rounded down. The rates do not vary run to
    // run (fixed seeds), but they move with a content edit (new words or grammars change the draws) or a generator change; a move of a few points is not a failure.
    private static readonly Dictionary<string, double> Floors = new(StringComparer.Ordinal)
    {
        ["catalog"] = 0.95,
        ["skeleton"] = 0.95,
        ["random"] = 0.92,
        ["all"] = 0.95,
    };

    // The same for the share of the realiser's draws that were kept (sentence / (rejected + sentence)): the re-roll rules show here before they show as skips, because
    // a draw that a rule rejects is usually made good by the next attempt. SkipBudgetTests bounds each reason; this bounds the sum.
    private static readonly Dictionary<string, double> DrawFloors = new(StringComparer.Ordinal)
    {
        ["catalog"] = 0.90,
        ["skeleton"] = 0.93,
        ["random"] = 0.80,
        ["all"] = 0.88,
    };

    [Fact]
    public void EveryPatternFamilyIsRealisedAtLeastAtItsFloor_OnFixedSeeds()
    {
        var families = new Dictionary<string, FuzzDiagnostics>(StringComparer.Ordinal);
        var all = new FuzzDiagnostics();
        for (var i = 0; i < Recipes; i++)
        {
            SentenceGen.Recipe(Harness).Sample(
                recipe =>
                {
                    var family = recipe.Source;
                    if (!families.TryGetValue(family, out var diagnostics))
                    {
                        families[family] = diagnostics = new FuzzDiagnostics();
                    }

                    SentenceGen.Realize(Harness, recipe, diagnostics: diagnostics);
                },
                iter: 1,
                threads: 1,
                seed: "M98R" + i.ToString("D8", CultureInfo.InvariantCulture));
        }

        foreach (var diagnostics in families.Values)
        {
            foreach (var key in Keys(diagnostics))
            {
                all.Add(key, diagnostics.Get(key));
            }
        }

        var streams = families.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => (Key: p.Key, Diagnostics: p.Value)).Append((Key: "all", Diagnostics: all)).ToArray();
        var table = Table(streams);
        output.WriteLine(table);

        var under = new List<string>();
        foreach (var (name, diagnostics) in streams)
        {
            var rate = Rate(diagnostics);
            Assert.True(Floors.ContainsKey(name), $"The family '{name}' has no floor.\n{table}");
            if (DrawRate(diagnostics) < DrawFloors[name])
            {
                under.Add($"{name}: {Percent(DrawRate(diagnostics))} of {diagnostics.Draws} draws kept (floor {Percent(DrawFloors[name])})");
            }

            if (rate < Floors[name])
            {
                under.Add($"{name}: {Percent(rate)} of {diagnostics.Recipes} recipes realised (floor {Percent(Floors[name])})");
            }
        }

        foreach (var name in Floors.Keys)
        {
            Assert.True(streams.Any(s => s.Item1 == name), $"The family '{name}' of the floors was never drawn.");
        }

        Assert.True(all.Recipes == Recipes, $"Expected {Recipes} recipes, counted {all.Recipes}.");
        Assert.True(under.Count == 0, $"A pattern family is realised less often than its floor (the generator avoids more of it):\n  {string.Join("\n  ", under)}\n{table}");
    }

    private static IEnumerable<string> Keys(FuzzDiagnostics diagnostics)
    {
        return diagnostics.Report().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => l.Trim()[..l.Trim().IndexOf(" = ", StringComparison.Ordinal)])
            .Where(static k => k.StartsWith("sentence:", StringComparison.Ordinal) || k.StartsWith("skip:", StringComparison.Ordinal) || k.StartsWith(FuzzDiagnostics.RejectedPrefix, StringComparison.Ordinal));
    }

    private static double Rate(FuzzDiagnostics diagnostics)
    {
        return diagnostics.Recipes == 0 ? 0.0 : (double)diagnostics.Sum("sentence:") / diagnostics.Recipes;
    }

    private static double DrawRate(FuzzDiagnostics diagnostics)
    {
        return diagnostics.Draws == 0 ? 0.0 : (double)diagnostics.Sum("sentence:") / diagnostics.Draws;
    }

    private static string Percent(double share)
    {
        return (share * 100).ToString("0.00", CultureInfo.InvariantCulture) + " %";
    }

    private static string Table((string Key, FuzzDiagnostics Diagnostics)[] streams)
    {
        var builder = new StringBuilder("Realisation rate by pattern family (recipes realised / recipes drawn), then the skips by reason:\n");
        foreach (var (name, d) in streams)
        {
            builder.Append(CultureInfo.InvariantCulture, $"  {name}: {d.Sum("sentence:")} / {d.Recipes} = {Percent(Rate(d))}; draws kept {d.Sum("sentence:")} / {d.Draws} = {Percent(DrawRate(d))}\n");
        }

        var reasons = streams.SelectMany(static s => Keys(s.Diagnostics)).Where(static k => k.StartsWith("skip:", StringComparison.Ordinal) || k.StartsWith(FuzzDiagnostics.RejectedPrefix, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var reason in reasons)
        {
            builder.Append("  ").Append(reason).Append(':');
            foreach (var (name, d) in streams)
            {
                builder.Append(CultureInfo.InvariantCulture, $" {name}={d.Get(reason)}");
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }
}
