namespace Slg.Phonology;

/// <summary>
/// Stage 6 of the G2P: allophone rules applied across the whole utterance, word boundaries included.
/// Order matters: nasal assimilation, then <c>b d g</c> to approximants, then <c>s</c> aspiration or voicing.
/// </summary>
internal static class AllophoneRules
{
    /// <summary>
    /// Rewrites <paramref name="phones"/> in place. <paramref name="syllableOfPhone"/> gives the utterance-wide
    /// syllable index of each phone and is used to tell syllable-final <c>s</c> from syllable-initial <c>s</c>.
    /// </summary>
    public static void Apply(List<string> phones, IReadOnlyList<int> syllableOfPhone, DialectConfig dialect)
    {
        ArgumentNullException.ThrowIfNull(phones);
        ArgumentNullException.ThrowIfNull(syllableOfPhone);
        ArgumentNullException.ThrowIfNull(dialect);

        AssimilateNasals(phones);
        Spirantize(phones);
        RealizeS(phones, syllableOfPhone, dialect);
    }

    /// <summary><c>n</c> becomes <c>m</c> before <c>p b m</c> and <c>ŋ</c> before <c>k g x</c>.</summary>
    private static void AssimilateNasals(List<string> phones)
    {
        for (var i = 0; i + 1 < phones.Count; i++)
        {
            if (phones[i] != PhoneInventory.N)
            {
                continue;
            }

            var next = phones[i + 1];
            if (next is PhoneInventory.P or PhoneInventory.B or PhoneInventory.M)
            {
                phones[i] = PhoneInventory.M;
            }
            else if (next is PhoneInventory.K or PhoneInventory.G or PhoneInventory.X)
            {
                phones[i] = PhoneInventory.VelarNasal;
            }
        }
    }

    /// <summary>
    /// <c>b d g</c> stay stops utterance-initially and after a nasal (and <c>d</c> after <c>l</c>);
    /// everywhere else they are the approximants <c>β ð ɣ</c>.
    /// </summary>
    private static void Spirantize(List<string> phones)
    {
        for (var i = 0; i < phones.Count; i++)
        {
            var phone = phones[i];
            if (phone is not (PhoneInventory.B or PhoneInventory.D or PhoneInventory.G))
            {
                continue;
            }

            if (i == 0)
            {
                continue;
            }

            var previous = phones[i - 1];
            var isStop = IsNasal(previous) || (phone == PhoneInventory.D && previous == PhoneInventory.L);
            if (isStop)
            {
                continue;
            }

            phones[i] = phone switch
            {
                PhoneInventory.B => PhoneInventory.Beta,
                PhoneInventory.D => PhoneInventory.Eth,
                _ => PhoneInventory.Gamma,
            };
        }
    }

    /// <summary>Syllable-final <c>s</c> becomes <c>h</c> when the dialect aspirates; otherwise <c>s</c> voices before a voiced consonant.</summary>
    private static void RealizeS(List<string> phones, IReadOnlyList<int> syllableOfPhone, DialectConfig dialect)
    {
        for (var i = 0; i < phones.Count; i++)
        {
            if (phones[i] != PhoneInventory.S)
            {
                continue;
            }

            if (dialect.SAspiration && IsSyllableFinal(phones, syllableOfPhone, i))
            {
                phones[i] = PhoneInventory.H;
            }
            else if (i + 1 < phones.Count && IsVoicedConsonant(phones[i + 1]))
            {
                phones[i] = PhoneInventory.VoicedS;
            }
        }
    }

    private static bool IsSyllableFinal(List<string> phones, IReadOnlyList<int> syllableOfPhone, int index)
    {
        for (var j = index - 1; j >= 0 && syllableOfPhone[j] == syllableOfPhone[index]; j--)
        {
            if (PhoneInventory.IsVowel(phones[j]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNasal(string phone)
    {
        return phone is PhoneInventory.M or PhoneInventory.N or PhoneInventory.PalatalNasal or PhoneInventory.VelarNasal;
    }

    private static bool IsVoicedConsonant(string phone)
    {
        return phone is PhoneInventory.B or PhoneInventory.D or PhoneInventory.G
            or PhoneInventory.Beta or PhoneInventory.Eth or PhoneInventory.Gamma
            or PhoneInventory.M or PhoneInventory.N or PhoneInventory.PalatalNasal
            or PhoneInventory.L or PhoneInventory.PalatalLateral
            or PhoneInventory.Tap or PhoneInventory.Trill
            or PhoneInventory.Yod or PhoneInventory.Zh;
    }
}
