using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// The Spanish pack (Latin American default, ADR 0005). Every member wraps a static class that stays where it is and keeps meaning Spanish
/// (<see cref="TextNormalizer"/>, <see cref="SpanishNumbers"/>, <see cref="SpanishPhoneticKey"/>, <see cref="SpanishG2p"/>, the punctuation fixer,
/// <see cref="RepairStrategy"/>); a wrapper returns exactly what its static returns. Since M34 it also creates the Spanish morphology
/// (<see cref="MorphologyGenerator"/>), analysis rules and template functions, and lists the v1 lexicon profile and error tags. M53 moves the pack out of the engine assembly.
/// </summary>
public sealed class SpanishLanguagePack : ILanguagePack
{
    private SpanishLanguagePack()
    {
    }

    // The Spanish polite phrase, once: the grammar defaults and the linter both read it from here (M33).
    private static readonly string[] PolitePhrase = ["por", "favor"];

    // The article and numeral homographs of the linter (un, una: the indefinite article and the numeral one), once.
    private static readonly string[] ArticleNumeralHomographs = ["un", "una"];

    /// <summary>The one instance.</summary>
    public static SpanishLanguagePack Instance { get; } = new();

    /// <inheritdoc />
    public string LanguageId => "es";

    /// <inheritdoc />
    public ITextNormalizer Normalizer { get; } = new SpanishNormalizer();

    /// <inheritdoc />
    public ILexiconFolding Folding { get; } = new SpanishFolding();

    /// <inheritdoc />
    public INumberWords Numbers { get; } = new SpanishNumberWords();

    /// <inheritdoc />
    public IPhoneticKey PhoneticKey { get; } = new SpanishPhoneticKeyAdapter();

    /// <inheritdoc />
    public IPunctuation Punctuation { get; } = new SpanishPunctuation();

    /// <inheritdoc />
    public IG2p? G2p { get; } = new SpanishG2pAdapter();

    /// <inheritdoc />
    public GrammarDefaults GrammarDefaults { get; } = new(true, WordClasses.SubjectPronounLemmas, PolitePhrase);

    /// <inheritdoc />
    public LintVocabulary Lint { get; } = new(WordClasses.SubjectPronounLemmas, PolitePhrase, ArticleNumeralHomographs);

    /// <inheritdoc />
    public RepairLines RepairFallback { get; } = new(RepairStrategy.AskAgainLine, RepairStrategy.SlowRepeatLine, RepairStrategy.GiveUpLine, RepairStrategy.OfferFormat);

    /// <inheritdoc />
    public IRecastPolicy Recast { get; } = new SpanishRecastPolicy();

    /// <inheritdoc />
    public SampleWords Samples { get; } = new("espada", "tener", "fuego", "tengo", "tiene", "yo tengo dos espadas", "gender.agree");

    /// <inheritdoc />
    public LexiconProfile LexiconProfile => LexiconProfile.V1;

    /// <inheritdoc />
    public ErrorTagSet ErrorTags => ErrorTagSet.V1;

    /// <inheritdoc />
    public IMorphology CreateMorphology(Lexicon.Lexicon lexicon) => new MorphologyGenerator(lexicon);

    /// <inheritdoc />
    public IAnalysisRules CreateAnalysisRules(Lexicon.Lexicon lexicon, IMorphology morphology) => new SpanishAnalysisRules(morphology);

    /// <inheritdoc />
    public ITemplateFunctions CreateTemplateFunctions(Lexicon.Lexicon lexicon, IMorphology morphology) => new SpanishTemplateFunctions(lexicon, morphology);

    private sealed class SpanishNormalizer : ITextNormalizer
    {
        public NormalizedText Normalize(string input, NormalizationOptions? options = null) => TextNormalizer.Normalize(input, options);
    }

    private sealed class SpanishFolding : ILexiconFolding
    {
        public string Key(string text) => TextFolding.Normalize(text);

        public string LenientKey(string text) => TextFolding.StripAccents(text);

        public string MatchingKey(string text) => Accents.Strip(text);
    }

    private sealed class SpanishNumberWords : INumberWords
    {
        public int MaxValue => SpanishNumbers.MaxValue;

        public string ToWords(int value) => SpanishNumbers.ToWords(value);

        public bool TryParse(IReadOnlyList<string> tokens, out int value) => SpanishNumbers.TryParse(tokens, out value);

        public bool TryParse(string text, out int value) => SpanishNumbers.TryParse(text, out value);

        public int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value) => SpanishNumbers.TryParsePrefix(tokens, start, out value);

        public string DigitsToWords(string text) => SpanishNumbers.DigitsToWords(text);

        public string Agree(string words, string gender) => NumberAgreement.Agree(words, gender);
    }

    private sealed class SpanishPhoneticKeyAdapter : IPhoneticKey
    {
        public string Compute(string word) => SpanishPhoneticKey.Compute(word);
    }

    private sealed class SpanishPunctuation : IPunctuation
    {
        public string Fix(string text) => PunctuationFixer.Fix(text);
    }

    private sealed class SpanishG2pAdapter : IG2p
    {
        public G2pResult Phonemize(string text, DialectConfig dialect) => SpanishG2p.Phonemize(text, dialect);

        public IReadOnlyList<G2pResult> PhonemizeAll(string text, IEnumerable<DialectConfig> dialects) => SpanishG2p.PhonemizeAll(text, dialects);

        public IReadOnlyList<G2pResult> PhonemizeAll(string text) => SpanishG2p.PhonemizeAll(text);
    }
}
