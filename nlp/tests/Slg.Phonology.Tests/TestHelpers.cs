namespace Slg.Phonology.Tests;

/// <summary>Shared helpers for the G2P tests.</summary>
internal static class TestHelpers
{
    public static G2pResult La(string text)
    {
        return SpanishG2p.Phonemize(text, DialectConfig.LatinAmericanNeutral);
    }

    public static G2pResult Es(string text)
    {
        return SpanishG2p.Phonemize(text, DialectConfig.CastilianStandard);
    }

    /// <summary>The spelling of each syllable, cut out of the normalized text with the alignment spans.</summary>
    public static string[] SyllableSpellings(G2pResult result)
    {
        return result.Syllables
            .Select(s => result.Text.Substring(s.GraphemeSpan.Start, s.GraphemeSpan.Length))
            .ToArray();
    }

    /// <summary>The spelling syllables joined with hyphens, as dictionaries show them.</summary>
    public static string Hyphenate(string text)
    {
        return string.Join('-', SyllableSpellings(La(text)));
    }

    public static string[] Tokens(string ipa)
    {
        return PhoneInventory.Tokenize(ipa).ToArray();
    }
}
