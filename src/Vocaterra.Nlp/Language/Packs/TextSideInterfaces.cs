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

    /// <summary>
    /// The spellings a writer may type for a lexicon form by leaving out a mark that <see cref="LenientKey"/> keeps, each in <see cref="LenientKey"/> form (M70). The
    /// lexicon reads such a spelling as the form, as it reads a word written without its accent, only when no lexicon form folds to the spelling itself (see
    /// <c>Lexicon.Lookup</c>). Spanish: an <c>n</c> for each <c>ñ</c> (<c>niño</c> gives <c>nino</c>, <c>mañana</c> <c>manana</c>), except where that spelling is
    /// a Spanish word of its own (<c>año</c> gives none, because <c>ano</c> is a word). The default is none.
    /// </summary>
    /// <param name="form">A lexicon form, in <see cref="Key"/> form.</param>
    IReadOnlyList<string> UnmarkedSpellings(string form) => [];
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

    /// <summary>
    /// Makes a spelled number agree with the gender of the noun it quantifies (Spanish: <c>uno</c> becomes <c>un</c> or <c>una</c>). Returns the words
    /// unchanged when the language has no such agreement for them.
    /// </summary>
    /// <param name="words">The number spelled by <see cref="ToWords"/>.</param>
    /// <param name="gender">The gender of the noun, <c>m</c> or <c>f</c>.</param>
    string Agree(string words, string gender);

    /// <summary>
    /// Like <see cref="Agree(string, string)"/>, for a number directly before its noun, whose lemma may change the form (Spanish: <c>un hada</c>, <c>un hacha</c>: a feminine
    /// noun that starts with a stressed a takes <c>un</c>, issue #42). A language with no such rule keeps the default, which ignores the noun.
    /// </summary>
    /// <param name="words">The number spelled by <see cref="ToWords"/>.</param>
    /// <param name="gender">The gender of the noun, <c>m</c> or <c>f</c>.</param>
    /// <param name="nounLemma">The lemma of the singular noun right after the number.</param>
    string Agree(string words, string gender, string nounLemma) => Agree(words, gender);
}

/// <summary>A sound-alike key: words that sound alike share a key.</summary>
public interface IPhoneticKey
{
    /// <summary>Computes the key of one word.</summary>
    string Compute(string word);
}

/// <summary>
/// The spelling policy of recasts: the player's sentence is rebuilt with the errors corrected, and the words that the lexicon knows only with
/// their marks (accents) take the marked spelling. Text-side only; the analysis-side rules of the language (M34) do not belong here.
/// </summary>
public interface IRecastPolicy
{
    /// <summary>
    /// The restored spellings of the player's words, keyed by the span start of the token. A token takes at most one spelling; a token that the policy
    /// does not restore is absent from the result. The caller decides whether a restoration applies (for example, not over an error edit).
    /// </summary>
    /// <param name="text">The normalized text of the player.</param>
    /// <param name="analysis">The analysis of that text.</param>
    /// <param name="lexicon">The lexicon the words are looked up in.</param>
    IReadOnlyDictionary<int, string> RestoredSpellings(NormalizedText text, AnalysisResult analysis, Lexicon.Lexicon lexicon);
}

/// <summary>
/// The words a pack uses for the orthography notes of the pipeline (M87): the short line a host shows beside its reply for an accent the learner left out
/// (<see cref="AccentNote"/>). A host may word a note in its own words, in the learner's language, from the note's members; this is the pack's own wording.
/// </summary>
public interface IOrthographyNotes
{
    /// <summary>The line for the note (Spanish: <c>«compré», con tilde: es pasado</c>).</summary>
    /// <param name="note">The note.</param>
    string Describe(AccentNote note);
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

/// <summary>
/// The sample words the content validator fills the slots of a dialogue template with, when it proves the template renders (M35). A slot read as a verb
/// takes <paramref name="Verb"/>, one read as a number takes the digit 2, and any other slot takes <paramref name="Noun"/>, except the reserved
/// feedback slots, which take <paramref name="Expected"/>, <paramref name="Actual"/>, <paramref name="Recast"/> and <paramref name="Tag"/>, and the
/// <c>spell</c> slot, which takes <paramref name="Spell"/>.
/// </summary>
/// <param name="Noun">A noun of the language (Spanish: <c>espada</c>).</param>
/// <param name="Verb">A verb of the language, in its citation form (Spanish: <c>tener</c>).</param>
/// <param name="Spell">A word to spell (Spanish: <c>fuego</c>).</param>
/// <param name="Expected">The expected form in a correction line (Spanish: <c>tengo</c>).</param>
/// <param name="Actual">The form the learner said in a correction line (Spanish: <c>tiene</c>).</param>
/// <param name="Recast">A short sentence of the language (Spanish: <c>yo tengo dos espadas</c>).</param>
/// <param name="Tag">An error tag of the language's set (Spanish: <c>gender.agree</c>).</param>
public sealed record SampleWords(string Noun, string Verb, string Spell, string Expected, string Actual, string Recast, string Tag);

/// <summary>The agent lines of the repair strategy, used when the bundle has no pool for them.</summary>
/// <param name="AskAgain">The line that asks the player to say it again.</param>
/// <param name="SlowRepeat">The line that introduces a slower repetition.</param>
/// <param name="GiveUp">The line that closes a failed repair.</param>
/// <param name="Offer">How the line that offers up to three choices is worded (M34, additive); null for the Spanish wording.</param>
public sealed record RepairLines(string AskAgain, string SlowRepeat, string GiveUp, OfferLineFormat? Offer = null);

/// <summary>
/// The wording of the repair step that offers the player up to three phrases to choose from, for example <c>¿Quieres decir «a», «b» o «c»?</c>. The text is
/// the pack's: a pack of another language gives its own question, quotes and conjunction (M34, additive).
/// </summary>
/// <param name="Prefix">The text before the first choice, including the trailing space (Spanish: <c>¿Quieres decir </c>).</param>
/// <param name="OpenQuote">The mark before each choice (Spanish: <c>«</c>).</param>
/// <param name="CloseQuote">The mark after each choice (Spanish: <c>»</c>).</param>
/// <param name="Separator">The text between two choices that are not the last pair (Spanish: <c>, </c>).</param>
/// <param name="LastSeparator">The text before the last choice (Spanish: <c> o </c>).</param>
/// <param name="Suffix">The text after the last choice (Spanish: <c>?</c>).</param>
public sealed record OfferLineFormat(string Prefix, string OpenQuote, string CloseQuote, string Separator, string LastSeparator, string Suffix)
{
    /// <summary>Builds the offer line for the choices, in order.</summary>
    /// <param name="choices">The offered phrases; at least one.</param>
    public string Format(IReadOnlyList<string> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var builder = new System.Text.StringBuilder(Prefix);
        for (var i = 0; i < choices.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == choices.Count - 1 ? LastSeparator : Separator);
            }

            builder.Append(OpenQuote).Append(choices[i]).Append(CloseQuote);
        }

        return builder.Append(Suffix).ToString();
    }
}
