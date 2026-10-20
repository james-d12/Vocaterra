using CsCheck;
using Slg.Core.Stages;
using Slg.Core.Taxonomy;
using Slg.Core.Verdicts;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Slg.Nlp.Lexicon;
using Xunit.Abstractions;
using SlotGrammarType = Slg.Nlp.SlotGrammar.SlotGrammar;

namespace Slg.Fuzzing.Tests;

/// <summary>
/// O3: a sentence realised correctly from a grammar analyzes clean (no grammar errors, every word known), matches its own grammar and
/// yields the slot values it was built from. Noise that cannot change the normalizer's output must not change the analysis either.
/// </summary>
public sealed class CorrectSentenceFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O3";

    // Below this many iterations the chance that a particular catalog grammar is drawn is too small to assert coverage.
    private const int MinimumIterationsForCoverage = 1000;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void CorrectSentences_AnalyzeClean_MatchTheirGrammar_AndYieldTheirSlots()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = Gen.Select(SentenceGen.Recipe(Harness), SentenceGen.SafeNoise, static (recipe, noise) => (recipe, noise));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            ((SentenceRecipe Recipe, TextNoise Noise) input) =>
            {
                var sentence = SentenceGen.Realize(Harness, input.Recipe, diagnostics: diagnostics);
                if (sentence is null)
                {
                    return;
                }

                var grammar = SlotGrammarType.Parse(input.Recipe.Pattern.Render());
                var noisy = input.Noise.Apply(sentence.Text);
                var normalized = PipelineHarness.Normalize(noisy);
                var analysis = Harness.Analyzer.Analyze(normalized, [grammar]);
                var context = $"{sentence.Describe()} noisy=\"{noisy}\" seed={input.Recipe.Seed}";

                var errors = analysis.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).ToArray();
                if (errors.Length > 0)
                {
                    throw Determinism.Fail(Oracle, $"A correct sentence produced grammar errors: {string.Join("; ", errors.Select(Describe))}.", context);
                }

                if (!string.Equals(normalized.Text, sentence.NormalizedText, StringComparison.Ordinal))
                {
                    throw Determinism.Fail(Oracle, $"Noise changed the normalized text: '{normalized.Text}' instead of '{sentence.NormalizedText}'.", context);
                }

                if (!string.Equals(analysis.MatchedGrammarId, grammar.Id, StringComparison.Ordinal))
                {
                    throw Determinism.Fail(Oracle, $"The sentence did not match its own grammar; matched '{analysis.MatchedGrammarId ?? "(none)"}'.", context);
                }

                var slots = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var slot in analysis.Slots)
                {
                    slots.TryAdd(slot.Name, slot.Value);
                }

                if (!SlotsAgree(slots, sentence, input.Recipe.Pattern.HasAmbiguousSlots(Harness.Words.PatternClassesOf) || HomographCompetes(input.Recipe.Pattern, sentence)))
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"Slot values differ. Expected {{{string.Join(", ", sentence.ExpectedSlots.Select(p => $"{p.Key}={p.Value}"))}}} but got {{{string.Join(", ", slots.Select(p => $"{p.Key}={p.Value}"))}}}.",
                        context);
                }

                if (analysis.Confidence != 1.0)
                {
                    throw Determinism.Fail(Oracle, $"Confidence is {analysis.Confidence}, but every word of the sentence is in the lexicon.", context);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        foreach (var pair in Harness.Words.Diagnostics)
        {
            diagnostics.Add(pair.Key, pair.Value);
        }

        output.WriteLine("Sentence generator diagnostics:\n" + diagnostics.Report());
        var realized = diagnostics.Sum("sentence:");
        var skipped = diagnostics.Sum("skip:");
        Assert.True(realized * 10 >= (realized + skipped) * 6, $"Only {realized} of {realized + skipped} recipes could be realised; the generator is no longer honest enough.\n{diagnostics.Report()}");

        // The homographs of the lexicon are generated, not skipped: the analyzer resolves the part of speech of each word (M18).
        Assert.True(diagnostics.HomographSkipsAreRare, $"{diagnostics.HomographSkips} of {diagnostics.Recipes} recipes were skipped as homographs.\n{diagnostics.Report()}");

        if (iterations >= MinimumIterationsForCoverage)
        {
            // Homographs must really be drawn, or the line above would hold for a generator that stopped using them.
            foreach (var kind in new[] { "a-noun-that-is-also-a-verb-form", "a-verb-form-that-is-also-a-noun", "a-noun-of-both-genders" })
            {
                Assert.True(
                    diagnostics.Get($"homograph:sentence-with-{kind}") * 50 >= realized,
                    $"Fewer than one sentence in fifty holds {kind.Replace('-', ' ')}.\n{diagnostics.Report()}");
            }
        }

        // Every grammar word must be in the lexicon: a catalog grammar with an unknown literal (the old `cuál` gap) would be skipped here, and the
        // utterance a learner says for it would be reported as vocab.unknown.
        Assert.True(diagnostics.Sum("skip:unknown-literal") == 0, $"A literal word of a catalog or generated grammar is missing from the lexicon.\n{diagnostics.Report()}");
        if (iterations >= MinimumIterationsForCoverage)
        {
            foreach (var grammar in Harness.Grammars.Grammars)
            {
                Assert.True(diagnostics.Get("catalog:" + grammar.Pattern.Render()) > 0, $"The catalog grammar '{grammar.Pattern.Render()}' was never realised.\n{diagnostics.Report()}");
            }
        }
    }

    // A word that is a noun and also a verb form (van, busca) or an adjective (adolescente) can be given to an optional [VERB] or [ADJ] token as well as to a [NOUN]
    // slot, and a verb that is also a noun to a [NOUN] token, so the matcher may legitimately bind the words in more than one way; this is the same ambiguity as two
    // optional tokens of one class, caused by the word.
    private static bool HomographCompetes(GrammarPattern pattern, GeneratedSentence sentence)
    {
        return (pattern.HasFlexibleTag("VERB") || pattern.HasFlexibleTag("NOUN") || pattern.HasFlexibleTag("ADJ"))
            && sentence.Tokens.Any(token => token.Generated
                && token.Role is TokenRole.Noun or TokenRole.Verb
                && Harness.Lexicon.Lookup(token.Norm) is var readings
                && readings.Select(static r => r.Pos).Where(static pos => pos is LexiconSchema.Noun or LexiconSchema.Verb or LexiconSchema.Adjective).Distinct().Count() > 1);
    }

    // With an ambiguous pattern the matcher may bind the same tokens to the slots in a different but equally valid way, so only the values count.
    private static bool SlotsAgree(Dictionary<string, string> actual, GeneratedSentence sentence, bool ambiguous)
    {
        var expected = sentence.ExpectedSlots;
        if (ambiguous)
        {
            // Optional tokens that compete for the same word may or may not take it, so the only claim left is that every extracted value
            // is the lemma (or the digits) of some word of the sentence.
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in sentence.Tokens)
            {
                candidates.Add(token.Norm);
                if (token.Lemma is not null)
                {
                    candidates.Add(token.Lemma);
                }

                foreach (var reading in Harness.Lexicon.Lookup(token.Norm))
                {
                    candidates.Add(reading.Lemma);
                }
            }

            return actual.Values.All(candidates.Contains);
        }

        return actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static string Describe(ErrorTag error)
    {
        return $"{error.Tag}@{error.Span.Start}+{error.Span.Length} expected '{error.Expected}' actual '{error.Actual}'";
    }
}
