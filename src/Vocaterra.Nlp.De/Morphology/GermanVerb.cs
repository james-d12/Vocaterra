namespace Vocaterra.Nlp.De.Morphology;

/// <summary>What the lexicon says about one German verb lemma (M38): its infinitive, particle, declension class, finite cells, imperatives and participles.</summary>
internal sealed class GermanVerb
{
    private static readonly string[] NoForms = [];

    private readonly Dictionary<(string Person, string Number, string Tense), List<string>> _finite = [];
    private readonly Dictionary<(string Person, string Number), List<string>> _imperative = [];
    private readonly List<string> _haben = [];
    private readonly List<string> _sein = [];
    private readonly List<string> _otherParticiples = [];

    private GermanVerb(string lemma, string infinitive, string? particle, string? declension)
    {
        Lemma = lemma;
        Infinitive = infinitive;
        Particle = particle;
        Declension = declension;
    }

    /// <summary>The lemma, lower case.</summary>
    public string Lemma { get; }

    /// <summary>The infinitive form.</summary>
    public string Infinitive { get; }

    /// <summary>The separable particle, or null.</summary>
    public string? Particle { get; }

    /// <summary>The declension class of the infinitive entry (<c>strong</c>, <c>weak</c>, <c>mixed</c>, <c>irregular</c>), or null.</summary>
    public string? Declension { get; }

    /// <summary>The infinitive without the particle.</summary>
    public string BaseInfinitive => GermanVerbRules.WithoutParticle(Infinitive, Particle);

    /// <summary>The Perfekt participles the lexicon lists with <c>haben</c>.</summary>
    public IReadOnlyList<string> ParticiplesWithHaben => _haben;

    /// <summary>The Perfekt participles the lexicon lists with <c>sein</c>.</summary>
    public IReadOnlyList<string> ParticiplesWithSein => _sein;

    /// <summary>Every Perfekt participle the lexicon lists, in entry order, without duplicates.</summary>
    public IEnumerable<string> AllParticiples => _haben.Concat(_sein).Concat(_otherParticiples).Distinct(StringComparer.Ordinal);

    /// <summary>Builds the verb from the entries of a lemma; null when the lemma has no verb infinitive.</summary>
    public static GermanVerb? From(IReadOnlyList<LexiconEntry> entries)
    {
        var verbs = entries.Where(static e => e.Pos == "verb").ToList();
        var infinitive = verbs.FirstOrDefault(static e => e.Features.VerbForm == "inf");
        if (infinitive is null)
        {
            return null;
        }

        var verb = new GermanVerb(infinitive.Lemma, infinitive.Form, infinitive.Features.Particle, infinitive.Features.Declension);
        foreach (var entry in verbs)
        {
            var f = entry.Features;
            switch (f.VerbForm)
            {
                case "part2-haben":
                    AddOnce(verb._haben, entry.Form);
                    continue;
                case "part2-sein":
                    AddOnce(verb._sein, entry.Form);
                    continue;
                case "part2":
                    AddOnce(verb._otherParticiples, entry.Form);
                    continue;
                case not null:
                    continue;
            }

            if (f.Person is null || f.Number is null)
            {
                continue;
            }

            if (f.Mood == "imp")
            {
                Add(verb._imperative, (f.Person, f.Number), entry.Form);
            }
            else if (f.Mood == "ind" && f.Tense is not null)
            {
                Add(verb._finite, (f.Person, f.Number, f.Tense), entry.Form);
            }
        }

        return verb;
    }

    /// <summary>The finite indicative forms of a cell in entry order (the lexicon sorts by form).</summary>
    public IReadOnlyList<string> Finite(string person, string number, string tense) =>
        _finite.TryGetValue((person, number, tense), out var forms) ? forms : NoForms;

    /// <summary>True when the lexicon has any finite indicative form of the verb in the tense (a verb with only third-person forms, <c>besagen</c>, is defective, not incomplete).</summary>
    public bool HasFinite(string tense) => _finite.Keys.Any(k => k.Tense == tense);

    /// <summary>The imperative forms of a person and number in entry order.</summary>
    public IReadOnlyList<string> Imperative(string person, string number) =>
        _imperative.TryGetValue((person, number), out var forms) ? forms : NoForms;

    private static void Add<TKey>(Dictionary<TKey, List<string>> map, TKey key, string form)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        AddOnce(list, form);
    }

    private static void AddOnce(List<string> list, string form)
    {
        if (!list.Contains(form, StringComparer.Ordinal))
        {
            list.Add(form);
        }
    }
}
