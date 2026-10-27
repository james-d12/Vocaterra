using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Es.Tests.Packs;

/// <summary>
/// The seams of M33 with a pack other than Spanish's text-side members: a pack that overrides one member and forwards the rest to the Spanish pack.
/// They show that the call sites read the pack they are given, and not the Spanish statics behind it.
/// </summary>
public class PackSeamTests
{
    /// <summary>The Spanish pack with one member replaced; the other members are Spanish.</summary>
    private sealed class OverriddenPack(GrammarDefaults? grammar = null, IG2p? g2p = null, bool noG2p = false, INumberWords? numbers = null) : ILanguagePack
    {
        private static readonly ILanguagePack Spanish = SpanishLanguagePack.Instance;

        public string LanguageId => "es";

        public ITextNormalizer Normalizer => Spanish.Normalizer;

        public ILexiconFolding Folding => Spanish.Folding;

        public INumberWords Numbers => numbers ?? Spanish.Numbers;

        public IPhoneticKey PhoneticKey => Spanish.PhoneticKey;

        public IPunctuation Punctuation => Spanish.Punctuation;

        public IG2p? G2p => noG2p ? null : g2p ?? Spanish.G2p;

        public GrammarDefaults GrammarDefaults => grammar ?? Spanish.GrammarDefaults;

        public LintVocabulary Lint => Spanish.Lint;

        public RepairLines RepairFallback => Spanish.RepairFallback;

        public IRecastPolicy Recast => Spanish.Recast;

        public SampleWords Samples => Spanish.Samples;

        public Vocaterra.Nlp.Language.Lexicon.LexiconProfile LexiconProfile => Spanish.LexiconProfile;

        public Vocaterra.Nlp.Core.Taxonomy.ErrorTagSet ErrorTags => Spanish.ErrorTags;

        public Vocaterra.Nlp.Language.Morphology.IMorphology CreateMorphology(Vocaterra.Nlp.Language.Lexicon.Lexicon lexicon) => Spanish.CreateMorphology(lexicon);

        public Vocaterra.Nlp.Language.Analysis.IAnalysisRules CreateAnalysisRules(Vocaterra.Nlp.Language.Lexicon.Lexicon lexicon, Vocaterra.Nlp.Language.Morphology.IMorphology morphology) =>
            Spanish.CreateAnalysisRules(lexicon, morphology);

        public ITemplateFunctions CreateTemplateFunctions(Vocaterra.Nlp.Language.Lexicon.Lexicon lexicon, Vocaterra.Nlp.Language.Morphology.IMorphology morphology) =>
            Spanish.CreateTemplateFunctions(lexicon, morphology);
    }

    private static IReadOnlyList<AnalyzedToken> Tokens(string text)
    {
        return Fixtures.Analyzer.Analyze(TestTokens.From(text)).Tokens;
    }

    [Fact]
    public void APackWithoutAnOptionalSubjectReadsItsOwnGrammarDefaults()
    {
        var spanish = SpanishLanguagePack.Instance.GrammarDefaults;
        var noSubject = new OverriddenPack(grammar: spanish with { OptionalSubjectPronoun = false });
        var grammar = SlotGrammarType.Parse("[VERB:querer] [NOUN:item]");

        // With the Spanish defaults the leading pronoun is optional; without it the same words do not match.
        Assert.NotNull(SlotGrammarMatcher.Match(Tokens("yo quiero agua"), grammar, SpanishLanguagePack.Instance));
        Assert.Null(SlotGrammarMatcher.Match(Tokens("yo quiero agua"), grammar, noSubject));
        Assert.NotNull(SlotGrammarMatcher.Match(Tokens("quiero agua"), grammar, noSubject));
    }

    [Fact]
    public void APackWithoutATrailingPolitePhraseDoesNotAcceptPorFavor()
    {
        var noPolite = new OverriddenPack(grammar: SpanishLanguagePack.Instance.GrammarDefaults with { TrailingPoliteWords = [] });
        var grammar = SlotGrammarType.Parse("[VERB:querer] [NOUN:item]");

        Assert.NotNull(SlotGrammarMatcher.Match(Tokens("quiero agua por favor"), grammar, SpanishLanguagePack.Instance));
        Assert.Null(SlotGrammarMatcher.Match(Tokens("quiero agua por favor"), grammar, noPolite));
    }

