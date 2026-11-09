using Vocaterra.Nlp.Es.Analysis;
using Vocaterra.Nlp.Es.Normalization;

namespace Vocaterra.Nlp.Es.Text;

/// <summary>
/// Spanish number agreement (the Spanish implementation of <see cref="INumberWords.Agree(string, string)"/>). Makes a spelled-out number agree with the noun that follows it: <c>uno</c> becomes <c>un</c> or <c>una</c>, <c>veintiuno</c> becomes <c>veintiún</c> or <c>veintiuna</c>.
/// </summary>
internal static class NumberAgreement
{
    public static string Agree(string words, string gender)
    {
        if (!words.EndsWith("uno", StringComparison.Ordinal))
        {
            return words;
        }

        var stem = words[..^3];
        var feminine = string.Equals(gender, "f", StringComparison.Ordinal);
        if (stem.EndsWith("veinti", StringComparison.Ordinal))
        {
            return stem + (feminine ? "una" : "ún");
        }

        return stem + (feminine ? "una" : "un");
    }

    /// <summary>
    /// <see cref="Agree(string, string)"/> for <c>uno</c> directly before its singular noun: a feminine noun that starts with a stressed a takes <c>un</c> (<c>un hada</c>,
    /// <c>un hacha</c>), as it takes the article <c>el</c> (issue #42; <c>WordClasses.TakesElBeforeFeminine</c>).
    /// </summary>
    public static string Agree(string words, string gender, string nounLemma)
    {
        ArgumentNullException.ThrowIfNull(nounLemma);
        return string.Equals(words, "uno", StringComparison.Ordinal) && string.Equals(gender, "f", StringComparison.Ordinal)
            && WordClasses.TakesElBeforeFeminine(TextFolding.Normalize(nounLemma))
            ? "un"
            : Agree(words, gender);
    }
}
