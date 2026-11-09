namespace Vocaterra.Nlp.De.Morphology;

/// <summary>
/// The regular (weak) German verb rules (M38): the stem, the present and preterite endings with the linking <c>e</c>, the imperative and the participle. They are the
/// fallback of <see cref="GermanMorphology"/> for a verb the lexicon lacks or lacks a cell of, and the generator of the regularised forms learners produce
/// (<c>fahrt</c> for <c>fährt</c>, <c>gehte</c> for <c>ging</c>, <c>gegeht</c> for <c>gegangen</c>). Pure; every argument is lower case.
/// </summary>
internal static class GermanVerbRules
{
    // The prefixes that take no ge- in the participle (the unstressed inseparable prefixes).
    private static readonly string[] InseparablePrefixes = ["be", "emp", "ent", "er", "miss", "ver", "zer"];

    private const string Vowels = "aeiouäöüy";

    /// <summary>The verb stem of an infinitive without its separable particle: <c>sagen</c> gives <c>sag</c>, <c>sammeln</c> <c>sammel</c>, <c>tun</c> <c>tu</c>.</summary>
    public static string Stem(string infinitive)
    {
        if (infinitive.EndsWith("eln", StringComparison.Ordinal) || infinitive.EndsWith("ern", StringComparison.Ordinal))
        {
            return infinitive[..^1];
        }

        if (infinitive.EndsWith("en", StringComparison.Ordinal))
        {
            return infinitive[..^2];
        }

        return infinitive.EndsWith('n') ? infinitive[..^1] : infinitive;
    }

    /// <summary>True when the stem takes a linking <c>e</c> before <c>st</c>, <c>t</c> and <c>te</c> (<c>arbeitest</c>, <c>atmet</c>, <c>öffnete</c>).</summary>
    public static bool NeedsLinkingE(string stem)
    {
        if (stem.Length == 0)
        {
            return false;
        }

        var last = stem[^1];
        if (last is 'd' or 't')
        {
            return true;
        }

        if (last is 'm' or 'n' && stem.Length >= 2)
        {
            var before = stem[^2];
            return Vowels.IndexOf(before, StringComparison.Ordinal) < 0 && before is not ('l' or 'r' or 'h' or 'm' or 'n');
        }

        return false;
    }

    /// <summary>The infinitive without its separable particle; the infinitive itself when the particle does not start it.</summary>
    public static string WithoutParticle(string infinitive, string? particle)
    {
        return particle is not null && infinitive.Length > particle.Length && infinitive.StartsWith(particle, StringComparison.Ordinal)
            ? infinitive[particle.Length..]
            : infinitive;
    }

    /// <summary>The regular present form (<c>pres</c>) of the base infinitive, or null for a person that does not exist.</summary>
    public static string? Present(string baseInfinitive, string person, string number)
    {
        var stem = Stem(baseInfinitive);
        var e = NeedsLinkingE(stem);
        switch (number, person)
        {
            case ("sg", "1"):
                return baseInfinitive.EndsWith("eln", StringComparison.Ordinal) ? stem[..^2] + "le" : stem + "e";
            case ("sg", "2"):
                return stem.Length > 0 && stem[^1] is 's' or 'ß' or 'x' or 'z' && !e ? stem + "t" : stem + (e ? "est" : "st");
            case ("sg", "3"):
            case ("pl", "2"):
                return stem + (e ? "et" : "t");
            case ("pl", "1"):
            case ("pl", "3"):
                return baseInfinitive;
            default:
                return null;
        }
    }

    /// <summary>The regular preterite (<c>pret</c>) of the base infinitive: the stem, <c>te</c> and the person ending.</summary>
    public static string? Preterite(string baseInfinitive, string person, string number)
    {
        var stem = Stem(baseInfinitive);
        var te = NeedsLinkingE(stem) ? "ete" : "te";
        return (number, person) switch
        {
            ("sg", "1") or ("sg", "3") => stem + te,
            ("sg", "2") => stem + te + "st",
            ("pl", "1") or ("pl", "3") => stem + te + "n",
            ("pl", "2") => stem + te + "t",
            _ => null,
        };
    }

    /// <summary>The regular imperative of the base infinitive for the second person singular (<c>sag</c>, <c>arbeite</c>) and plural (<c>sagt</c>).</summary>
    public static string? Imperative(string baseInfinitive, string number)
    {
        var stem = Stem(baseInfinitive);
        return number switch
        {
            "sg" => NeedsLinkingE(stem) ? stem + "e" : stem,
            "pl" => stem + (NeedsLinkingE(stem) ? "et" : "t"),
            _ => null,
        };
    }

    /// <summary>
    /// The regular Perfekt participle: the particle, <c>ge</c>, the stem and <c>t</c>. Without <paramref name="expected"/> (a verb the lexicon lacks) <c>ge</c> is left out after an
    /// inseparable prefix and in <c>-ieren</c>; with it (the lexicon's participle) the variant with or without <c>ge</c> that shares the longer start with it is taken.
    /// </summary>
    public static string Participle(string infinitive, string? particle, string? expected = null)
    {
        var baseInfinitive = WithoutParticle(infinitive, particle);
        var stem = Stem(baseInfinitive);
        var ending = NeedsLinkingE(stem) ? "et" : "t";
        var prefix = particle ?? string.Empty;
        var withGe = prefix + "ge" + stem + ending;
        var withoutGe = prefix + stem + ending;
        if (expected is not null)
        {
            return CommonPrefix(withoutGe, expected) > CommonPrefix(withGe, expected) ? withoutGe : withGe;
        }

        var noGe = particle is null
            && (baseInfinitive.EndsWith("ieren", StringComparison.Ordinal)
                || InseparablePrefixes.Any(p => baseInfinitive.StartsWith(p, StringComparison.Ordinal) && baseInfinitive.Length > p.Length + 2));
        return noGe ? withoutGe : withGe;
    }

    private static int CommonPrefix(string a, string b)
    {
        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        return i;
    }
}
