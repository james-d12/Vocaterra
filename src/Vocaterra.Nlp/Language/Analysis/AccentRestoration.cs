namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;

/// <summary>
/// Names the accented spelling of a word that the analysis read as an accented word although the learner wrote it without the accent (<c>esta</c> read as <c>está</c> in
/// <c>dónde esta el árbol</c>), so that a recast can restore the accent as it does for a word the lexicon lacks as spelled (<c>arbol</c>). Nothing about the accent is an
/// error: the analysis does not report it, and only a recast that is built for another reason uses this.
/// </summary>
public static class AccentRestoration
{
    /// <summary>
    /// Returns, for each token of the analysis that is spelled without an accent, is a lexicon form and was read as one accented word, the position of the token in the text
    /// (<c>Token.Span.Start</c>) and that accented spelling. A token with two accented spellings among its readings (<c>este</c> as <c>esté</c> or <c>éste</c>) is left out. The
    /// choice is the one <see cref="ReadingResolver"/> makes, from the words around the token; a slot grammar that bound the word as typed is not known here.
    /// </summary>
    /// <param name="lexicon">The lexicon the words are looked up in.</param>
    /// <param name="analysis">The analysis of the text.</param>
    /// <param name="rules">The language's analysis rules (<see cref="IAnalysisRules"/>).</param>
    public static IReadOnlyDictionary<int, string> SpellingsFor(Lexicon.Lexicon lexicon, AnalysisResult analysis, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(analysis);
        var found = new List<IReadOnlyList<Lexicon.AccentVariant>>(analysis.Tokens.Count);
        foreach (var token in analysis.Tokens)
        {
            found.Add(token.Readings.Count == 0 ? [] : lexicon.LookupWithAccentVariants(token.Token.Text).Variants);
        }

        var readings = found.Select(static variants => (IReadOnlyList<LexicalFeatures>)[.. variants.Select(static v => v.Reading)]).ToList();
        var chosen = new AccentVariantChoice(rules).Choose(analysis.Tokens, readings, _ => false);
        var spellings = new Dictionary<int, string>();
        for (var i = 0; i < analysis.Tokens.Count; i++)
        {
            if (chosen[i] is not { Count: > 0 } picked)
            {
                continue;
            }

            var forms = found[i].Where(v => picked.Contains(v.Reading)).Select(static v => v.Form).Distinct(StringComparer.Ordinal).ToList();
            if (forms.Count == 1)
            {
                spellings[analysis.Tokens[i].Token.Span.Start] = forms[0];
            }
        }

        return spellings;
    }
}
