namespace Vocaterra.Nlp.Nlp.Morphology;

using Vocaterra.Nlp.Nlp.Lexicon;

/// <summary>A regularized irregular verb form (such as <c>sabo</c>) with the lexicon form the learner should have used.</summary>
internal sealed record RegularizedForm(string Lemma, string Expected, string Person, string Number, string Tense);

/// <summary>Generates Spanish word forms: plurals, articles, adjective agreement and verb conjugation, lexicon first and rules second.</summary>
public sealed class MorphologyGenerator
{
    private static readonly string[] PresentAr = ["o", "as", "a", "amos", "áis", "an"];
    private static readonly string[] PresentEr = ["o", "es", "e", "emos", "éis", "en"];
    private static readonly string[] PresentIr = ["o", "es", "e", "imos", "ís", "en"];
    private static readonly string[] PreteriteAr = ["é", "aste", "ó", "amos", "asteis", "aron"];
    private static readonly string[] PreteriteErIr = ["í", "iste", "ió", "imos", "isteis", "ieron"];
    private static readonly string[] PreteriteVowelStem = ["í", "íste", "yó", "ímos", "ísteis", "yeron"];
    private static readonly string[] InvariantOrComparative =
    [
        "mejor", "peor", "mayor", "menor", "superior", "inferior", "exterior", "interior", "anterior", "posterior", "ulterior",
    ];

    private readonly Lexicon _lexicon;
    private readonly Lazy<Dictionary<string, RegularizedForm>> _regularized;

    /// <summary>Creates a generator that prefers forms from the given lexicon and falls back to spelling rules.</summary>
    public MorphologyGenerator(Lexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        _lexicon = lexicon;
        _regularized = new Lazy<Dictionary<string, RegularizedForm>>(() => lexicon.Derive("regularized-forms", BuildRegularizedIndex));
    }

    /// <summary>Returns the plural of a noun: the lexicon plural when known, else vowel adds s, consonant adds es, z becomes ces, unstressed final s is unchanged.</summary>
    public string Pluralize(string noun)
    {
        var word = Require(noun);
        return LexiconNounForm(word, LexiconSchema.Plural) ?? PluralizeByRule(word);
    }

    /// <summary>Returns the singular of a noun: the lexicon singular when known, else the inverse of the plural rules.</summary>
    public string Singularize(string noun)
    {
        var word = Require(noun);
        return LexiconNounForm(word, LexiconSchema.Singular) ?? SingularizeByRule(word);
    }

    /// <summary>Returns the noun form for a lemma in the given number (<c>sg</c> or <c>pl</c>), lexicon first.</summary>
    public string InflectNoun(string lemma, string number)
    {
        var word = Require(lemma);
        var target = RequireNumber(number);
        return LexiconNounForm(word, target) ?? (target == LexiconSchema.Plural ? PluralizeByRule(word) : word);
    }

    /// <summary>Returns el/la/los/las or un/una/unos/unas for the gender and number (<c>m|f</c>, <c>sg|pl</c>).</summary>
    public string Article(Definiteness definiteness, string gender, string number)
    {
        var g = RequireGender(gender);
        var n = RequireNumber(number);
        var masculine = g == LexiconSchema.Masculine;
        var singular = n == LexiconSchema.Singular;
        if (definiteness == Definiteness.Definite)
        {
            return singular ? (masculine ? "el" : "la") : (masculine ? "los" : "las");
        }

        return singular ? (masculine ? "un" : "una") : (masculine ? "unos" : "unas");
    }

    /// <summary>Like <see cref="Article(Definiteness, string, string)"/> but uses el/un for feminine singular nouns that start with a stressed a (agua, arma).</summary>
    public string Article(Definiteness definiteness, string gender, string number, string nounLemma)
    {
        ArgumentNullException.ThrowIfNull(nounLemma);
        if (RequireGender(gender) == LexiconSchema.Feminine
            && RequireNumber(number) == LexiconSchema.Singular
            && WordClasses.TakesElBeforeFeminine(TextFolding.Normalize(nounLemma)))
        {
            return Article(definiteness, LexiconSchema.Masculine, number);
        }

        return Article(definiteness, gender, number);
    }

