namespace Slg.Phonology;

/// <summary>One phone produced for a word, with the grapheme it came from and the syllable that owns it.</summary>
/// <param name="Phone">The phone token.</param>
/// <param name="GraphemeIndex">Index of the originating grapheme within the word.</param>
/// <param name="Syllable">Word-local index of the owning syllable.</param>
internal readonly record struct MappedPhone(string Phone, int GraphemeIndex, int Syllable);

/// <summary>A silent <c>h</c> within a word, and the word-local phone index before which a learner's <c>h</c> would go.</summary>
/// <param name="GraphemeIndex">Index of the <c>h</c> grapheme within the word.</param>
/// <param name="PhoneIndex">Word-local phone insertion index.</param>
internal readonly record struct SilentSite(int GraphemeIndex, int PhoneIndex);

/// <summary>The phones of one word before cross-word allophone rules.</summary>
/// <param name="Phones">Phones in order.</param>
/// <param name="SilentH">Silent <c>h</c> letters.</param>
internal sealed record MappedWord(IReadOnlyList<MappedPhone> Phones, IReadOnlyList<SilentSite> SilentH);

/// <summary>
/// Stage 5 of the G2P: context-sensitive grapheme to phone mapping for one syllabified word. Output uses
/// underlying <c>b d g</c> and <c>s</c>; <see cref="AllophoneRules"/> turns them into allophones across word boundaries.
/// </summary>
internal static class PhoneMapper
{
    /// <summary>Maps a syllabified word to phones for a dialect.</summary>
    public static MappedWord Map(SyllabifiedWord word, DialectConfig dialect)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(dialect);

        var graphemes = word.Graphemes;
        var syllableOf = new int[graphemes.Count];
        for (var s = 0; s < word.Syllables.Count; s++)
        {
            for (var g = word.Syllables[s].FirstGrapheme; g < word.Syllables[s].EndGrapheme; g++)
            {
                syllableOf[g] = s;
            }
        }

        var phones = new List<MappedPhone>(graphemes.Count + 2);
        var silent = new List<SilentSite>();
        for (var i = 0; i < graphemes.Count; i++)
        {
            var grapheme = graphemes[i];
            var syllable = syllableOf[i];
            switch (word.Roles[i])
            {
                case GraphemeRole.Nucleus:
                    phones.Add(new MappedPhone(VowelPhone(grapheme.Text[0]), i, syllable));
                    break;
                case GraphemeRole.Glide:
                    phones.Add(new MappedPhone(grapheme.Text[0] is 'u' or 'ü' ? PhoneInventory.W : PhoneInventory.J, i, syllable));
                    break;
                default:
                    MapConsonant(word, i, syllable, dialect, phones, silent);
                    break;
            }
        }

