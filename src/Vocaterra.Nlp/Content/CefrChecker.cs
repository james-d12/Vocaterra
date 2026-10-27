using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Response.Templates;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Content;

/// <summary>A lemma used by dialogue templates or prompt texts that is not in the CEFR list, with the lines or prompts that use it.</summary>
/// <param name="Lemma">The lemma (the token text itself when the lexicon does not know the word).</param>
/// <param name="InLexicon">True when the lexicon knows the word.</param>
/// <param name="LineIds">Ids of the dialogue lines (or prompt ids, in a prompt finding) that use it, in ordinal order.</param>
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

/// <summary>The outcome of checking the text of prompts (<see cref="PromptSpec.Text"/>) against a CEFR level.</summary>
/// <param name="TotalLemmas">Distinct lemmas in the text of all prompts that have text.</param>
/// <param name="CoveredLemmas">How many of them are in the level's list (or a lower level's).</param>
/// <param name="Missing">The lemmas that are not, in ordinal order; <see cref="CefrMissingLemma.LineIds"/> holds prompt ids.</param>
public sealed record CefrPromptReport(int TotalLemmas, int CoveredLemmas, IReadOnlyList<CefrMissingLemma> Missing)
{
    /// <summary>Structural equality including the missing list.</summary>
    public bool Equals(CefrPromptReport? other)
    {
        return other is not null
            && TotalLemmas == other.TotalLemmas
            && CoveredLemmas == other.CoveredLemmas
            && Missing.SequenceEqual(other.Missing);
    }

    /// <summary>Hash consistent with <see cref="Equals(CefrPromptReport)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(TotalLemmas);
        hash.Add(CoveredLemmas);
        foreach (var missing in Missing)
        {
            hash.Add(missing);
        }

        return hash.ToHashCode();
    }
}

/// <summary>The outcome of checking dialogue templates against a CEFR level, and optionally the text of prompts.</summary>
/// <param name="Level">The level that was checked (lower case).</param>
/// <param name="TotalLemmas">Distinct lemmas in the literal text of all dialogue templates (numbers excluded).</param>
/// <param name="CoveredLemmas">How many of them are in the level's list (or a lower level's).</param>
/// <param name="Missing">The lemmas that are not, in ordinal order.</param>
/// <param name="Prompts">The prompt-text findings, or null when the check was run without prompts.</param>
public sealed record CefrReport(
    string Level,
    int TotalLemmas,
    int CoveredLemmas,
    IReadOnlyList<CefrMissingLemma> Missing,
    CefrPromptReport? Prompts = null)
{
    /// <summary>Structural equality including the missing lists and the prompt findings.</summary>
    public bool Equals(CefrReport? other)
    {
        return other is not null
            && string.Equals(Level, other.Level, StringComparison.Ordinal)
            && TotalLemmas == other.TotalLemmas
            && CoveredLemmas == other.CoveredLemmas
            && Missing.SequenceEqual(other.Missing)
            && Equals(Prompts, other.Prompts);
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

        hash.Add(Prompts);
        return hash.ToHashCode();
    }
}

