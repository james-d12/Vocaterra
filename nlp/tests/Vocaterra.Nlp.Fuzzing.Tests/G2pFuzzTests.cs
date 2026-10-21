using CsCheck;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pronunciation;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O8: the grapheme-to-phoneme conversion. Phones are tokens of the inventory; the syllable spans partition each word of the normalized text in
/// order and own the phones in order; every word has exactly one stressed syllable; the result is deterministic and its IPA string splits back
/// into the same phones; dialects differ only at the sites the dialect switches control; and every predicted-error variant is one edit away from
/// the base phones (or only moves the stress) and names a pronunciation tag.
/// </summary>
public sealed class G2pFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O8";

    // The predicted-error variants of a text cost one copy of the phone list each, so the variant checks run on texts up to this many phones.
    private const int MaxPhonesForVariants = 200;

    // The sites the dialect switches may change: seseo (θ, s), voiced s (z), aspiration (h), yeísmo and the sound of ll/y (ʎ ʝ ʃ ʒ). The plan lists
    // θ s ʎ ʝ h; z (voiced s) and ʃ ʒ (rioplatense) are added because the shipped dialects produce them.
    private static readonly HashSet<string> DialectSites = [PhoneInventory.Theta, PhoneInventory.S, PhoneInventory.VoicedS, PhoneInventory.H, PhoneInventory.PalatalLateral, PhoneInventory.Yod, PhoneInventory.Sh, PhoneInventory.Zh];

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void Phonemize_ProducesWellFormedDeterministicResults_ForAnyTextAndDialect()
    {
        var diagnostics = new FuzzDiagnostics();
        var gen = Gen.Select(SpanishTextGen.Text(Harness.Words), PhoneGen.Dialect, PhoneGen.Dialect, static (text, first, second) => (text, first, second));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        gen.Sample(
            ((string Text, DialectConfig First, DialectConfig Second) generated) =>
            {
                var (text, first, second) = generated;
                var input = new { text, first, second };
                var result = Phonemize(text, first, input);
                CheckStructure(result, input);
                Determinism.AssertSame(Oracle, "SpanishG2p.Phonemize", input, () => result, () => SpanishG2p.Phonemize(text, first));

                var other = Phonemize(text, second, input);
                CheckStructure(other, input);
                CheckDialectsDifferOnlyAtDialectSites(result, other, input);

                var all = SpanishG2p.PhonemizeAll(text);
                CheckDistinctVariants(all, text, input);

                if (result.Phones.Count <= MaxPhonesForVariants)
                {
                    CheckPredictedErrors(result, input);
                    diagnostics.Count("predicted-errors:checked");
                }
                else
                {
                    diagnostics.Count("predicted-errors:skipped-long");
                }

                diagnostics.Count(result.Phones.Count == 0 ? "result:empty" : "result:phones");
                diagnostics.Count($"variants:{Math.Min(all.Count, 3)}");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("G2P diagnostics:\n" + diagnostics.Report());
        Assert.True(diagnostics.Get("result:phones") > iterations / 2, $"Most generated texts must produce phones.\n{diagnostics.Report()}");
        Assert.True(diagnostics.Get("variants:2") + diagnostics.Get("variants:3") > 0, $"Some text must have several dialect variants.\n{diagnostics.Report()}");
        Assert.True(diagnostics.Get("predicted-errors:checked") > iterations / 2, $"Predicted errors must be checked for most texts.\n{diagnostics.Report()}");
    }

    private static G2pResult Phonemize(string text, DialectConfig dialect, object input)
    {
        try
        {
            return SpanishG2p.Phonemize(text, dialect);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"Phonemize threw {ex.GetType().Name}: {ex.Message}", input);
        }
    }

    private static void CheckStructure(G2pResult result, object input)
    {
        foreach (var phone in result.Phones)
        {
            if (!PhoneInventory.IsKnown(phone))
            {
                throw Determinism.Fail(Oracle, $"The phone '{phone}' is not in the inventory.", input);
            }
        }

        // Words of the normalized text (single spaces), with their spans.
        var words = new List<TextSpan>();
        var start = 0;
        foreach (var word in result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            words.Add(new TextSpan(start, word.Length));
            start += word.Length + 1;
        }

        if (result.Text.Length > 0 && string.Join(' ', result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) != result.Text)
        {
            throw Determinism.Fail(Oracle, $"The normalized text '{result.Text}' is not single-space separated words.", input);
        }

        // Phones are partitioned by the syllables, in order.
        var nextPhone = 0;
        foreach (var syllable in result.Syllables)
        {
            if (syllable.PhoneStart != nextPhone || syllable.PhoneCount < 0)
            {
                throw Determinism.Fail(Oracle, $"Syllable phone ranges do not partition the phones: a syllable starts at {syllable.PhoneStart}, expected {nextPhone}.", input);
            }

            nextPhone += syllable.PhoneCount;
        }

        if (nextPhone != result.Phones.Count)
        {
            throw Determinism.Fail(Oracle, $"The syllables own {nextPhone} phones but there are {result.Phones.Count}.", input);
        }

        // Syllable spans partition each word's letters in order, and each word has exactly one stressed syllable.
        var previousWord = -1;
        var index = 0;
        while (index < result.Syllables.Count)
        {
            var wordIndex = result.Syllables[index].WordIndex;
            if (wordIndex <= previousWord || wordIndex >= words.Count)
            {
                throw Determinism.Fail(Oracle, $"Syllable {index} belongs to word {wordIndex}, after word {previousWord} (the text has {words.Count} words).", input);
            }

            previousWord = wordIndex;
            var word = words[wordIndex];
            var cursor = word.Start;
            var stressed = 0;
            var last = index;
            while (last < result.Syllables.Count && result.Syllables[last].WordIndex == wordIndex)
            {
                var span = result.Syllables[last].GraphemeSpan;
                if (span.Start != cursor || span.Length <= 0)
                {
                    throw Determinism.Fail(Oracle, $"Syllable {last} of word {wordIndex} covers {span} but the letters continue at {cursor}.", input);
                }

                cursor = span.End;
                stressed += result.Syllables[last].Stressed ? 1 : 0;
                last++;
            }

            if (cursor != word.End)
            {
                throw Determinism.Fail(Oracle, $"The syllables of word {wordIndex} ('{result.Text.Substring(word.Start, word.Length)}') cover up to {cursor}, not to {word.End}.", input);
            }

            if (stressed != 1)
            {
                throw Determinism.Fail(Oracle, $"Word {wordIndex} ('{result.Text.Substring(word.Start, word.Length)}') has {stressed} stressed syllables.", input);
            }

            index = last;
        }

        var firstStressed = -1;
        for (var s = 0; s < result.Syllables.Count; s++)
        {
            if (result.Syllables[s].Stressed)
            {
                firstStressed = s;
                break;
            }
        }

        if (result.StressedSyllableIndex != firstStressed)
        {
            throw Determinism.Fail(Oracle, $"StressedSyllableIndex is {result.StressedSyllableIndex} but the first stressed syllable is {firstStressed}.", input);
        }

        // Every phone has a grapheme span inside its text; the silent letters are h's.
        if (result.PhoneSpans.Count != result.Phones.Count)
        {
            throw Determinism.Fail(Oracle, $"{result.PhoneSpans.Count} phone spans for {result.Phones.Count} phones.", input);
        }

        foreach (var span in result.PhoneSpans)
        {
            if (span.Start < 0 || span.Length <= 0 || span.End > result.Text.Length)
            {
                throw Determinism.Fail(Oracle, $"A phone span {span} lies outside the text of length {result.Text.Length}.", input);
            }
        }

        foreach (var silent in result.SilentLetters)
        {
            if (silent.Span.Start < 0 || silent.Span.End > result.Text.Length || result.Text.Substring(silent.Span.Start, silent.Span.Length) != "h" || silent.PhoneIndex < 0 || silent.PhoneIndex > result.Phones.Count)
            {
                throw Determinism.Fail(Oracle, $"Silent letter {silent.Span} at phone {silent.PhoneIndex} is not an h inside the text.", input);
            }
        }

        CheckIpaString(result, input);
    }

    // The display string drops nothing: removing its stress marks, syllable dots and word spaces and splitting the rest gives the phones back.
    // The one pair whose concatenation reads as a different token is t followed by ʃ (it spells the affricate tʃ); the inventory documentation says
    // phone sequences are lists for that reason, so a result with that pair is exempt from the split.
    private static void CheckIpaString(G2pResult result, object input)
    {
        var ipa = result.ToIpaString();
        var stressMarks = ipa.Count(static c => c == PhoneInventory.StressMark[0]);
        var wordsWithSyllables = result.Syllables.Select(static s => s.WordIndex).Distinct().Count();
        if (stressMarks != wordsWithSyllables)
        {
            throw Determinism.Fail(Oracle, $"The IPA string '{ipa}' has {stressMarks} stress marks for {wordsWithSyllables} words.", input);
        }

        var joined = PhoneInventory.Join(result.Phones);
        var stripped = ipa.Replace(PhoneInventory.StressMark, string.Empty, StringComparison.Ordinal).Replace(PhoneInventory.SyllableSeparator, string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!string.Equals(stripped, joined, StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, $"The IPA string '{ipa}' does not spell the phones '{joined}'.", input);
        }

        var ambiguous = false;
        for (var i = 1; i < result.Phones.Count; i++)
        {
            ambiguous |= result.Phones[i - 1] == PhoneInventory.T && result.Phones[i] == PhoneInventory.Sh;
        }

        if (!ambiguous && !PhoneInventory.Tokenize(stripped).SequenceEqual(result.Phones))
        {
            throw Determinism.Fail(Oracle, $"Tokenize('{stripped}') does not give the phones back.", input);
        }
    }

    // Two dialects see the same letters in the same syllables; their phones differ only where a dialect switch acts.
    private static void CheckDialectsDifferOnlyAtDialectSites(G2pResult left, G2pResult right, object input)
    {
        if (left.Text != right.Text || left.Phones.Count != right.Phones.Count || left.Syllables.Count != right.Syllables.Count)
        {
            throw Determinism.Fail(Oracle, $"Two dialects produced different shapes: {left.Phones.Count} and {right.Phones.Count} phones, {left.Syllables.Count} and {right.Syllables.Count} syllables.", input);
        }

        for (var i = 0; i < left.Syllables.Count; i++)
        {
            if (left.Syllables[i].GraphemeSpan != right.Syllables[i].GraphemeSpan || left.Syllables[i].Stressed != right.Syllables[i].Stressed || left.Syllables[i].PhoneCount != right.Syllables[i].PhoneCount)
            {
                throw Determinism.Fail(Oracle, $"Two dialects syllabified or stressed the text differently at syllable {i}.", input);
            }
        }

        for (var i = 0; i < left.Phones.Count; i++)
        {
            if (left.Phones[i] != right.Phones[i] && !(DialectSites.Contains(left.Phones[i]) && DialectSites.Contains(right.Phones[i])))
            {
                throw Determinism.Fail(Oracle, $"Dialects differ at phone {i}: '{left.Phones[i]}' versus '{right.Phones[i]}', which is not a dialect site.", input);
            }
        }
    }

    private static void CheckDistinctVariants(IReadOnlyList<G2pResult> all, string text, object input)
    {
        if (all.Count == 0 || all.Count > DialectConfig.AllShipped.Count)
        {
            throw Determinism.Fail(Oracle, $"PhonemizeAll returned {all.Count} variants for {DialectConfig.AllShipped.Count} dialects.", input);
        }

        if (!all[0].Equals(SpanishG2p.Phonemize(text, DialectConfig.AllShipped[0])))
        {
            throw Determinism.Fail(Oracle, "The first variant is not the primary dialect's.", input);
        }

        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                if (all[i].Phones.SequenceEqual(all[j].Phones) && all[i].Syllables.SequenceEqual(all[j].Syllables))
                {
                    throw Determinism.Fail(Oracle, $"Variants {i} and {j} are the same realization.", input);
                }
            }
        }
    }

    // Every predicted error is one edit from the base (a substitution, an insertion), or the same phones with the stress moved; each names a
    // pronunciation tag; the list is ordered by position and has no duplicates.
    private static void CheckPredictedErrors(G2pResult result, object input)
    {
        var variants = PredictedErrorVariants.Generate(result);
        PredictedErrorVariant? previous = null;
        foreach (var variant in variants)
        {
            if (!ErrorTags.IsPronunciation(variant.ErrorTag) || !ErrorTags.All.Contains(variant.ErrorTag))
            {
                throw Determinism.Fail(Oracle, $"A predicted error names '{variant.ErrorTag}', which is not a pronunciation tag.", input);
            }

            if (variant.GraphemeSpan.Start < 0 || variant.GraphemeSpan.End > result.Text.Length)
            {
                throw Determinism.Fail(Oracle, $"The span {variant.GraphemeSpan} of a {variant.ErrorTag} variant is outside the text.", input);
            }

            if (previous is not null && variant.GraphemeSpan.Start < previous.GraphemeSpan.Start)
            {
                throw Determinism.Fail(Oracle, $"Predicted errors are not ordered by position: {previous.GraphemeSpan} before {variant.GraphemeSpan}.", input);
            }

            if (previous is not null && previous.Equals(variant))
            {
                throw Determinism.Fail(Oracle, $"The {variant.ErrorTag} variant at {variant.GraphemeSpan} is listed twice.", input);
            }

            previous = variant;

            if (variant.ErrorTag == ErrorTags.PronStress)
            {
                if (!variant.Phones.SequenceEqual(result.Phones)
                    || variant.StressedSyllableIndex < 0
                    || variant.StressedSyllableIndex >= result.Syllables.Count
                    || result.Syllables[variant.StressedSyllableIndex].Stressed
                    || result.Syllables[variant.StressedSyllableIndex].GraphemeSpan != variant.GraphemeSpan)
                {
                    throw Determinism.Fail(Oracle, $"A pron.stress variant at {variant.GraphemeSpan} must keep the phones and name another syllable of the word.", input);
                }

                continue;
            }

            if (variant.StressedSyllableIndex != -1)
            {
                throw Determinism.Fail(Oracle, $"A {variant.ErrorTag} variant carries a stressed syllable index.", input);
            }

            var alignment = PhoneAligner.Align(variant.Phones, result.Phones);
            if (alignment.Distance != 1)
            {
                throw Determinism.Fail(Oracle, $"A {variant.ErrorTag} variant at {variant.GraphemeSpan} is {alignment.Distance} edits from the base phones, not one.", input);
            }

            var edit = alignment.Steps.Single(static step => step.Op != AlignmentOp.Match);
            bool ok;
            if (variant.ErrorTag == ErrorTags.PronR)
            {
                ok = edit.Op == AlignmentOp.Substitute && variant.Phones[edit.HypIndex] == PhoneInventory.EnglishR && result.Phones[edit.RefIndex] is PhoneInventory.Tap or PhoneInventory.Trill;
            }
            else if (variant.ErrorTag == ErrorTags.PronSilentH)
            {
                ok = edit.Op == AlignmentOp.Insert && variant.Phones[edit.HypIndex] == PhoneInventory.H;
            }
            else
            {
                ok = edit.Op == AlignmentOp.Substitute && IsVowelReduction(result.Phones[edit.RefIndex], variant.Phones[edit.HypIndex]);
            }

            if (!ok)
            {
                throw Determinism.Fail(Oracle, $"A {variant.ErrorTag} variant at {variant.GraphemeSpan} has the edit {edit.Op} ({(edit.RefIndex >= 0 ? result.Phones[edit.RefIndex] : "-")} -> {(edit.HypIndex >= 0 ? variant.Phones[edit.HypIndex] : "-")}), which that tag does not describe.", input);
            }
        }
    }

    private static bool IsVowelReduction(string expected, string heard)
    {
        return (expected, heard) is (PhoneInventory.A, PhoneInventory.Schwa) or (PhoneInventory.E, PhoneInventory.EnglishEi) or (PhoneInventory.O, PhoneInventory.EnglishOu);
    }
}
