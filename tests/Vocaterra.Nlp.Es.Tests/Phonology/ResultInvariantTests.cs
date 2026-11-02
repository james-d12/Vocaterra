using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Es.Tests.Phonology;

/// <summary>Structural invariants of <see cref="G2pResult"/> over a broad sample, plus determinism.</summary>
public class ResultInvariantTests
{
    private static readonly string[] Sample =
    [
        "hola", "buenos días", "teléfono", "construir", "Uruguay", "examen", "México", "oaxaca", "stop", "exhibir",
        "inhumano", "pingüino", "cerveza por favor", "¿Cómo te llamas?", "los amigos del niño", "un beso", "la bodega",
        "ciudad", "país", "huevo", "yo y tú", "extranjero", "hámster", "subrayar", "wifi", "jeans", "y", "hoy",
        "zapatos", "señor López", "mañana por la mañana", "lloviendo", "guerra", "quiero", "chocolate",
    ];

    [Fact]
    public void SyllablePhoneRangesPartitionThePhoneList()
    {
        foreach (var text in Sample)
        {
            foreach (var dialect in DialectConfig.AllShipped)
            {
                var result = SpanishG2p.Phonemize(text, dialect);

                var cursor = 0;
                foreach (var syllable in result.Syllables)
                {
                    Assert.Equal(cursor, syllable.PhoneStart);
                    Assert.True(syllable.PhoneCount >= 0);
                    cursor += syllable.PhoneCount;
                }

                Assert.Equal(result.Phones.Count, cursor);
            }
        }
    }

    [Fact]
    public void SyllableSpansTileEachWordInsideTheNormalizedText()
    {
        foreach (var text in Sample)
        {
            var result = TestHelpers.La(text);

            var words = result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rebuilt = result.Syllables
                .GroupBy(static s => s.WordIndex)
                .Select(group => string.Concat(group.Select(s => result.Text.Substring(s.GraphemeSpan.Start, s.GraphemeSpan.Length))))
                .ToArray();

            Assert.Equal(words, rebuilt);
            Assert.All(result.Syllables, s => Assert.InRange(s.GraphemeSpan.End, 1, result.Text.Length));
        }
    }

    [Fact]
    public void EveryWordHasExactlyOneStressedSyllable()
    {
        foreach (var text in Sample)
        {
            var result = TestHelpers.La(text);

            foreach (var word in result.Syllables.GroupBy(static s => s.WordIndex))
            {
                Assert.Single(word, static s => s.Stressed);
            }

            Assert.Equal(result.Syllables.Count == 0 ? -1 : result.Syllables.ToList().FindIndex(static s => s.Stressed), result.StressedSyllableIndex);
        }
    }

    [Fact]
    public void PhoneSpansAreParallelToPhonesAndInsideTheText()
    {
        foreach (var text in Sample)
        {
            var result = TestHelpers.La(text);

            Assert.Equal(result.Phones.Count, result.PhoneSpans.Count);
            Assert.All(result.PhoneSpans, span => Assert.True(span.Start >= 0 && span.End <= result.Text.Length && span.Length > 0));
            Assert.All(result.Phones, phone => Assert.True(PhoneInventory.IsKnown(phone), phone));
        }
    }

    [Fact]
    public void SilentLettersPointAtHsAndInsideThePhoneList()
    {
        foreach (var text in Sample)
        {
            var result = TestHelpers.La(text);

            foreach (var silent in result.SilentLetters)
            {
                Assert.Equal("h", result.Text.Substring(silent.Span.Start, silent.Span.Length));
                Assert.InRange(silent.PhoneIndex, 0, result.Phones.Count);
            }
        }
    }

    [Fact]
    public void SameInputTwiceGivesEqualResults()
    {
        foreach (var text in Sample)
        {
            foreach (var dialect in DialectConfig.AllShipped)
            {
                var first = SpanishG2p.Phonemize(text, dialect);
                var second = SpanishG2p.Phonemize(text, dialect);

                Assert.Equal(first, second);
                Assert.Equal(first.GetHashCode(), second.GetHashCode());
                Assert.Equal(first.ToIpaString(), second.ToIpaString());
                Assert.NotSame(first, second);
            }
        }
    }

