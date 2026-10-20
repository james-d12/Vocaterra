namespace Slg.Response.Templates;

/// <summary>Makes a spelled-out number agree with the noun that follows it: <c>uno</c> becomes <c>un</c> or <c>una</c>, <c>veintiuno</c> becomes <c>veintiún</c> or <c>veintiuna</c>.</summary>
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
}