/// <summary>Finds the lemmas of dialogue templates and of prompt texts that are above a CEFR level.</summary>
/// <remarks>
/// Only the literal text of a template is analyzed (placeholders such as <c>{item}</c> are removed first), with indefinite articles kept.
/// Prompt text is plain text: the core does not fill slots in it and the host shows it as written, so it is analyzed as written.
/// A word counts as covered when any of its lexicon readings has a lemma in the list; numbers and numeral readings are ignored. A level's allowed set is its own
/// list plus the lists of all lower levels (CEFR codes compare in ordinal order, so <c>a2</c> includes <c>a1</c>).
/// </remarks>
public static class CefrChecker
{
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    /// <summary>Checks the bundle's dialogue templates and prompt texts against the level; throws <see cref="ArgumentException"/> when the bundle has no list for it.</summary>
    public static CefrReport Check(ContentBundle bundle, string level)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return Check(bundle.Dialogue, bundle.Lexicon, bundle.Language, AllowedLemmas(bundle.Cefr, level), level, bundle.Prompts);
    }

    /// <summary>
    /// Checks the canonical phrase of every answer of every answer set against the level (aliases are alternatives the learner may say, so they are not held to it).
    /// The result reuses the prompt report shape: <see cref="CefrMissingLemma.LineIds"/> holds answer set ids. Throws <see cref="ArgumentException"/> when the bundle has no list for the level.
    /// </summary>
    public static CefrPromptReport CheckAnswerSets(ContentBundle bundle, string level)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var allowed = AllowedLemmas(bundle.Cefr, level);
        var language = bundle.Language;
        var analyzer = new LinguisticAnalyzer(bundle.Lexicon, language);
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        var missing = new SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)>(StringComparer.Ordinal);
        foreach (var set in bundle.AnswerSets.Values.OrderBy(static set => set.AnswerSetId, StringComparer.Ordinal))
        {
            foreach (var answer in set.Answers)
            {
                Tally(analyzer, language, answer.Canonical, set.AnswerSetId, allowed, covered, missing);
            }
        }

        var lemmas = ToMissing(missing);
        return new CefrPromptReport(covered.Count + lemmas.Length, covered.Count, lemmas);
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

    /// <summary>
    /// Checks the templates of a dialogue catalog against an explicit set of allowed lemmas, and the text of the prompts when they are given.
    /// Prompts without text are skipped; a null <paramref name="prompts"/> leaves <see cref="CefrReport.Prompts"/> null.
    /// </summary>
    public static CefrReport Check(
        DialogueCatalog dialogue,
        NlpLexicon lexicon,
        ILanguagePack language,
        IReadOnlySet<string> allowed,
        string level,
        IReadOnlyDictionary<string, PromptSpec>? prompts = null)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(allowed);
        var analyzer = new LinguisticAnalyzer(lexicon, language);
        var covered = new SortedSet<string>(StringComparer.Ordinal);
        var missing = new SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)>(StringComparer.Ordinal);
        foreach (var pool in dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                Tally(analyzer, language, LiteralText(variant.Text), variant.LineId, allowed, covered, missing);
            }
        }

        var missingLemmas = ToMissing(missing);
        CefrPromptReport? promptReport = null;
        if (prompts is not null)
        {
            var promptCovered = new SortedSet<string>(StringComparer.Ordinal);
            var promptMissing = new SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)>(StringComparer.Ordinal);
            foreach (var prompt in prompts.Values.OrderBy(static prompt => prompt.PromptId, StringComparer.Ordinal))
            {
                if (prompt.Text is { } text)
                {
                    Tally(analyzer, language, text, prompt.PromptId, allowed, promptCovered, promptMissing);
                }
            }

            var promptLemmas = ToMissing(promptMissing);
            promptReport = new CefrPromptReport(promptCovered.Count + promptLemmas.Length, promptCovered.Count, promptLemmas);
        }

        return new CefrReport(level.ToLowerInvariant(), covered.Count + missingLemmas.Length, covered.Count, missingLemmas, promptReport);
    }

    /// <summary>
    /// Formats a report as plain text: a count line and one line per missing lemma for the dialogue templates, then the same pair of lines
    /// for the prompt texts when the report has them.
    /// </summary>
    public static string Format(CefrReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"cefr {report.Level}: {report.TotalLemmas} lemmas in dialogue templates, ")
            .Append(CultureInfo.InvariantCulture, $"{report.CoveredLemmas} covered, {report.Missing.Count} not in the list\n");
        AppendMissing(builder, report.Missing);
        if (report.Prompts is { } prompts)
        {
            builder.Append(CultureInfo.InvariantCulture, $"cefr {report.Level}: {prompts.TotalLemmas} lemmas in prompt text, ")
                .Append(CultureInfo.InvariantCulture, $"{prompts.CoveredLemmas} covered, {prompts.Missing.Count} not in the list\n");
            AppendMissing(builder, prompts.Missing);
        }

        return builder.ToString();
    }

    private static void AppendMissing(StringBuilder builder, IReadOnlyList<CefrMissingLemma> missing)
    {
        foreach (var lemma in missing)
        {
            builder.Append("  ").Append(lemma.Lemma).Append(lemma.InLexicon ? string.Empty : " (not in lexicon)")
                .Append(": ").Append(string.Join(", ", lemma.LineIds)).Append('\n');
        }
    }

    // Adds the lemmas of one text to the covered set or to the missing map; sourceId is a dialogue line id or a prompt id.
    private static void Tally(
        LinguisticAnalyzer analyzer,
        ILanguagePack language,
        string text,
        string sourceId,
        IReadOnlySet<string> allowed,
        SortedSet<string> covered,
        SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)> missing)
    {
        var analysis = analyzer.Analyze(language.Normalizer.Normalize(text, KeepArticles));
        foreach (var token in analysis.Tokens)
        {
            if (IsNumber(token.Token.Text))
            {
                continue;
            }

            if (token.Readings.Count == 0)
            {
                Note(missing, token.Token.Text, false, sourceId);
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
                Note(missing, label.Lemma, true, sourceId);
            }
        }
    }

    private static CefrMissingLemma[] ToMissing(SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)> missing)
    {
        return missing.Select(static pair => new CefrMissingLemma(pair.Key, pair.Value.InLexicon, [.. pair.Value.Lines])).ToArray();
    }

    private static void Note(
        SortedDictionary<string, (bool InLexicon, SortedSet<string> Lines)> missing,
        string lemma,
        bool inLexicon,
        string sourceId)
    {
        if (!missing.TryGetValue(lemma, out var entry))
        {
            entry = (inLexicon, new SortedSet<string>(StringComparer.Ordinal));
            missing.Add(lemma, entry);
        }

        entry.Lines.Add(sourceId);
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
