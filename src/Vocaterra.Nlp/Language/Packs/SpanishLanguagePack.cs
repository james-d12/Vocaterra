using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// The Spanish pack (Latin American default, ADR 0005). Every member wraps a static class that stays where it is and keeps meaning Spanish
/// (<see cref="TextNormalizer"/>, <see cref="SpanishNumbers"/>, <see cref="SpanishPhoneticKey"/>, <see cref="SpanishG2p"/>, the punctuation fixer,
/// <see cref="RepairStrategy"/>); a wrapper returns exactly what its static returns. M53 moves the pack out of the engine assembly.
/// </summary>
public sealed class SpanishLanguagePack : ILanguagePack
{
    private SpanishLanguagePack()
    {
    }

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
    public GrammarDefaults GrammarDefaults { get; } = new(true, WordClasses.SubjectPronounLemmas, ["por", "favor"]);

    /// <inheritdoc />
    public LintVocabulary Lint { get; } = new(WordClasses.SubjectPronounLemmas, ["por", "favor"], ["un", "una"]);

    /// <inheritdoc />
    public RepairLines RepairFallback { get; } = new(RepairStrategy.AskAgainLine, RepairStrategy.SlowRepeatLine, RepairStrategy.GiveUpLine);

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