    /// <summary>Returns the adjective form agreeing in gender and number; the lemma is the masculine singular. Lexicon forms win over rules.</summary>
    public string AgreeAdjective(string lemma, string gender, string number)
    {
        var word = Require(lemma);
        var g = RequireGender(gender);
        var n = RequireNumber(number);
        var adjectiveLemma = ResolveAdjectiveLemma(word);
        string? best = null;
        foreach (var entry in _lexicon.EntriesForLemma(adjectiveLemma))
        {
            if (entry.Pos == LexiconSchema.Adjective
                && string.Equals(entry.Features.Gender, g, StringComparison.Ordinal)
                && string.Equals(entry.Features.Number, n, StringComparison.Ordinal)
                && (best is null || entry.Form.Length > best.Length))
            {
                // Longest form wins so that the apocopated gran/buen/mal never replace grande/bueno/malo.
                best = entry.Form;
            }
        }

        if (best is not null)
        {
            return best;
        }

        var singular = g == LexiconSchema.Feminine ? FeminineSingular(adjectiveLemma) : adjectiveLemma;
        return n == LexiconSchema.Plural ? PluralizeByRule(singular) : singular;
    }

    /// <summary>Returns the indicative form for a lemma, person (<c>1|2|3</c>), number and tense, or null when it cannot be generated.</summary>
    public string? Conjugate(string lemma, string person, string number, string tense)
    {
        var word = Require(lemma);
        var p = RequirePerson(person);
        var n = RequireNumber(number);
        ArgumentNullException.ThrowIfNull(tense);
        return LexiconConjugate(word, p, n, tense) ?? ConjugateRegular(word, p, n, tense);
    }

    /// <summary>Detects forms such as <c>sabo</c> or <c>tení</c> that are the regular paradigm cell of an irregular lexicon verb.</summary>
    public bool IsRegularizedIrregular(string form, out string lemma, out string expected)
    {
        if (TryGetRegularization(form, out var found))
        {
            lemma = found.Lemma;
            expected = found.Expected;
            return true;
        }

        lemma = string.Empty;
        expected = string.Empty;
        return false;
    }

    /// <summary>Generates a form from the regular -ar/-er/-ir paradigm (present and preterite only); null otherwise.</summary>
    internal static string? ConjugateRegular(string lemma, string person, string number, string tense)
    {
        if (lemma.Length < 3 || !(lemma.EndsWith("ar", StringComparison.Ordinal)
            || lemma.EndsWith("er", StringComparison.Ordinal) || lemma.EndsWith("ir", StringComparison.Ordinal)))
        {
            return null;
        }

        var ending = lemma[^2..];
        var stem = lemma[..^2];
        var index = (person[0] - '1') + (number == LexiconSchema.Plural ? 3 : 0);
        if (tense == LexiconSchema.Present)
        {
            if (index == 0)
            {
                return FirstPersonPresent(stem, ending);
            }

            var endings = ending == "ar" ? PresentAr : ending == "er" ? PresentEr : PresentIr;
            return stem + endings[index];
        }

        if (tense == LexiconSchema.Preterite)
        {
            if (ending == "ar")
            {
                return index == 0 ? FirstPersonPreteriteAr(stem) : stem + PreteriteAr[index];
            }

            var vowelStem = stem.Length > 0
                && "aeou".Contains(stem[^1], StringComparison.Ordinal)
                && !stem.EndsWith("qu", StringComparison.Ordinal)
                && !stem.EndsWith("gu", StringComparison.Ordinal);
            return stem + (vowelStem ? PreteriteVowelStem[index] : PreteriteErIr[index]);
        }

        return null;
    }

