using CsCheck;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O4, the key oracle: one mutation of a correct sentence yields exactly the error tag it is named after, at the mutated word, with the
/// original form as the correction; and agreement mistakes do not stop the sentence from matching its grammar.
/// </summary>
public sealed class MutationTagFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O4";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void OneMutation_YieldsExactlyTheNamedTag_WithTheOriginalFormAsCorrection()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = Gen.Select(MutationGen.Case, SentenceGen.SafeNoise, static (mutationCase, noise) => (mutationCase, noise));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            ((MutationCase Case, TextNoise Noise) input) =>
            {
                var mutation = MutationGen.Build(Harness, input.Case, diagnostics);
                if (mutation is null)
                {
                    return;
                }

                var noisy = input.Noise.Apply(mutation.Mutated.Text);
                var normalized = PipelineHarness.Normalize(noisy);
                var grammar = SlotGrammarType.Parse(mutation.Source.PatternText);
                var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
                var context = $"{mutation.Describe()} noisy=\"{noisy}\" case={input.Case}";

                // A name the grammar no longer binds is an unknown word for the analyzer (see UnboundNames).
                var names = UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, analysis.MatchedGrammarId is not null);
                if (names is null)
                {
                    diagnostics.Count("unpredicted:name-known-to-lexicon");
                    return;
                }

                var difference = Compare([.. mutation.Errors, .. names], analysis.Errors);
                if (difference is not null && KnownTolerances.IsTiedParse(analysis, grammar))
                {
                    diagnostics.Count($"excused:tied-parse:{mutation.Kind}");
                    return;
                }

                if (difference is not null)
                {
                    throw Determinism.Fail(Oracle, $"The analyzer's errors differ from the mutation's: {difference}.", context);
                }

                if (mutation.KeepsGrammar && !string.Equals(analysis.MatchedGrammarId, grammar.Id, StringComparison.Ordinal))
                {
                    throw Determinism.Fail(Oracle, $"The mutated sentence no longer matches its grammar (matched '{analysis.MatchedGrammarId ?? "(none)"}').", context);
                }

                diagnostics.Count($"tag:{mutation.Tag}");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Mutation generator diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Sum("excused:") * 100 <= diagnostics.Sum("tag:") + diagnostics.Sum("excused:"), $"More than one case in a hundred was excused as a tied parse.\n{diagnostics.Report()}");
        Assert.True(diagnostics.HomographSkipsAreRare, $"{diagnostics.HomographSkips} of {diagnostics.Recipes} recipes were skipped as homographs.\n{diagnostics.Report()}");
        foreach (var kind in Enum.GetValues<MutationKind>())
        {
            var applied = diagnostics.Get($"mutation:{kind}:applied");
            var unavailable = diagnostics.Get($"mutation:{kind}:unavailable");
            Assert.True(
                applied >= 20 && applied >= unavailable,
                $"The generator can no longer reach {kind}: applied {applied}, unavailable {unavailable}.\n{diagnostics.Report()}");
        }
    }

    // Returns null when the expected and actual errors are the same set (tag, span, expected, actual), otherwise a description of the difference.
    internal static string? Compare(IReadOnlyList<ExpectedError> expected, IReadOnlyList<ErrorTag> actual)
    {
        var wanted = expected.Select(static e => (e.Tag, e.Span, e.Expected, e.Actual)).ToHashSet();
        var found = actual.Select(static e => (e.Tag, e.Span, e.Expected, e.Actual)).ToHashSet();
        var missing = wanted.Except(found).ToArray();
        var extra = found.Except(wanted).ToArray();
        if (missing.Length == 0 && extra.Length == 0)
        {
            return null;
        }

        static string Show((string Tag, TextSpan Span, string Expected, string Actual) e) => $"{e.Tag}@{e.Span.Start}+{e.Span.Length} expected '{e.Expected}' actual '{e.Actual}'";
        return $"missing [{string.Join("; ", missing.Select(Show))}] extra [{string.Join("; ", extra.Select(Show))}]";
    }
}
