using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Es.Analysis;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Es.Text;

/// <summary>
/// The Spanish recast policy: a word the lexicon knows only once its accents are ignored takes the lexicon's accented spelling (<c>dias</c> to
/// <c>días</c>, and since M70 <c>manana</c> to <c>mañana</c>), a word the analysis read as an accented word takes that spelling (<see cref="AccentRestoration"/>),
/// and a verb the analysis read as its accented form takes it (M70: <c>compre</c> to <c>compré</c>, an <see cref="AccentNote"/>). The first rule wins a token.
/// </summary>
/// <remarks>
/// This is the Spanish implementation of <see cref="IRecastPolicy"/> (M33; the moved logic of <c>RecastBuilder</c>). A token is keyed by its span start,
/// and a token takes no spelling when its text is not the one at its span, as before the move. The caller still drops a restoration that overlaps an
/// error edit.
/// </remarks>
internal sealed class SpanishRecastPolicy : IRecastPolicy
{
    /// <inheritdoc />
    public IReadOnlyDictionary<int, string> RestoredSpellings(NormalizedText text, AnalysisResult analysis, Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        var analysed = AccentRestoration.SpellingsFor(lexicon, analysis, new SpanishAnalysisRules(new Morphology.MorphologyGenerator(lexicon)));
        var result = new Dictionary<int, string>();
        foreach (var token in text.Tokens)
        {
            var span = token.Span;
            if (span.Start < 0
                || span.Length <= 0
                || span.End > text.Text.Length
                || !string.Equals(text.Text.Substring(span.Start, span.Length), token.Text, StringComparison.Ordinal))
            {
                continue;
            }

            if (AccentInsensitiveSpelling(token.Text, lexicon) is { } spelling)
            {
                result[span.Start] = spelling;
            }
            else if (analysed.TryGetValue(span.Start, out var read))
            {
                result[span.Start] = read;
            }
            else if (analysis.Notes?.FirstOrDefault(n => n.Span == span) is { } note)
            {
                // M70: a verb read as its accented form (compre as compré) takes that spelling, as an accent variant read from its neighbours does.
                result[span.Start] = note.Accented;
            }
        }

        return result;
    }

    private static string? AccentInsensitiveSpelling(string word, Lexicon lexicon)
    {
        var lookup = lexicon.Lookup(word);
        if (lookup.Count == 0 || !lookup.IsAccentInsensitive)
        {
            return null;
        }

        var spellings = lexicon.SpellingsIgnoringAccents(word);
        return spellings.Count > 0 && !string.Equals(spellings[0], word, StringComparison.Ordinal) ? spellings[0] : null;
    }
}
