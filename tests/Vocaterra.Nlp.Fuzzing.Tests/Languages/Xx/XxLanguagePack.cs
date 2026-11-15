using System.Text;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Languages.Xx;

/// <summary>
/// The toy language of M35: a made-up language, <c>xx</c>, with no accent, no inverted mark, no subject pronoun and no Spanish word. Every member
/// is trivial and non-Spanish. It proves that the pipeline, the loader and the oracles run through a pack alone, with no Spanish code behind it.
/// It is written against the engine's public API only, so it is the proof that a language can sit beside Spanish without touching the engine.
/// It lives in the fuzz test project and is not a shipped language.
/// </summary>
public sealed class XxLanguagePack : ILanguagePack
{
    /// <summary>The language id of the toy pack.</summary>
    public const string Id = "xx";

    /// <summary>The number words of the toy language, for 0 to 3. Invented: no word of any real language.</summary>
    public static readonly string[] NumberWords = ["nu", "ko", "lo", "ri"];

    /// <summary>The one instance.</summary>
    public static XxLanguagePack Instance { get; } = new();

    private XxLanguagePack()
    {
    }

    /// <inheritdoc />
    public string LanguageId => Id;

    /// <summary>The text with each lone surrogate read as U+FFFD, in NFC: the form every member of the toy pack works on, so none of them throws.</summary>
    /// <param name="text">The raw text.</param>
    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return XxNormalizer.ReplaceLoneSurrogates(text).Normalize(NormalizationForm.FormC);
    }

    /// <inheritdoc />
    public ITextNormalizer Normalizer { get; } = new XxNormalizer();

    /// <inheritdoc />
    public ILexiconFolding Folding { get; } = new XxFolding();

    /// <inheritdoc />
    public INumberWords Numbers { get; } = new XxNumbers();

    /// <inheritdoc />
    public IPhoneticKey PhoneticKey { get; } = new XxPhoneticKey();

    /// <inheritdoc />
    public IPunctuation Punctuation { get; } = new XxPunctuation();

    /// <summary>The toy language has no grapheme-to-phoneme converter, so pronunciation scoring is off for it.</summary>
    public IG2p? G2p => null;

    /// <inheritdoc />
    public GrammarDefaults GrammarDefaults { get; } = new(false, [], []);

    /// <inheritdoc />
    public LintVocabulary Lint { get; } = new([], [], []);

    /// <inheritdoc />
    public RepairLines RepairFallback { get; } = new(
        "Say it again, please.",
        "Slowly, once more.",
        "We will stop here.",
        new OfferLineFormat("Did you mean ", "'", "'", ", ", " or ", "?"));

    /// <inheritdoc />
    public IRecastPolicy Recast { get; } = new XxRecastPolicy();

    /// <inheritdoc />
    public SampleWords Samples { get; } = new("ka", "mo", "ri", "nu", "ko", "ka mo", "vocab.unknown");

    /// <summary>A profile with one number system and no gender, person, tense or mood.</summary>
    public LexiconProfile LexiconProfile { get; } = new(
        genders: [],
        numbers: LexiconSchema.Numbers,
        persons: [],
        tenses: [],
        moods: []);

    /// <inheritdoc />
    public IOrthographyNotes Orthography { get; } = new XxOrthographyNotes();

    /// <summary>The toy language's own tags, in rank order. None of them is a Spanish tag.</summary>
    public ErrorTagSet ErrorTags { get; } = new(["vocab.unknown", "vocab.wrong", "xx.word_order"]);

    /// <inheritdoc />
    public IMorphology CreateMorphology(NlpLexicon lexicon) => new XxMorphology();

    /// <inheritdoc />
    public IAnalysisRules CreateAnalysisRules(NlpLexicon lexicon, IMorphology morphology) => new XxAnalysisRules();

    /// <inheritdoc />
    public ITemplateFunctions CreateTemplateFunctions(NlpLexicon lexicon, IMorphology morphology) => new XxTemplateFunctions();

    /// <summary>
    /// Splits on runs of letters and digits, lower cases the tokens and keeps the source spelling in <see cref="Token.Original"/>. Text is the tokens joined
    /// with single spaces. The numeric option does not apply: the toy language writes numbers as digits.
    /// </summary>
    private sealed class XxNormalizer : ITextNormalizer
    {
        public NormalizedText Normalize(string input, NormalizationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(input);
            var source = Clean(input);
            var tokens = new List<Token>();
            var joined = new StringBuilder();
            var index = 0;
            while (index < source.Length)
            {
                if (!char.IsLetterOrDigit(source, index))
                {
                    index++;
                    continue;
                }

                var start = index;
                while (index < source.Length && char.IsLetterOrDigit(source, index))
                {
                    index++;
                }

                var original = source[start..index];
                if (joined.Length > 0)
                {
                    joined.Append(' ');
                }

                var span = new TextSpan(joined.Length, original.Length);
                var text = original.ToLowerInvariant();
                joined.Append(text);
                tokens.Add(new Token(text, span, original));
            }

            return new NormalizedText(joined.ToString(), tokens);
        }

        internal static string ReplaceLoneSurrogates(string text)
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
    }

    /// <summary>NFC and ordinal lower case; the toy language has no marks to fold away.</summary>
    private sealed class XxFolding : ILexiconFolding
    {
        public string Key(string text) => Clean(text).ToLowerInvariant();

        public string LenientKey(string text) => Key(text);

        public string MatchingKey(string text) => Clean(text);
    }

    /// <summary>Digits stay digits; the four number words are the only words.</summary>
    private sealed class XxNumbers : INumberWords
    {
        public int MaxValue => NumberWords.Length - 1;

        public string ToWords(int value)
        {
            if (value < 0 || value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The toy language spells 0 to 3.");
            }

            return NumberWords[value];
        }

        public bool TryParse(IReadOnlyList<string> tokens, out int value)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            value = 0;
            return tokens.Count == 1 && TryWord(tokens[0], out value);
        }

        public bool TryParse(string text, out int value)
        {
            ArgumentNullException.ThrowIfNull(text);
            return TryParse(text.Split(' ', StringSplitOptions.RemoveEmptyEntries), out value);
        }

        public int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            value = 0;
            return start >= 0 && start < tokens.Count && TryWord(tokens[start], out value) ? 1 : 0;
        }

        public string DigitsToWords(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return text;
        }

        public string Agree(string words, string gender)
        {
            ArgumentNullException.ThrowIfNull(words);
            return words;
        }

        private static bool TryWord(string word, out int value)
        {
            value = Array.IndexOf(NumberWords, word);
            return value >= 0;
        }
    }

    /// <summary>The letters of the word, lower case: a sound-alike key that keeps only the letters.</summary>
    private sealed class XxPhoneticKey : IPhoneticKey
    {
        public string Compute(string word)
        {
            ArgumentNullException.ThrowIfNull(word);
            var builder = new StringBuilder(word.Length);
            foreach (var c in word.ToLowerInvariant())
            {
                if (c is >= 'a' and <= 'z')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>Collapses whitespace; the toy language has no paired marks.</summary>
    private sealed class XxPunctuation : IPunctuation
    {
        public string Fix(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>The toy language has no accent, so its note is the accented form alone.</summary>
    private sealed class XxOrthographyNotes : IOrthographyNotes
    {
        public string Describe(AccentNote note) => note.Accented;
    }

    /// <summary>The toy language restores no accents, so a recast keeps the words as they are.</summary>
    private sealed class XxRecastPolicy : IRecastPolicy
    {
        public IReadOnlyDictionary<int, string> RestoredSpellings(NormalizedText text, AnalysisResult analysis, NlpLexicon lexicon) =>
            new Dictionary<int, string>();
    }
}
