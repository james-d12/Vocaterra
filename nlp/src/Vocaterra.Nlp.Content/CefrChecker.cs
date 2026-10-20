using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content;

/// <summary>A lemma used by dialogue templates that is not in the CEFR list, with the lines that use it.</summary>
/// <param name="Lemma">The lemma (the token text itself when the lexicon does not know the word).</param>
/// <param name="InLexicon">True when the lexicon knows the word.</param>
/// <param name="LineIds">Ids of the dialogue lines that use it, in ordinal order.</param>
public sealed record CefrMissingLemma(string Lemma, bool InLexicon, IReadOnlyList<string> LineIds)
{
    /// <summary>Structural equality including the line ids.</summary>
    public bool Equals(CefrMissingLemma? other)
    {
        return other is not null
            && string.Equals(Lemma, other.Lemma, StringComparison.Ordinal)
            && InLexicon == other.InLexicon
            && LineIds.SequenceEqual(other.LineIds);
    }

    /// <summary>Hash consistent with <see cref="Equals(CefrMissingLemma)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Lemma, StringComparer.Ordinal);
        hash.Add(InLexicon);
        foreach (var id in LineIds)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

/// <summary>The outcome of checking dialogue templates against a CEFR level.</summary>
/// <param name="Level">The level that was checked (lower case).</param>
/// <param name="TotalLemmas">Distinct lemmas in the literal text of all dialogue templates (numbers excluded).</param>
/// <param name="CoveredLemmas">How many of them are in the level's list (or a lower level's).</param>
/// <param name="Missing">The lemmas that are not, in ordinal order.</param>
public sealed record CefrReport(string Level, int TotalLemmas, int CoveredLemmas, IReadOnlyList<CefrMissingLemma> Missing)
{
    /// <summary>Structural equality including the missing list.</summary>
    public bool Equals(CefrReport? other)
    {
        return other is not null
            && string.Equals(Level, other.Level, StringComparison.Ordinal)
            && TotalLemmas == other.TotalLemmas
            && CoveredLemmas == other.CoveredLemmas
            && Missing.SequenceEqual(other.Missing);
    }

    /// <summary>Hash consistent with <see cref="Equals(CefrReport)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Level, StringComparer.Ordinal);
        hash.Add(TotalLemmas);
        hash.Add(CoveredLemmas);
        foreach (var missing in Missing)
        {
            hash.Add(missing);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Finds the lemmas of dialogue templates that are above a CEFR level.</summary>
/// <remarks>
/// Only the literal text of a template is analyzed (placeholders such as <c>{item}</c> are removed first), with indefinite articles kept.
/// A word counts as covered when any of its lexicon readings has a lemma in the list; numbers and numeral readings are ignored. A level's allowed set is its own
/// list plus the lists of all lower levels (CEFR codes compare in ordinal order, so <c>a2</c> includes <c>a1</c>).
/// </remarks>
public static class CefrChecker
{
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    /// <summary>Checks the bundle's dialogue templates against the level; throws <see cref="ArgumentException"/> when the bundle has no list for it.</summary>
    public static CefrReport Check(ContentBundle bundle, string level)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return Check(bundle.Dialogue, bundle.Lexicon, AllowedLemmas(bundle.Cefr, level), level);
    }

    /// <summary>Returns the lemmas allowed at the level: its own list and all lower levels' lists, in ordinal order.</summary>
    public static IReadOnlySet<string> AllowedLemmas(IReadOnlyDictionary<string, IReadOnlySet<string>> cefr, string level)
    {
        ArgumentNullException.ThrowIfNull(cefr);
        ArgumentException.ThrowIfNullOrEmpty(level);
        var key = level.ToLowerInvariant();
        if (!cefr.ContainsKey(key))
        {
            throw new ArgumentException($"The bundle has no CEFR list for level '{level}'.", nameof(level));
        }

        var allowed = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var pair in cefr.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.CompareOrdinal(pair.Key, key) <= 0)
            {
                allowed.UnionWith(pair.Value);
            }
        }

        return allowed;
    }

    /// <summary>Checks the templates of a dialogue catalog against an explicit set of allowed lemmas.</summary>
    public static CefrReport Check(DialogueCatalog dialogue, NlpLexicon lexicon, IReadOnlySet<string> allowed, string level)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(allowed);
        var analyzer = new LinguisticAnalyzer(lexicon);
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        var missing = new SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)>(StringComparer.Ordinal);
        foreach (var pool in dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                var analysis = analyzer.Analyze(TextNormalizer.Normalize(LiteralText(variant.Text), KeepArticles));
                foreach (var token in analysis.Tokens)
                {
                    if (IsNumber(token.Token.Text))
                    {
                        continue;
                    }

                    if (token.Readings.Count == 0)
                    {
                        Note(missing, token.Token.Text, false, variant.LineId);
                        continue;
                    }

                    // Numerals such as "dos" (and the numeral reading of "una") are ignored; "una" still counts as the article "un".
                    var readings = token.Readings.Where(static reading => !string.Equals(reading.Pos, LexiconSchema.Numeral, StringComparison.Ordinal)).ToArray();
                    if (readings.Length == 0)
                    {
                        continue;
                    }

                    var hit = readings.FirstOrDefault(reading => allowed.Contains(reading.Lemma));
                    if (hit is not null)
                    {
                        covered.Add(hit.Lemma);
                    }
                    else
                    {
                        // A word with several readings is listed under the lemma that is spelled like the word when there is one (oro, not orar), else under the first.
                        var label = readings.FirstOrDefault(reading => string.Equals(reading.Lemma, token.Token.Text, StringComparison.Ordinal)) ?? readings[0];
                        Note(missing, label.Lemma, true, variant.LineId);
                    }
                }
            }
        }

        var missingLemmas = missing.Select(static pair => new CefrMissingLemma(pair.Key, pair.Value.InLexicon, [.. pair.Value.Lines])).ToArray();
        return new CefrReport(level.ToLowerInvariant(), covered.Count + missingLemmas.Length, covered.Count, missingLemmas);
    }

    /// <summary>Formats a report as plain text: a count line, then one line per missing lemma.</summary>
    public static string Format(CefrReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"cefr {report.Level}: {report.TotalLemmas} lemmas in dialogue templates, ")
            .Append(CultureInfo.InvariantCulture, $"{report.CoveredLemmas} covered, {report.Missing.Count} not in the list\n");
        foreach (var lemma in report.Missing)
        {
            builder.Append("  ").Append(lemma.Lemma).Append(lemma.InLexicon ? string.Empty : " (not in lexicon)")
                .Append(": ").Append(string.Join(", ", lemma.LineIds)).Append('\n');
        }

        return builder.ToString();
    }

    private static void Note(
        SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)> missing,
        string lemma,
        bool inLexicon,
        string lineId)
    {
        if (!missing.TryGetValue(lemma, out var entry))
        {
            entry = (inLexicon, new SortedSet<string>(StringComparer.Ordinal));
            missing.Add(lemma, entry);
        }

        entry.Lines.Add(lineId);
    }

    private static bool IsNumber(string text)
    {
        return text.Length > 0 && text.All(char.IsAsciiDigit);
    }

    // Removes {placeholders} (and the escapes {{ and }}) so only the template author's own words are analyzed.
    private static string LiteralText(string template)
    {
        var builder = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                i += 2;
            }
            else if (c == '{')
            {
                var close = template.IndexOf('}', i + 1);
                i = close < 0 ? template.Length : close + 1;
                builder.Append(' ');
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }
}
