using CsCheck;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O2: the analyzer never crashes on any text, gives the same answer on a fresh instance, and its output is well-formed: errors ordered
/// by span start and tag rank without duplicates, tags from the taxonomy, spans inside the text, confidence in [0, 1].
/// </summary>
public sealed class AnalyzerFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O2";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void Analyze_NeverCrashes_IsDeterministic_AndReturnsWellFormedResults()
    {
        // Free text three times in four; a realised grammar sentence (so the grammars actually match and slots are extracted) otherwise.
        var sentences = SentenceGen.Recipe(Harness).Select(recipe => SentenceGen.Realize(Harness, recipe)?.Text ?? string.Empty);
        var texts = Gen.Frequency((3, (IGen<string>)SpanishTextGen.Text(Harness.Words)), (1, (IGen<string>)sentences));
        var gen = Gen.Select(texts, Gen.Bool, static (text, withGrammars) => (text, withGrammars));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);
        var run = 0;
        var diagnostics = new FuzzDiagnostics();

        gen.Sample(
            ((string Text, bool WithGrammars) input) =>
            {
                var normalized = PipelineHarness.Normalize(input.Text);
                var grammars = input.WithGrammars ? Harness.Bundle.Intents.Grammars : [];
                var context = new { text = input.Text, normalized = normalized.Text, withGrammars = input.WithGrammars };

                AnalysisResult first;
                try
                {
                    first = Harness.Analyzer.Analyze(normalized, grammars);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"Analyze threw {ex.GetType().Name}: {ex.Message}", context);
                }

                CheckWellFormed(first, normalized, grammars.Select(static g => g.Id).ToHashSet(StringComparer.Ordinal), context);
                foreach (var error in first.Errors)
                {
                    diagnostics.Count($"tag:{error.Tag}");
                }

                diagnostics.Count(first.MatchedGrammarId is null ? "grammar:none" : "grammar:matched");

                // Two runs on the shared analyzer always; every eighth iteration also compares against an analyzer built from scratch, because
                // its morphology index is built lazily and hidden state would show there.
                Determinism.AssertSame(Oracle, "Analyze", context, () => first, () => Harness.Analyzer.Analyze(normalized, grammars));
                if (Interlocked.Increment(ref run) % 8 == 0)
                {
                    var fresh = Harness.NewAnalyzer();
                    Determinism.AssertSame(Oracle, "Analyze on a fresh analyzer", context, () => first, () => fresh.Analyze(normalized, grammars));
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Analyzer input diagnostics:\n" + diagnostics.Report());

        // The inputs must keep reaching the analyzer's main error paths, or this oracle is only a crash test.
        foreach (var tag in new[] { ErrorTags.VocabUnknown, ErrorTags.ConjPerson, ErrorTags.GenderAgree, ErrorTags.NounNumber })
        {
            Assert.True(diagnostics.Get($"tag:{tag}") > 0, $"No generated text produced {tag}.\n{diagnostics.Report()}");
        }
    }

    private static void CheckWellFormed(AnalysisResult result, NormalizedText text, IReadOnlySet<string> grammarIds, object context)
    {
        if (result.Tokens.Count != text.Tokens.Count || result.Tokens.Zip(text.Tokens).Any(static pair => pair.First.Token != pair.Second))
        {
            throw Determinism.Fail(Oracle, "The analyzed tokens are not the normalized tokens.", context);
        }

        foreach (var token in result.Tokens)
        {
            foreach (var reading in token.Readings)
            {
                if (reading.Lemma.Length == 0 || !LexiconSchema.PartsOfSpeech.Contains(reading.Pos))
                {
                    throw Determinism.Fail(Oracle, $"Token '{token.Token.Text}' has an invalid reading ({reading.Lemma}/{reading.Pos}).", context);
                }
            }
        }

        if (!(result.Confidence is >= 0.0 and <= 1.0))
        {
            throw Determinism.Fail(Oracle, $"Confidence {result.Confidence} is outside [0, 1].", context);
        }

        var seen = new HashSet<(string Tag, int Start, int Length)>();
        ErrorTag? previous = null;
        foreach (var error in result.Errors)
        {
            if (!ErrorTags.IsKnown(error.Tag))
            {
                throw Determinism.Fail(Oracle, $"Error tag '{error.Tag}' is not in ErrorTags.All.", context);
            }

            if (error.Span.Start < 0 || error.Span.Length <= 0 || error.Span.End > text.Text.Length)
            {
                throw Determinism.Fail(Oracle, $"Error {error.Tag} has span {error.Span} outside the text of length {text.Text.Length}.", context);
            }

            if (!seen.Add((error.Tag, error.Span.Start, error.Span.Length)))
            {
                throw Determinism.Fail(Oracle, $"Two errors share the tag {error.Tag} and span {error.Span}.", context);
            }

            if (!string.Equals(text.Text.Substring(error.Span.Start, error.Span.Length), error.Actual, StringComparison.Ordinal))
            {
                throw Determinism.Fail(Oracle, $"Error {error.Tag} reports actual '{error.Actual}' but its span covers '{text.Text.Substring(error.Span.Start, error.Span.Length)}'.", context);
            }

            if (previous is { } before && (before.Span.Start, Rank(before.Tag)).CompareTo((error.Span.Start, Rank(error.Tag))) > 0)
            {
                throw Determinism.Fail(Oracle, $"Errors are not ordered by span start and tag rank: {before.Tag}@{before.Span.Start} precedes {error.Tag}@{error.Span.Start}.", context);
            }

            previous = error;
        }

        foreach (var slot in result.Slots)
        {
            if (slot.Name.Length == 0 || slot.Span.Start < 0 || slot.Span.Length <= 0 || slot.Span.End > text.Text.Length)
            {
                throw Determinism.Fail(Oracle, $"Slot '{slot.Name}' has an invalid span {slot.Span}.", context);
            }
        }

        if (result.MatchedGrammarId is null ? result.Slots.Count > 0 : !grammarIds.Contains(result.MatchedGrammarId))
        {
            throw Determinism.Fail(Oracle, $"Matched grammar '{result.MatchedGrammarId ?? "(none)"}' does not fit the slots or the grammars supplied.", context);
        }
    }

    private static int Rank(string tag)
    {
        var index = ErrorTags.All.ToList().IndexOf(tag);
        return index < 0 ? int.MaxValue : index;
    }
}
