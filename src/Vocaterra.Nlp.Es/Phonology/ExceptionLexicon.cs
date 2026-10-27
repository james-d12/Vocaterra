using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Vocaterra.Nlp.Es.Phonology;

/// <summary>
/// A word whose pronunciation is listed instead of derived: its syllables as phone tokens and the stressed syllable.
/// Tokens are written for a distinción/yeísta speaker and adapted per dialect (<c>θ</c> follows seseo, <c>ʝ</c> follows the
/// <c>ll</c>/<c>y</c> sound); <c>b d g s</c> are underlying and still go through <see cref="AllophoneRules"/>.
/// </summary>
/// <param name="Word">The normalized spelling this entry was derived for.</param>
/// <param name="Syllables">The phone tokens of each syllable.</param>
/// <param name="StressedSyllable">Index of the stressed syllable.</param>
internal sealed record ExceptionEntry(string Word, IReadOnlyList<IReadOnlyList<string>> Syllables, int StressedSyllable);

/// <summary>
/// Stage 7 of the G2P: the exception lexicon (loanwords, <c>México</c>, <c>subrayar</c>), loaded from the embedded
/// <c>Data/exceptions.json</c>. Lookup is by whole normalized word, with a fallback that ignores written accents.
/// Entries flagged <c>inflect</c> also match their plural (<c>+s</c>) and feminine (<c>-o</c> to <c>-a</c>, optionally <c>+s</c>) forms.
/// </summary>
internal sealed class ExceptionLexicon
{
    private const string ResourceName = "Vocaterra.Nlp.Phonology.Data.exceptions.json";

    private static readonly Lazy<ExceptionLexicon> DefaultInstance = new(LoadEmbedded);

    private readonly Dictionary<string, ExceptionEntry> exact;
    private readonly Dictionary<string, ExceptionEntry> unaccented;

    private ExceptionLexicon(Dictionary<string, ExceptionEntry> exact, Dictionary<string, ExceptionEntry> unaccented, IReadOnlyList<string> listedWords)
    {
        this.exact = exact;
        this.unaccented = unaccented;
        ListedWords = listedWords;
    }

    /// <summary>The lexicon shipped in the assembly.</summary>
    public static ExceptionLexicon Default => DefaultInstance.Value;

    /// <summary>The words listed in the data file (not the derived inflections), in ordinal order.</summary>
    public IReadOnlyList<string> ListedWords { get; }

    /// <summary>Parses an exceptions document.</summary>
    public static ExceptionLexicon Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var exact = new Dictionary<string, ExceptionEntry>(StringComparer.Ordinal);
        var listed = new List<string>();
        var inflecting = new List<ExceptionEntry>();

        foreach (var element in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            var word = element.GetProperty("word").GetString() ?? throw new JsonException("Exception entry without a word.");
            var ipa = element.GetProperty("ipa").GetString() ?? throw new JsonException($"Exception entry '{word}' without ipa.");
            var entry = ParseEntry(word, ipa);
            if (!exact.TryAdd(word, entry))
            {
                throw new JsonException($"Duplicate exception entry '{word}'.");
            }

            listed.Add(word);
            if (element.TryGetProperty("inflect", out var inflect) && inflect.GetBoolean())
            {
                inflecting.Add(entry);
            }
        }

        foreach (var entry in inflecting)
        {
            foreach (var derived in Inflections(entry))
            {
                exact.TryAdd(derived.Word, derived);
            }
        }

        listed.Sort(StringComparer.Ordinal);
        var unaccented = new Dictionary<string, ExceptionEntry>(StringComparer.Ordinal);
        foreach (var key in exact.Keys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            var plain = StripAccents(key);
            if (!exact.ContainsKey(plain))
            {
                unaccented.TryAdd(plain, exact[key]);
            }
        }

        return new ExceptionLexicon(exact, unaccented, listed.AsReadOnly());
    }

    /// <summary>Looks a normalized word up; accents are optional in the query.</summary>
    public bool TryGet(string word, [MaybeNullWhen(false)] out ExceptionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(word);
        if (exact.TryGetValue(word, out var found) || unaccented.TryGetValue(StripAccents(word), out found))
        {
            entry = found;
            return true;
        }

        entry = default!;
        return false;
    }

    /// <summary>Removes written accents (but keeps <c>ñ</c> and <c>ü</c>, which are different letters).</summary>
    internal static string StripAccents(string word)
    {
        var decomposed = word.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        for (var i = 0; i < decomposed.Length; i++)
        {
            var character = decomposed[i];
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
                continue;
            }

            // Keep the tilde of ñ and the diaeresis of ü.
            var isTilde = character == '̃' && i > 0 && decomposed[i - 1] == 'n';
            var isDiaeresis = character == '̈' && i > 0 && decomposed[i - 1] == 'u';
            if (isTilde || isDiaeresis)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static ExceptionLexicon LoadEmbedded()
    {
        using var stream = typeof(ExceptionLexicon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    private static ExceptionEntry ParseEntry(string word, string ipa)
    {
        var syllables = new List<IReadOnlyList<string>>();
        var stressed = -1;
        foreach (var part in ipa.Split('.'))
        {
            var text = part;
            if (text.StartsWith(PhoneInventory.StressMark, StringComparison.Ordinal))
            {
                if (stressed >= 0)
                {
                    throw new JsonException($"Exception entry '{word}' has more than one stress mark.");
                }

                stressed = syllables.Count;
                text = text[PhoneInventory.StressMark.Length..];
            }

            var tokens = PhoneInventory.Tokenize(text);
            if (tokens.Count == 0)
            {
                throw new JsonException($"Exception entry '{word}' has an empty syllable.");
            }

            syllables.Add(tokens);
        }

        if (stressed < 0)
        {
            throw new JsonException($"Exception entry '{word}' has no stress mark.");
        }

        return new ExceptionEntry(word, syllables.AsReadOnly(), stressed);
    }

    private static IEnumerable<ExceptionEntry> Inflections(ExceptionEntry entry)
    {
        yield return WithSuffix(entry, entry.Word + "s", appendS: true, swapFinalO: false);
        var last = entry.Syllables[^1];
        if (entry.Word.EndsWith('o') && last[^1] == PhoneInventory.O)
        {
            var stem = entry.Word[..^1];
            yield return WithSuffix(entry, stem + "a", appendS: false, swapFinalO: true);
            yield return WithSuffix(entry, stem + "as", appendS: true, swapFinalO: true);
        }
    }

    private static ExceptionEntry WithSuffix(ExceptionEntry entry, string word, bool appendS, bool swapFinalO)
    {
        var syllables = entry.Syllables.Select(static s => s.ToList()).ToList();
        var last = syllables[^1];
        if (swapFinalO)
        {
            last[^1] = PhoneInventory.A;
        }

        if (appendS)
        {
            last.Add(PhoneInventory.S);
        }

        return new ExceptionEntry(word, syllables.Select(static s => (IReadOnlyList<string>)s.AsReadOnly()).ToList().AsReadOnly(), entry.StressedSyllable);
    }
}
