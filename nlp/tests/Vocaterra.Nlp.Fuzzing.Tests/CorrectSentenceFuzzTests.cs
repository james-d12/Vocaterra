using CsCheck;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Xunit.Abstractions;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

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

    /// <summary>
    /// A learner who types without accents writes <c>el</c> for <c>él</c> and <c>esta</c> for <c>está</c>, and both spellings are lexicon words (M26b, CR-013). When a correct sentence has
    /// an accented word whose plain spelling is also a lexicon form, and the words next to it leave no doubt which one is meant (an independent model of the rule below), the same sentence
    /// written without that accent must analyze exactly as the accented one does: the same errors at the same places. The cases the neighbours leave open are counted and not asserted,
    /// because a reader cannot tell them apart either (<c>él compra</c> and <c>el compra</c>, where <c>compra</c> is a noun as well).
    /// </summary>
    [Fact]
    public void CorrectSentences_WithAnAccentDroppedWhereTheNeighboursLeaveNoDoubt_AnalyzeAsTheAccentedSentenceDoes()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = SentenceGen.Recipe(Harness);
        var iterations = FuzzOptions.Iterations(FuzzOptions.GrammarVar, FuzzOptions.GrammarDefault);

        gen.Sample(
            (SentenceRecipe recipe) =>
            {
                var sentence = SentenceGen.Realize(Harness, recipe, diagnostics: diagnostics);
                if (sentence is null)
                {
                    return;
                }

                var tokens = sentence.Tokens.Select(static t => t.Norm).ToArray();
                var plain = tokens.ToArray();
                var changed = new List<string>();
                for (var i = 0; i < tokens.Length; i++)
                {
                    var spelling = tokens[i];
                    var folded = Harness.Lexicon.LookupWithAccentVariants(Accents.Strip(spelling));
                    if (spelling == Accents.Strip(spelling) || folded.Exact.Count == 0 || !folded.Variants.Any(v => v.Form == spelling))
                    {
                        continue;
                    }

                    if (!UnaccentedReadingIsContradicted(tokens, i, sentence.Tokens[i].Role))
                    {
                        diagnostics.Count("accent:open-" + Accents.Strip(spelling));
                        continue;
                    }

                    diagnostics.Count("accent:dropped-" + Accents.Strip(spelling));
                    plain[i] = Accents.Strip(spelling);
                    changed.Add(spelling);
                }

                if (changed.Count == 0)
                {
                    return;
                }

                var accented = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', tokens)), []);
                var dropped = Harness.Analyzer.Analyze(PipelineHarness.Normalize(string.Join(' ', plain)), []);
                var context = $"{sentence.Describe()} dropped=[{string.Join(", ", changed)}] unaccented=\"{string.Join(' ', plain)}\" seed={recipe.Seed}";
                var expected = accented.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(Describe).ToArray();
                var actual = dropped.Errors.Where(static e => !ErrorTags.IsPronunciation(e.Tag)).Select(Describe).ToArray();
                if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"Dropping the accent changed the errors: with it [{string.Join("; ", expected)}], without it [{string.Join("; ", actual)}].",
                        context);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Unaccented function word diagnostics:\n" + diagnostics.Report());
        if (iterations >= MinimumIterationsForCoverage)
        {
            // The accented function words must really be drawn and unaccented, or the oracle above would hold for a generator that stopped producing them.
            Assert.True(diagnostics.Sum("accent:dropped-") > 0, $"No accent was ever dropped from a function word.\n{diagnostics.Report()}");
        }
    }

    // The independent model of the resolver's rule for the words the generators draw: a subject pronoun (él, tú) written without its accent is read as the pronoun when the next word
    // cannot go on a noun phrase (no noun, adjective or determiner reading); está and estás written without the accent are the verb when the words after them are not a noun phrase
    // (an article right after, or no noun at the end of the adjectives that follow). Anything else leaves the reader in doubt.
    private static bool UnaccentedReadingIsContradicted(string[] tokens, int index, TokenRole role)
    {
        var spelling = tokens[index];
        if (role == TokenRole.Pronoun && spelling is "él" or "tú")
        {
            return index + 1 < tokens.Length && !CanGoOnANounPhrase(tokens[index + 1]);
        }

        if (role == TokenRole.Verb && spelling is "está" or "estás")
        {
            var next = index + 1;
            if (next >= tokens.Length)
            {
                return true;
            }

            var readings = PartsOfSpeech(tokens[next]);
            if (readings.Count > 0 && readings.All(static pos => pos == LexiconSchema.Article))
            {
                return true;
            }

            // The adjectives, articles and numerals (digits are numerals) after the word belong to the noun phrase that a demonstrative would open.
            var head = next;
            while (head < tokens.Length && PartsOfSpeech(tokens[head]) is { Count: > 0 } found && found.All(static pos => pos is LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral))
            {
                head++;
            }

            return head >= tokens.Length || (PartsOfSpeech(tokens[head]) is { Count: > 0 } headParts && headParts.All(static pos => pos != LexiconSchema.Noun));
        }

        return false;
    }

    private static List<string> PartsOfSpeech(string token)
    {
        return token.Length > 0 && token.All(char.IsAsciiDigit) ? [LexiconSchema.Numeral] : [.. Harness.Lexicon.Lookup(token).Select(static r => r.Pos)];
    }

    private static bool CanGoOnANounPhrase(string token)
    {
        var parts = PartsOfSpeech(token);
        return parts.Count == 0 || parts.Any(static pos => pos is LexiconSchema.Noun or LexiconSchema.Adjective or LexiconSchema.Article or LexiconSchema.Numeral);
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
