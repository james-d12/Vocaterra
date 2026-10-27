using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Language.Lexicon;

/// <summary>
/// The language-neutral folding (M53): the key is <see cref="TextKey.Of"/> (NFC and invariant lower case), the lenient key is the same (no optional marks
/// are folded away) and the matching key is the text as it is. It is what the engine uses where no pack's folding is given, for example in
/// <see cref="Lexicon.Empty"/>; a language's pack brings its own.
/// </summary>
public sealed class InvariantLexiconFolding : ILexiconFolding
{
    private InvariantLexiconFolding()
    {
    }

    /// <summary>The one instance.</summary>
    public static InvariantLexiconFolding Instance { get; } = new();

    /// <inheritdoc />
    public string Key(string text) => TextKey.Of(text);

    /// <inheritdoc />
    public string LenientKey(string text) => TextKey.Of(text);

    /// <inheritdoc />
    public string MatchingKey(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text;
    }
}