    [Fact]
    public void ADifferentPolitePhraseIsMatchedWordByWord()
    {
        var bitte = new OverriddenPack(grammar: SpanishLanguagePack.Instance.GrammarDefaults with { TrailingPoliteWords = ["bitte"] });
        var grammar = SlotGrammarType.Parse("[VERB:querer] [NOUN:item]");

        var match = SlotGrammarMatcher.Match(Tokens("quiero agua bitte"), grammar, bitte);

        Assert.NotNull(match);
        Assert.Null(SlotGrammarMatcher.Match(Tokens("quiero agua por favor"), grammar, bitte));
    }

    [Fact]
    public void TheSpeechRequestHasNoPhonesWhenThePackHasNoG2p()
    {
        var silent = new OverriddenPack(noG2p: true);

        var request = SpeechRequests.Create("Tengo 2 espadas", "voz", silent);

        Assert.Equal("Tengo dos espadas", request.Text);
        Assert.Null(request.Phones);
        Assert.NotNull(SpeechRequests.Create("Tengo 2 espadas", "voz", SpanishLanguagePack.Instance).Phones);
    }

    /// <summary>Number words for 0 to 3 only (a toy language's), which the pack's Spanish members do not use.</summary>
    private sealed class SmallNumbers : INumberWords
    {
        private static readonly string[] Words = ["nu", "ko", "lo", "ri"];

        public int MaxValue => 3;

        public string ToWords(int value) => value is >= 0 and <= 3 ? Words[value] : throw new ArgumentOutOfRangeException(nameof(value), value, "0 to 3 only.");

        public bool TryParse(IReadOnlyList<string> tokens, out int value)
        {
            value = Array.IndexOf(Words, tokens.Count == 1 ? tokens[0] : string.Empty);
            return value >= 0;
        }

        public bool TryParse(string text, out int value) => TryParse(text.Split(' '), out value);

        public int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value)
        {
            value = start < tokens.Count ? Array.IndexOf(Words, tokens[start]) : -1;
            return value >= 0 ? 1 : 0;
        }

        public string DigitsToWords(string text) => text;

        public string Agree(string words, string gender) => words;
    }

    [Fact]
    public void ARecastSpellsOnlyTheNumbersThePackCanSpell()
    {
        // M35: a digit run above the pack's MaxValue (7 for a language spelled to 3) stays as digits, and does not throw. The 2 is spelled.
        var pack = new OverriddenPack(numbers: new SmallNumbers());
        var text = SpanishTextNormalizer.Normalize("tengo 2 espadas 7", new NormalizationOptions { NumbersToDigits = false, ConvertIndefiniteArticles = false });
        var recast = RecastBuilder.Build(text, new AnalysisResult([], [], [], null, 1.0), SpanishLexicon.Empty, pack);
        Assert.Equal("tengo lo espadas 7", recast);
    }

    [Fact]
    public void TheNumberAgreementOfThePackIsTheSpanishOne()
    {
        // The pack's Agree is the Spanish agreement: uno becomes un or una, veintiuno veintiún or veintiuna.
        Assert.Equal("una", SpanishLanguagePack.Instance.Numbers.Agree("uno", "f"));
        Assert.Equal("un", SpanishLanguagePack.Instance.Numbers.Agree("uno", "m"));
        Assert.Equal("veintiún", SpanishLanguagePack.Instance.Numbers.Agree("veintiuno", "m"));
        Assert.Equal("veintiuna", SpanishLanguagePack.Instance.Numbers.Agree("veintiuno", "f"));
        Assert.Equal("dos", SpanishLanguagePack.Instance.Numbers.Agree("dos", "f"));
    }

    [Fact]
    public void TheKokoroLanguageIsOnlyFoldedIntoTheHashWhenItIsNotSpanish()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kokoro-lang-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "espeak-ng-data"));
        try
        {
            foreach (var file in new[] { "model.onnx", "voices.bin", "tokens.txt" })
            {
                File.WriteAllText(Path.Combine(dir, file), "x");
            }

            const string manifestHash = "0123456789abcdef";
            var spanish = new KokoroSynthesizer(dir, 28, 1.0f, manifestHash);
            var spanishExplicit = new KokoroSynthesizer(dir, 28, 1.0f, manifestHash, "es");
            var english = new KokoroSynthesizer(dir, 28, 1.0f, manifestHash, "en-us");

            // The Spanish hash is the one the adapter always had (kokoro, manifest hash, speaker, speed); a language other than es is added.
            Assert.Equal(DeterministicHash.Combine("kokoro", manifestHash, "28", "1"), spanish.ModelHash);
            Assert.Equal(spanish.ModelHash, spanishExplicit.ModelHash);
            Assert.Equal(DeterministicHash.Combine("kokoro", manifestHash, "28", "1", "lang", "en-us"), english.ModelHash);
            Assert.NotEqual(spanish.ModelHash, english.ModelHash);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
