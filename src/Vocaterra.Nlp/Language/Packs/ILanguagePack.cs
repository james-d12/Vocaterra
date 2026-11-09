using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// Everything the engine needs to know about one target language on the text side: how its text is normalized and folded, how its numbers,
/// sounds and punctuation work, and the small closed vocabularies the grammar defaults, the linter and the repair strategy use. A pack is pure,
/// deterministic code (ADR 0002): ordinal comparison, invariant culture, no clock, no randomness.
/// </summary>
/// <remarks>
/// The text-side members came in M32 and M33; the analysis-side members (the morphology, the analysis rules, the template functions, the lexicon profile and
/// the error tag set) came in M34. A host reaches the pack of a loaded bundle through
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

    /// <summary>
    /// The ids of the grammatical cases of the language, such as <c>nom</c>, <c>acc</c>, <c>dat</c>, <c>gen</c> (M88, additive; the default is none, which is Spanish). The
    /// template filters <c>article</c> and <c>words</c> take a case only from this list, and a pack with none rejects a case argument with a clear error.
    /// </summary>
    IReadOnlyList<string> GrammaticalCases => [];

    /// <summary>
    /// True when the pack can expand grammars that declare <c>order: "v2"</c> (M88, additive; the default is false, which is Spanish). The intent catalog loader fails
    /// for a grammar that declares it when this is false.
    /// </summary>
    bool SupportsV2Order => false;

    /// <summary>The closed word lists the content linter checks against.</summary>
    LintVocabulary Lint { get; }

    /// <summary>The agent lines the repair strategy falls back to when a bundle has no pool for them.</summary>
    RepairLines RepairFallback { get; }

    /// <summary>The spelling policy of recasts (accent restoration). A narrow text-side member added in M33; the rest of the recast is neutral.</summary>
    IRecastPolicy Recast { get; }

    /// <summary>The wording of the pipeline's orthography notes (M87). Spanish: <c>SpanishAccentNotes</c>.</summary>
    IOrthographyNotes Orthography { get; }

    /// <summary>
    /// The words the content validator renders every dialogue template with (a noun, a verb, a spelling and the reserved feedback samples), so a template
    /// is checked in the language it is written in (M35). Spanish: the values the validator always used.
    /// </summary>
    SampleWords Samples { get; }

    /// <summary>The closed vocabularies of the language's lexicon files, which every lexicon entry is validated against (M34).</summary>
    LexiconProfile LexiconProfile { get; }

    /// <summary>The error tags of the language and their ranks (M34). Spanish: <see cref="ErrorTagSet.V1"/>.</summary>
    ErrorTagSet ErrorTags { get; }

    /// <summary>Creates the morphology of the language over a lexicon (M34).</summary>
    /// <param name="lexicon">The bundle's lexicon, built with this pack's <see cref="Folding"/>.</param>
    IMorphology CreateMorphology(Lexicon.Lexicon lexicon);

    /// <summary>Creates the analysis rules of the language (M34). The word-class members must not depend on the lexicon.</summary>
    /// <param name="lexicon">The bundle's lexicon.</param>
    /// <param name="morphology">The morphology from <see cref="CreateMorphology"/> over the same lexicon.</param>
    IAnalysisRules CreateAnalysisRules(Lexicon.Lexicon lexicon, IMorphology morphology);

    /// <summary>Creates the functions behind the filters of dialogue templates (M34).</summary>
    /// <param name="lexicon">The bundle's lexicon.</param>
    /// <param name="morphology">The morphology from <see cref="CreateMorphology"/> over the same lexicon.</param>
    ITemplateFunctions CreateTemplateFunctions(Lexicon.Lexicon lexicon, IMorphology morphology);
}
