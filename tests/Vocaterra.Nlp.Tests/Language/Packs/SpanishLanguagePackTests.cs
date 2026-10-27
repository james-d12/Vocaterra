using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Language.Packs.Tests;

/// <summary>Each member of <see cref="SpanishLanguagePack"/> returns exactly what the static it wraps returns (milestone M32).</summary>
public class SpanishLanguagePackTests
{
    private static readonly SpanishLanguagePack Pack = SpanishLanguagePack.Instance;

    public static TheoryData<string> Texts() => new()
    {
        "Yo quiero dos espadas",
        "¿Dónde está la ÁRBOL?",
        "veintiún años y treinta y un gatos",
        "El pingüino come ñandú, señor.",
        "mil novecientos noventa y nueve",
        "ya  ¿qué  hora es ?",
        string.Empty,
        "a\ud800b",
    };

    [Fact]
    public void TheIdIsSpanishAndTheInstanceIsASingleton()
    {
        Assert.Equal("es", Pack.LanguageId);
        Assert.Same(SpanishLanguagePack.Instance, Pack);
        Assert.NotNull(Pack.G2p);
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void TheNormalizerEqualsTextNormalizer(string text)
    {
        foreach (var options in new NormalizationOptions?[] { null, new(ExpandContractions: true), new(NumbersToDigits: false), new(ConvertIndefiniteArticles: false) })
        {
            Assert.Equal(TextNormalizer.Normalize(text, options), Pack.Normalizer.Normalize(text, options));
        }
    }

    [Fact]
    public void TheFoldingKeysThrowOnIllFormedTextLikeTheStatics()
    {
        // The lexicon keys are built from normalized text, which has no lone surrogate; the statics have always thrown here and the wrapper does not hide it.
        Assert.Throws<ArgumentException>(() => TextFolding.Normalize("a\ud800b"));
        Assert.Throws<ArgumentException>(() => Pack.Folding.Key("a\ud800b"));
        Assert.Throws<ArgumentException>(() => Pack.Folding.LenientKey("a\ud800b"));
        Assert.Equal(Accents.Strip("a\ud800b"), Pack.Folding.MatchingKey("a\ud800b"));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void TheFoldingEqualsTheStaticFoldings(string text)
    {
        if (text.Contains('\ud800'))
        {
            return;
        }

        Assert.Equal(TextFolding.Normalize(text), Pack.Folding.Key(text));
        Assert.Equal(TextFolding.StripAccents(text), Pack.Folding.LenientKey(text));
        Assert.Equal(Accents.Strip(text), Pack.Folding.MatchingKey(text));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    public void TheNumbersEqualSpanishNumbers(string text)
    {
        var tokens = TextNormalizer.Normalize(text, new NormalizationOptions(NumbersToDigits: false)).Tokens.Select(token => token.Text).ToList();

        Assert.Equal(SpanishNumbers.MaxValue, Pack.Numbers.MaxValue);
        Assert.Equal(SpanishNumbers.DigitsToWords(text + " 21 y 1999"), Pack.Numbers.DigitsToWords(text + " 21 y 1999"));
        Assert.Equal(SpanishNumbers.TryParse(text, out var staticText), Pack.Numbers.TryParse(text, out var packText));
        Assert.Equal(staticText, packText);
        Assert.Equal(SpanishNumbers.TryParse(tokens, out var staticTokens), Pack.Numbers.TryParse(tokens, out var packTokens));
        Assert.Equal(staticTokens, packTokens);
        for (var start = 0; start < tokens.Count; start++)
        {
            Assert.Equal(SpanishNumbers.TryParsePrefix(tokens, start, out var staticPrefix), Pack.Numbers.TryParsePrefix(tokens, start, out var packPrefix));
            Assert.Equal(staticPrefix, packPrefix);
        }
    }

    [Fact]
    public void ToWordsEqualsSpanishNumbersForEverySampledValue()
    {
        foreach (var value in Enumerable.Range(0, 2200).Concat([9999, 12_345, 100_000, 999_998, SpanishNumbers.MaxValue]))
        {
            Assert.Equal(SpanishNumbers.ToWords(value), Pack.Numbers.ToWords(value));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Pack.Numbers.ToWords(SpanishNumbers.MaxValue + 1));
    }

    [Theory]
    [InlineData("camión")]
    [InlineData("hierba")]
    [InlineData("vaca")]
    [InlineData("zapato")]
    [InlineData("")]
    public void ThePhoneticKeyEqualsSpanishPhoneticKey(string word)
    {
        Assert.Equal(SpanishPhoneticKey.Compute(word), Pack.PhoneticKey.Compute(word));
    }

    [Theory]
    [InlineData("¿ Cómo  estás ?")]
    [InlineData("hola ¡qué bien!  ")]
    [InlineData("cómo estás? bien!")]
    [InlineData("")]
    public void ThePunctuationEqualsPunctuationFixer(string text)
    {
        Assert.Equal(PunctuationFixer.Fix(text), Pack.Punctuation.Fix(text));
    }

    [Theory]
    [InlineData("espada")]
    [InlineData("Dónde está la llave")]
    [InlineData("quiero dos jarras")]
    public void TheG2pEqualsSpanishG2p(string text)
    {
        var g2p = Pack.G2p!;
        foreach (var dialect in new[] { DialectConfig.LatinAmericanNeutral, DialectConfig.CastilianStandard, DialectConfig.Rioplatense })
        {
            Assert.Equal(SpanishG2p.Phonemize(text, dialect), g2p.Phonemize(text, dialect));
        }

        Assert.Equal(SpanishG2p.PhonemizeAll(text), g2p.PhonemizeAll(text));
        var dialects = new[] { DialectConfig.CastilianStandard, DialectConfig.Rioplatense };
        Assert.Equal(SpanishG2p.PhonemizeAll(text, dialects), g2p.PhonemizeAll(text, dialects));
    }

    [Fact]
    public void TheRepairLinesEqualTheRepairStrategyConstants()
    {
        Assert.Equal(RepairStrategy.AskAgainLine, Pack.RepairFallback.AskAgain);
        Assert.Equal(RepairStrategy.SlowRepeatLine, Pack.RepairFallback.SlowRepeat);
        Assert.Equal(RepairStrategy.GiveUpLine, Pack.RepairFallback.GiveUp);
    }

    [Fact]
    public void TheGrammarDefaultsAndLintVocabularyListTheSpanishWords()
    {
        Assert.True(Pack.GrammarDefaults.OptionalSubjectPronoun);
        Assert.Equal(["por", "favor"], Pack.GrammarDefaults.TrailingPoliteWords);
        Assert.Equal(["por", "favor"], Pack.Lint.PolitePhrase);
        Assert.Equal(["un", "una"], Pack.Lint.ArticleNumeralHomographs);
        Assert.Equal(WordClasses.SubjectPronounLemmas, Pack.Lint.SubjectPronouns);
        Assert.Equal(Pack.Lint.SubjectPronouns, Pack.GrammarDefaults.SubjectPronouns);
        Assert.All(Pack.GrammarDefaults.SubjectPronouns, pronoun => Assert.True(WordClasses.IsSubjectPronoun(pronoun)));
    }
}
