using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// The German profile of the wiktextract import (M36). German entries keep their spelling (nouns are capitalised; the lexicon's key folds the case), the
/// lemma of a verb is its infinitive, and the lexicon entries carry the cases (<c>nom acc dat gen</c>), the three genders, the declension class
/// (<c>strong weak mixed</c>, for adjectives their declension after no article, the definite article and the indefinite article), the separable prefix
/// (<c>particle</c>) and the verb form (<c>inf part1 part2 part2-haben part2-sein</c>, the Perfekt auxiliary being part of the participle's form).
/// </summary>
/// <remarks>
/// The present and the Präteritum indicative, the Konjunktiv II (as <c>pret</c>, mood <c>subj</c>) and the imperative of a verb are imported; the
/// Konjunktiv I, the compound tenses, comparatives and superlatives are not. A finite form of a separable verb is written as two words in Wiktionary
/// (<c>stehe auf</c>); it becomes the entry <c>stehe</c> with the particle <c>auf</c>. Reflexive verbs (<c>sich freuen</c>) have a multi-word head word and are skipped.
/// </remarks>
internal sealed partial class GermanKaikkiProfile : KaikkiLanguageProfile
{
    public const string Nominative = "nom";
    public const string Accusative = "acc";
    public const string Dative = "dat";
    public const string Genitive = "gen";
    public const string Neuter = "n";

    /// <summary>The separable prefixes the profile knows (the particle values); a verb whose prefix is not here is skipped.</summary>
    public static readonly ImmutableArray<string> Particles =
    [
        "ab", "an", "auf", "aus", "bei", "ein", "fest", "fort", "her", "hin", "los", "mit", "nach", "vor", "weg", "zu", "zurück", "zusammen", "weiter",
        "vorbei", "heraus", "herein", "herauf", "herunter", "hinaus", "hinein", "hinauf", "hinunter", "herum", "heran", "hervor", "hoch", "nieder", "wieder",
        "entgegen", "empor", "voraus", "voran", "um", "durch", "über", "unter", "hinzu", "dar", "raus", "rein", "rum", "runter", "rauf", "rüber", "ran",
        "davon", "dabei", "dazu", "drauf", "heim", "frei", "statt", "teil", "auseinander", "aneinander", "aufeinander", "beisammen", "hinterher", "hinweg",
        "umher", "vorher", "vorweg", "herbei", "herab", "hinab", "kennen", "spazieren", "bereit", "kaputt", "da", "inne",
    ];

    public static readonly LexiconProfile LexProfile = new(
        genders: ["m", "f", "n"],
        numbers: LexiconSchema.Numbers,
        persons: LexiconSchema.Persons,
        tenses: ["pres", "pret"],
        moods: LexiconSchema.Moods,
        cases: [Nominative, Accusative, Dative, Genitive],
        verbForms: ["inf", "part1", "part2", "part2-haben", "part2-sein"],
        declensions: ["strong", "weak", "mixed", "irregular"],
        particles: Particles,
        registers: []);

    public const string NotAnInfinitive = "verb head word is not an infinitive (reflexive and multi-word verbs are skipped)";
    public const string UnknownParticle = "separable verb whose prefix is not in the profile's particle list";

    private static readonly ImmutableDictionary<string, string> PartsOfSpeech = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["noun"] = LexiconSchema.Noun,
        ["verb"] = LexiconSchema.Verb,
        ["adj"] = LexiconSchema.Adjective,
        ["det"] = LexiconSchema.Adjective,
        ["adv"] = LexiconSchema.Adverb,
        ["particle"] = LexiconSchema.Adverb,
        ["prep"] = LexiconSchema.Preposition,
        ["postp"] = LexiconSchema.Preposition,
        ["conj"] = LexiconSchema.Conjunction,
        ["pron"] = LexiconSchema.Pronoun,
        ["article"] = LexiconSchema.Article,
        ["intj"] = LexiconSchema.Interjection,
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableHashSet<string> UnsuitableSenseTags = ImmutableHashSet.Create(
        StringComparer.Ordinal, "obsolete", "archaic", "dated", "nonstandard", "neologism", "proscribed", "misspelling", "error-misspelling");

