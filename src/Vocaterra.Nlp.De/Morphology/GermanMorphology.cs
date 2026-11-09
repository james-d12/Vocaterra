using System.Collections.Concurrent;
using System.Globalization;

namespace Vocaterra.Nlp.De.Morphology;

/// <summary>A regularised German form a learner produced (M38) with the kind of slip the analyzer can name.</summary>
/// <param name="Form">The reading: the lemma, the form the learner should have used, and the cell (a Perfekt participle has the tense <c>perf</c> and an empty person and number).</param>
/// <param name="Kind">The slip: <see cref="GermanMorphology.StemChangeMissed"/>, <see cref="GermanMorphology.WeakPreterite"/> or <see cref="GermanMorphology.WeakParticiple"/>.</param>
public sealed record GermanRegularizedForm(RegularizedForm Form, string Kind);

/// <summary>A finite verb form split for a main clause: the finite part and the separable particle that goes to the end of the clause (<c>stehe</c> and <c>auf</c>).</summary>
/// <param name="Finite">The finite form; for the Perfekt the auxiliary and the participle (<c>bin aufgestanden</c>), which are not split.</param>
/// <param name="Particle">The separable particle, or null when the verb has none or the form is not split.</param>
public sealed record GermanSeparableForm(string Finite, string? Particle);

/// <summary>
/// The German morphology (M38): articles, possessives and <c>kein</c> by case, noun forms by number and case, the adjective endings, the verb forms with their
/// stem changes, the Perfekt with its auxiliary, the separable split and the regularised forms learners produce, lexicon first and rules second. A noun is returned
/// capitalised (the lexicon is lower case); every other form is lower case. Pure and deterministic; the lexicon is the German <c>wiktionary-de.json</c> (M36).
/// </summary>
public sealed class GermanMorphology : IMorphology
{
    /// <summary>The slip of a stem change missed in the present (<c>fahrt</c> for <c>fährt</c>, <c>lest</c> for <c>liest</c>).</summary>
    public const string StemChangeMissed = "stem-change";

    /// <summary>The slip of a strong preterite made weak (<c>gehte</c> for <c>ging</c>).</summary>
    public const string WeakPreterite = "weak-preterite";

    /// <summary>The slip of a strong participle made weak (<c>gegeht</c> for <c>gegangen</c>).</summary>
    public const string WeakParticiple = "weak-participle";

    private static readonly string[] Persons = ["1", "2", "3"];
    private static readonly string[] Numbers = ["sg", "pl"];

    // Verbs whose Perfekt takes sein when the lexicon lists both auxiliaries (it does for fahren and aufstehen): the verbs of motion and of change of state.
    private static readonly HashSet<string> SeinVerbs = new(StringComparer.Ordinal)
    {
        "gehen", "kommen", "fahren", "laufen", "fliegen", "bleiben", "reisen", "steigen", "fallen", "springen", "rennen", "wandern", "sein", "werden",
        "aufstehen", "einschlafen", "umziehen", "aussteigen", "einsteigen", "ankommen", "abfahren", "wachsen", "sterben", "passieren", "geschehen",
    };

    // The verbs whose regular forms are not slips a learner makes (their stem is not the infinitive minus -en).
    private static readonly HashSet<string> NoRegularization = new(StringComparer.Ordinal) { "sein", "werden" };

    private readonly Lexicon _lexicon;
    private readonly ConcurrentDictionary<string, GermanVerb?> _verbs = new(StringComparer.Ordinal);
    private readonly Lazy<Dictionary<string, GermanRegularizedForm>> _regularized;

    /// <summary>Creates the morphology over a German lexicon.</summary>
    /// <param name="lexicon">The lexicon, built with the German folding and profile.</param>
    public GermanMorphology(Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        _lexicon = lexicon;
        _regularized = new Lazy<Dictionary<string, GermanRegularizedForm>>(() => lexicon.Derive("german-regularized-forms", BuildRegularizedIndex));
    }

