using CsCheck;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Response.Templates;
using Xunit.Abstractions;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O5, the closed loop: generate a correct sentence, mutate it, analyze it, recast it, and analyze the recast. The recast must be the
/// corrected sentence and must analyze without grammar errors. This is the strongest guard of the evaluator, because the recast builder and
/// the analyzer have to agree on every correction the analyzer names.
/// </summary>
public sealed class RecastLoopFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O5";

    // The recast speaks a numeral 1 as un or una, which the default normalization turns back into 1, so both sides are compared that way.
    private static readonly NormalizationOptions Permissive = new(ConvertIndefiniteArticles: true);

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void Recast_RestoresTheCorrectedSentence_AndReAnalyzesClean()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = MutationGen.Case;
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            (MutationCase mutationCase) =>
            {
                var mutation = MutationGen.Build(Harness, mutationCase, diagnostics);
                if (mutation is null)
                {
                    return;
                }

                var grammar = SlotGrammarType.Parse(mutation.Source.PatternText);
                var normalized = PipelineHarness.Normalize(mutation.Mutated.Text);
                var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
                var context = $"{mutation.Describe()} case={mutationCase}";

                // A name the grammar no longer binds and the lexicon knows (the invented name Mirá is the voseo imperative mirá) is checked like any other word, and the recast
                // corrects what those checks find; the case is not compared, as in O4 (see UnboundNames; seed belmI3pADeia failed on main before M26b).
                if (UnboundNames.Expect(mutation.Mutated.Tokens, normalized, Harness.Words, analysis.MatchedGrammarId is not null) is null)
                {
                    diagnostics.Count("unpredicted:name-known-to-lexicon");
                    return;
                }

                string recast;
                try
                {
                    recast = RecastBuilder.Build(normalized, analysis, SpanishLexicon.Empty, SpanishLanguagePack.Instance);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"RecastBuilder.Build threw {ex.GetType().Name}: {ex.Message}", context);
                }

                // The target is the corrected sentence; for a non-word there is nothing to correct to, so the recast must leave the words alone.
                var target = mutation.CorrectedNormalized ?? mutation.Mutated.NormalizedText;
                var wanted = SpanishTextNormalizer.Normalize(target, Permissive).Text;
                var actual = SpanishTextNormalizer.Normalize(recast, Permissive).Text;
                if (!string.Equals(wanted, actual, StringComparison.Ordinal) && KnownTolerances.IsTiedParse(analysis, grammar))
                {
                    diagnostics.Count($"excused:tied-parse:{mutation.Kind}");
                    return;
                }

                if (!string.Equals(wanted, actual, StringComparison.Ordinal))
                {
                    throw Determinism.Fail(Oracle, $"The recast '{recast}' normalizes to '{actual}' instead of the corrected sentence '{wanted}'.", context);
                }

                if (mutation.CorrectedNormalized is null)
                {
                    diagnostics.Count($"recast:{mutation.Kind}:unchanged");
                    return;
                }

                // A name the grammar no longer binds is an unknown word for the analyzer, and that is the only error the recast may keep for it (see UnboundNames).
                var again = Harness.Analyzer.Analyze(PipelineHarness.Normalize(recast), [grammar]);
                var names = UnboundNames.Expect(mutation.Mutated.Tokens, PipelineHarness.Normalize(recast), Harness.Words, again.MatchedGrammarId is not null);
                if (names is null)
                {
                    diagnostics.Count("unpredicted:name-known-to-lexicon");
                    return;
                }

                var unexpected = again.Errors.Where(e => !names.Any(n => SameError(n, e))).ToList();
                var unreported = names.Where(n => !again.Errors.Any(e => SameError(n, e))).ToList();
                if ((unexpected.Count > 0 || unreported.Count > 0) && KnownTolerances.IsTiedParse(again, grammar))
                {
                    diagnostics.Count($"excused:tied-parse:{mutation.Kind}");
                    return;
                }

                if (unexpected.Count > 0)
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"The recast '{recast}' still has errors: {string.Join("; ", unexpected.Select(static e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length} expected '{e.Expected}' actual '{e.Actual}'"))}.",
                        context);
                }

                if (unreported.Count > 0)
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"The recast '{recast}' does not report its unbound name(s): {string.Join("; ", unreported.Select(static e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length} actual '{e.Actual}'"))}.",
                        context);
                }

                diagnostics.Count($"recast:{mutation.Kind}:closed");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Recast loop diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Sum("excused:") * 100 <= diagnostics.Sum("recast:"), $"More than one case in a hundred was excused as a tied parse.\n{diagnostics.Report()}");
        Assert.True(diagnostics.HomographSkipsAreRare, $"{diagnostics.HomographSkips} of {diagnostics.Recipes} recipes were skipped as homographs.\n{diagnostics.Report()}");
        foreach (var kind in Enum.GetValues<MutationKind>().Where(static k => k != MutationKind.VocabUnknown))
        {
            Assert.True(diagnostics.Get($"recast:{kind}:closed") >= 20, $"The loop was closed fewer than 20 times for {kind}.\n{diagnostics.Report()}");
        }
    }

    // The same tag, span, expected form and actual form.
    private static bool SameError(ExpectedError expected, ErrorTag actual)
    {
        return expected.Tag == actual.Tag && expected.Span == actual.Span && expected.Expected == actual.Expected && expected.Actual == actual.Actual;
    }
}