    [Fact]
    public void ResultsDifferWhenDialectOrTextDiffer()
    {
        Assert.NotEqual(TestHelpers.La("cerveza"), TestHelpers.Es("cerveza"));
        Assert.NotEqual(TestHelpers.La("casa"), TestHelpers.La("mesa"));
    }

    [Fact]
    public void ToIpaStringSeparatesWordsSyllablesAndMarksStress()
    {
        Assert.Equal("te.ˈle.fo.no", TestHelpers.La("teléfono").ToIpaString());
        Assert.Equal("ˈka.sa ˈɣɾan.de", TestHelpers.La("casa grande").ToIpaString());
        Assert.Equal(string.Empty, TestHelpers.La(string.Empty).ToIpaString());
    }

    [Fact]
    public void ThePhoneInventoryTokenizesMultiCodePointPhones()
    {
        Assert.Equal(new[] { "tʃ", "o", "k", "o" }, PhoneInventory.Tokenize("tʃoko"));
        Assert.Equal(new[] { "t", "s" }, PhoneInventory.Tokenize("ts"));
        Assert.Equal(new[] { "k", "eɪ" }, PhoneInventory.Tokenize("keɪ"));
        Assert.Throws<FormatException>(() => PhoneInventory.Tokenize("q"));
        Assert.Equal("tʃo", PhoneInventory.Join(PhoneInventory.Tokenize("tʃo")));
        Assert.Equal(PhoneInventory.All.Count, PhoneInventory.All.Distinct().Count());
        Assert.All(PhoneInventory.All, static p => Assert.Equal(p, string.Concat(PhoneInventory.Tokenize(p))));
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => SpanishG2p.Phonemize(null!, DialectConfig.LatinAmericanNeutral));
        Assert.Throws<ArgumentNullException>(() => SpanishG2p.Phonemize("hola", null!));
        Assert.Throws<ArgumentNullException>(() => SpanishG2p.PhonemizeAll("hola", null!));
        Assert.Throws<ArgumentNullException>(() => PredictedErrorVariants.Generate(null!));
    }

    [Fact]
    public void ArbitraryLetterSoupNeverBreaksTheInvariants()
    {
        const string alphabet = "abcdefghijklmnñopqrstuvwxyzáéíóúü   ";
        var rng = SeededRng.ForInteraction(2026, "g2p-fuzz", 1);
        for (var n = 0; n < 400; n++)
        {
            var length = 1 + rng.NextInt(14);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                chars[i] = alphabet[rng.NextInt(alphabet.Length)];
            }

            var text = new string(chars);
            foreach (var dialect in DialectConfig.AllShipped)
            {
                var result = SpanishG2p.Phonemize(text, dialect);

                Assert.Equal(result.Phones.Count, result.Syllables.Sum(static s => s.PhoneCount));
                Assert.Equal(result.Phones.Count, result.PhoneSpans.Count);
                foreach (var word in result.Syllables.GroupBy(static s => s.WordIndex))
                {
                    Assert.Single(word, static s => s.Stressed);
                }

                _ = result.ToIpaString();
                _ = PredictedErrorVariants.Generate(result);
            }
        }
    }

    [Fact]
    public void LongRepeatedTextKeepsItsAlignment()
    {
        var text = string.Join(' ', Enumerable.Repeat("buenos días señor López", 50));

        var result = TestHelpers.La(text);

        Assert.Equal(50 * 4, result.Syllables.Select(static s => s.WordIndex).Distinct().Count());
        Assert.Equal(new TextSpan(0, 3), result.Syllables[0].GraphemeSpan);
        Assert.Equal(result.Text.Length, result.Syllables[^1].GraphemeSpan.End);
    }
}