    /// <summary>The persons (ich, du, er/sie/es, wir, ihr, sie/Sie: the formal <c>Sie</c> shares the third plural) and the tenses of the German table: <c>pres</c>, <c>pret</c> (where the lexicon has it) and <c>perf</c>.</summary>
    public ParadigmLayout Paradigm { get; } = new(
        [new("1", "sg"), new("2", "sg"), new("3", "sg"), new("1", "pl"), new("2", "pl"), new("3", "pl")],
        ["pres", "pret", "perf"]);

    // ----- articles, possessives, kein -----

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number) => Article(definiteness, gender, number, string.Empty, null);

    /// <inheritdoc />
    public string Article(Definiteness definiteness, string gender, string number, string nounLemma) => Article(definiteness, gender, number, nounLemma, null);

    /// <summary>
    /// The article by gender, number and case: <c>der die das</c>, <c>den</c>, <c>dem</c>, <c>des</c> (definite) and <c>ein eine einen einem eines</c> (indefinite, empty in the plural).
    /// A null case is the nominative.
    /// </summary>
    /// <param name="definiteness">Definite or indefinite.</param>
    /// <param name="gender">The gender <c>m</c>, <c>f</c> or <c>n</c> (ignored in the plural).</param>
    /// <param name="number">The number.</param>
    /// <param name="nounLemma">The noun lemma (unused in German).</param>
    /// <param name="grammaticalCase">The case <c>nom</c>, <c>acc</c>, <c>dat</c>, <c>gen</c>, or null.</param>
    public string Article(Definiteness definiteness, string gender, string number, string nounLemma, string? grammaticalCase)
    {
        var slot = Slot(gender, number);
        var grammaticalCaseId = CaseOf(grammaticalCase);
        if (definiteness == Definiteness.Definite)
        {
            return DefiniteTable[grammaticalCaseId][slot];
        }

        return slot == 3 ? string.Empty : "ein" + EinEndings[grammaticalCaseId][slot];
    }

    /// <summary>The negative article <c>kein</c> with its ending for gender, number and case (<c>keine</c>, <c>keinen</c>, <c>keinem</c>).</summary>
    /// <param name="gender">The gender.</param>
    /// <param name="number">The number.</param>
    /// <param name="grammaticalCase">The case, or null for the nominative.</param>
    public string Kein(string gender, string number, string? grammaticalCase) => "kein" + EinEndings[CaseOf(grammaticalCase)][Slot(gender, number)];

    /// <summary>
    /// A possessive article with its ending (<c>mein meine meinen unserem eurer Ihr</c>). The possessor is <c>ich du er sie es wir ihr</c> or the formal <c>Sie</c> (capital
    /// S); <c>sie</c> serves the third person singular feminine and the third plural, which are both <c>ihr</c>.
    /// </summary>
    /// <param name="possessor">The possessor pronoun.</param>
    /// <param name="gender">The gender of the possessed noun.</param>
    /// <param name="number">The number of the possessed noun.</param>
    /// <param name="grammaticalCase">The case of the possessed noun, or null for the nominative.</param>
    public string Possessive(string possessor, string gender, string number, string? grammaticalCase)
    {
        var stem = possessor switch
        {
            "ich" => "mein",
            "du" => "dein",
            "er" or "es" => "sein",
            "sie" => "ihr",
            "wir" => "unser",
            "ihr" => "euer",
            "Sie" => "Ihr",
            _ => throw new ArgumentException($"'{possessor}' is not a possessor; use ich, du, er, sie, es, wir, ihr or Sie.", nameof(possessor)),
        };
        var ending = EinEndings[CaseOf(grammaticalCase)][Slot(gender, number)];
        if (stem == "euer" && ending.Length > 0 && ending[0] == 'e')
        {
            stem = "eur";
        }

        return stem + ending;
    }

    private static readonly string[][] DefiniteTable =
    [
        ["der", "die", "das", "die"],
        ["den", "die", "das", "die"],
        ["dem", "der", "dem", "den"],
        ["des", "der", "des", "der"],
    ];

    private static readonly string[][] EinEndings =
    [
        ["", "e", "", "e"],
        ["en", "e", "", "e"],
        ["em", "er", "em", "en"],
        ["es", "er", "es", "er"],
    ];

    // The strong endings (those of the definite article), then the weak and the mixed ones.
    private static readonly string[][] StrongAdjective =
    [
        ["er", "e", "es", "e"],
        ["en", "e", "es", "e"],
        ["em", "er", "em", "en"],
        ["en", "er", "en", "er"],
    ];

    private static readonly string[][] WeakAdjective =
    [
        ["e", "e", "e", "en"],
        ["en", "e", "e", "en"],
        ["en", "en", "en", "en"],
        ["en", "en", "en", "en"],
    ];

    private static readonly string[][] MixedAdjective =
    [
        ["er", "e", "es", "en"],
        ["en", "e", "es", "en"],
        ["en", "en", "en", "en"],
        ["en", "en", "en", "en"],
    ];

    private static int CaseOf(string? grammaticalCase) => grammaticalCase switch
    {
        null or "nom" => 0,
        "acc" => 1,
        "dat" => 2,
        "gen" => 3,
        _ => throw new ArgumentException($"'{grammaticalCase}' is not a German case; use nom, acc, dat or gen.", nameof(grammaticalCase)),
    };

    private static int Slot(string gender, string number)
    {
        if (number == "pl")
        {
            return 3;
        }

        if (number != "sg")
        {
            throw new ArgumentException($"'{number}' is not a number; use sg or pl.", nameof(number));
        }

        return gender switch
        {
            "m" => 0,
            "f" => 1,
            "n" => 2,
            _ => throw new ArgumentException($"'{gender}' is not a German gender; use m, f or n.", nameof(gender)),
        };
    }

    // ----- nouns -----

    /// <summary>
    /// The gender (<c>m</c>, <c>f</c>, <c>n</c>) of a noun lemma by the lexicon (that of its first uncased singular entry, Wiktionary's first gender: <c>Mensch</c> is masculine, <c>Tee</c> too),
    /// or null when the lexicon lacks the noun.
    /// </summary>
    /// <param name="lemma">The noun lemma.</param>
    public string? GenderOf(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return PrimaryGender(NounEntries(ResolveNounLemma(lemma) ?? string.Empty));
    }

    private static string? PrimaryGender(IReadOnlyList<LexiconEntry> entries) =>
        (entries.FirstOrDefault(static e => e.Features.Gender is not null && e.Features.Number == "sg" && e.Features.Case is null)
            ?? entries.FirstOrDefault(static e => e.Features.Gender is not null))?.Features.Gender;

    /// <inheritdoc />
    public string Pluralize(string noun) => InflectNounCore(noun, "pl", "nom", plural: true);

    /// <inheritdoc />
    public string Singularize(string noun)
    {
        ArgumentNullException.ThrowIfNull(noun);
        var lemma = ResolveNounLemma(noun, asPlural: true);
        if (lemma is not null)
        {
            return NounForm(lemma, "sg", "nom") is { } form ? Capitalize(form) : Capitalize(lemma);
        }

        var key = _lexicon.Folding.Key(noun);
        return Capitalize(key.EndsWith("innen", StringComparison.Ordinal) ? key[..^2] : key);
    }

    /// <inheritdoc />
    public string InflectNoun(string lemma, string number) => InflectNounCore(lemma, number, "nom", plural: number == "pl");

    /// <summary>The noun form for a lemma, number and case (<c>junge</c>, <c>sg</c>, <c>acc</c> gives <c>Jungen</c>; <c>kind</c>, <c>pl</c>, <c>dat</c> gives <c>Kindern</c>), lexicon first, capitalised.</summary>
    /// <param name="lemma">The noun lemma.</param>
    /// <param name="number">The number.</param>
    /// <param name="grammaticalCase">The case, or null for the nominative.</param>
    public string DeclineNoun(string lemma, string number, string? grammaticalCase)
    {
        _ = CaseOf(grammaticalCase);
        return InflectNounCore(lemma, number, grammaticalCase ?? "nom", plural: number == "pl");
    }

    private string InflectNounCore(string lemma, string number, string grammaticalCase, bool plural)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var resolved = ResolveNounLemma(lemma);
        if (resolved is not null && NounForm(resolved, number, grammaticalCase) is { } form)
        {
            return Capitalize(form);
        }

        var key = _lexicon.Folding.Key(lemma);
        return Capitalize(NounByRule(key, plural, grammaticalCase));
    }

    // The guess for a noun the lexicon lacks: the commonest plural endings, the dative plural -n and the masculine genitive -s.
    private static string NounByRule(string key, bool plural, string grammaticalCase)
    {
        if (!plural)
        {
            return grammaticalCase == "gen" && !key.EndsWith('s') && !key.EndsWith("ung", StringComparison.Ordinal) ? key + "s" : key;
        }

        var pluralForm = key switch
        {
            _ when key.EndsWith('e') => key + "n",
            _ when key.EndsWith("in", StringComparison.Ordinal) => key + "nen",
            _ when key.EndsWith("el", StringComparison.Ordinal) || key.EndsWith("er", StringComparison.Ordinal) || key.EndsWith("en", StringComparison.Ordinal) => key,
            _ => key + "e",
        };
        return grammaticalCase == "dat" && !pluralForm.EndsWith('n') && !pluralForm.EndsWith('s') ? pluralForm + "n" : pluralForm;
    }

    private IReadOnlyList<LexiconEntry> NounEntries(string lemma) =>
        lemma.Length == 0 ? [] : _lexicon.EntriesForLemma(lemma).Where(static e => e.Pos == "noun").ToList();

    // The noun lemma a word is: the word itself when it is a lemma with a singular, else the lemma of its noun reading. The lexicon has a few plural-only "lemmas"
    // (tage) beside the real one (tag); asked for the singular of a plural, the reading of a lemma with a singular wins.
    private string? ResolveNounLemma(string word, bool asPlural = false)
    {
        var key = _lexicon.Folding.Key(word);
        var direct = NounEntries(key);
        var lemmas = _lexicon.Lookup(key).Where(static r => r.Pos == "noun").Select(r => _lexicon.Folding.Key(r.Lemma)).Distinct(StringComparer.Ordinal).ToList();
        var withSingular = lemmas.Where(l => l != key && NounEntries(l).Any(static e => e.Features.Number == "sg")).ToList();
        if (asPlural && withSingular.Count > 0)
        {
            return withSingular[0];
        }

        if (direct.Count > 0)
        {
            return key;
        }

        return lemmas.Count > 0 ? lemmas[0] : null;
    }

    // The lexicon's form for a cell, with the tie-breaks of the variants Wiktionary lists (Mutter: muttern and mütter; Tag: tag and tage in the dative).
    private string? NounForm(string lemma, string number, string grammaticalCase)
    {
        var entries = NounEntries(lemma);
        var singular = entries.FirstOrDefault(static e => e.Features.Number == "sg" && (e.Features.Case is null or "nom"))?.Form ?? lemma;
        var gender = PrimaryGender(entries);
        var ofGender = entries.Where(e => e.Features.Gender == gender).ToList();
        if (ofGender.Count > 0)
        {
            entries = ofGender;
        }

        var cell = entries.Where(e => e.Features.Number == number && e.Features.Case == grammaticalCase).ToList();
        if (cell.Count == 0 && grammaticalCase == "nom")
        {
            cell = [.. entries.Where(e => e.Features.Number == number && e.Features.Case is null)];
        }

        if (cell.Count == 0)
        {
            return null;
        }

        if (cell.Any(static e => e.Features.Declension == "weak"))
        {
            cell = [.. cell.Where(static e => e.Features.Declension == "weak")];
        }

        var forms = cell.Select(static e => e.Form).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (number == "sg")
        {
            return grammaticalCase == "gen" ? forms[0] : forms.OrderBy(static f => f.Length).First();
        }

        var others = forms.Where(f => !string.Equals(f, singular, StringComparison.Ordinal)).ToList();
        if (others.Count > 0)
        {
            forms = others;
        }

        if (!HasUmlaut(singular) && forms.Any(HasUmlaut))
        {
            forms = [.. forms.Where(HasUmlaut)];
        }

        if (grammaticalCase == "dat" && forms.Any(static f => f.EndsWith('n')))
        {
            forms = [.. forms.Where(static f => f.EndsWith('n'))];
        }

        return forms[0];
    }

    private static bool HasUmlaut(string form) => form.AsSpan().IndexOfAny("äöü") >= 0;

    private static string Capitalize(string word) =>
        word.Length == 0 ? word : string.Concat(char.ToUpper(word[0], CultureInfo.InvariantCulture).ToString(), word.AsSpan(1));

    // ----- adjectives -----

    /// <inheritdoc />
    public string AgreeAdjective(string lemma, string gender, string number) => DeclineAdjective(lemma, gender, number, "nom", null);

    /// <summary>
    /// The adjective with the ending for gender, number, case and the article before it: strong with no article, weak after the definite article, mixed after the
    /// indefinite one (<c>gut</c> to <c>guter</c>, <c>gute</c>, <c>guten</c>). The lexicon's cell first, then the endings; the stem changes are the lexicon's
    /// (<c>hoch</c> gives <c>hohe</c>) or the rules' (<c>dunkel</c> gives <c>dunkle</c>). The parts the template filter <c>decl</c> calls (M40).
    /// </summary>
    /// <param name="adjective">The adjective lemma.</param>
    /// <param name="gender">The gender (ignored in the plural).</param>
    /// <param name="number">The number.</param>
    /// <param name="grammaticalCase">The case.</param>
    /// <param name="articleKind">The article before the adjective, or null for none.</param>
    public string DeclineAdjective(string adjective, string gender, string number, string grammaticalCase, Definiteness? articleKind)
    {
        ArgumentNullException.ThrowIfNull(adjective);
        var slot = Slot(gender, number);
        var grammaticalCaseId = CaseOf(grammaticalCase);
        var key = _lexicon.Folding.Key(adjective);
        var (table, declension) = articleKind switch
        {
            null => (StrongAdjective, "strong"),
            Definiteness.Definite => (WeakAdjective, "weak"),
            _ => (MixedAdjective, "mixed"),
        };

        var entries = _lexicon.EntriesForLemma(key).Where(static e => e.Pos == "adj").ToList();
        var caseName = new[] { "nom", "acc", "dat", "gen" }[grammaticalCaseId];
        var cell = entries.Where(e => e.Features.Declension == declension && e.Features.Number == number && e.Features.Case == caseName
            && (number == "pl" || e.Features.Gender is null || e.Features.Gender == gender)).Select(static e => e.Form).Order(StringComparer.Ordinal).FirstOrDefault();
        if (cell is not null)
        {
            return cell;
        }

        var stem = entries
            .Where(static e => e.Features.Declension == "strong" && e.Features.Number == "pl" && e.Features.Case == "nom" && e.Form.EndsWith('e'))
            .Select(static e => e.Form[..^1])
            .OrderBy(static f => f.Length)
            .ThenBy(static f => f, StringComparer.Ordinal)
            .FirstOrDefault() ?? AdjectiveStem(key);
        return stem + table[grammaticalCaseId][slot];
    }

    private static string AdjectiveStem(string key)
    {
        if (key == "hoch")
        {
            return "hoh";
        }

        if ((key.EndsWith("el", StringComparison.Ordinal) && key.Length > 3) || key.EndsWith("auer", StringComparison.Ordinal) || key.EndsWith("euer", StringComparison.Ordinal))
        {
            return key[..^2] + key[^1];
        }

        return key;
    }

    // ----- verbs -----

    private GermanVerb? VerbOf(string key) => _verbs.GetOrAdd(key, k => GermanVerb.From(_lexicon.EntriesForLemma(k)));

    /// <inheritdoc />
    public string? Conjugate(string lemma, string person, string number, string tense)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var key = _lexicon.Folding.Key(lemma);
        return tense switch
        {
            "pres" or "pret" => Finite(key, person, number, tense),
            "imp" => Imperative(key, person, number),
            "perf" => Perfect(key, person, number),
            _ => null,
        };
    }

    /// <summary>
    /// The verb lemma's separable particle (<c>aufstehen</c> gives <c>auf</c>), or null for an inseparable verb or one the lexicon lacks. What the template filter
    /// <c>particle</c> calls (M40).
    /// </summary>
    /// <param name="verbLemma">The verb lemma.</param>
    public string? SeparableParticle(string verbLemma)
    {
        ArgumentNullException.ThrowIfNull(verbLemma);
        return VerbOf(_lexicon.Folding.Key(verbLemma))?.Particle;
    }

    /// <summary>
    /// The finite form of a main clause with its separable particle split off: <c>aufstehen</c>, <c>1</c>, <c>sg</c>, <c>pres</c> gives <c>stehe</c> and <c>auf</c>
    /// (<see cref="Conjugate"/> gives <c>stehe</c> alone: the particle goes to the end of the clause). Null where <see cref="Conjugate"/> gives none.
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    /// <param name="person">The person.</param>
    /// <param name="number">The number.</param>
    /// <param name="tense">The tense.</param>
    public GermanSeparableForm? Split(string lemma, string person, string number, string tense)
    {
        var form = Conjugate(lemma, person, number, tense);
        if (form is null)
        {
            return null;
        }

        return tense is "pres" or "pret" or "imp" ? new GermanSeparableForm(form, SeparableParticle(lemma)) : new GermanSeparableForm(form, null);
    }

    /// <summary>
    /// The auxiliary of the Perfekt, <c>haben</c> or <c>sein</c>, from the lexicon's participle entries; where it lists both (<c>fahren</c>, <c>aufstehen</c>) <c>sein</c> for the
    /// verbs of motion and change of state, else <c>haben</c>.
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    public string Auxiliary(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var key = _lexicon.Folding.Key(lemma);
        return VerbOf(key) is { } verb ? AuxiliaryOf(verb) : "haben";
    }

    /// <summary>The Perfekt participle (<c>gegangen</c>, <c>aufgestanden</c>), from the lexicon or the weak rule; null for a strong verb the lexicon lacks.</summary>
    /// <param name="lemma">The verb lemma.</param>
    public string? Participle(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var key = _lexicon.Folding.Key(lemma);
        var verb = VerbOf(key);
        if (verb is null)
        {
            return IsPlausibleInfinitive(key) ? GermanVerbRules.Participle(key, null) : null;
        }

        var preferred = AuxiliaryOf(verb) == "sein" ? verb.ParticiplesWithSein : verb.ParticiplesWithHaben;
        if (preferred.Count > 0)
        {
            return preferred[0];
        }

        var any = verb.AllParticiples.FirstOrDefault();
        if (any is not null)
        {
            return any;
        }

        return verb.Declension == "weak" ? GermanVerbRules.Participle(verb.Infinitive, verb.Particle) : null;
    }

    private static string AuxiliaryOf(GermanVerb verb)
    {
        var haben = verb.ParticiplesWithHaben.Count > 0;
        var sein = verb.ParticiplesWithSein.Count > 0;
        if (haben != sein)
        {
            return sein ? "sein" : "haben";
        }

        return sein && (SeinVerbs.Contains(verb.BaseInfinitive) || SeinVerbs.Contains(verb.Lemma)) ? "sein" : "haben";
    }

    private static bool IsPlausibleInfinitive(string key) => key.Length > 3 && key.EndsWith('n');

    private string? Finite(string key, string person, string number, string tense)
    {
        var verb = VerbOf(key);
        if (verb is not null)
        {
            var forms = verb.Finite(person, number, tense);
            if (forms.Count > 0)
            {
                if (tense != "pres")
                {
                    return forms[0];
                }

                // The shortest form, but of the first person singular the one with the e (sammle, not the colloquial sammel).
                var candidates = person == "1" && number == "sg" && forms.Any(static f => f.EndsWith('e')) ? forms.Where(static f => f.EndsWith('e')) : forms;
                return candidates.OrderBy(static f => f.Length).ThenBy(static f => f, StringComparer.Ordinal).First();
            }

            if (verb.Declension != "weak" || verb.HasFinite(tense))
            {
                return null;
            }
        }
        else if (!IsPlausibleInfinitive(key))
        {
            return null;
        }

        var baseInfinitive = verb?.BaseInfinitive ?? key;
        return tense == "pres" ? GermanVerbRules.Present(baseInfinitive, person, number) : GermanVerbRules.Preterite(baseInfinitive, person, number);
    }

    private string? Imperative(string key, string person, string number)
    {
        if (person == "2")
        {
            var verb = VerbOf(key);
            if (verb is not null)
            {
                var forms = verb.Imperative("2", number);
                if (forms.Count > 0)
                {
                    // The short form (fahr), but the one with the e where the stem needs it (arbeite).
                    var ordered = forms.OrderBy(static f => f.Length).ThenBy(static f => f, StringComparer.Ordinal).ToList();
                    return number == "sg" && GermanVerbRules.NeedsLinkingE(GermanVerbRules.Stem(verb.BaseInfinitive)) ? ordered[^1] : ordered[0];
                }
            }

            if (verb is not null ? verb.Declension == "weak" : IsPlausibleInfinitive(key))
            {
                return GermanVerbRules.Imperative(verb?.BaseInfinitive ?? key, number);
            }

            return null;
        }

        // Let us go, and the formal Sie: the present form of the first and the third person plural (gehen wir, gehen Sie).
        return number == "pl" && person is "1" or "3" ? Finite(key, person, number, "pres") : null;
    }

    private string? Perfect(string key, string person, string number)
    {
        var verb = VerbOf(key);
        var participle = Participle(key);
        if (participle is null)
        {
            return null;
        }

        var auxiliary = verb is null ? "haben" : AuxiliaryOf(verb);
        var finite = Finite(auxiliary, person, number, "pres");
        return finite is null ? null : finite + " " + participle;
    }

    /// <summary>
    /// The cells of a verb's table (the Book's): present, preterite (empty where the lexicon has none) and Perfekt for ich, du, er/sie/es, wir, ihr, sie/Sie. A separable verb's
    /// present and preterite show the particle after the finite form (<c>stehe … auf</c>); the Perfekt shows the auxiliary and the participle (<c>bin aufgestanden</c>).
    /// </summary>
    /// <param name="lemma">The verb lemma.</param>
    public IReadOnlyList<ParadigmCell> ParadigmOf(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var particle = SeparableParticle(lemma);
        var cells = new List<ParadigmCell>(Paradigm.Tenses.Count * Paradigm.Persons.Count);
        foreach (var tense in Paradigm.Tenses)
        {
            foreach (var person in Paradigm.Persons)
            {
                var form = Conjugate(lemma, person.Person, person.Number, tense);
                if (form is not null && particle is not null && tense is "pres" or "pret")
                {
                    form = form + " … " + particle;
                }

                cells.Add(new ParadigmCell(person, tense, form));
            }
        }

        return cells;
    }

    // ----- regularised forms -----

    /// <inheritdoc />
    public bool IsRegularizedIrregular(string form, out string lemma, out string expected)
    {
        if (TryGetRegularization(form, out var result))
        {
            lemma = result.Lemma;
            expected = result.Expected;
            return true;
        }

        lemma = string.Empty;
        expected = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetRegularization(string form, out RegularizedForm result)
    {
        if (TryGetGermanRegularization(form, out var german))
        {
            result = german.Form;
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// A form the lexicon lacks that is a strong verb's regular cell (<c>gehte</c>, <c>gegeht</c>, <c>fahrst</c>), with the kind of slip. Words the lexicon knows are never reported
    /// (see <see cref="TryGetRegularizationOfWord"/>). The analyzer's marker (M39); generation never produces these forms.
    /// </summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The regularised form and its kind.</param>
    public bool TryGetGermanRegularization(string form, out GermanRegularizedForm result)
    {
        ArgumentNullException.ThrowIfNull(form);
        var key = _lexicon.Folding.Key(form);
        var reading = _lexicon.Lookup(key);
        if ((reading.Count == 0 || reading.IsAccentInsensitive) && _regularized.Value.TryGetValue(key, out var found))
        {
            result = found;
            return true;
        }

        result = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetRegularizationOfWord(string form, out RegularizedForm result)
    {
        if (TryGetGermanRegularizationOfWord(form, out var german))
        {
            result = german.Form;
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// A form that is also a lexicon word but is, for another cell, the present form with its stem change missed (<c>fahrt</c>, the second person plural, for the third person
    /// singular <c>fährt</c>; <c>lest</c> for <c>liest</c>). Only that kind: a known preterite or participle is not a slip.
    /// </summary>
    /// <param name="form">The word as typed.</param>
    /// <param name="result">The regularised form and its kind.</param>
    public bool TryGetGermanRegularizationOfWord(string form, out GermanRegularizedForm result)
    {
        ArgumentNullException.ThrowIfNull(form);
        var key = _lexicon.Folding.Key(form);
        var reading = _lexicon.Lookup(key);
        if (reading.Count > 0 && !reading.IsAccentInsensitive && _regularized.Value.TryGetValue(key, out var found) && found.Kind == StemChangeMissed)
        {
            result = found;
            return true;
        }

        result = null!;
        return false;
    }

    private Dictionary<string, GermanRegularizedForm> BuildRegularizedIndex(Lexicon lexicon)
    {
        var index = new Dictionary<string, GermanRegularizedForm>(StringComparer.Ordinal);
        // A separable verb shares the stem forms of its simple verb (abgehen and gehen both have ging), so the simple verb comes first, then the shorter lemma.
        var lemmas = lexicon.Lemmas.Select(lexicon.Folding.Key).Distinct(StringComparer.Ordinal)
            .OrderBy(l => VerbOf(l)?.Particle is not null)
            .ThenBy(static l => l.Length)
            .ThenBy(static l => l, StringComparer.Ordinal);
        foreach (var lemma in lemmas)
        {
            if (NoRegularization.Contains(lemma) || VerbOf(lemma) is not { } verb || verb.Declension is null or "weak")
            {
                continue;
            }

            var baseInfinitive = verb.BaseInfinitive;
            foreach (var number in Numbers)
            {
                foreach (var person in Persons)
                {
                    if (number == "sg" && person is "2" or "3")
                    {
                        Add(index, lemma, GermanVerbRules.Present(baseInfinitive, person, number), person, number, "pres", StemChangeMissed);
                    }
                }
            }

            foreach (var number in Numbers)
            {
                foreach (var person in Persons)
                {
                    Add(index, lemma, GermanVerbRules.Preterite(baseInfinitive, person, number), person, number, "pret", WeakPreterite);
                }
            }

            Add(index, lemma, GermanVerbRules.Participle(verb.Infinitive, verb.Particle, Participle(lemma)), string.Empty, string.Empty, "perf", WeakParticiple);
        }

        return index;
    }

    private void Add(Dictionary<string, GermanRegularizedForm> index, string lemma, string? regular, string person, string number, string tense, string kind)
    {
        if (regular is null)
        {
            return;
        }

        var expected = tense == "perf" ? Participle(lemma) : Finite(lemma, person, number, tense);
        if (expected is null || string.Equals(regular, expected, StringComparison.Ordinal))
        {
            return;
        }

        var verb = VerbOf(lemma)!;
        var lexical = tense == "perf" ? verb.AllParticiples : verb.Finite(person, number, tense);
        if (lexical.Contains(regular, StringComparer.Ordinal))
        {
            return;
        }

        index.TryAdd(regular, new GermanRegularizedForm(new RegularizedForm(lemma, expected, person, number, tense), kind));
    }
}
