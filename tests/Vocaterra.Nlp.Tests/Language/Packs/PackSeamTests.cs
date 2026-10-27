using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.SlotGrammar;
using Vocaterra.Nlp.Language.Tests.Analysis;
using Vocaterra.Nlp.Response.Speech;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Language.Packs.Tests;

/// <summary>
/// The seams of M33 with a pack other than Spanish's text-side members: a pack that overrides one member and forwards the rest to the Spanish pack.
/// They show that the call sites read the pack they are given, and not the Spanish statics behind it.
/// </summary>
public class PackSeamTests
{
    /// <summary>The Spanish pack with one member replaced; the other members are Spanish.</summary>
    private sealed class OverriddenPack(GrammarDefaults? grammar = null, IG2p? g2p = null, bool noG2p = false) : ILanguagePack
    {
        private static readonly ILanguagePack Spanish = SpanishLanguagePack.Instance;

        public string LanguageId => "es";

        public ITextNormalizer Normalizer => Spanish.Normalizer;

        public ILexiconFolding Folding => Spanish.Folding;

        public INumberWords Numbers => Spanish.Numbers;

        public IPhoneticKey PhoneticKey => Spanish.PhoneticKey;

        public IPunctuation Punctuation => Spanish.Punctuation;

        public IG2p? G2p => noG2p ? null : g2p ?? Spanish.G2p;

        public GrammarDefaults GrammarDefaults => grammar ?? Spanish.GrammarDefaults;

        public LintVocabulary Lint => Spanish.Lint;

        public RepairLines RepairFallback => Spanish.RepairFallback;

        public IRecastPolicy Recast => Spanish.Recast;

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
