using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Shared builders for the pronunciation tests.</summary>
internal static class TestSupport
{
    public const string FakeHash = "test-model-hash";

    public static FakePhonemeRecognizer Fake { get; } = FakePhonemeRecognizer.ForSpanish();

    /// <summary>The accepted variants for a text across every shipped dialect.</summary>
    public static IReadOnlyList<G2pResult> Variants(string text)
    {
        return SpanishG2p.PhonemizeAll(text);
    }

    /// <summary>The primary (Latin American neutral) variant as a one-element list.</summary>
    public static IReadOnlyList<G2pResult> Primary(string text)
    {
        return [SpanishG2p.Phonemize(text, DialectConfig.LatinAmericanNeutral)];
    }

    /// <summary>Predicted errors for every accepted variant.</summary>
    public static IReadOnlyList<PredictedErrorVariant> Predicted(IReadOnlyList<G2pResult> variants)
    {
        return PronunciationScorer.PredictedErrorsFor(variants);
    }

    /// <summary>A hand-built one-syllable variant without phone spans.</summary>
    public static G2pResult Manual(params string[] phones)
    {
        return new G2pResult(
            phones,
            [new SyllableAlignment(new TextSpan(0, phones.Length), 0, phones.Length, true)],
            0,
            DialectConfig.LatinAmericanNeutral);
    }

    /// <summary>Tokenizes a concatenated IPA string.</summary>
    public static string[] Ipa(string ipa)
    {
        return [.. PhoneInventory.Tokenize(ipa)];
    }

    /// <summary>Builds posteriors from probabilities (one row per frame) over the vocabulary <c>&lt;blank&gt;</c> plus the given phones.</summary>
    public static PhonePosteriors Matrix(string[] phones, double[][] probabilities)
    {
        string[] vocabulary = [CtcDecoder.BlankSymbol, .. phones];
        var logProbs = new float[probabilities.Length * vocabulary.Length];
        for (var frame = 0; frame < probabilities.Length; frame++)
        {
            Assert.Equal(vocabulary.Length, probabilities[frame].Length);
            for (var column = 0; column < vocabulary.Length; column++)
            {
                logProbs[(frame * vocabulary.Length) + column] = (float)Math.Log(probabilities[frame][column]);
            }
        }

        return new PhonePosteriors(vocabulary, logProbs, probabilities.Length, 0.02);
    }

    /// <summary>Builds posteriors whose argmax at each frame is the given column.</summary>
    public static PhonePosteriors Hot(string[] vocabulary, int[] argmaxPerFrame)
    {
        var logProbs = new float[argmaxPerFrame.Length * vocabulary.Length];
        Array.Fill(logProbs, FakePhonemeRecognizer.FloorLogProb);
        for (var frame = 0; frame < argmaxPerFrame.Length; frame++)
        {
            logProbs[(frame * vocabulary.Length) + argmaxPerFrame[frame]] = 0f;
        }

        return new PhonePosteriors(vocabulary, logProbs, argmaxPerFrame.Length, 0.02);
    }

    /// <summary>Returns the error tags as <c>tag@start+length</c> strings for compact assertions.</summary>
    public static string[] Describe(IReadOnlyList<ErrorTag> errors)
    {
        return [.. errors.Select(static e => $"{e.Tag}@{e.Span.Start}+{e.Span.Length}")];
    }
}