        ResyllabifyAcrossSilentH(word, phones);
        return new MappedWord(phones, silent);
    }

    /// <summary>
    /// A syllable whose onset is only a silent <c>h</c> takes over the last consonant phone of the previous syllable,
    /// as in speech: <c>in-hu-ma-no</c> is <c>i.nu.ma.no</c> and <c>ex-hi-bir</c> is <c>ek.si.βiɾ</c>. The spelling
    /// spans of the syllables are unchanged.
    /// </summary>
    private static void ResyllabifyAcrossSilentH(SyllabifiedWord word, List<MappedPhone> phones)
    {
        for (var s = 1; s < word.Syllables.Count; s++)
        {
            var first = word.Syllables[s].FirstGrapheme;
            var onsetIsOnlyH = word.Graphemes[first].Text == "h"
                && first + 1 < word.Graphemes.Count
                && word.Roles[first + 1] != GraphemeRole.Consonant;
            if (!onsetIsOnlyH)
            {
                continue;
            }

            var last = phones.FindLastIndex(phone => phone.Syllable == s - 1);
            if (last >= 0 && word.Roles[phones[last].GraphemeIndex] == GraphemeRole.Consonant)
            {
                phones[last] = phones[last] with { Syllable = s };
            }
        }
    }

    private static string VowelPhone(char letter)
    {
        return letter switch
        {
            'a' or 'á' => PhoneInventory.A,
            'e' or 'é' => PhoneInventory.E,
            'i' or 'í' or 'y' => PhoneInventory.I,
            'o' or 'ó' => PhoneInventory.O,
            _ => PhoneInventory.U,
        };
    }

    private static void MapConsonant(
        SyllabifiedWord word,
        int index,
        int syllable,
        DialectConfig dialect,
        List<MappedPhone> phones,
        List<SilentSite> silent)
    {
        var graphemes = word.Graphemes;
        var text = graphemes[index].Text;
        var softensNext = index + 1 < graphemes.Count && DigraphGrouper.IsFrontVowel(graphemes[index + 1].Text);

        void Add(string phone, int owner) => phones.Add(new MappedPhone(phone, index, owner));

        switch (text)
        {
            case "ch":
                Add(PhoneInventory.Ch, syllable);
                break;
            case "ll":
                Add(dialect.LlPhone, syllable);
                break;
            case "rr":
                Add(PhoneInventory.Trill, syllable);
                break;
            case "qu":
            case "k":
            case "q":
                Add(PhoneInventory.K, syllable);
                break;
            case "gu":
                Add(PhoneInventory.G, syllable);
                break;
            case "gü":
                Add(PhoneInventory.G, syllable);
                Add(PhoneInventory.W, syllable);
                break;
            case "b":
            case "v":
                Add(PhoneInventory.B, syllable);
                break;
            case "c":
                Add(softensNext ? dialect.SibilantPhone : PhoneInventory.K, syllable);
                break;
            case "z":
                Add(dialect.SibilantPhone, syllable);
                break;
            case "d":
                Add(PhoneInventory.D, syllable);
                break;
            case "f":
                Add(PhoneInventory.F, syllable);
                break;
            case "g":
                Add(softensNext ? PhoneInventory.X : PhoneInventory.G, syllable);
                break;
            case "j":
                Add(PhoneInventory.X, syllable);
                break;
            case "h":
                silent.Add(new SilentSite(index, phones.Count));
                break;
            case "l":
                Add(PhoneInventory.L, syllable);
                break;
            case "m":
                Add(PhoneInventory.M, syllable);
                break;
            case "n":
                Add(PhoneInventory.N, syllable);
                break;
            case "ñ":
                Add(PhoneInventory.PalatalNasal, syllable);
                break;
            case "p":
                Add(PhoneInventory.P, syllable);
                break;
            case "r":
                Add(IsTrilledSingleR(graphemes, index) ? PhoneInventory.Trill : PhoneInventory.Tap, syllable);
                break;
            case "s":
                Add(PhoneInventory.S, syllable);
                break;
            case "t":
                Add(PhoneInventory.T, syllable);
                break;
            case "w":
                Add(PhoneInventory.W, syllable);
                break;
            case "x":
                MapX(word, index, syllable, Add);
                break;
            case "y":
                Add(dialect.YPhone, syllable);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Word-initial <c>x</c> is <c>s</c>; elsewhere it is <c>ks</c>. Between vowels the <c>k</c> closes the
    /// previous syllable while the grapheme stays in the next one (<c>ek.sa.men</c>).
    /// </summary>
    private static void MapX(SyllabifiedWord word, int index, int syllable, Action<string, int> add)
    {
        if (index == 0)
        {
            add(PhoneInventory.S, syllable);
            return;
        }

        var opensSyllable = syllable > 0 && word.Syllables[syllable].FirstGrapheme == index;
        add(PhoneInventory.K, opensSyllable ? syllable - 1 : syllable);
        add(PhoneInventory.S, syllable);
    }

    private static bool IsTrilledSingleR(IReadOnlyList<Grapheme> graphemes, int index)
    {
        return index == 0 || graphemes[index - 1].Text is "n" or "l" or "s";
    }
}
