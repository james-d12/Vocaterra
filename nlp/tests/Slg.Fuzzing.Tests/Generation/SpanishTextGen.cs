using System.Globalization;
using System.Text;
using CsCheck;
using Slg.Core.Determinism;
using Slg.Nlp.Lexicon;
using Slg.Nlp.Normalization;

namespace Slg.Fuzzing.Tests.Generation;

/// <summary>
/// One bundle of low-probability noise transforms applied to a phrase: casing, accent stripping and adding, NFD decomposition,
/// <c>¿¡?!,.</c> insertion, doubled and edge whitespace, number words and digits swapped, and an adversarial token spliced in.
/// Every component is a small integer so CsCheck can shrink it, and <c>0</c> always means "leave alone".
/// </summary>
/// <param name="CaseMode">0 none, 1 upper, 2 lower, 3 title case, 4 alternating.</param>
/// <param name="AccentMode">0 none, 1 strip accents, 2 add a precomposed accent to a vowel, 3 add a combining acute to a vowel.</param>
/// <param name="Nfd">Decompose the text to NFD at the end.</param>
/// <param name="Punctuation">0 none, 1 wrap in ¿?, 2 wrap in ¡!, 3 put commas and periods between words, 4 scatter every mark.</param>
/// <param name="Spaces">0 none, 1 double spaces, 2 leading and trailing whitespace, 3 tabs and newlines between words.</param>
/// <param name="NumberMode">0 none, 1 number words to digits, 2 digits to number words.</param>
/// <param name="AdversarialIndex">Index into <see cref="AdversarialCorpus.Strings"/> of a token to splice in, or -1.</param>
/// <param name="Seed">Chooses the positions the transforms act on.</param>
public sealed record TextNoise(int CaseMode, int AccentMode, bool Nfd, int Punctuation, int Spaces, int NumberMode, int AdversarialIndex, int Seed)
{
    /// <summary>No noise at all.</summary>
    public static TextNoise None { get; } = new(0, 0, false, 0, 0, 0, -1, 0);

    /// <summary>
    /// Applies the transforms in a fixed order: numbers, accents, case, punctuation, spaces, decomposition, and last the adversarial token.
    /// The token goes in verbatim and last, so a lone surrogate in it reaches the stage under test as it is (the stages must cope with it).
    /// </summary>
    public string Apply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rng = new SeededRng((ulong)(uint)Seed);
        var result = text;
        if (NumberMode != 0)
        {
            result = SwapNumbers(result, NumberMode);
        }

        result = ApplyAccent(result, AccentMode, rng);
        result = ApplyCase(result, CaseMode);
        result = ApplyPunctuation(result, Punctuation, rng);
        result = ApplySpaces(result, Spaces, rng);
        if (Nfd)
        {
            result = result.Normalize(NormalizationForm.FormD);
        }

