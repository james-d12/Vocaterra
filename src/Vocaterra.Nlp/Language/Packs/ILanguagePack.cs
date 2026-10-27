namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// Everything the engine needs to know about one target language on the text side: how its text is normalized and folded, how its numbers,
/// sounds and punctuation work, and the small closed vocabularies the grammar defaults, the linter and the repair strategy use. A pack is pure,
/// deterministic code (ADR 0002): ordinal comparison, invariant culture, no clock, no randomness.
/// </summary>
/// <remarks>
/// This is the text-side half of the pack (milestone M32). The analysis-side members (morphology, analysis rules, template functions, the lexicon
/// profile, the error tag set and the recast policy) join in M34. A host reaches the pack of a loaded bundle through
/// <see cref="Vocaterra.Nlp.Content.ContentBundle.Language"/>. See ADR 0011.
/// </remarks>
public interface ILanguagePack
{
    /// <summary>The primary language subtag the pack serves, lower case ASCII letters: <c>es</c>, <c>de</c>. The registry key.</summary>
    string LanguageId { get; }

    /// <summary>Turns raw learner or recognizer text into tokens.</summary>
    ITextNormalizer Normalizer { get; }

    /// <summary>The lookup key and the lenient key of lexicon and answer matching.</summary>
    ILexiconFolding Folding { get; }

    /// <summary>Number words: parse, spell and replace digits.</summary>
    INumberWords Numbers { get; }

    /// <summary>The sound-alike key for answer matching.</summary>
    IPhoneticKey PhoneticKey { get; }

    /// <summary>Cleans rendered text (spacing, paired marks).</summary>
    IPunctuation Punctuation { get; }

    /// <summary>The grapheme-to-phoneme converter, or null when pronunciation scoring and phone-fed speech are off for this language.</summary>
    IG2p? G2p { get; }

    /// <summary>The implicit optional parts every slot grammar of this language allows.</summary>
    GrammarDefaults GrammarDefaults { get; }

    /// <summary>The closed word lists the content linter checks against.</summary>
    LintVocabulary Lint { get; }

    /// <summary>The agent lines the repair strategy falls back to when a bundle has no pool for them.</summary>
    RepairLines RepairFallback { get; }
}
