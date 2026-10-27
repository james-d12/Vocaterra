using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Normalization;
using Xunit.Abstractions;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O1: the normalizer never crashes, is idempotent, and returns well-formed tokens whose spans index into the text and whose
/// <c>Original</c> strings come from the input, in order. The generators feed raw ill-formed UTF-16 (lone surrogates) on purpose: the
/// normalization entry points (<c>SpanishTextNormalizer.Normalize</c>, <c>Accents.Strip</c>, <c>SpanishPhoneticKey.Compute</c>) must read them as
/// U+FFFD instead of throwing. The body is <see cref="LanguageOracles.Normalizer"/>, run on the Spanish language (M35); the Spanish entry points
/// are checked here as well.
/// </summary>
public sealed class NormalizerFuzzTests(ITestOutputHelper output)
{
    [Fact]
    public void Normalize_NeverCrashes_IsIdempotent_AndReturnsWellFormedTokens()
    {
        LanguageOracles.Normalizer(FuzzLanguage.Spanish, output, SpanishEntryPoints);
    }

    [Fact]
    public void Normalize_RejectsNull_WithArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SpanishTextNormalizer.Normalize(null!));
    }

    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    // The Spanish static entry points, with the raw text, lone surrogates included: total, and a phonetic key over a-z, 0-9 and space.
    private static void SpanishEntryPoints(string text)
    {
        try
        {
            var stripped = Vocaterra.Nlp.Es.Normalization.Accents.Strip(text);
            var key = Vocaterra.Nlp.Es.Normalization.SpanishPhoneticKey.Compute(text);
            if (HasLoneSurrogate(stripped))
            {
                throw Determinism.Fail("O1", "Accents.Strip left a lone surrogate in its result.", text);
            }

            if (key.Any(static c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ')))
            {
                throw Determinism.Fail("O1", $"The phonetic key '{key}' has a character outside a-z, 0-9 and space.", text);
            }
        }
        catch (ArgumentException ex)
        {
            throw Determinism.Fail("O1", $"Accents.Strip or SpanishPhoneticKey.Compute threw {ex.GetType().Name}: {ex.Message}", text);
        }
    }
}
