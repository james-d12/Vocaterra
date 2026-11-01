using System.Globalization;
using System.Text;
using CsCheck;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// M65 (e), the skip budget. The realiser re-rolls a draw it cannot spell correctly (a coordinated subject it cannot tell, a colour before a noun, <c>ser</c> of a
/// place...), and a re-roll leaves out a whole class of sentence from O3, O4 and O5 without a failure: one of them hid the review's R1 (<c>tú y yo fuimos</c> graded
/// wrong). This test draws the sentences of O3 and the mutations of O4 and O5 at their default depth from fixed seeds, counts every rejected draw by its reason
/// (<see cref="FuzzDiagnostics.RejectedPrefix"/>: each failed attempt of the realiser and each sentence <see cref="SentenceGen.Realize"/> rejects) and fails when one
/// reason takes more than its budget of the draws: <see cref="DefaultBudget"/>, or the budget <see cref="Budgets"/> names. A new reason has no entry there, so it falls
/// under the default and cannot silently eat a class of sentence. The measured shares are in the README ("The skip budget").
/// </summary>
public sealed class SkipBudgetTests(ITestOutputHelper output)
{
    /// <summary>The share of the draws a reason of rejection may take unless <see cref="Budgets"/> names it.</summary>
    private const double DefaultBudget = 0.05;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // A reason that takes more than the default gets its own budget here: its measured share in the larger of the two streams plus a small margin, and a one-line
    // reason why the share is that high. None did when the budget was set (M65, on content/game-v1 at 9dfa08b: the largest was accent-variant-homograph, 4.21 % of the
    // draws of O3; README, "The skip budget"). Lower a budget when a change lowers its share; never raise one to make a change pass.
    private static readonly Dictionary<string, (double Budget, string Reason)> Budgets = new(StringComparer.Ordinal);

    [Fact]
    public void NoReasonOfRejectionTakesMoreThanItsBudgetOfTheDraws()
    {
        var sentences = new FuzzDiagnostics();
        var mutations = new FuzzDiagnostics();
        for (var i = 0; i < FuzzOptions.GrammarDefault; i++)
        {
            // CsCheck seeds only the first case of a run, so the stream is drawn one case per fixed seed (as FuzzLanguageStreamTests does).
            SentenceGen.Recipe(Harness).Sample(recipe => SentenceGen.Realize(Harness, recipe, diagnostics: sentences), iter: 1, threads: 1, seed: Seed('S', i));
            MutationGen.Case.Sample(mutationCase => MutationGen.Build(Harness, mutationCase, mutations), iter: 1, threads: 1, seed: Seed('M', i));
        }

        var streams = new[] { (Name: "O3 sentences", Diagnostics: sentences), (Name: "O4 and O5 mutations", Diagnostics: mutations) };
        output.WriteLine(Table(streams));
        var over = new List<string>();
        foreach (var (name, diagnostics) in streams)
        {
            Assert.True(diagnostics.Draws > FuzzOptions.GrammarDefault, $"The {name} stream drew only {diagnostics.Draws} sentences.");
            foreach (var reason in Reasons(diagnostics))
            {
                var share = Share(diagnostics, reason);
                var budget = Budgets.TryGetValue(reason, out var named) ? named.Budget : DefaultBudget;
                if (share > budget)
                {
                    over.Add($"{reason} took {Percent(share)} of the {diagnostics.Draws} draws of the {name} stream (budget {Percent(budget)})");
                }
            }
        }

        Assert.True(over.Count == 0, $"A reason of rejection takes more than its budget of the draws:\n  {string.Join("\n  ", over)}\n{Table(streams)}");
        foreach (var (reason, (budget, why)) in Budgets)
        {
            Assert.True(budget > DefaultBudget && budget < 1 && !string.IsNullOrWhiteSpace(why), $"The budget of {reason} ({budget}) needs a reason and must be above the default.");
        }
    }

    private static IEnumerable<string> Reasons(FuzzDiagnostics diagnostics)
    {
        return diagnostics.Report()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith(FuzzDiagnostics.RejectedPrefix, StringComparison.Ordinal))
            .Select(static line => line[FuzzDiagnostics.RejectedPrefix.Length..line.IndexOf(" = ", StringComparison.Ordinal)]);
    }

    private static double Share(FuzzDiagnostics diagnostics, string reason)
    {
        return (double)diagnostics.Get(FuzzDiagnostics.RejectedPrefix + reason) / diagnostics.Draws;
    }

    private static string Percent(double share)
    {
        return (share * 100).ToString("0.00", CultureInfo.InvariantCulture) + " %";
    }

    // One row per reason (ordinal order): its share of each stream's draws, and its share of the stream's recipes that gave no sentence after every attempt (skip:).
    private static string Table((string Name, FuzzDiagnostics Diagnostics)[] streams)
    {
        var reasons = streams.SelectMany(static s => Reasons(s.Diagnostics)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var builder = new StringBuilder();
        builder.Append("| Reason |");
        foreach (var (name, _) in streams)
        {
            builder.Append(CultureInfo.InvariantCulture, $" {name}: draws | {name}: recipes |");
        }

        builder.Append('\n').Append("| --- |").Append(string.Concat(Enumerable.Repeat(" ---: | ---: |", streams.Length))).Append('\n');
        foreach (var reason in reasons)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{reason}` |");
            foreach (var (_, diagnostics) in streams)
            {
                // The skip of an unknown literal carries the literal (skip:unknown-literal:<word>); every other skip is the reason alone.
                var skipped = reason == "unknown-literal" ? diagnostics.Sum("skip:unknown-literal:") : diagnostics.Get("skip:" + reason);
                builder.Append(CultureInfo.InvariantCulture, $" {Percent(Share(diagnostics, reason))} | {Percent((double)skipped / diagnostics.Recipes)} |");
            }

            builder.Append('\n');
        }

        builder.Append("| all draws |");
        foreach (var (_, diagnostics) in streams)
        {
            builder.Append(CultureInfo.InvariantCulture, $" {diagnostics.Draws} | {diagnostics.Recipes} |");
        }

        return builder.Append('\n').ToString();
    }

    // The fixed seed of the i-th case of a stream (12 characters, as CsCheck prints them).
    private static string Seed(char stream, int i)
    {
        return "M65" + stream + i.ToString("D8", CultureInfo.InvariantCulture);
    }
}
