using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;

namespace Vocaterra.Nlp.Fuzzing.Tests.Oracles;

/// <summary>
/// The errors that the names of a sentence must produce when the sentence's grammar does not bind them (M17b). A name (<c>[WORD]</c>) is bound only
/// when the grammar matches the sentence; a mutation that breaks the match (a leading adverb, say) leaves the name a plain word, and the analyzer then
/// reports a word the lexicon lacks as <c>vocab.unknown</c>. The mutation and recast oracles expect exactly that. A name the lexicon knows, left unbound, is
/// checked like any other word, and the oracles do not predict those checks, so the case is not compared (it is counted instead).
/// </summary>
internal static class UnboundNames
{
    /// <summary>
    /// The errors expected for the names of the sentence: none when the grammar matched (it binds every name), the <c>vocab.unknown</c> errors of the names
    /// the lexicon lacks when it did not, and null when a name the lexicon knows is left unbound. <paramref name="normalized"/> is the text the analyzer
    /// saw, so the spans are the analyzer's.
    /// </summary>
    public static IReadOnlyList<ExpectedError>? Expect(IReadOnlyList<SentenceToken> tokens, NormalizedText normalized, LexiconCatalog words, bool grammarMatched)
    {
        if (grammarMatched)
        {
            return [];
        }

        if (normalized.Tokens.Count != tokens.Count)
        {
            return null;
        }

        var expected = new List<ExpectedError>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Role != TokenRole.Name)
            {
                continue;
            }

            var norm = tokens[i].Norm;
            if (words.Lexicon.Lookup(norm).Count > 0 || words.Morphology.IsRegularizedIrregular(norm, out _, out _))
            {
                return null;
            }

            expected.Add(new ExpectedError(ErrorTags.VocabUnknown, normalized.Tokens[i].Span, string.Empty, normalized.Tokens[i].Text));
        }

        return expected;
    }
}
