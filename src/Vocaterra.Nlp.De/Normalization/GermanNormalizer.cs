namespace Vocaterra.Nlp.De.Normalization;

/// <summary>
/// The German pack's <see cref="ITextNormalizer"/> (M37): NFC, invariant lower case with <see cref="Token.Original"/> kept as typed (the capitals of the nouns and of
/// the formal <c>Sie</c> live there), punctuation removed (the German quotes <c>„…“</c> and <c>»…«</c> included), white space collapsed, optional number-word
/// conversion and optional expansion of the preposition-article contractions, and the final <c>?</c> or <c>!</c>.
/// </summary>
/// <remarks>
/// Letters (umlauts and <c>ß</c> included) and digits are kept; hyphens between two word characters are kept; every other character separates tokens. The
/// normalizer folds nothing: <c>schön</c> and <c>schoen</c> are two spellings that <see cref="GermanFolding"/> reads as one word. Contractions expand with their case:
/// <c>am</c> is <c>an dem</c> (dative), <c>ans</c> is <c>an das</c> (accusative), <c>zur</c> is <c>zu der</c>; both tokens carry the contraction as their
/// <see cref="Token.Original"/>. Numbers are read by <see cref="GermanNumbers"/>; the article forms <c>ein eine einen einem einer eines</c> are never turned into
/// <c>1</c>, whatever <see cref="NormalizationOptions.ConvertIndefiniteArticles"/> says, because their ending carries the gender and the case that the analysis
/// reads (<c>einen Hund</c> and <c>ein Hund</c> are not the same sentence); <c>eins</c> and every compound (<c>einundzwanzig</c>) are converted. The final mark is
/// found behind German closing quotes (<c>Wie viel kostet das?“</c>, <c>«Hallo!»</c>).
/// </remarks>
public sealed class GermanNormalizer : ITextNormalizer
{
    // Closing marks the final-mark search reads through: German double and single closers (“ ” ‘ ’ « ›), the opening forms of the other convention (» ‹) and „ ‚.
    private const string Closers = "\"'“”„‘’‚«»‹›)]";

    private GermanNormalizer()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanNormalizer Instance { get; } = new();

    /// <inheritdoc />
    public NormalizedText Normalize(string input, NormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= NormalizationOptions.Default;

        var clean = TextTokenizer.Clean(input);
        var words = TextTokenizer.Split(clean);
        if (options.NumbersToDigits)
        {
            words = ConvertNumbers(words);
        }

        if (options.ExpandContractions)
        {
            words = ExpandContractions(words);
        }

        var built = TextTokenizer.Build(words);
        return options.CaptureFinalMark ? built with { FinalMark = TextTokenizer.FinalMarkOf(StripClosers(clean)) } : built;
    }

    // The text without the white space and the closing marks at its end, so that the final mark is the character in front of them.
    private static string StripClosers(string text)
    {
        var end = text.Length;
        while (end > 0 && (char.IsWhiteSpace(text[end - 1]) || Closers.Contains(text[end - 1], StringComparison.Ordinal)))
        {
            end--;
        }

        return text[..end];
    }

    private static List<TokenizedWord> ConvertNumbers(List<TokenizedWord> words)
    {
        var keys = new string[words.Count];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = GermanNumbers.KeyOf(words[i].Text);
        }

        var result = new List<TokenizedWord>(words.Count);
        var index = 0;
        while (index < words.Count)
        {
            var used = GermanNumbers.ParsePrefixOfKeys(keys, index, out var value);
            if (used == 0 || (used == 1 && GermanNumbers.IsArticleForm(keys[index])))
            {
                result.Add(words[index]);
                index++;
                continue;
            }

            var original = used == 1 ? words[index].Original : JoinOriginals(words, index, used);
            result.Add(new TokenizedWord(value.ToString(System.Globalization.CultureInfo.InvariantCulture), original));
            index += used;
        }

        return result;
    }

    private static string JoinOriginals(List<TokenizedWord> words, int index, int count)
    {
        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = words[index + i].Original;
        }

        return string.Join(' ', parts);
    }

    private static List<TokenizedWord> ExpandContractions(List<TokenizedWord> words)
    {
        var result = new List<TokenizedWord>(words.Count + 2);
        foreach (var word in words)
        {
            if (Contractions.TryGetValue(word.Text, out var expansion))
            {
                result.Add(new TokenizedWord(expansion.Preposition, word.Original));
                result.Add(new TokenizedWord(expansion.Article, word.Original));
            }
            else
            {
                result.Add(word);
            }
        }

        return result;
    }

    // The preposition-article contractions and the words they stand for, with the case the article carries (dem: dative, das and der after a dative preposition
    // of place: the contraction keeps the case of the full form it replaces). The two-letter spellings of the umlaut forms are listed beside them and expand to the two-letter spelling.
    internal static readonly IReadOnlyDictionary<string, (string Preposition, string Article)> Contractions = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["am"] = ("an", "dem"),
        ["im"] = ("in", "dem"),
        ["zum"] = ("zu", "dem"),
        ["zur"] = ("zu", "der"),
        ["beim"] = ("bei", "dem"),
        ["vom"] = ("von", "dem"),
        ["ins"] = ("in", "das"),
        ["ans"] = ("an", "das"),
        ["aufs"] = ("auf", "das"),
        ["ums"] = ("um", "das"),
        ["durchs"] = ("durch", "das"),
        ["fürs"] = ("für", "das"),
        ["fuers"] = ("fuer", "das"),
        ["übers"] = ("über", "das"),
        ["uebers"] = ("ueber", "das"),
    };
}
