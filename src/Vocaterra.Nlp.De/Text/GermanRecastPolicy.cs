using System.Text;

namespace Vocaterra.Nlp.De.Text;

/// <summary>
/// The German recast policy (M37). <see cref="RestoredSpellings"/> restores the umlauts: a word the lexicon knows only once its umlauts are written as <c>ae oe ue</c>
/// (or bare) takes the lexicon's spelling (<c>schoen</c> to <c>schön</c>, <c>Grüsse</c> to <c>grüsse</c>); it never turns <c>ss</c> into <c>ß</c>, because the Swiss spelling is no
/// error (D-game-27 item 6), so <c>Strasse</c> stays and <c>Grüsse</c> keeps its <c>ss</c>. <see cref="RestoredCapitals"/> restores the capitals the normalizer lower-cased:
/// a noun (by the resolved reading's part of speech), a name, the formal <c>Sie</c> (a pronoun reading whose register is <c>formal</c>) and the first word of the
/// sentence. A token that needs both gets its umlauts and its capital from <see cref="RestoredSpellings"/>, so the two edits never overlap.
/// </summary>
/// <remarks>
/// <see cref="MissingCapitals"/> lists the tokens whose typed form (<see cref="Token.Original"/>) lacks the capital they need: the input of the typed-input-only
/// <see cref="ErrorTags.OrthCapital"/> tag, which the analysis rules (M39) raise when the input was typed and never when it came from a recognizer. A word that no reading
/// of the lexicon calls a noun gets no capital, and a word read both as noun and as another class is capitalized only when the resolver chose the noun.
/// </remarks>
public sealed class GermanRecastPolicy : IRecastPolicy
{
    private GermanRecastPolicy()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanRecastPolicy Instance { get; } = new();

    /// <inheritdoc />
    public IReadOnlyDictionary<int, string> RestoredSpellings(NormalizedText text, AnalysisResult analysis, Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        var result = new Dictionary<int, string>();
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            var token = text.Tokens[i];
            if (!IsAtItsSpan(text, token) || UmlautSpelling(token.Text, lexicon) is not { } spelling)
            {
                continue;
            }

            result[token.Span.Start] = NeedsCapital(text, analysis, lexicon, i) ? Capitalize(spelling) : spelling;
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<int, string> RestoredCapitals(NormalizedText text, AnalysisResult analysis, Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        var result = new Dictionary<int, string>();
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            var token = text.Tokens[i];
            if (IsAtItsSpan(text, token) && UmlautSpelling(token.Text, lexicon) is null && NeedsCapital(text, analysis, lexicon, i))
            {
                result[token.Span.Start] = Capitalize(token.Text);
            }
        }

        return result;
    }

    /// <summary>
    /// The tokens that need a capital and whose typed form does not start with one: their index in <paramref name="text"/> and the spelling with the capital.
    /// A token without a letter, or typed with a capital already, is not listed.
    /// </summary>
    /// <param name="text">The normalized text.</param>
    /// <param name="analysis">The analysis of the text.</param>
    /// <param name="lexicon">The lexicon.</param>
    public static IReadOnlyList<(int TokenIndex, string Expected)> MissingCapitals(NormalizedText text, AnalysisResult analysis, Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(lexicon);
        var missing = new List<(int, string)>();
        for (var i = 0; i < text.Tokens.Count; i++)
        {
            var token = text.Tokens[i];
            var original = token.Original;
            if (original.Length == 0 || !char.IsLetter(original[0]) || char.IsUpper(original[0]) || !NeedsCapital(text, analysis, lexicon, i))
            {
                continue;
            }

            var spelling = UmlautSpelling(token.Text, lexicon) ?? token.Text;
            missing.Add((i, Capitalize(spelling)));
        }

        return missing;
    }

    private static bool IsAtItsSpan(NormalizedText text, Token token)
    {
        var span = token.Span;
        return span.Start >= 0
            && span.Length > 0
            && span.End <= text.Text.Length
            && string.Equals(text.Text.Substring(span.Start, span.Length), token.Text, StringComparison.Ordinal)
            && token.Text.Length > 0
            && char.IsLetter(token.Text[0]);
    }

    // The first token of the sentence, a name, a noun the resolver chose, or a formal pronoun.
    private static bool NeedsCapital(NormalizedText text, AnalysisResult analysis, Lexicon lexicon, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var token = text.Tokens[index];
        var word = analysis.Words is { } words && index < words.Count && words[index].Token.Span == token.Span ? words[index] : null;
        if (word is not null)
        {
            if (word.Role == WordRole.Name)
            {
                return true;
            }

            return word.Reading is { } reading && IsCapitalReading(reading);
        }

        // An analysis built by hand has no resolved words: the word is a noun when every reading it has is one.
        var readings = analysis.Tokens is { } tokens && index < tokens.Count ? tokens[index].Readings : lexicon.Lookup(token.Text);
        return readings.Count > 0 && readings.All(IsCapitalReading);
    }

    private static bool IsCapitalReading(LexicalFeatures reading) =>
        reading.Pos == LexiconSchema.Noun || (reading.Pos == LexiconSchema.Pronoun && reading.Register == FormalRegister);

    private const string FormalRegister = "formal";

    // The lexicon's spelling of a word typed with ae oe ue or a bare vowel for an umlaut; null when the word is a lexicon form, has no umlaut spelling,
    // or the spelling only differs by ß. The Swiss ss stays where the writer typed it.
    private static string? UmlautSpelling(string word, Lexicon lexicon)
    {
        var lookup = lexicon.Lookup(word);
        if (lookup.Count == 0 || !lookup.IsAccentInsensitive)
        {
            return null;
        }

        var typedHasSharpS = word.Contains('ß', StringComparison.Ordinal);
        foreach (var spelling in lexicon.SpellingsIgnoringAccents(word))
        {
            var candidate = typedHasSharpS ? spelling : spelling.Replace("ß", "ss", StringComparison.Ordinal);
            if (!string.Equals(candidate, word, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Capitalize(string word)
    {
        if (word.Length == 0)
        {
            return word;
        }

        var first = new StringBuilder(2);
        var length = char.IsSurrogate(word[0]) && word.Length > 1 ? 2 : 1;
        first.Append(word.AsSpan(0, length).ToString().ToUpperInvariant());
        return first.Append(word.AsSpan(length)).ToString();
    }
}