    /// <summary>Applies the plural spelling rules without consulting the lexicon.</summary>
    internal static string PluralizeByRule(string word)
    {
        var last = word[^1];
        if (last is 'a' or 'e' or 'i' or 'o' or 'u' or 'á' or 'é' or 'í' or 'ó' or 'ú')
        {
            return word + "s";
        }

        if (last == 'y')
        {
            return word.Length > 1 && SpanishOrthography.IsVowel(word[^2]) ? word + "es" : word + "s";
        }

        if (last == 'z')
        {
            return word[..^1] + "ces";
        }

        var nuclei = SpanishOrthography.Nuclei(word);
        if (nuclei.Count == 0)
        {
            return word + "es";
        }

        if (last == 's')
        {
            return PluralizeFinalS(word, nuclei);
        }

        if (last == 'x')
        {
            return nuclei.Count > 1 ? word : word + "es";
        }

        if (last == 'n')
        {
            var lastNucleus = nuclei[^1];
            var accent = SpanishOrthography.WrittenAccentIndex(word, lastNucleus);
            if (accent >= 0)
            {
                // lección -> lecciones, jardín -> jardines: the stress now falls on the penultimate syllable.
                return SpanishOrthography.RemoveAccentAt(word, accent) + "es";
            }

            if (nuclei.Count > 1 && word.EndsWith("en", StringComparison.Ordinal) && !word.Any(SpanishOrthography.IsAccentedVowel))
            {
                // joven -> jóvenes, examen -> exámenes: a stressed penultimate syllable needs the written accent.
                return SpanishOrthography.AddAccentAt(word, SpanishOrthography.AccentPosition(word, nuclei[^2])) + "es";
            }
        }

        return word + "es";
    }

    /// <summary>Applies the inverse of the plural spelling rules without consulting the lexicon.</summary>
    internal static string SingularizeByRule(string word)
    {
        if (word.Length > 3 && word.EndsWith("ces", StringComparison.Ordinal))
        {
            return word[..^3] + "z";
        }

        if (word.Length > 2 && word.EndsWith("es", StringComparison.Ordinal))
        {
            var stem = word[..^2];
            var last = stem[^1];
            if (last is 'í' or 'ú')
            {
                return stem;
            }

            if (last == 'y')
            {
                return stem;
            }

            var validFinalConsonant = last is 'd' or 'l' or 'n' or 'r' or 's' or 'j' or 'x'
                && stem.Length > 1
                && SpanishOrthography.IsVowel(stem[^2]);
            return validFinalConsonant ? RestoreSingularAccent(stem) : word[..^1];
        }

        if (word.Length > 1 && word.EndsWith('s'))
        {
            var stem = word[..^1];
            if (!SpanishOrthography.IsVowel(stem[^1]))
            {
                return word;
            }

            var unstressedIsUs = (word.EndsWith("is", StringComparison.Ordinal) || word.EndsWith("us", StringComparison.Ordinal))
                && SpanishOrthography.Nuclei(word).Count > 1;
            return unstressedIsUs ? word : stem;
        }

        return word;
    }

    /// <summary>Looks up a regularized form in the index of the lexicon's irregular verbs; known words are never reported.</summary>
    internal bool TryGetRegularization(string form, out RegularizedForm result)
    {
        ArgumentNullException.ThrowIfNull(form);
        var key = TextFolding.Normalize(form);
        if (key.Length > 0 && _regularized.Value.TryGetValue(key, out var found) && !IsKnownWord(key))
        {
            result = found;
            return true;
        }

        result = null!;
        return false;
    }

    private bool IsKnownWord(string key)
    {
        // An accented form with no exact entry (estó) is a deliberate spelling, so the accent-insensitive fallback (esto) does not make it known.
        var found = _lexicon.Lookup(key);
        return found.Count > 0 && (!found.IsAccentInsensitive || string.Equals(TextFolding.StripAccents(key), key, StringComparison.Ordinal));
    }

    private static string FirstPersonPresent(string stem, string ending)
    {
        if (ending == "ar")
        {
            return stem + "o";
        }

        if (stem.EndsWith("gu", StringComparison.Ordinal))
        {
            return stem[..^1] + "o";
        }

        if (stem.EndsWith('g'))
        {
            return stem[..^1] + "jo";
        }

        if (stem.Length > 1 && stem.EndsWith('c') && !SpanishOrthography.IsVowel(stem[^2]))
        {
            return stem[..^1] + "zo";
        }

        return stem + "o";
    }

