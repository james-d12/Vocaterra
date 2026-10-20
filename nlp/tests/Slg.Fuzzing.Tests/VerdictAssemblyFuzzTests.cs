using CsCheck;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Evaluation;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;
using MatchType = Slg.Core.Stages.MatchType;

namespace Slg.Fuzzing.Tests;

/// <summary>
/// O10: verdict assembly follows its documented rules for any stage results. Correctness is one minus the weights of the surviving errors,
/// confidence is the minimum of the stage confidences, the communicated flag follows the two documented rules through the margin, errors are
/// unique per tag and span and sorted, the order of unordered inputs does not matter, and the verdict survives a canonical JSON round trip.
/// </summary>
public sealed class VerdictAssemblyFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O10";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    // Parts of speech that never count as the answer word (mirror of the assembler's documented first-content-token rule).
    private static readonly HashSet<string> FunctionPartsOfSpeech = new(StringComparer.OrdinalIgnoreCase) { "art", "conj", "det", "prep", "pron" };

    [Fact]
    public void Assemble_FollowsTheDocumentedRules_ForAnyStageResults()
    {
        var gen = Gen.Select(VerdictGen.Inputs(Harness), Gen.ULong, static (inputs, seed) => (inputs, seed));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            ((VerdictInputs Inputs, ulong Seed) input) =>
            {
                var inputs = input.Inputs;
                Verdict verdict;
                try
                {
                    verdict = VerdictAssembler.Assemble(inputs);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Assemble threw {ex.GetType().Name}: {ex.Message}", inputs);
                }

                var settings = inputs.Config.For(inputs.Prompt.Difficulty);
                CheckErrors(verdict, inputs);
                CheckSlots(verdict, inputs);
                CheckCorrectness(verdict, inputs, settings);
                CheckConfidence(verdict, inputs);
                CheckCommunicated(verdict, inputs, settings);
                CheckResolvedBy(verdict, inputs);

                if (verdict.Intent != inputs.Intent.Intent
                    || !verdict.Versions.Equals(inputs.Versions)
                    || !verdict.Pronunciation.SequenceEqual(inputs.Pronunciation?.Phones ?? []))
                {
                    throw Determinism.Fail(Oracle, "The verdict does not carry the intent, version stamp and phone scores of its inputs.", inputs);
                }

                // Unordered inputs in another order, and a second assembly, give the identical verdict.
                var permuted = VerdictGen.Permuted(inputs, new SeededRng(input.Seed));
                Determinism.AssertSame(Oracle, "Assemble (shuffled errors, slots and word lists)", inputs, () => verdict, () => VerdictAssembler.Assemble(permuted));

                // The verdict survives canonical JSON byte for byte, and the outcome band can always be derived from it.
                var json = CanonicalJson.Serialize(verdict);
                var back = CanonicalJson.Deserialize<Verdict>(json);
                if (!string.Equals(CanonicalJson.Serialize(back), json, StringComparison.Ordinal) || !back.Equals(verdict))
                {
                    throw Determinism.Fail(Oracle, "The verdict does not round-trip through CanonicalJson.", inputs);
                }

                var band = OutcomeBand.Classify(verdict, inputs.Config, inputs.Prompt.Difficulty);
                if ((band.Kind == OutcomeBandKind.NotUnderstood) == verdict.Communicated)
                {
                    throw Determinism.Fail(Oracle, $"The outcome band {band.Kind} contradicts Communicated={verdict.Communicated}.", inputs);
                }

                diagnostics.Count(verdict.Communicated ? "communicated:yes" : "communicated:no");
                diagnostics.Count($"resolvedBy:{verdict.ResolvedBy}");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Verdict diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("communicated:yes") > 0 && diagnostics.Get("communicated:no") > 0, "Both outcomes of the communicated rule must be reached.\n" + diagnostics.Report());
    }

    private static void CheckErrors(Verdict verdict, VerdictInputs inputs)
    {
        var keys = new HashSet<(string, int, int)>();
        ErrorTag? previous = null;
        foreach (var error in verdict.Errors)
        {
            if (!keys.Add((error.Tag, error.Span.Start, error.Span.Length)))
            {
                throw Determinism.Fail(Oracle, $"Two verdict errors share the tag {error.Tag} and span {error.Span}.", inputs);
            }

            if (previous is { } before && Compare(before, error) > 0)
            {
                throw Determinism.Fail(Oracle, $"Errors are not sorted by span start, tag order and expected text: {before.Tag}@{before.Span.Start} precedes {error.Tag}@{error.Span.Start}.", inputs);
            }

            previous = error;
        }

        // Every (tag, span) of the stage errors survives, and nothing appears except the derived wrong-word error.
        var supplied = inputs.Analysis.Errors.Concat(inputs.Pronunciation?.Errors ?? []).Select(static e => (e.Tag, e.Span.Start, e.Span.Length)).ToHashSet();
        var derived = DerivedVocabWrong(inputs);
        var expected = new HashSet<(string, int, int)>(supplied);
        if (derived is not null)
        {
            expected.Add((derived.Tag, derived.Span.Start, derived.Span.Length));
        }

        if (!keys.SetEquals(expected))
        {
            throw Determinism.Fail(Oracle, $"The verdict errors {Show(keys)} differ from the stage errors plus the derived vocab.wrong {Show(expected)}.", inputs);
        }

        // Among errors that share a tag and span the smallest (expected, actual) wins, whatever the input order.
        foreach (var error in verdict.Errors)
        {
            var candidates = inputs.Analysis.Errors.Concat(inputs.Pronunciation?.Errors ?? [])
                .Where(e => e.Tag == error.Tag && e.Span == error.Span)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            var smallest = candidates.OrderBy(static e => e.Expected, StringComparer.Ordinal).ThenBy(static e => e.Actual, StringComparer.Ordinal).First();
            if (error.Expected != smallest.Expected || error.Actual != smallest.Actual)
            {
                throw Determinism.Fail(Oracle, $"For {error.Tag}@{error.Span.Start} the verdict kept '{error.Expected}'/'{error.Actual}' instead of the smallest '{smallest.Expected}'/'{smallest.Actual}'.", inputs);
            }
        }
    }

    // Documented order: span start, then taxonomy rank (unknown tags last), then expected text; the remaining keys only break ties.
    private static int Compare(ErrorTag left, ErrorTag right)
    {
        var byStart = left.Span.Start.CompareTo(right.Span.Start);
        if (byStart != 0)
        {
            return byStart;
        }

        var byRank = Rank(left.Tag).CompareTo(Rank(right.Tag));
        return byRank != 0 ? byRank : string.CompareOrdinal(left.Expected, right.Expected);
    }

    private static int Rank(string tag)
    {
        var index = ErrorTags.All.ToList().IndexOf(tag);
        return index < 0 ? ErrorTags.All.Count : index;
    }

    private static string Show(IEnumerable<(string Tag, int Start, int Length)> keys)
    {
        return "[" + string.Join("; ", keys.OrderBy(static k => k.Start).ThenBy(static k => k.Tag, StringComparer.Ordinal).Select(static k => $"{k.Tag}@{k.Start}+{k.Length}")) + "]";
    }

    // The derived error exists when nothing matched, the prompt is constrained, and the first content token is a word the lexicon knows.
    private static ErrorTag? DerivedVocabWrong(VerdictInputs inputs)
    {
        if (inputs.Match.Type != MatchType.None || inputs.Prompt.Mode != RecognitionMode.Constrained)
        {
            return null;
        }

        Token? first = null;
        foreach (var analyzed in inputs.Analysis.Tokens)
        {
            var isFunction = analyzed.Readings.Count > 0 && analyzed.Readings.All(r => FunctionPartsOfSpeech.Contains(r.Pos));
            if (isFunction)
            {
                continue;
            }

            var token = analyzed.Token;
            if (first is null || (token.Span.Start, token.Span.Length, token.Text).CompareTo((first.Span.Start, first.Span.Length, first.Text)) < 0)
            {
                first = token;
            }
        }

        return first is not null && inputs.KnownLexiconWords.Contains(first.Text)
            ? new ErrorTag(ErrorTags.VocabWrong, first.Span, inputs.Prompt.AnswerSetId, first.Text)
            : null;
    }

    private static void CheckSlots(Verdict verdict, VerdictInputs inputs)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slot in inputs.Analysis.Slots.OrderBy(static s => s.Name, StringComparer.Ordinal).ThenBy(static s => s.Span.Start).ThenBy(static s => s.Span.Length).ThenBy(static s => s.Value, StringComparer.Ordinal))
        {
            expected.TryAdd(slot.Name, slot.Value);
        }

        foreach (var pair in inputs.Intent.Slots)
        {
            expected[pair.Key] = pair.Value;
        }

        if (verdict.Slots.Count != expected.Count || expected.Any(pair => !verdict.Slots.TryGetValue(pair.Key, out var value) || value != pair.Value))
        {
            throw Determinism.Fail(Oracle, "The verdict slots are not the analysis slots (earliest span wins per name) overlaid by the intent slots.", inputs);
        }
    }

    private static void CheckCorrectness(Verdict verdict, VerdictInputs inputs, DifficultyEvaluationConfig settings)
    {
        var penalty = verdict.Errors.Sum(error => settings.WeightFor(error.Tag));
        var expected = Math.Clamp(1.0 - penalty, 0.0, 1.0);
        if (double.IsNaN(verdict.Correctness) || verdict.Correctness is < 0.0 or > 1.0 || Math.Abs(verdict.Correctness - expected) > 1e-9)
        {
            throw Determinism.Fail(Oracle, $"Correctness {verdict.Correctness} is not 1 - sum of weights = {expected} (within 1e-9) for {verdict.Errors.Count} errors.", inputs);
        }
    }

    private static void CheckConfidence(Verdict verdict, VerdictInputs inputs)
    {
        static double Clean(double value) => double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);
        var expected = Clean(inputs.Asr.Confidence);
        if (inputs.Match.Type != MatchType.None)
        {
            expected = Math.Min(expected, Clean(inputs.Match.Confidence));
        }

        expected = Math.Min(expected, Clean(inputs.Analysis.Confidence));

        // An unknown intent only counts when nothing matched: a quiz prompt has no intent to find.
        if (!inputs.Intent.Intent.IsUnknown || inputs.Match.Type == MatchType.None)
        {
            expected = Math.Min(expected, Clean(inputs.Intent.Confidence));
        }

        if (inputs.Pronunciation is not null)
        {
            expected = Math.Min(expected, Clean(inputs.Pronunciation.Confidence));
        }

        if (double.IsNaN(verdict.Confidence) || verdict.Confidence is < 0.0 or > 1.0 || verdict.Confidence != expected)
        {
            throw Determinism.Fail(Oracle, $"Confidence {verdict.Confidence} is not the minimum of the stage confidences, {expected}.", inputs);
        }
    }

    private static void CheckCommunicated(Verdict verdict, VerdictInputs inputs, DifficultyEvaluationConfig settings)
    {
        static double Clean(double value) => double.IsNaN(value) ? 0.0 : Math.Clamp(value, 0.0, 1.0);
        var margin = new Margin(settings.CommunicatedConfidenceThreshold, settings.Epsilon);
        var byIntent = !inputs.Intent.Intent.IsUnknown
            && inputs.RequiredSlots.All(name => verdict.Slots.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
            && margin.Evaluate(verdict.Confidence) != MarginDecision.Reject;
        var byAnswer = inputs.Match.Type != MatchType.None && margin.Evaluate(Clean(inputs.Match.Confidence)) != MarginDecision.Reject;
        if (verdict.Communicated != (byIntent || byAnswer))
        {
            throw Determinism.Fail(Oracle, $"Communicated is {verdict.Communicated} but the documented rule gives {byIntent || byAnswer} (intent rule {byIntent}, answer rule {byAnswer}).", inputs);
        }
    }

    private static void CheckResolvedBy(Verdict verdict, VerdictInputs inputs)
    {
        var expected = inputs.Match.Type switch
        {
            MatchType.Exact => ResolvedBy.Exact,
            MatchType.Alias => ResolvedBy.Alias,
            MatchType.Fuzzy => ResolvedBy.Fuzzy,
            _ when inputs.Intent.Intent.IsUnknown => ResolvedBy.Unresolved,
            _ => inputs.Intent.Rung switch
            {
                IntentRung.Grammar => ResolvedBy.Grammar,
                IntentRung.Rules => ResolvedBy.Rules,
                IntentRung.Embedding => ResolvedBy.Embedding,
                _ => ResolvedBy.Unresolved,
            },
        };
        if (verdict.ResolvedBy != expected)
        {
            throw Determinism.Fail(Oracle, $"ResolvedBy is {verdict.ResolvedBy} but the match type {inputs.Match.Type} and rung {inputs.Intent.Rung} give {expected}.", inputs);
        }
    }
}
