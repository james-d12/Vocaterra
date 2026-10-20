using System.Text;
using CsCheck;
using Slg.Core.Stages;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Slg.Nlp.Normalization;
using Xunit.Abstractions;

namespace Slg.Fuzzing.Tests;

/// <summary>
/// O1: the normalizer never crashes, is idempotent, and returns well-formed tokens whose spans index into the text and whose
/// <c>Original</c> strings come from the input, in order.
/// </summary>
public sealed class NormalizerFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O1";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    [Fact]
    public void Normalize_NeverCrashes_IsIdempotent_AndReturnsWellFormedTokens()
    {
        var gen = Gen.Select(
            SpanishTextGen.Text(Harness.Words),
            Gen.Bool,
            Gen.Bool,
            Gen.Bool,
            static (text, contractions, numbers, articles) => (text, new NormalizationOptions(contractions, numbers, articles)));
        var iterations = FuzzOptions.Iterations(FuzzOptions.GeneralVar, FuzzOptions.GeneralDefault);

        gen.Sample(
            ((string Text, NormalizationOptions Options) input) =>
            {
                var (text, options) = input;
                var first = Normalize(text, options);

                CheckWellFormed(first, text, options);

                // Idempotence: feeding the normalized text back changes neither the text nor the token texts and spans. Token.Original
                // legitimately differs on the second pass (it is the first pass's output), so it is not compared.
                var second = Normalize(first.Text, options);
                if (!Same(first, second))
                {
                    throw Determinism.Fail(Oracle, $"Normalize is not idempotent: '{first.Text}' became '{second.Text}'.", new { text, options });
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);
    }

    [Fact]
    public void Normalize_RejectsNull_WithArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextNormalizer.Normalize(null!));
    }

    private static NormalizedText Normalize(string text, NormalizationOptions options)
    {
        try
        {
            return TextNormalizer.Normalize(text, options);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"Normalize threw {ex.GetType().Name}: {ex.Message}", new { text, options });
        }
    }

    private static bool Same(NormalizedText left, NormalizedText right)
    {
        return string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            && left.Tokens.Count == right.Tokens.Count
            && left.Tokens.Zip(right.Tokens).All(static pair => pair.First.Text == pair.Second.Text && pair.First.Span == pair.Second.Span);
    }

    private static void CheckWellFormed(NormalizedText result, string input, NormalizationOptions options)
    {
        object Context() => new { input, options, result };

        if (!result.Text.IsNormalized(NormalizationForm.FormC))
        {
            throw Determinism.Fail(Oracle, "Text is not in NFC.", Context());
        }

        if (string.Join(' ', result.Tokens.Select(static t => t.Text)) != result.Text)
        {
            throw Determinism.Fail(Oracle, "Text is not the single-space join of the token texts.", Context());
        }

        var previousEnd = -1;
        foreach (var token in result.Tokens)
        {
            var span = token.Span;
            if (span.Start < 0 || span.Length <= 0 || span.End > result.Text.Length)
            {
                throw Determinism.Fail(Oracle, $"Span {span} of '{token.Text}' is out of range for a text of length {result.Text.Length}.", Context());
            }

            if (span.Start <= previousEnd)
            {
                throw Determinism.Fail(Oracle, $"Span {span} of '{token.Text}' overlaps or precedes the previous token.", Context());
            }

            previousEnd = span.End;
            if (!string.Equals(result.Text.Substring(span.Start, span.Length), token.Text, StringComparison.Ordinal))
            {
                throw Determinism.Fail(Oracle, $"Span {span} does not cover token '{token.Text}'.", Context());
            }

            if (token.Text.Any(char.IsWhiteSpace))
            {
                throw Determinism.Fail(Oracle, $"Token '{token.Text}' contains whitespace.", Context());
            }
        }

        CheckOriginals(result, input, options);
    }

    // string.Normalize rejects lone surrogates, which the adversarial corpus contains on purpose; the normalizer never puts one inside a word,
    // so replacing them in this oracle's own copy of the input does not change any Original it looks for.
    private static string ReplaceLoneSurrogates(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(text[i]).Append(text[i + 1]);
                i++;
            }
            else
            {
                builder.Append(char.IsSurrogate(text[i]) ? '�' : text[i]);
            }
        }

        return builder.ToString();
    }

    // Token.Original is the NFC form of the source substring a token came from. With number words converted, a multi-word number joins its
    // words with single spaces, so each space-separated piece of an Original must be found in the input, and the pieces must follow the
    // input order.
    private static void CheckOriginals(NormalizedText result, string input, NormalizationOptions options)
    {
        var nfc = ReplaceLoneSurrogates(input).Normalize(NormalizationForm.FormC);
        var cursor = 0;
        string? previousOriginal = null;
        foreach (var token in result.Tokens)
        {
            // Contraction expansion turns one source word (al, del) into two tokens that share its Original.
            if (options.ExpandContractions && string.Equals(token.Original, previousOriginal, StringComparison.Ordinal))
            {
                continue;
            }

            previousOriginal = token.Original;
            foreach (var piece in token.Original.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var found = nfc.IndexOf(piece, cursor, StringComparison.Ordinal);
                if (found < 0)
                {
                    throw Determinism.Fail(Oracle, $"Original '{token.Original}' of token '{token.Text}' is not an in-order substring of the input.", new { input, options, result });
                }

                cursor = found + piece.Length;
            }
        }
    }
}
