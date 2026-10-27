using System.Globalization;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>
/// Rebuilds the player's sentence with every grammar error corrected, for example <c>yo tiene dos espada</c> becomes <c>yo tengo dos espadas</c>.
/// Digit tokens are spelled out because the result is meant to be spoken.
/// </summary>
/// <remarks>
/// The spelling of the words (digits as words, the restored accents) comes from the language pack: <see cref="Build(NormalizedText, AnalysisResult, NlpLexicon, ILanguagePack)"/>
/// takes the bundle's pack. The overloads without a pack are the Spanish compatibility entry points (they use <see cref="SpanishLanguagePack.Instance"/>).
/// </remarks>
public static class RecastBuilder
{
    /// <summary>
    /// Builds the Spanish recast without a lexicon, so no accent is restored; see the overload that takes one. Spanish compatibility entry point.
    /// </summary>
    public static string Build(NormalizedText playerText, AnalysisResult analysis)
    {
        return Build(playerText, analysis, NlpLexicon.Empty, SpanishLanguagePack.Instance);
    }

    /// <summary>
    /// Builds the Spanish recast with the lexicon (see the overload with a pack). Spanish compatibility entry point: a bundle's recast is
    /// <c>RecastBuilder.Build(text, analysis, bundle.Lexicon, bundle.Language)</c>.
    /// </summary>
    public static string Build(NormalizedText playerText, AnalysisResult analysis, NlpLexicon lexicon)
    {
        return Build(playerText, analysis, lexicon, SpanishLanguagePack.Instance);
    }

    /// <summary>
    /// Applies every error tag that has a non-empty <c>Expected</c> to the normalized text by span, from the last span to the first.
    /// Pronunciation tags are skipped. Where spans overlap, the wider edit wins and narrower edits inside it are merged into its replacement;
    /// partially overlapping edits are dropped. Digit tokens such as <c>2</c> are replaced with words (<c>dos</c>, and <c>un</c>/<c>una</c> before a noun).
    /// A word that the lexicon knows only once its accents are ignored (<c>dias</c>) takes the lexicon's accented spelling (<c>días</c>), the first in
    /// ordinal order when several accented spellings fold alike. A word that is a lexicon form as spelled is kept, so an accent-less word that is
    /// another word (<c>se</c>, <c>si</c>, <c>mas</c>) is corrected only when the analysis read it as the accented word that the words around it call for
    /// (<c>esta</c> in <c>dónde esta el árbol</c> is <c>está</c>; see <see cref="AccentRestoration"/>).
    /// </summary>
    public static string Build(NormalizedText playerText, AnalysisResult analysis, NlpLexicon lexicon, ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(playerText);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(language);
        var text = playerText.Text;
        var edits = ResolveErrorEdits(text, analysis);
        AddRestoredSpellings(playerText, language.Recast.RestoredSpellings(playerText, analysis, lexicon), edits);
        AddNumberEdits(playerText, analysis, language.Numbers, edits);

        edits.Sort(static (a, b) => b.Start.CompareTo(a.Start));
        foreach (var edit in edits)
        {
            text = string.Concat(text.AsSpan(0, edit.Start), edit.Replacement, text.AsSpan(edit.End));
        }

        return text;
    }

