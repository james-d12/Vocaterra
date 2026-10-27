using CsCheck;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// A sentence as the oracles see it: the text the pipeline reads, and the grammar it was built from. <see cref="PatternSource"/> is <c>catalog</c> when
/// the sentence was built from the bundle's own grammar <see cref="PatternText"/> (which is that grammar's id), and anything else when it was not.
/// </summary>
/// <param name="Text">The sentence as written.</param>
/// <param name="PatternSource">Where the pattern came from (<c>catalog</c> for a grammar of the bundle).</param>
/// <param name="PatternText">The canonical pattern text, which is also the grammar id.</param>
public sealed record FuzzSentence(string Text, string PatternSource, string PatternText);

/// <summary>
/// The per-language inputs of the fuzz oracles (research 4.8): the pack, the bundle, a sentence realiser, a mutation set and a text generator. The
/// Spanish classes run on <see cref="Spanish"/>, whose members are the generators the suite always used, so the same seed gives the same cases
/// (<c>FuzzLanguageStreamTests</c> proves it). A new language is a new record of the same shape (<c>Languages/Xx</c> is the toy one).
/// </summary>
/// <param name="Pack">The language's pack: the text and analysis seams every oracle goes through.</param>
/// <param name="Bundle">The bundle the oracles run on (its lexicon, intents, prompts, agents and dialogue).</param>
/// <param name="FreeText">The text generator: free text the learner might say, benign and adversarial.</param>
/// <param name="Sentences">The sentence realiser: a sentence built from the language's grammars (or its words), or null when none can be built.</param>
/// <param name="Mutations">The mutation set of the language (the kinds O4 and O5 apply); empty when the language has none yet.</param>
public sealed record FuzzLanguage(
    ILanguagePack Pack,
    ContentBundle Bundle,
    Gen<string> FreeText,
    Gen<FuzzSentence?> Sentences,
    IReadOnlyList<MutationKind> Mutations)
{
    /// <summary>The Spanish record: the shared harness's bundle, the Spanish pack and the generators the Spanish suite has always used.</summary>
    public static FuzzLanguage Spanish { get; } = new(
        SpanishLanguagePack.Instance,
        PipelineHarness.Shared.Bundle,
        SpanishTextGen.Text(PipelineHarness.Shared.Words),
        SentenceGen.Recipe(PipelineHarness.Shared).Select(static recipe => FromRealised(SentenceGen.Realize(PipelineHarness.Shared, recipe))),
        Enum.GetValues<MutationKind>());

    /// <summary>The realised Spanish sentence as a <see cref="FuzzSentence"/>, or null when the realiser gave none.</summary>
    public static FuzzSentence? FromRealised(GeneratedSentence? sentence)
    {
        return sentence is null ? null : new FuzzSentence(sentence.Text, sentence.PatternSource, sentence.PatternText);
    }
}
