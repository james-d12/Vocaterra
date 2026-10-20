using System.Diagnostics;
using CsCheck;
using Vocaterra.Nlp.Core.Configuration;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pronunciation;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O9: pronunciation scoring. Posteriors that spell the reference score 1 everywhere and tag nothing (both scorers); one predicted learner error
/// gives exactly that tag at that grapheme span; every score and confidence is a finite number in [0, 1]; scoring is deterministic across fresh
/// instances; and the CTC forced alignment returns ordered, disjoint, non-empty segments, one per reference phone.
/// </summary>
/// <remarks>
/// Scoring cost grows with the cube of the phone count (see the README), so the cases are bounded: inventory sequences are 1 to
/// <see cref="PhoneGen.MaxSequenceLength"/> phones and phrases at most <see cref="PhoneGen.MaxSpokenPhones"/>.
/// </remarks>
public sealed class PronunciationFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O9";

    // Masses this close to one half leave the winner of the frame to float rounding, so no claim is made about them.
    private const double MassMargin = 0.02;

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void AnyPhoneSequence_ScoresInRange_Deterministically_AndAlignsInOrder()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.SignalVar, FuzzOptions.SignalDefault);

        PhoneGen.SequenceCase.Sample(
            c =>
            {
                var fake = FakePhonemeRecognizer.ForSpanish();
                var reference = c.Reference;
                var posteriors = c.Posteriors(fake);
                var predicted = PronunciationScorer.PredictedErrorsFor([reference]);

                var result = Score(new PronunciationScorer(), posteriors, [reference], predicted, c.Difficulty, c.Mode, c);
                CheckWellFormed(result, reference, posteriors, c);
                CheckAlignment(posteriors, reference.Phones, c);

                Determinism.AssertSame(
                    Oracle,
                    "Pronunciation scoring",
                    c,
                    () => result,
                    () => new PronunciationScorer().Score(c.Posteriors(FakePhonemeRecognizer.ForSpanish()), [c.Reference], PronunciationScorer.PredictedErrorsFor([c.Reference]), c.Difficulty, c.Mode, "fuzz-phoneme"));

                // Posteriors that spell the reference score 1 everywhere, in both modes.
                if (c.IsPerfect || (c.Confusion == 0 && c.Mumble is { } m && m.TargetMass > 0.5 + MassMargin && !c.CollapsesEqualNeighbours))
                {
                    CheckPerfect(result, c);
                    diagnostics.Count("perfect:" + c.Mode);
                }
                else if (c.Mumble is null && c.Confusion is >= 1 and <= 5 && c.Spoken.SequenceEqual(c.Phones) is false)
                {
                    // A systematic substitution: every replaced phone is heard as something else, so its score is low.
                    CheckSubstitutedPhonesScoreLow(result, c);
                    diagnostics.Count("substituted:" + c.Mode);
                }
                else if (c.Mumble is { } low && low.TargetMass < 0.5 - MassMargin && c.Confusion == 0 && !c.CollapsesEqualNeighbours)
                {
                    if (result.WordScore >= 1.0)
                    {
                        throw Determinism.Fail(Oracle, $"A phone whose intended sound has only {low.TargetMass:F3} of the mass still scored a perfect word.", c);
                    }

                    diagnostics.Count("mumbled:" + c.Mode);
                }

                diagnostics.Count("mode:" + c.Mode);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Phone sequence diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(10, iterations / 100));
        foreach (var mode in Enum.GetValues<ScoringMode>())
        {
            Assert.True(diagnostics.Get("perfect:" + mode) >= minimum, $"Too few perfect cases for {mode}.\n{diagnostics.Report()}");
            Assert.True(diagnostics.Get("substituted:" + mode) >= minimum, $"Too few substitution cases for {mode}.\n{diagnostics.Report()}");
            Assert.True(diagnostics.Get("mumbled:" + mode) >= minimum, $"Too few mumble cases for {mode}.\n{diagnostics.Report()}");
        }
    }

    [Fact]
    public void SpokenPhrases_ScorePerfectly_OrYieldExactlyTheInjectedPredictedError()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.SignalVar, FuzzOptions.SignalDefault);

        PhoneGen.SpokenCase(Harness).Sample(
            c =>
            {
                var variants = SpanishG2p.PhonemizeAll(c.Text);
                var spokenVariant = variants[c.SpokenDialect % variants.Count];
                if (spokenVariant.Phones.Count is 0 or > PhoneGen.MaxSpokenPhones)
                {
                    diagnostics.Count(spokenVariant.Phones.Count == 0 ? "skip:no-phones" : "skip:too-long");
                    return;
                }

                var fake = FakePhonemeRecognizer.ForSpanish();
                var predicted = PronunciationScorer.PredictedErrorsFor(variants);

                // The error the learner makes: one of the spoken variant's own predicted errors (pron.stress is not audible in phone posteriors).
                // The index picks the kind first (r, silent h, vowel in turn) so the rarer kinds are not drowned out by the many vowels.
                var injectable = PredictedErrorVariants.Generate(spokenVariant).Where(static v => v.ErrorTag != ErrorTags.PronStress).ToArray();
                PredictedErrorVariant? error = null;
                if (c.ErrorIndex >= 0 && injectable.Length > 0)
                {
                    var preferred = new[] { ErrorTags.PronR, ErrorTags.PronSilentH, ErrorTags.PronVowel }[c.ErrorIndex % 3];
                    var ofKind = injectable.Where(v => v.ErrorTag == preferred).ToArray();
                    var pool = ofKind.Length > 0 ? ofKind : injectable;
                    error = pool[(c.ErrorIndex / 3) % pool.Length];
                }

                var heard = error?.Phones ?? spokenVariant.Phones;

                PhonePosteriors posteriors;
                if (c.Mumble is { } mumble)
                {
                    var index = mumble.Index % heard.Count;
                    var alternative = mumble.Alternative == heard[index] ? PhoneInventory.All.First(p => p != heard[index]) : mumble.Alternative;
                    posteriors = fake.FromMumbled(heard, index, alternative, mumble.TargetMass, c.Shape.FramesPerPhone, c.Shape.BlankFramesBetween, c.Shape.EdgeBlankFrames);
                }
                else
                {
                    posteriors = fake.FromPhones(heard, c.Shape.FramesPerPhone, c.Shape.BlankFramesBetween, edgeBlankFrames: c.Shape.EdgeBlankFrames);
                }

                var result = Score(new PronunciationScorer(), posteriors, variants, predicted, c.Difficulty, c.Mode, c);
                foreach (var tag in result.Errors)
                {
                    if (tag.Tag == ErrorTags.PronStress || !ErrorTags.IsPronunciation(tag.Tag))
                    {
                        throw Determinism.Fail(Oracle, $"The scorer reported '{tag.Tag}', which is not a v1 phone-level pronunciation tag.", c);
                    }

                    if (tag.Span.Start < 0 || tag.Span.Length < 0 || tag.Span.End > spokenVariant.Text.Length)
                    {
                        throw Determinism.Fail(Oracle, $"The error span {tag.Span} lies outside the text of length {spokenVariant.Text.Length}.", c);
                    }
                }

                Determinism.AssertSame(
                    Oracle,
                    "Pronunciation scoring of a phrase",
                    c,
                    () => result,
                    () => new PronunciationScorer().Score(posteriors, SpanishG2p.PhonemizeAll(c.Text), PronunciationScorer.PredictedErrorsFor(SpanishG2p.PhonemizeAll(c.Text)), c.Difficulty, c.Mode, "fuzz-phoneme"));

                var collapses = c.Shape.BlankFramesBetween == 0 && Enumerable.Range(1, heard.Count - 1).Any(i => heard[i] == heard[i - 1]);
                if (c.Mumble is not null || collapses)
                {
                    diagnostics.Count("unclaimed:mumble-or-collapse");
                    return;
                }

                if (error is null)
                {
                    // The learner says exactly what the dialect variant says: every phone scores 1 and nothing is tagged.
                    if (result.Phones.Any(static p => p.Score != 1.0) || result.Errors.Count > 0 || result.WordScore != 1.0)
                    {
                        throw Determinism.Fail(Oracle, $"Posteriors that spell the reference gave scores [{string.Join(' ', result.Phones.Select(static p => p.Score))}] and tags [{string.Join(' ', result.Errors.Select(static e => e.Tag))}].", c);
                    }

                    if (result.Phones.Count != spokenVariant.Phones.Count || !result.Phones.Select(static p => p.Expected).SequenceEqual(spokenVariant.Phones))
                    {
                        throw Determinism.Fail(Oracle, "The scored phones are not the spoken variant's phones.", c);
                    }

                    diagnostics.Count("perfect:" + c.Mode);
                    return;
                }

                // When the scorer picked the variant the learner started from, the reference is unambiguous and exactly the injected error must come
                // out. (A learner error can sit closer to another dialect's variant, for example an inserted h next to an aspirated s; then the
                // scorer legitimately explains the speech with that variant and no claim is made.)
                if (!result.Phones.Select(static p => p.Expected).SequenceEqual(spokenVariant.Phones))
                {
                    diagnostics.Count("unclaimed:another-dialect-variant-chosen");
                    return;
                }

                var expectedCount = result.Errors.Count(e => e.Tag == error.ErrorTag && e.Span == error.GraphemeSpan);
                if (result.Errors.Count != 1 || expectedCount != 1)
                {
                    throw Determinism.Fail(
                        Oracle,
                        $"Injected {error.ErrorTag} at {error.GraphemeSpan} (heard [{string.Join(' ', heard)}] for [{string.Join(' ', spokenVariant.Phones)}]) but the scorer reported [{string.Join("; ", result.Errors.Select(static e => $"{e.Tag}@{e.Span} '{e.Expected}' -> '{e.Actual}'"))}].",
                        c);
                }

                var tagged = result.Errors[0];
                if (error.ErrorTag == ErrorTags.PronSilentH ? tagged.Expected.Length != 0 || tagged.Actual != PhoneInventory.H : tagged.Expected.Length == 0 || tagged.Actual.Length == 0 || tagged.Expected == tagged.Actual)
                {
                    throw Determinism.Fail(Oracle, $"The {error.ErrorTag} tag carries expected '{tagged.Expected}' and actual '{tagged.Actual}'.", c);
                }

                diagnostics.Count("injected:" + error.ErrorTag);
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Spoken phrase diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(10, iterations / 100));
        foreach (var mode in Enum.GetValues<ScoringMode>())
        {
            Assert.True(diagnostics.Get("perfect:" + mode) >= minimum, $"Too few perfectly spoken phrases for {mode}.\n{diagnostics.Report()}");
        }

        foreach (var tag in new[] { ErrorTags.PronR, ErrorTags.PronVowel, ErrorTags.PronSilentH })
        {
            Assert.True(diagnostics.Get("injected:" + tag) >= minimum, $"Too few injected {tag} errors.\n{diagnostics.Report()}");
        }
    }

    /// <summary>
    /// Prints the measured cost of scoring a transcript of growing length (the README table). Set <c>VOCATERRA_FUZZ_COST_CURVE=1</c> to run it; a plain
    /// run does nothing, because the long rows take seconds.
    /// </summary>
    [Fact]
    public void CostCurve_IsPrintedOnRequest()
    {
        if (EnvironmentSettings.Get("VOCATERRA_FUZZ_COST_CURVE", "SLGFUZZ_COST_CURVE") != "1")
        {
            return;
        }

        var fake = FakePhonemeRecognizer.ForSpanish();
        var scorer = new PronunciationScorer();
        foreach (var characters in new[] { 25, 50, 100, 200, 400, 800 })
        {
            var text = string.Concat(Enumerable.Repeat("hola amigo ", 100))[..characters].Trim();
            var watch = Stopwatch.StartNew();
            var variants = SpanishG2p.PhonemizeAll(text);
            var g2p = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var predicted = PronunciationScorer.PredictedErrorsFor(variants);
            var predictedFor = watch.Elapsed.TotalMilliseconds;
            var posteriors = fake.FromPhones(variants[0].Phones);
            watch.Restart();
            _ = scorer.Score(posteriors, variants, predicted, Difficulty.Beginner, ScoringMode.Gop, "cost-curve");
            var gop = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            foreach (var variant in predicted)
            {
                _ = PhoneAligner.Align(variant.Phones, variants[0].Phones);
            }

            var align = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            _ = CtcForcedAligner.Align(posteriors, variants[0].Phones);
            var viterbi = watch.Elapsed.TotalMilliseconds;
            output.WriteLine($"chars={characters} phones={variants[0].Phones.Count} predicted={predicted.Count} g2p={g2p:F1}ms PredictedErrorsFor={predictedFor:F1}ms Score(Gop)={gop:F1}ms PhoneAligner.Align x{predicted.Count}={align:F1}ms CtcForcedAligner={viterbi:F1}ms");
        }
    }

    private static PronunciationResult Score(
        PronunciationScorer scorer,
        PhonePosteriors posteriors,
        IReadOnlyList<G2pResult> variants,
        IReadOnlyList<PredictedErrorVariant> predicted,
        Difficulty difficulty,
        ScoringMode mode,
        object input)
    {
        try
        {
            return scorer.Score(posteriors, variants, predicted, difficulty, mode, "fuzz-phoneme");
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"The {mode} scorer threw {ex.GetType().Name}: {ex.Message}", input);
        }
    }

    // Every number is finite and in range; the phones scored are the reference's; the tags are sorted and unique.
    private static void CheckWellFormed(PronunciationResult result, G2pResult reference, PhonePosteriors posteriors, PhoneSequenceCase c)
    {
        if (result.Phones.Count != reference.Phones.Count)
        {
            throw Determinism.Fail(Oracle, $"{result.Phones.Count} phone scores for {reference.Phones.Count} reference phones.", c);
        }

        var total = 0.0;
        for (var i = 0; i < result.Phones.Count; i++)
        {
            var phone = result.Phones[i];
            if (!(phone.Score >= 0.0 && phone.Score <= 1.0) || !double.IsFinite(phone.Score))
            {
                throw Determinism.Fail(Oracle, $"Phone {i} ('{phone.Expected}') scored {phone.Score}.", c);
            }

            if (phone.Expected != reference.Phones[i])
            {
                throw Determinism.Fail(Oracle, $"Phone {i} is scored as '{phone.Expected}' but the reference has '{reference.Phones[i]}'.", c);
            }

            if (phone.Actual.Length != 0 && !posteriors.Vocabulary.Contains(phone.Actual))
            {
                throw Determinism.Fail(Oracle, $"Phone {i} was heard as '{phone.Actual}', which is not in the vocabulary.", c);
            }

            total += phone.Score;
        }

        if (!(result.WordScore >= 0.0 && result.WordScore <= 1.0) || !double.IsFinite(result.WordScore))
        {
            throw Determinism.Fail(Oracle, $"The word score is {result.WordScore}.", c);
        }

        if (result.Phones.Count > 0 && Math.Abs(result.WordScore - (total / result.Phones.Count)) > 1e-6)
        {
            throw Determinism.Fail(Oracle, $"The word score {result.WordScore} is not the mean {total / result.Phones.Count} of the phone scores.", c);
        }

        if (!(result.Confidence >= 0.0 && result.Confidence <= 1.0) || !double.IsFinite(result.Confidence))
        {
            throw Determinism.Fail(Oracle, $"The confidence is {result.Confidence}.", c);
        }

        if (result.ModelHash != "fuzz-phoneme")
        {
            throw Determinism.Fail(Oracle, $"The model hash '{result.ModelHash}' is not the recognizer's.", c);
        }

        TagOrder(result.Errors, c);
    }

    private static void TagOrder(IReadOnlyList<ErrorTag> errors, object input)
    {
        for (var i = 0; i < errors.Count; i++)
        {
            var tag = errors[i];
            if (tag.Tag == ErrorTags.PronStress || !ErrorTags.IsPronunciation(tag.Tag) || !ErrorTags.All.Contains(tag.Tag))
            {
                throw Determinism.Fail(Oracle, $"Unexpected tag '{tag.Tag}'.", input);
            }

            if (i > 0 && errors.Take(i).Any(previous => previous == tag))
            {
                throw Determinism.Fail(Oracle, $"The tag {tag.Tag}@{tag.Span} is reported twice.", input);
            }

            if (i > 0 && (errors[i - 1].Span.Start, errors[i - 1].Span.Length).CompareTo((tag.Span.Start, tag.Span.Length)) > 0)
            {
                throw Determinism.Fail(Oracle, $"Tags are not ordered by span: {errors[i - 1].Span} before {tag.Span}.", input);
            }
        }
    }

    private static void CheckPerfect(PronunciationResult result, PhoneSequenceCase c)
    {
        if (result.Phones.Any(static p => p.Score != 1.0) || result.Errors.Count > 0 || result.WordScore != 1.0)
        {
            throw Determinism.Fail(Oracle, $"Posteriors that spell the reference gave scores [{string.Join(' ', result.Phones.Select(static p => p.Score))}] and tags [{string.Join(' ', result.Errors.Select(static e => e.Tag))}].", c);
        }

        if (c.Mumble is null && result.Confidence != 1.0)
        {
            throw Determinism.Fail(Oracle, $"Perfect posteriors gave a confidence of {result.Confidence}.", c);
        }
    }

    private static void CheckSubstitutedPhonesScoreLow(PronunciationResult result, PhoneSequenceCase c)
    {
        var replaced = Enumerable.Range(0, c.Phones.Count).Where(i => c.Spoken[i] != c.Phones[i]).ToArray();
        if (replaced.Length == 0)
        {
            return;
        }

        foreach (var index in replaced)
        {
            if (c.CollapsesEqualNeighbours)
            {
                return;
            }

            if (result.Phones[index].Score > 0.5)
            {
                throw Determinism.Fail(Oracle, $"Phone {index} ('{c.Phones[index]}') was heard as '{c.Spoken[index]}' but scored {result.Phones[index].Score}.", c);
            }
        }

        if (result.WordScore >= 1.0)
        {
            throw Determinism.Fail(Oracle, "A substituted phone still gave a perfect word score.", c);
        }
    }

    // The forced alignment: null exactly when the target needs more frames than exist; otherwise one ordered, disjoint, non-empty segment per phone.
    private static void CheckAlignment(PhonePosteriors posteriors, IReadOnlyList<string> phones, PhoneSequenceCase c)
    {
        var alignment = CtcForcedAligner.Align(posteriors, phones);
        var feasible = posteriors.FrameCount >= CtcForcedAligner.MinimumFrames(phones);
        if (alignment is null)
        {
            if (feasible)
            {
                throw Determinism.Fail(Oracle, $"The alignment is null although {posteriors.FrameCount} frames hold the minimum of {CtcForcedAligner.MinimumFrames(phones)}.", c);
            }

            return;
        }

        if (!feasible)
        {
            throw Determinism.Fail(Oracle, $"{posteriors.FrameCount} frames cannot hold {CtcForcedAligner.MinimumFrames(phones)} but the alignment succeeded.", c);
        }

        if (alignment.Segments.Count != phones.Count || !double.IsFinite(alignment.LogLikelihood) || alignment.LogLikelihood > 1e-6)
        {
            throw Determinism.Fail(Oracle, $"{alignment.Segments.Count} segments for {phones.Count} phones, log-likelihood {alignment.LogLikelihood}.", c);
        }

        var previousEnd = 0;
        for (var k = 0; k < alignment.Segments.Count; k++)
        {
            var segment = alignment.Segments[k];
            var minimumStart = k > 0 && phones[k] == phones[k - 1] ? previousEnd + 1 : previousEnd;
            if (segment.PhoneIndex != k || segment.Phone != phones[k] || segment.StartFrame < minimumStart || segment.EndFrame <= segment.StartFrame || segment.EndFrame > posteriors.FrameCount)
            {
                throw Determinism.Fail(Oracle, $"Segment {k} ('{segment.Phone}') covers frames [{segment.StartFrame}, {segment.EndFrame}) after the previous one ended at {previousEnd} (frame count {posteriors.FrameCount}).", c);
            }

            previousEnd = segment.EndFrame;
        }
    }
}