    private static string FirstPersonPreteriteAr(string stem)
    {
        if (stem.EndsWith('c'))
        {
            return stem[..^1] + "qué";
        }

        if (stem.EndsWith('g'))
        {
            return stem + "ué";
        }

        return stem.EndsWith('z') ? stem[..^1] + "cé" : stem + "é";
    }

    private static string PluralizeFinalS(string word, List<(int Start, int Length)> nuclei)
    {
        if (nuclei.Count == 1)
        {
            return word + "es";
        }

        var accent = SpanishOrthography.WrittenAccentIndex(word, nuclei[^1]);
        return accent >= 0 ? SpanishOrthography.RemoveAccentAt(word, accent) + "es" : word;
    }

    private static string RestoreSingularAccent(string stem)
    {
        var last = stem[^1];
        if (last is not ('n' or 's'))
        {
            return stem;
        }

        var nuclei = SpanishOrthography.Nuclei(stem);
        if (nuclei.Count < 2)
        {
            return stem;
        }

        for (var i = 0; i < nuclei.Count - 1; i++)
        {
            var accent = SpanishOrthography.WrittenAccentIndex(stem, nuclei[i]);
            if (accent >= 0)
            {
                // jóven -> joven, imágen -> imagen: a grave word ending in n or s needs no accent.
                return last == 'n' ? SpanishOrthography.RemoveAccentAt(stem, accent) : stem;
            }
        }

        if (SpanishOrthography.WrittenAccentIndex(stem, nuclei[^1]) >= 0)
        {
            return stem;
        }

        // canción, jardín, inglés: an unaccented plural of a stressed-last-syllable word gets its accent back.
        return SpanishOrthography.AddAccentAt(stem, SpanishOrthography.AccentPosition(stem, nuclei[^1]));
    }

    private static string FeminineSingular(string lemma)
    {
        if (lemma.EndsWith('o'))
        {
            return lemma[..^1] + "a";
        }

        if (lemma.EndsWith('a') || lemma.EndsWith('e'))
        {
            return lemma;
        }

        if (lemma.EndsWith("or", StringComparison.Ordinal) && Array.IndexOf(InvariantOrComparative, lemma) < 0)
        {
            return lemma + "a";
        }

        if (lemma.EndsWith("ol", StringComparison.Ordinal))
        {
            return lemma + "a";
        }

        foreach (var suffix in new[] { "án", "ón", "ín", "és" })
        {
            if (lemma.EndsWith(suffix, StringComparison.Ordinal))
            {
                return lemma[..^2] + SpanishOrthography.RemoveAccent(suffix[0]) + suffix[1] + "a";
            }
        }

        return lemma;
    }

    private static string Require(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var word = TextFolding.Normalize(text.Trim());
        if (word.Length == 0)
        {
            throw new ArgumentException("A non-empty word is required.", nameof(text));
        }

        return word;
    }

    private static string RequireGender(string gender)
    {
        ArgumentNullException.ThrowIfNull(gender);
        return gender is LexiconSchema.Masculine or LexiconSchema.Feminine
            ? gender
            : throw new ArgumentException($"Unknown gender '{gender}'; expected m or f.", nameof(gender));
    }

    private static string RequireNumber(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        return number is LexiconSchema.Singular or LexiconSchema.Plural
            ? number
            : throw new ArgumentException($"Unknown number '{number}'; expected sg or pl.", nameof(number));
    }

    private static string RequirePerson(string person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return person is "1" or "2" or "3"
            ? person
            : throw new ArgumentException($"Unknown person '{person}'; expected 1, 2 or 3.", nameof(person));
    }

    private string ResolveAdjectiveLemma(string word)
    {
        if (_lexicon.ContainsLemma(word))
        {
            return word;
        }

        var readings = _lexicon.Lookup(word);
        foreach (var reading in readings)
        {
            if (reading.Pos == LexiconSchema.Adjective)
            {
                return reading.Lemma;
            }
        }

        return word;
    }

