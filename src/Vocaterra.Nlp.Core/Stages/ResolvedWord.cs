namespace Vocaterra.Nlp.Core.Stages;

/// <summary>What the analysis made of one token: a word with a reading, a name bound to a grammar's <c>[WORD]</c> element, or an unknown word.</summary>
public enum WordRole
{
    /// <summary>A word the lexicon knows; <see cref="ResolvedWord.Reading"/> is its chosen reading.</summary>
    Word,

    /// <summary>A word the best slot grammar binds to a <c>[WORD]</c> element (a name); it has no reading.</summary>
    Name,

    /// <summary>A word the lexicon does not know and no grammar binds; it has no reading.</summary>
    Unknown,
}

/// <summary>
/// One normalized token with its role and the reading the resolver chose for it.
/// </summary>
/// <param name="Token">The token: its text in the normalized string, its span there and the surface as typed (<see cref="Token.Original"/>).</param>
/// <param name="Role">Whether the token is a word, a name or unknown.</param>
/// <param name="Reading">
/// The reading that remains after resolution, with its lemma, part of speech and features; null for a name or an unknown word. When resolution left
/// several readings of the word, the first of them in lexicon order is reported (the agreement checks read the word as the same class).
/// </param>
public sealed record ResolvedWord(Token Token, WordRole Role, LexicalFeatures? Reading);
