using System.Globalization;
using System.Text;
using Slg.Core.Determinism;

namespace Slg.Asr;

/// <summary>Builds the JSON phrase-list grammar that a constrained recognizer (Vosk) is restricted to.</summary>
public static class GrammarBuilder
{
    /// <summary>The out-of-grammar token Vosk emits for speech that matches no phrase.</summary>
    public const string UnknownToken = "[unk]";

    /// <summary>
    /// Returns the grammar as a JSON array string: the phrases lower-cased (invariant culture), NFC-normalized, stripped of everything
    /// except letters, digits and apostrophes, whitespace-collapsed, de-duplicated and ordinal-sorted, followed by <c>[unk]</c>. Phrases that are empty after
    /// normalization or equal to <c>[unk]</c> are dropped.
    /// </summary>
    public static string Build(IReadOnlyList<string> phrases)
    {
        return CanonicalJson.Serialize(BuildList(phrases));
    }

    /// <summary>Returns the same phrases as <see cref="Build"/> as a list, ending with <c>[unk]</c>.</summary>
    public static IReadOnlyList<string> BuildList(IReadOnlyList<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        var unique = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var phrase in phrases)
        {
            ArgumentNullException.ThrowIfNull(phrase, nameof(phrases));
            if (string.Equals(phrase.Trim(), UnknownToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalized = NormalizePhrase(phrase);
            if (normalized.Length > 0)
            {
                unique.Add(normalized);
            }
        }

        var result = new List<string>(unique.Count + 1);
        result.AddRange(unique);
        result.Add(UnknownToken);
        return result;
    }

    /// <summary>Normalizes one phrase the way <see cref="Build"/> does; the result may be empty.</summary>
    public static string NormalizePhrase(string phrase)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        var text = phrase.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            var keep = rune.Value == '\'' || category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark;
            if (keep)
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
                builder.Append(rune.ToString());
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }
}
