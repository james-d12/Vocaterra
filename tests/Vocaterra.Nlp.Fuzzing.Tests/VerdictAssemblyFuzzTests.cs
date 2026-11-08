using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Xunit.Abstractions;
using MatchType = Vocaterra.Nlp.Core.Stages.MatchType;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O10: verdict assembly follows its documented rules for any stage results. Correctness is one minus the weights of the surviving errors,
/// confidence is the minimum of the stage confidences, the communicated flag follows the two documented rules through the margin, errors are
/// unique per tag and span and sorted, the derived vocab.wrong follows its rule (M67, M72, M75: a near miss names its word, the answer's word as its expected
/// text), the off-prompt intent is carried only for a line the prompt did not take (M72), the order of unordered inputs does not matter, and the verdict survives
/// a canonical JSON round trip.
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
                CheckErrors(verdict, inputs, diagnostics);
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

                // M72: the off-prompt intent is the intent stage's for a line the prompt did not take (an unknown intent, no answer matched), which is never communicated.
                var offPrompt = inputs.Intent.Intent.IsUnknown && inputs.Match.Type == MatchType.None ? inputs.Intent.OffPromptIntent : null;
                if (!Nullable.Equals(verdict.OffPromptIntent, offPrompt) || (verdict.OffPromptIntent is not null && verdict.Communicated))
                {
                    throw Determinism.Fail(Oracle, $"The off-prompt intent is '{verdict.OffPromptIntent}' (communicated {verdict.Communicated}); the rule gives '{offPrompt}', never on a communicated line.", inputs);
                }

                diagnostics.Count(verdict.OffPromptIntent is null ? "offPrompt:none" : "offPrompt:set");

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
        Assert.True(diagnostics.Get("offPrompt:set") > 0, "A verdict with an off-prompt intent must be reached (M72).\n" + diagnostics.Report());
        Assert.True(
            diagnostics.Get("derived:near-miss") > 0 && diagnostics.Get("derived:first-content-word") > 0 && diagnostics.Get("derived:near-miss-not-tagged") > 0,
            "A derived vocab.wrong of each kind, and a near miss the rule does not tag, must be reached (M75).\n" + diagnostics.Report());
    }

    private static void CheckErrors(Verdict verdict, VerdictInputs inputs, FuzzDiagnostics diagnostics)
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

        // M75: the derived error names its word. Where no stage error has its tag and span, the verdict carries it as the model gives it: a near miss's word with the
        // answer's word as the expected text; else the first content word with the set's single answer, or no word at a quiz of several answers.
        if (derived is not null && !supplied.Contains((derived.Tag, derived.Span.Start, derived.Span.Length)))
        {
            var carried = verdict.Errors.Single(e => e.Tag == derived.Tag && e.Span == derived.Span);
            if (!carried.Equals(derived))
            {
                throw Determinism.Fail(Oracle, $"The derived vocab.wrong is '{carried.Expected}'/'{carried.Actual}' at {carried.Span}; the rule gives '{derived.Expected}'/'{derived.Actual}'.", inputs);
            }
        }

        diagnostics.Count(derived is null ? "derived:none" : DerivedKind(inputs, derived));
        if (inputs.NearMiss is not null && (derived is null || derived.Span != inputs.NearMiss.Span))
        {
            diagnostics.Count("derived:near-miss-not-tagged");
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

    // The derived error exists when nothing matched, the prompt is constrained and the line is a miss the tag can explain by naming a word (M67, M72, M75):
    // (1) a near miss of an answer: the line resolved no intent or an intent that no slot grammar gave, and the inputs hold its near miss (the one word by which it
    // misses the nearest answer phrase, a slip pair), a word the lexicon knows; the tag is on that word, with the answer's word as its expected text. M67's rule
    // (the distance to the nearest answer within the fuzzy budget for the line's own length) is gone: it read the matcher's distance 0 at a set with no answers as
    // a near miss of every line. (2) Else, a line that resolved no intent at a prompt that names its word (an answer set of one canonical answer, or a quiz: a
    // prompt that allows no intent): the tag is on the first content token when the lexicon knows it, with the set's single canonical answer as its expected
    // text, or none when the set has no single answer (VerdictGen supplies none, the set's answers, or one). A line that resolved no intent far from every one of
    // several answers, or with the answers unknown, is a correct line the prompt does not take and has none (M72). The heard word of a number is the learner's
    // (M70): a token in digits that the learner said in words reports its words.
    private static ErrorTag? DerivedVocabWrong(VerdictInputs inputs)
    {
        if (inputs.Match.Type != MatchType.None || inputs.Prompt.Mode != RecognitionMode.Constrained)
        {
            return null;
        }

        var unknown = inputs.Intent.Intent.IsUnknown;
        if ((unknown || inputs.Intent.Rung != IntentRung.Grammar) && inputs.NearMiss is { } nearMiss && inputs.KnownLexiconWords.Contains(nearMiss.Actual))
        {
            return Heard(new ErrorTag(ErrorTags.VocabWrong, nearMiss.Span, nearMiss.Expected, nearMiss.Actual), inputs);
        }

        var namesTheWord = inputs.CanonicalAnswers?.Count == 1 || inputs.Prompt.AllowedIntentIds.Count == 0;
        if (!unknown || !namesTheWord)
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
            ? Heard(new ErrorTag(ErrorTags.VocabWrong, first.Span, inputs.CanonicalAnswers is { Count: 1 } answers ? answers[0] : string.Empty, first.Text), inputs)
            : null;
    }

    // M70: an error on a token written in digits that the learner said in words reports the learner's words (dos, not 2).
    private static ErrorTag Heard(ErrorTag error, VerdictInputs inputs)
    {
        static bool Digits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
        var token = inputs.Analysis.Tokens.Select(static t => t.Token).FirstOrDefault(t => t.Span == error.Span && t.Text == error.Actual);
        return token is not null && Digits(token.Text) && !Digits(token.Original) ? error with { Actual = token.Original.ToLowerInvariant() } : error;
    }

    private static string DerivedKind(VerdictInputs inputs, ErrorTag derived)
    {
        return inputs.NearMiss is { } nearMiss && nearMiss.Span == derived.Span && nearMiss.Expected == derived.Expected
            ? "derived:near-miss"
            : "derived:first-content-word";
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