        return AdversarialIndex >= 0
            ? Splice(result, AdversarialCorpus.Strings[AdversarialIndex % AdversarialCorpus.Strings.Count], rng)
            : result;
    }

    private static string SwapNumbers(string text, int mode)
    {
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (mode == 1 && SpanishNumbers.TryParse(words[i], out var parsed) && words[i].Length > 0 && !char.IsAsciiDigit(words[i][0]))
            {
                words[i] = parsed.ToString(CultureInfo.InvariantCulture);
            }
            else if (mode == 2 && words[i].Length is > 0 and <= 6 && words[i].All(char.IsAsciiDigit))
            {
                words[i] = SpanishNumbers.ToWords(int.Parse(words[i], NumberStyles.None, CultureInfo.InvariantCulture));
            }
        }

        return string.Join(' ', words);
    }

    private static string Splice(string text, string token, SeededRng rng)
    {
        var words = text.Split(' ').ToList();
        words.Insert(rng.NextInt(words.Count + 1), token);
        return string.Join(' ', words);
    }

    private static string ApplyAccent(string text, int mode, SeededRng rng)
    {
        if (mode == 0 || text.Length == 0)
        {
            return text;
        }

        if (mode == 1)
        {
            return Accents.Strip(text);
        }

        var vowels = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if ("aeiouAEIOU".Contains(text[i], StringComparison.Ordinal))
            {
                vowels.Add(i);
            }
        }

        if (vowels.Count == 0)
        {
            return text;
        }

        var at = vowels[rng.NextInt(vowels.Count)];
        if (mode == 3)
        {
            return text.Insert(at + 1, "\u0301");
        }

        const string plain = "aeiouAEIOU";
        const string accented = "áéíóúÁÉÍÓÚ";
        return string.Concat(text.AsSpan(0, at), accented[plain.IndexOf(text[at], StringComparison.Ordinal)].ToString(), text.AsSpan(at + 1));
    }

    private static string ApplyCase(string text, int mode)
    {
        switch (mode)
        {
            case 1:
                return text.ToUpperInvariant();
            case 2:
                return text.ToLowerInvariant();
            case 3:
                return string.Join(' ', text.Split(' ').Select(static w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
            case 4:
                var builder = new StringBuilder(text.Length);
                for (var i = 0; i < text.Length; i++)
                {
                    builder.Append(i % 2 == 0 ? char.ToUpperInvariant(text[i]) : char.ToLowerInvariant(text[i]));
                }

                return builder.ToString();
            default:
                return text;
        }
    }

    private static string ApplyPunctuation(string text, int mode, SeededRng rng)
    {
        switch (mode)
        {
            case 1:
                return "¿" + text + "?";
            case 2:
                return "¡" + text + "!";
            case 3:
                return string.Join(rng.NextInt(2) == 0 ? ", " : ". ", text.Split(' '));
            case 4:
                var marks = "¿¡?!,.;:";
                var builder = new StringBuilder(text);
                var count = 1 + rng.NextInt(4);
                for (var i = 0; i < count; i++)
                {
                    builder.Insert(rng.NextInt(builder.Length + 1), marks[rng.NextInt(marks.Length)]);
                }

                return builder.ToString();
            default:
                return text;
        }
    }

    private static string ApplySpaces(string text, int mode, SeededRng rng)
    {
        switch (mode)
        {
            case 1:
                return text.Replace(" ", "  ", StringComparison.Ordinal);
            case 2:
                return "  \t" + text + " \n ";
            case 3:
                var separators = new[] { "\t", "\n", "\r\n", " \u00a0 " };
                return string.Join(separators[rng.NextInt(separators.Length)], text.Split(' '));
            default:
                return text;
        }
    }
}

/// <summary>
/// Lexicon-driven free-text generators: words from every part of speech, short phrases of one to eight tokens, loosely structured phrases
/// whose agreement is deliberately left to chance, noise transforms, and the adversarial mix.
/// </summary>
public static class SpanishTextGen
{
    /// <summary>The noise generator; each component is mostly zero so most phrases stay close to plain Spanish.</summary>
    public static Gen<TextNoise> Noise { get; } = Gen.Select(
        Mostly(Gen.Int[1, 4], 5),
        Mostly(Gen.Int[1, 3], 6),
        Mostly(Gen.Bool.Select(static _ => true), 6, false),
        Mostly(Gen.Int[1, 4], 5),
        Mostly(Gen.Int[1, 3], 6),
        Mostly(Gen.Int[1, 2], 6),
        Mostly(Gen.Int[0, AdversarialCorpus.Strings.Count - 1], 12, -1),
        Gen.Int,
        static (caseMode, accent, nfd, punctuation, spaces, numbers, adversarial, seed) =>
            new TextNoise(caseMode, accent, nfd, punctuation, spaces, numbers, adversarial, seed));

    /// <summary>A single lexicon form drawn uniformly from every part of speech.</summary>
    public static Gen<string> Word(LexiconCatalog words)
    {
        return Gen.OneOfConst([.. words.AllForms]);
    }

    /// <summary>A phrase of <paramref name="min"/> to <paramref name="max"/> lexicon forms separated by single spaces.</summary>
    public static Gen<string> Phrase(LexiconCatalog words, int min = 1, int max = 8)
    {
        return Word(words).List[min, max].Select(static list => string.Join(' ', list));
    }

    /// <summary>
    /// A phrase built from word classes in a plausible order (article, noun, adjective, pronoun, verb, numeral, preposition) with every
    /// form drawn independently, so number, gender and person agree only by chance. This is what makes the analyzer's error paths fire.
    /// </summary>
    public static Gen<string> Structured(LexiconCatalog words)
    {
        var lexicon = words.Lexicon;
        string[] Forms(string pos) => [.. lexicon.Entries.Where(e => e.Pos == pos && !e.Form.Contains(' ', StringComparison.Ordinal)).Select(static e => e.Form).Distinct(StringComparer.Ordinal)];
        var articles = Gen.OneOfConst(Forms(LexiconSchema.Article));
        var nouns = Gen.OneOfConst(Forms(LexiconSchema.Noun));
        var adjectives = Gen.OneOfConst(Forms(LexiconSchema.Adjective));
        var pronouns = Gen.OneOfConst([.. words.SubjectPronouns, .. words.Clitics]);
        var verbs = Gen.OneOfConst(Forms(LexiconSchema.Verb));
        var numerals = Gen.OneOfConst(Forms(LexiconSchema.Numeral));
        var adverbs = Gen.OneOfConst([.. words.Adverbs, .. LexiconCatalog.PastTimeAdverbs]);
        var prepositions = Gen.OneOfConst([.. words.Prepositions]);

        const int slotCount = 12;
        var keep = Gen.Bool.List[slotCount, slotCount];
        var picks = Gen.Select(
            articles, nouns, adjectives, pronouns, verbs, numerals, adverbs, prepositions,
            static (a, n, j, p, v, num, adv, prep) => new[] { a, n, j, p, v, num, adv, prep });
        var picks2 = Gen.Select(articles, nouns, adjectives, nouns, static (a, n, j, n2) => new[] { a, n, j, n2 });
        return Gen.Select(keep, picks, picks2, static (mask, first, second) =>
        {
            // Pattern: pron? adv? verb num? art? noun adj? prep art? noun adj?
            var tokens = new List<string>(12);
            void Add(int slot, string word)
            {
                if (mask[slot])
                {
                    tokens.Add(word);
                }
            }

            Add(0, first[3]);
            Add(7, first[6]);
            tokens.Add(first[4]);
            Add(3, first[5]);
            Add(2, first[0]);
            tokens.Add(first[1]);
            Add(4, first[2]);
            Add(8, first[7]);
            Add(9, second[0]);
            Add(10, second[1]);
            Add(11, second[2]);
            return string.Join(' ', tokens);
        });
    }

    /// <summary>A benign phrase (word salad two times in five, structured otherwise) with optional noise applied.</summary>
    public static Gen<string> Noisy(LexiconCatalog words)
    {
        var benign = Gen.Frequency(
            (2, (IGen<string>)Phrase(words)),
            (3, (IGen<string>)Structured(words)));
        return Gen.Select(benign, Noise, static (phrase, noise) => noise.Apply(phrase));
    }

    /// <summary>
    /// The suite-wide free-text input: noisy benign phrases three times in four, an adversarial corpus string (with noise on top) otherwise.
    /// </summary>
    public static Gen<string> Text(LexiconCatalog words)
    {
        return AdversarialCorpus.Mix(Noisy(words));
    }

    private static Gen<T> Mostly<T>(Gen<T> rare, int zeroWeight, T? zero = default)
    {
        return Gen.Frequency(
            (zeroWeight, (IGen<T>)Gen.Const(zero!)),
            (1, (IGen<T>)rare));
    }
}