    private string? LexiconNounForm(string word, string targetNumber)
    {
        var readings = _lexicon.Lookup(word);
        if (readings.Count == 0 || readings.IsAccentInsensitive)
        {
            return null;
        }

        foreach (var reading in readings)
        {
            if (reading.Pos == LexiconSchema.Noun && string.Equals(reading.Number, targetNumber, StringComparison.Ordinal))
            {
                return word;
            }
        }

        foreach (var reading in readings)
        {
            if (reading.Pos != LexiconSchema.Noun)
            {
                continue;
            }

            foreach (var entry in _lexicon.EntriesForLemma(reading.Lemma))
            {
                if (entry.Pos == LexiconSchema.Noun
                    && string.Equals(entry.Features.Number, targetNumber, StringComparison.Ordinal)
                    && (entry.Features.Gender is null || reading.Gender is null || entry.Features.Gender == reading.Gender))
                {
                    return entry.Form;
                }
            }
        }

        return null;
    }

    private string? LexiconConjugate(string lemma, string person, string number, string tense)
    {
        foreach (var entry in _lexicon.EntriesForLemma(lemma))
        {
            if (entry.Pos == LexiconSchema.Verb
                && string.Equals(entry.Features.Tense, tense, StringComparison.Ordinal)
                && string.Equals(entry.Features.Person, person, StringComparison.Ordinal)
                && string.Equals(entry.Features.Number, number, StringComparison.Ordinal)
                && (entry.Features.Mood is null || entry.Features.Mood == LexiconSchema.Indicative))
            {
                return entry.Form;
            }
        }

        return null;
    }

    private static Dictionary<string, RegularizedForm> BuildRegularizedIndex(Lexicon lexicon)
    {
        var index = new Dictionary<string, RegularizedForm>(StringComparer.Ordinal);
        string[] persons = ["1", "2", "3"];
        string[] numbers = [LexiconSchema.Singular, LexiconSchema.Plural];
        string[] tenses = [LexiconSchema.Present, LexiconSchema.Preterite];
        var cells = new string?[tenses.Length * numbers.Length * persons.Length];
        foreach (var lemma in lexicon.Lemmas)
        {
            // Stems shorter than two letters (ser, ir, dar, ver) have no plausible regularized forms, and only -ar, -er and -ir verbs have a regular paradigm.
            if (lemma.Length < 4 || !(lemma.EndsWith("ar", StringComparison.Ordinal) || lemma.EndsWith("er", StringComparison.Ordinal) || lemma.EndsWith("ir", StringComparison.Ordinal)))
            {
                continue;
            }

            // One pass over the entries of the lemma fills the present and preterite cells (the first entry of a cell wins, as LexiconConjugate would find it).
            Array.Clear(cells);
            var hasTense = false;
            foreach (var entry in lexicon.EntriesForLemma(lemma))
            {
                if (entry.Pos != LexiconSchema.Verb || entry.Features.Tense is null)
                {
                    continue;
                }

                hasTense = true;
                if (entry.Features.Mood is not (null or LexiconSchema.Indicative))
                {
                    continue;
                }

                var cell = CellIndex(tenses, numbers, persons, entry.Features.Tense, entry.Features.Number, entry.Features.Person);
                if (cell >= 0 && cells[cell] is null)
                {
                    cells[cell] = entry.Form;
                }
            }

            if (!hasTense)
            {
                continue;
            }

            for (var t = 0; t < tenses.Length; t++)
            {
                for (var n = 0; n < numbers.Length; n++)
                {
                    for (var p = 0; p < persons.Length; p++)
                    {
                        var actual = cells[(t * numbers.Length * persons.Length) + (n * persons.Length) + p];
                        if (actual is null)
                        {
                            continue;
                        }

                        var regular = ConjugateRegular(lemma, persons[p], numbers[n], tenses[t]);
                        if (regular is null || string.Equals(actual, regular, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        index.TryAdd(regular, new RegularizedForm(lemma, actual, persons[p], numbers[n], tenses[t]));
                    }
                }
            }
        }

        return index;
    }

    private static int CellIndex(string[] tenses, string[] numbers, string[] persons, string? tense, string? number, string? person)
    {
        var t = Array.IndexOf(tenses, tense);
        var n = Array.IndexOf(numbers, number);
        var p = Array.IndexOf(persons, person);
        return t < 0 || n < 0 || p < 0 ? -1 : (t * numbers.Length * persons.Length) + (n * persons.Length) + p;
    }
}
