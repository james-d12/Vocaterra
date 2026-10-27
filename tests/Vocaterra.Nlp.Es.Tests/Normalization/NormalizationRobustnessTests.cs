using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Es.Tests.Normalization;

/// <summary>Seeded pseudo-random sweeps that check invariants of normalization, phonetic keys and matching on arbitrary text.</summary>
public class NormalizationRobustnessTests
{
    private static readonly string[] Pieces =
    [
        "a", "e", "o", "n", "l", "r", "s", "ñ", "ü", "á", "é", "ó", "́", "̃", "̈", "H", "LL", "qu", "gu", "ch",
        "1", "22", "0", " ", "  ", "\t", "\n", " ", "-", "--", "'", "\"", "¿", "?", "¡", "!", ",", ".", ";", ":", "(", ")", "…",
        "😀", "uno", "una", "un", "dos", "veinte", "veintiún", "treinta", "y", "cien", "ciento", "mil", "cero", "al", "del",
        "quinientos", "nueve", "hola", "casa", "espada",
    ];

    private static string Generate(SeededRng rng)
    {
        var length = 1 + rng.NextInt(14);
        var parts = new string[length];
        for (var i = 0; i < length; i++)
        {
            parts[i] = Pieces[rng.NextInt(Pieces.Length)];
        }

        return string.Concat(parts);
    }

    [Fact]
    public void NormalizedTextAlwaysSatisfiesItsInvariants()
    {
        var rng = new SeededRng(0x4D34UL);
        var optionSets = new[]
        {
            NormalizationOptions.Default,
            new NormalizationOptions(ExpandContractions: true),
            new NormalizationOptions(NumbersToDigits: false),
            new NormalizationOptions(ExpandContractions: true, ConvertIndefiniteArticles: false),
        };

        for (var n = 0; n < 3000; n++)
        {
            var input = Generate(rng);
            foreach (var options in optionSets)
            {
                var result = SpanishTextNormalizer.Normalize(input, options);

                Assert.True(result.Text.IsNormalized(System.Text.NormalizationForm.FormC), input);
                Assert.Equal(result.Text, string.Join(' ', result.Tokens.Select(t => t.Text)));
                Assert.DoesNotContain("  ", result.Text, StringComparison.Ordinal);
                Assert.Equal(result.Text.Trim(), result.Text);
                foreach (var token in result.Tokens)
                {
                    Assert.NotEmpty(token.Text);
                    Assert.Equal(token.Text, result.Text.Substring(token.Span.Start, token.Span.Length));
                    Assert.False(string.IsNullOrEmpty(token.Original));
                }

            }
        }
    }

    [Fact]
    public void NormalizationIsIdempotentWithDefaultOptions()
    {
        var rng = new SeededRng(0x4D35UL);
        for (var n = 0; n < 3000; n++)
        {
            var input = Generate(rng);
            var once = SpanishTextNormalizer.Normalize(input).Text;
            var twice = SpanishTextNormalizer.Normalize(once).Text;

            Assert.Equal(once, twice);
        }
    }

    [Fact]
    public void PhoneticKeyAndMatcherNeverThrowAndStayInRange()
    {
        var rng = new SeededRng(0x4D36UL);
        var set = new AnswerSet(
            "sweep",
            [
                new Answer("car", "coche", ["carro", "auto"], []),
                new Answer("sword", "espada", [], []),
                new Answer("hello", "¡Hola, buenas tardes!", ["2 espadas"], []),
            ]);

        for (var n = 0; n < 3000; n++)
        {
            var input = Generate(rng);
            var key = SpanishPhoneticKey.Compute(input);
            Assert.All(key, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ', input));

            var text = SpanishTextNormalizer.Normalize(input);
            foreach (var difficulty in Enum.GetValues<Difficulty>())
            {
                var result = AnswerMatcher.Match(text, set, difficulty, SpanishLanguagePack.Instance);
                Assert.InRange(result.Confidence, 0.0, 1.0);
                Assert.True(result.Distance >= 0);
                Assert.Equal(result.AnswerId is null, result.Type == Vocaterra.Nlp.Core.Stages.MatchType.None);
            }
        }
    }
}
