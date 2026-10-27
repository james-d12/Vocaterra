using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>Turns raw learner or recognizer text into <see cref="NormalizedText"/>.</summary>
public interface ITextNormalizer
{
    /// <summary>Normalizes text with the given options (the default options when <paramref name="options"/> is null).</summary>
    NormalizedText Normalize(string input, NormalizationOptions? options = null);
}

/// <summary>The keys under which words are looked up and matched.</summary>
public interface ILexiconFolding
{
    /// <summary>The lookup key: NFC and invariant lower case.</summary>
    string Key(string text);

    /// <summary>The lenient lookup key: <see cref="Key"/> with the language's optional marks folded away (Spanish: acute accents and diaeresis; the letter n-tilde is kept).</summary>
    string LenientKey(string text);

    /// <summary>The key of answer matching: marks removed from the text as it is (no case folding), never applied to normalized text.</summary>
    string MatchingKey(string text);
}

/// <summary>Number words of one language.</summary>
public interface INumberWords
{
    /// <summary>The largest value <see cref="ToWords"/> and the parsers handle.</summary>
    int MaxValue { get; }

    /// <summary>Spells an integer from 0 to <see cref="MaxValue"/> in words.</summary>
    string ToWords(int value);

    /// <summary>Parses a whole token sequence as one number.</summary>
    bool TryParse(IReadOnlyList<string> tokens, out int value);

    /// <summary>Parses a text of number words as one number.</summary>
    bool TryParse(string text, out int value);

    /// <summary>Parses the longest number that starts at <paramref name="start"/>; returns the number of tokens consumed (0 when none).</summary>
    int TryParsePrefix(IReadOnlyList<string> tokens, int start, out int value);

    /// <summary>Replaces stand-alone digit runs with words.</summary>
    string DigitsToWords(string text);
}

/// <summary>A sound-alike key: words that sound alike share a key.</summary>
public interface IPhoneticKey
{
    /// <summary>Computes the key of one word.</summary>
    string Compute(string word);
}

/// <summary>Cleans rendered text.</summary>
public interface IPunctuation
{
    /// <summary>Collapses whitespace, attaches closing marks and balances the language's paired marks.</summary>
    string Fix(string text);
}

/// <summary>A grapheme-to-phoneme converter.</summary>
public interface IG2p
{
    /// <summary>Converts text to phones under one dialect.</summary>
    G2pResult Phonemize(string text, DialectConfig dialect);

    /// <summary>Converts text under each given dialect.</summary>
    IReadOnlyList<G2pResult> PhonemizeAll(string text, IEnumerable<DialectConfig> dialects);

    /// <summary>Converts text under every default dialect of the language.</summary>
    IReadOnlyList<G2pResult> PhonemizeAll(string text);
}

/// <summary>The implicit optional parts every slot grammar of a language allows.</summary>
/// <param name="OptionalSubjectPronoun">True when a grammar may be preceded by an explicit subject pronoun it does not list.</param>
/// <param name="SubjectPronouns">The subject pronoun lemmas, in the order the language lists them.</param>
/// <param name="TrailingPoliteWords">The words of the optional trailing polite phrase (Spanish: <c>por</c>, <c>favor</c>); empty when there is none.</param>
public sealed record GrammarDefaults(bool OptionalSubjectPronoun, IReadOnlyList<string> SubjectPronouns, IReadOnlyList<string> TrailingPoliteWords);

/// <summary>The closed word lists the content linter checks against.</summary>
/// <param name="SubjectPronouns">The subject pronouns a grammar example may begin with.</param>
/// <param name="PolitePhrase">The words of the polite phrase an example may end with.</param>
/// <param name="ArticleNumeralHomographs">Words that are both an indefinite article and the numeral one.</param>
public sealed record LintVocabulary(IReadOnlyList<string> SubjectPronouns, IReadOnlyList<string> PolitePhrase, IReadOnlyList<string> ArticleNumeralHomographs);

/// <summary>The agent lines of the repair strategy, used when the bundle has no pool for them.</summary>
/// <param name="AskAgain">The line that asks the player to say it again.</param>
/// <param name="SlowRepeat">The line that introduces a slower repetition.</param>
/// <param name="GiveUp">The line that closes a failed repair.</param>
public sealed record RepairLines(string AskAgain, string SlowRepeat, string GiveUp);