    private static readonly ImmutableHashSet<string> InflectionSenseTags = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "nominative", "accusative", "dative", "genitive", "singular", "plural", "participle", "past", "present", "imperative", "subjunctive", "indicative",
        "infinitive", "first-person", "second-person", "third-person", "comparative", "superlative", "gerund", "inflection", "dependent", "colloquial-form");

    private static readonly ImmutableHashSet<string> IgnoredFormTags = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "table-tags", "inflection-template", "class", "romanization", "multiword-construction", "obsolete", "archaic", "dated", "rare", "nonstandard",
        "uncommon", "proscribed", "alternative", "dialectal", "regional", "colloquial", "poetic", "literary", "informal", "humorous", "jargon",
        "Switzerland", "Austria", "Liechtenstein", "Luxembourg", "Southern-Germany", "southern-Germany", "Upper-German", "Swiss-German", "Northwest-German",
        "diminutive", "comparative", "superlative", "includes-article", "with-article", "abbreviation", "misspelling", "subordinate-clause", "short-form",
        "enclitic", "pronunciation-spelling", "initialism", "clipping", "historical", "formal", "honorific", "predicative");

    public override string Name => "German";

    public override string LanguageCode => "de";

    public override string ImporterVersion => "de-1.0.0";

    public override LexiconProfile Profile => LexProfile;

    protected override ILexiconFolding Folding => InvariantLexiconFolding.Instance;

    internal override string FrequencyKey(string form) => FrequencyList.Fold(form);

    internal override string? ImportEntry(JsonElement root, List<LexiconEntry> entries, HandAuthoredLemmas? handAuthored)
    {
        var word = KaikkiImporter.GetString(root, "word");
        var rawPos = KaikkiImporter.GetString(root, "pos");
        if (string.IsNullOrWhiteSpace(word))
        {
            return "no head word";
        }

        if (rawPos is null)
        {
            return "no part of speech";
        }

        if (string.Equals(rawPos, "num", StringComparison.Ordinal))
        {
            return "numeral (lemma is a digit string in the lexicon schema)";
        }

        if (!PartsOfSpeech.TryGetValue(rawPos, out var pos))
        {
            return $"unsupported part of speech '{rawPos}'";
        }

        // A sense that is "form of" another entry is an inflected form only when it says which form (nominative plural, past participle, ...): the agent noun
        // Lehrer ("agent noun of lehren") and the feminine Lehrerin are lemmas with a form_of link to their base word.
        if (pos != LexiconSchema.Pronoun && (KaikkiImporter.AllSenses(root, "form_of") && KaikkiImporter.AllSensesTagged(root, InflectionSenseTags) || KaikkiImporter.IsInflectedFormPage(root)))
        {
            return KaikkiImporter.InflectedFormReason;
        }

        if (KaikkiImporter.AllSenses(root, "alt_of"))
        {
            return KaikkiImporter.AlternativeFormReason;
        }

        if (KaikkiImporter.AllSensesTagged(root, UnsuitableSenseTags))
        {
            return "every sense is obsolete, archaic, dated, nonstandard, a neologism or a misspelling";
        }

        var lemma = word.Trim().Normalize(NormalizationForm.FormC);
        if (lemma.Any(char.IsWhiteSpace) || lemma.StartsWith('-') || lemma.EndsWith('-'))
        {
            return "multi-word or affix head word";
        }

        if (!lemma.All(char.IsLetter))
        {
            return "head word has characters other than letters";
        }

        if (pos == LexiconSchema.Verb && !lemma.EndsWith('n'))
        {
            return NotAnInfinitive;
        }

        var made = new List<LexiconEntry>();
        string? reason = null;
        switch (pos)
        {
            case LexiconSchema.Verb:
                reason = AddVerb(root, lemma, made);
                break;
            case LexiconSchema.Noun:
                AddNoun(root, lemma, made);
                break;
            case LexiconSchema.Pronoun:
                AddPronoun(root, lemma, made);
                break;
            case LexiconSchema.Adjective:
            case LexiconSchema.Article:
                AddCells(root, lemma, pos, made);
                break;
            default:
                made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
                break;
        }

        if (reason is not null)
        {
            return reason;
        }

        entries.AddRange(made.Distinct());
        return null;
    }

    // Wiktionary hangs the whole table of the personal pronouns (ich, du, er, ... with all four cases) on every one of them and tags du, ihr and Sie as
    // unrecognised, so the table is not read: a pronoun is its head word. The inflected forms (mich, dir, euch) are pages of their own, tagged with their
    // case, and become pronouns of their own with that case when it is unambiguous.
    private static void AddPronoun(JsonElement root, string lemma, List<LexiconEntry> made)
    {
        string? kase = null;
        if (root.TryGetProperty("senses", out var senses) && senses.ValueKind == JsonValueKind.Array)
        {
            var cases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sense in senses.EnumerateArray())
            {
                var tags = KaikkiImporter.Tags(sense);
                if (KaikkiImporter.AllSenses(root, "form_of") && Case(tags, 0) is { } found)
                {
                    cases.Add(found);
                }
            }

            kase = cases.Count == 1 ? cases.First() : null;
        }

        made.Add(new LexiconEntry(lemma, lemma, LexiconSchema.Pronoun, kase is null ? EntryFeatures.None : new EntryFeatures(null, null, null, null, null, kase)));
    }

    private static bool Usable(string text, ImmutableHashSet<string> tags, bool allowTwoWords)
    {
        if (text.Length == 0 || tags.Overlaps(IgnoredFormTags) || tags.Any(static tag => tag.StartsWith("error-", StringComparison.Ordinal)))
        {
            return false;
        }

        var words = text.Split(' ');
        return (words.Length == 1 || (allowTwoWords && words.Length == 2)) && words.All(static w => w.Length > 0 && w.All(char.IsLetter));
    }

    private static string? Case(ImmutableHashSet<string> tags, int index)
    {
        string[] order = ["nominative", "accusative", "dative", "genitive"];
        string[] values = [Nominative, Accusative, Dative, Genitive];
        var seen = 0;
        for (var i = 0; i < order.Length; i++)
        {
            if (tags.Contains(order[i]) && seen++ == index)
            {
                return values[i];
            }
        }

        return null;
    }

    private static string? GenderOf(ImmutableHashSet<string> tags)
    {
        return tags.Contains("masculine") ? LexiconSchema.Masculine : tags.Contains("feminine") ? LexiconSchema.Feminine : tags.Contains("neuter") ? Neuter : null;
    }

    private static string? NumberOf(ImmutableHashSet<string> tags)
    {
        return tags.Contains("plural") ? LexiconSchema.Plural : tags.Contains("singular") ? LexiconSchema.Singular : null;
    }

    private static string? ClassOf(ImmutableHashSet<string> tags)
    {
        return tags.Contains("strong") ? "strong" : tags.Contains("weak") ? "weak" : tags.Contains("mixed") ? "mixed" : null;
    }

    // The declension or conjugation class from the first parenthesis of the head template's expansion: "(strong, genitive ...", "(class 7 strong, ...", "(weak, ...".
    private static string? HeadClass(JsonElement root, bool verb)
    {
        if (!root.TryGetProperty("head_templates", out var templates) || templates.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var template in templates.EnumerateArray())
        {
            if (KaikkiImporter.GetString(template, "expansion") is not { } expansion)
            {
                continue;
            }

            var open = expansion.IndexOf('(', StringComparison.Ordinal);
            if (open < 0)
            {
                continue;
            }

            var group = expansion[(open + 1)..].Split([',', ')'])[0].Trim();
            if (group.Contains(" or ", StringComparison.Ordinal))
            {
                return null;
            }

            if (group.EndsWith("strong", StringComparison.Ordinal))
            {
                return "strong";
            }

            if (group.EndsWith("mixed", StringComparison.Ordinal))
            {
                return "mixed";
            }

            if (group.EndsWith("weak", StringComparison.Ordinal))
            {
                return "weak";
            }

            return verb && (group.Contains("irregular", StringComparison.Ordinal) || group.Contains("preterite-present", StringComparison.Ordinal)) ? "irregular" : null;
        }

        return null;
    }

    private static IReadOnlyList<string> NounGenders(JsonElement root, out bool pluralOnly)
    {
        var genders = new SortedSet<string>(StringComparer.Ordinal);
        pluralOnly = false;
        if (root.TryGetProperty("head_templates", out var templates) && templates.ValueKind == JsonValueKind.Array)
        {
            foreach (var template in templates.EnumerateArray())
            {
                if (KaikkiImporter.GetString(template, "name") != "de-noun" || !template.TryGetProperty("args", out var args)
                    || KaikkiImporter.GetString(args, "1") is not { } first)
                {
                    continue;
                }

                var token = first.Split([',', '.', ':', '<', '['])[0];
                if (token is "m" or "f" or "n")
                {
                    genders.Add(token);
                }
                else if (token is "mp" or "fp" or "np")
                {
                    genders.Add(token[..1]);
                    pluralOnly = true;
                }
                else if (token == "p")
                {
                    pluralOnly = true;
                }
            }
        }

        if (genders.Count == 0 && root.TryGetProperty("senses", out var senses) && senses.ValueKind == JsonValueKind.Array)
        {
            foreach (var sense in senses.EnumerateArray())
            {
                var gender = GenderOf(KaikkiImporter.Tags(sense));
                if (gender is not null)
                {
                    genders.Add(gender);
                }
            }
        }

        pluralOnly |= KaikkiImporter.AllSensesTagged(root, ImmutableHashSet.Create(StringComparer.Ordinal, "plural-only"));
        return [.. genders];
    }

    private static void AddNoun(JsonElement root, string lemma, List<LexiconEntry> made)
    {
        var genders = NounGenders(root, out var pluralOnly);
        var declension = HeadClass(root, verb: false);
        var forms = KaikkiImporter.Forms(root).Select(static f => (f.Text, Tags: KaikkiImporter.Tags(f.Element))).Where(f => Usable(f.Text, f.Tags, false)).ToList();
        var hasCells = forms.Any(static f => Case(f.Tags, 0) is not null && NumberOf(f.Tags) is not null);
        foreach (var (text, tags) in forms)
        {
            var number = NumberOf(tags);
            var cases = new List<string?>();
            for (var i = 0; Case(tags, i) is { } c; i++)
            {
                cases.Add(c);
            }

            if (hasCells ? cases.Count == 0 || number is null : cases.Count == 0 && number is null)
            {
                continue;
            }

            number ??= LexiconSchema.Singular;
            if (cases.Count == 0)
            {
                cases.Add(null);
            }

            foreach (var kase in cases)
            {
                AddNounEntries(text, lemma, number, kase, declension, genders, made);
            }
        }

        if (!made.Any(entry => entry.Form == lemma))
        {
            AddNounEntries(lemma, lemma, pluralOnly ? LexiconSchema.Plural : LexiconSchema.Singular, null, declension, genders, made);
        }
    }

    private static void AddNounEntries(string form, string lemma, string number, string? kase, string? declension, IReadOnlyList<string> genders, List<LexiconEntry> made)
    {
        if (genders.Count == 0)
        {
            made.Add(new LexiconEntry(form, lemma, LexiconSchema.Noun, new EntryFeatures(null, number, null, null, null, kase, Declension: declension)));
            return;
        }

        foreach (var gender in genders)
        {
            made.Add(new LexiconEntry(form, lemma, LexiconSchema.Noun, new EntryFeatures(null, number, null, null, gender, kase, Declension: declension)));
        }
    }

    // Adjectives, articles, determiners and pronouns: one entry per cell of the declension table (case, gender, number, and for adjectives the class).
    private static void AddCells(JsonElement root, string lemma, string pos, List<LexiconEntry> made)
    {
        foreach (var form in KaikkiImporter.Forms(root))
        {
            var tags = KaikkiImporter.Tags(form.Element);
            if (!Usable(form.Text, tags, false))
            {
                continue;
            }

            var person = tags.Contains("first-person") ? "1" : tags.Contains("second-person") ? "2" : tags.Contains("third-person") ? "3" : null;
            var number = NumberOf(tags);
            var gender = GenderOf(tags);
            for (var i = 0; Case(tags, i) is { } kase; i++)
            {
                var features = new EntryFeatures(person, number ?? (gender is not null ? LexiconSchema.Singular : null), null, null, gender, kase, Declension: ClassOf(tags));
                made.Add(new LexiconEntry(form.Text, lemma, pos, features));
            }
        }

        if (!made.Any(entry => entry.Form == lemma && entry.Features.Case is null))
        {
            made.Add(new LexiconEntry(lemma, lemma, pos, EntryFeatures.None));
        }
    }

    private static string? AddVerb(JsonElement root, string lemma, List<LexiconEntry> made)
    {
        var declension = HeadClass(root, verb: true);
        var forms = KaikkiImporter.Forms(root).Select(static f => (f.Text, Tags: KaikkiImporter.Tags(f.Element), Raw: f.Element.TryGetProperty("raw_tags", out _))).ToList();
        var auxiliaries = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (text, tags, _) in forms)
        {
            if (tags.Contains("auxiliary") && text is "haben" or "sein")
            {
                auxiliaries.Add(text);
            }
        }

        string? prefix = null;
        var finite = new List<LexiconEntry>();
        foreach (var (text, tags, raw) in forms)
        {
            if (!Usable(text, tags, true) || raw)
            {
                continue;
            }

            var words = text.Split(' ');
            string? particle = null;
            if (words.Length == 2)
            {
                if (!lemma.StartsWith(words[1], StringComparison.Ordinal) || lemma.Length == words[1].Length)
                {
                    continue;
                }

                if (!Particles.Contains(words[1]))
                {
                    return UnknownParticle;
                }

                particle = words[1];
                prefix = particle;
            }

            var single = words[0];
            if (tags.Contains("participle") && words.Length == 1)
            {
                if (tags.Contains("past"))
                {
                    foreach (var form in auxiliaries.Count == 0 ? ["part2"] : auxiliaries.Select(static a => "part2-" + a))
                    {
                        made.Add(new LexiconEntry(single, lemma, LexiconSchema.Verb, new EntryFeatures(null, null, null, null, null, VerbForm: form, Declension: declension)));
                    }
                }
                else if (tags.Contains("present"))
                {
                    made.Add(new LexiconEntry(single, lemma, LexiconSchema.Verb, new EntryFeatures(null, null, null, null, null, VerbForm: "part1")));
                }

                continue;
            }

            if (tags.Contains("infinitive") || tags.Contains("participle"))
            {
                continue;
            }

            var person = tags.Contains("first-person") ? "1" : tags.Contains("second-person") ? "2" : tags.Contains("third-person") ? "3" : null;
            var number = NumberOf(tags);
            if (person is null || number is null)
            {
                continue;
            }

            string tense;
            string mood;
            if (tags.Contains("imperative"))
            {
                if (person != "2")
                {
                    continue;
                }

                tense = null!;
                mood = LexiconSchema.Imperative;
            }
            else if (tags.Contains("subjunctive-ii"))
            {
                (tense, mood) = (LexiconSchema.Preterite, LexiconSchema.Subjunctive);
            }
            else if (tags.Contains("subjunctive") || tags.Contains("subjunctive-i") || tags.Contains("future") || tags.Contains("perfect") || tags.Contains("pluperfect"))
            {
                continue;
            }
            else if (tags.Contains("present"))
            {
                (tense, mood) = (LexiconSchema.Present, LexiconSchema.Indicative);
            }
            else if (tags.Contains("preterite"))
            {
                (tense, mood) = (LexiconSchema.Preterite, LexiconSchema.Indicative);
            }
            else
            {
                continue;
            }

            finite.Add(new LexiconEntry(single, lemma, LexiconSchema.Verb, new EntryFeatures(person, number, tense, mood, null, Particle: particle)));
        }

        made.Add(new LexiconEntry(lemma, lemma, LexiconSchema.Verb, new EntryFeatures(null, null, null, null, null, VerbForm: "inf", Particle: prefix, Declension: declension)));
        made.AddRange(finite);
        return null;
    }
}