    private static List<Edit> ResolveErrorEdits(string text, AnalysisResult analysis)
    {
        var candidates = new List<(Edit Edit, int Order)>();
        for (var i = 0; i < analysis.Errors.Count; i++)
        {
            var error = analysis.Errors[i];
            var span = error.Span;
            if (ErrorTags.IsPronunciation(error.Tag)
                || string.IsNullOrWhiteSpace(error.Expected)
                || span.Start < 0
                || span.Length <= 0
                || span.End > text.Length
                || string.Equals(text.Substring(span.Start, span.Length), error.Expected, StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add((new Edit(span.Start, span.Length, error.Expected), i));
        }

        candidates.Sort(static (a, b) =>
        {
            var cmp = b.Edit.Length.CompareTo(a.Edit.Length);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.Edit.Start.CompareTo(b.Edit.Start);
            return cmp != 0 ? cmp : a.Order.CompareTo(b.Order);
        });

        var accepted = new List<Edit>();
        foreach (var (candidate, _) in candidates)
        {
            var overlapping = accepted.Find(a => a.Start < candidate.End && candidate.Start < a.End);
            if (overlapping is null)
            {
                accepted.Add(candidate);
            }
            else if (candidate.Start >= overlapping.Start && candidate.End <= overlapping.End)
            {
                var original = text.Substring(candidate.Start, candidate.Length);
                overlapping.Replacement = SubstituteWords(overlapping.Replacement, original, candidate.Replacement);
            }
        }

        return accepted;
    }

    // The policy's restored spellings, applied where no error edit already covers the token (the same rule for every restoration).
    private static void AddRestoredSpellings(NormalizedText playerText, IReadOnlyDictionary<int, string> spellings, List<Edit> edits)
    {
        foreach (var token in playerText.Tokens)
        {
            var span = token.Span;
            if (span.Start < 0
                || span.Length <= 0
                || span.End > playerText.Text.Length
                || !spellings.TryGetValue(span.Start, out var spelling)
                || !string.Equals(playerText.Text.Substring(span.Start, span.Length), token.Text, StringComparison.Ordinal)
                || edits.Exists(e => e.Start < span.End && span.Start < e.End))
            {
                continue;
            }

            edits.Add(new Edit(span.Start, span.Length, spelling));
        }
    }

    private static string SubstituteWords(string replacement, string original, string corrected)
    {
        var padded = " " + replacement + " ";
        var index = padded.IndexOf(" " + original + " ", StringComparison.Ordinal);
        if (index < 0)
        {
            return replacement;
        }

        var start = index + 1;
        var result = string.Concat(padded.AsSpan(0, start), corrected, padded.AsSpan(start + original.Length));
        return result[1..^1];
    }

    private static void AddNumberEdits(NormalizedText playerText, AnalysisResult analysis, INumberWords numbers, List<Edit> edits)
    {
        for (var i = 0; i < playerText.Tokens.Count; i++)
        {
            var token = playerText.Tokens[i];
            var span = token.Span;
            // A digit run the language cannot spell (above INumberWords.MaxValue, which a toy language sets at 3; M35) stays as digits.
            if (!IsSpellableNumber(token.Text, out var value)
                || value > numbers.MaxValue
                || span.Start < 0
                || span.Length <= 0
                || span.End > playerText.Text.Length
                || edits.Exists(e => e.Start < span.End && span.Start < e.End))
            {
                continue;
            }

            var words = numbers.ToWords(value);
            if (i + 1 < playerText.Tokens.Count)
            {
                words = numbers.Agree(words, GenderAfter(analysis, playerText, i));
            }

            edits.Add(new Edit(span.Start, span.Length, words));
        }
    }

    private static bool IsSpellableNumber(string text, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > 6 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        value = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        return true;
    }

    // The numeral agrees with the noun it quantifies, which can stand behind adjectives (1 grande queso gives un grande queso, not una), so the
    // adjectives are looked through first; without a noun the gender of the next word decides.
    private static string GenderAfter(AnalysisResult analysis, NormalizedText playerText, int numberIndex)
    {
        for (var j = numberIndex + 1; j < playerText.Tokens.Count; j++)
        {
            var readings = analysis.Tokens
                .Where(t => t.Token.Span.Start == playerText.Tokens[j].Span.Start)
                .SelectMany(static t => t.Readings)
                .ToList();
            var noun = readings.Find(static r => string.Equals(r.Pos, "noun", StringComparison.Ordinal) && r.Gender is not null);
            if (noun is not null)
            {
                return noun.Gender!;
            }

            if (!readings.Exists(static r => string.Equals(r.Pos, "adj", StringComparison.Ordinal)))
            {
                break;
            }
        }

        return GenderOf(analysis, playerText.Tokens[numberIndex + 1]);
    }

    private static string GenderOf(AnalysisResult analysis, Token next)
    {
        string? fallback = null;
        foreach (var analyzed in analysis.Tokens)
        {
            if (analyzed.Token.Span.Start != next.Span.Start)
            {
                continue;
            }

            foreach (var reading in analyzed.Readings)
            {
                if (reading.Gender is null)
                {
                    continue;
                }

                if (string.Equals(reading.Pos, "noun", StringComparison.Ordinal))
                {
                    return reading.Gender;
                }

                fallback ??= reading.Gender;
            }
        }

        return fallback ?? "m";
    }

    private sealed class Edit(int start, int length, string replacement)
    {
        public int Start { get; } = start;

        public int Length { get; } = length;

        public int End => Start + Length;

        public string Replacement { get; set; } = replacement;
    }
}
