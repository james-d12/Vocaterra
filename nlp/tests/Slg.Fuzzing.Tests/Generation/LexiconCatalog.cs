using Slg.Core.Stages;
using Slg.Nlp.Lexicon;
using Slg.Nlp.Morphology;
using Slg.Nlp.Normalization;
using NlpLexicon = Slg.Nlp.Lexicon.Lexicon;

namespace Slg.Fuzzing.Tests.Generation;

/// <summary>A noun with both numbers in the lexicon, as the sentence generator needs it.</summary>
/// <param name="Lemma">The lemma (singular form).</param>
/// <param name="Gender">The lexicon gender, <c>m</c> or <c>f</c>.</param>
/// <param name="Singular">The singular surface form.</param>
/// <param name="Plural">The plural surface form (differs from the singular).</param>
/// <param name="AlsoFiniteVerb">True when a form of this noun also reads as a finite verb (for example <c>lanza</c>).</param>
public sealed record NounLexeme(string Lemma, string Gender, string Singular, string Plural, bool AlsoFiniteVerb);

/// <summary>A verb whose six present cells (and optionally the six preterite cells) are all in the lexicon.</summary>
/// <param name="Lemma">The infinitive, which is also the lemma.</param>
/// <param name="HasPreterite">True when all six preterite cells are in the lexicon.</param>
public sealed record VerbLexeme(string Lemma, bool HasPreterite);

/// <summary>
/// The lexicon seen through the generators' eyes: forms by part of speech, nouns with both numbers, verbs with full paradigms and the
/// small closed classes the analyzer special-cases. The closed classes in this file mirror <c>WordClasses</c> and <c>AnalysisTables</c> of
/// <c>Slg.Nlp</c>, which are internal; if the analyzer's tables change, the sentence and mutation oracles fail and say which list drifted.
/// </summary>
public sealed class LexiconCatalog
{
    /// <summary>Adjectives that behave as determiners (mirror of <c>WordClasses.DeterminerAdjectives</c>, minus the adverb-like <c>cuánto</c>).</summary>
    public static readonly IReadOnlyList<string> DeterminerAdjectiveLemmas =
        ["mucho", "poco", "otro", "varios", "todo", "mi", "tu", "su", "nuestro", "vuestro", "este", "ese", "aquel"];

    /// <summary>Adjectives that normally precede the noun (mirror of <c>AnalysisTables.PrenominalAdjectives</c>).</summary>
    public static readonly IReadOnlyList<string> PrenominalLemmas = ["grande", "bueno", "malo"];

    /// <summary>Adjectives that take <c>estar</c> (mirror of <c>AnalysisTables.EstarAdjectives</c>).</summary>
    public static readonly IReadOnlyList<string> EstarAdjectives =
        ["cansado", "enfermo", "contento", "triste", "ocupado", "preocupado", "enojado", "listo", "lleno", "vacío", "cerrado", "abierto"];

    /// <summary>Adjectives that take <c>ser</c> (mirror of <c>AnalysisTables.SerAdjectives</c>).</summary>
    public static readonly IReadOnlyList<string> SerAdjectives =
        ["alto", "bajo", "inteligente", "simpático", "amable", "guapo", "feo", "importante", "fácil", "difícil", "trabajador"];

    /// <summary>Adverbs that put a sentence in the past (mirror of <c>AnalysisTables.PastTimeAdverbs</c>).</summary>
    public static readonly IReadOnlyList<string> PastTimeAdverbs = ["ayer", "anoche", "anteayer"];

    /// <summary>Nouns that combine with <c>pasado</c> to mark past time (mirror of <c>AnalysisTables.TimeNouns</c>).</summary>
    public static readonly IReadOnlyList<string> TimeNouns =
        ["semana", "mes", "año", "día", "noche", "vez", "hora", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo"];

    /// <summary>Feminine nouns that take <c>el</c> and <c>un</c> (mirror of <c>WordClasses.FeminineNounsTakingEl</c>).</summary>
    public static readonly IReadOnlyList<string> FeminineNounsTakingEl =
        ["agua", "arma", "hacha", "águila", "alma", "aula", "hada", "hambre", "área", "ala"];

    /// <summary>Pronouns that can be the explicit subject of a verb (mirror of <c>WordClasses.SubjectPronouns</c>).</summary>
    public static readonly IReadOnlyList<string> SubjectPronounLemmas =
        ["yo", "tú", "él", "ella", "usted", "nosotros", "nosotras", "vosotros", "vosotras", "ellos", "ellas", "ustedes"];

    /// <summary>Object pronouns that may sit between a subject pronoun and its verb (mirror of <c>WordClasses.Clitics</c>).</summary>
    public static readonly IReadOnlyList<string> CliticLemmas = ["me", "te", "lo", "le", "nos", "les"];

    private static readonly string[] Genders = [LexiconSchema.Masculine, LexiconSchema.Feminine];
    private static readonly string[] Numbers = [LexiconSchema.Singular, LexiconSchema.Plural];

    /// <summary>Builds the catalog from a lexicon and a morphology generator over it.</summary>
    public LexiconCatalog(NlpLexicon lexicon, MorphologyGenerator morphology)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        Lexicon = lexicon;
        Morphology = morphology;

        var forms = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in lexicon.Entries)
        {
            forms.Add(entry.Form);
        }

        AllForms = [.. forms];
        FoldedForms = new HashSet<string>(forms.Select(Accents.Strip), StringComparer.Ordinal);
        Nouns = BuildNouns();
        Verbs = BuildVerbs();
        DescriptiveAdjectives = BuildAdjectives();
        Adverbs = FormsOf(LexiconSchema.Adverb)
            .Where(f => !PastTimeAdverbs.Contains(f) && !f.Contains(' ', StringComparison.Ordinal) && OnlyReadings(f, LexiconSchema.Adverb, LexiconSchema.Interjection))
            .ToArray();
        Prepositions = FormsOf(LexiconSchema.Preposition).Where(f => OnlyReadings(f, LexiconSchema.Preposition)).ToArray();
        Conjunctions = FormsOf(LexiconSchema.Conjunction).Where(f => OnlyReadings(f, LexiconSchema.Conjunction)).ToArray();
        Interjections = FormsOf(LexiconSchema.Interjection)
            .Where(f => !f.Contains(' ', StringComparison.Ordinal) && OnlyReadings(f, LexiconSchema.Adverb, LexiconSchema.Interjection))
            .ToArray();
        SubjectPronouns = SubjectPronounLemmas.Where(p => lexicon.Lookup(p).Count > 0).ToArray();
        Clitics = CliticLemmas.Where(p => lexicon.Lookup(p).Count > 0 && OnlyReadings(p, LexiconSchema.Pronoun)).ToArray();
    }

    /// <summary>The lexicon the catalog was built from.</summary>
    public NlpLexicon Lexicon { get; }

    /// <summary>The morphology generator over <see cref="Lexicon"/>.</summary>
    public MorphologyGenerator Morphology { get; }

    /// <summary>Every distinct surface form of the lexicon, ordinal-sorted.</summary>
    public IReadOnlyList<string> AllForms { get; }

    /// <summary>Every surface form with accents stripped; a word in this set is known to the analyzer even when mis-accented.</summary>
    public IReadOnlySet<string> FoldedForms { get; }

    /// <summary>Nouns with distinct singular and plural forms and a lexicon gender.</summary>
    public IReadOnlyList<NounLexeme> Nouns { get; }

    /// <summary>Verbs with a complete present paradigm and an infinitive entry.</summary>
    public IReadOnlyList<VerbLexeme> Verbs { get; }

    /// <summary>Descriptive adjectives (no determiners) that have a lexicon form for every gender and number.</summary>
    public IReadOnlyList<string> DescriptiveAdjectives { get; }

    /// <summary>Adverbs without the past-time markers and without spaces, whose every reading is an adverb or interjection (the analyzer skips them between a subject and its verb).</summary>
    public IReadOnlyList<string> Adverbs { get; }

    /// <summary>Prepositions whose every reading is a preposition.</summary>
    public IReadOnlyList<string> Prepositions { get; }

    /// <summary>Conjunctions whose every reading is a conjunction (the word <c>como</c> is also a verb form and is left out).</summary>
    public IReadOnlyList<string> Conjunctions { get; }

    /// <summary>Single-word interjections whose every reading is an adverb or interjection.</summary>
    public IReadOnlyList<string> Interjections { get; }

    /// <summary>Subject pronouns present in the lexicon.</summary>
    public IReadOnlyList<string> SubjectPronouns { get; }

    /// <summary>Clitic pronouns present in the lexicon.</summary>
    public IReadOnlyList<string> Clitics { get; }

    /// <summary>
    /// Returns the form of an adjective lemma for one gender and number, or null when the lexicon lacks a compatible form or the form also
    /// reads as a noun or finite verb (the analyzer would then not treat it as a plain adjunct).
    /// </summary>
    public string? AdjectiveForm(string lemma, string gender, string number)
    {
        var form = Morphology.AgreeAdjective(lemma, gender, number);
        var compatible = false;
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Noun || (reading.Pos == LexiconSchema.Verb && reading.Person is not null))
            {
                return null;
            }

            compatible |= reading.Pos == LexiconSchema.Adjective
                && (reading.Gender is null || reading.Gender == gender)
                && (reading.Number is null || reading.Number == number);
        }

        return compatible ? form : null;
    }

    /// <summary>True when the form has at least one reading and every reading has one of the given parts of speech.</summary>
    public bool OnlyReadings(string form, params string[] partsOfSpeech)
    {
        var readings = Lexicon.Lookup(form);
        return readings.Count > 0 && readings.All(r => partsOfSpeech.Contains(r.Pos));
    }

    /// <summary>Returns the lexicon forms of the determiner adjectives that agree with a gender and number (mi, este, otros, ...).</summary>
    public IReadOnlyList<string> DeterminerAdjectiveForms(string gender, string number)
    {
        var forms = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var lemma in DeterminerAdjectiveLemmas)
        {
            foreach (var entry in Lexicon.EntriesForLemma(lemma))
            {
                if (entry.Pos == LexiconSchema.Adjective
                    && (entry.Features.Gender is null || entry.Features.Gender == gender)
                    && (entry.Features.Number is null || entry.Features.Number == number))
                {
                    forms.Add(entry.Form);
                }
            }
        }

        return [.. forms];
    }

    /// <summary>Returns the pattern tags a literal word also matches according to its lexicon readings (<c>NOUN</c>, <c>ADJ</c>, <c>DET</c>, ...).</summary>
    public IEnumerable<string> PatternClassesOf(string word)
    {
        foreach (var reading in Lexicon.Lookup(word))
        {
            switch (reading.Pos)
            {
                case LexiconSchema.Noun:
                    yield return "NOUN";
                    break;
                case LexiconSchema.Verb:
                    yield return "VERB";
                    break;
                case LexiconSchema.Adjective:
                    yield return "ADJ";
                    if (DeterminerAdjectiveLemmas.Contains(reading.Lemma))
                    {
                        yield return "DET";
                    }

                    break;
                case LexiconSchema.Article:
                    yield return "ART";
                    yield return "NUM";
                    yield return "DET";
                    break;
                case LexiconSchema.Numeral:
                    yield return "NUM";
                    yield return "ART";
                    break;
                case LexiconSchema.Pronoun:
                    yield return "PRON";
                    break;
                case LexiconSchema.Adverb:
                    yield return "ADV";
                    break;
                case LexiconSchema.Preposition:
                    yield return "PREP";
                    break;
                case LexiconSchema.Conjunction:
                    yield return "CONJ";
                    break;
                default:
                    yield return "INTERJ";
                    break;
            }
        }
    }

    /// <summary>True when the surface form has at least one finite verb reading.</summary>
    public bool HasFiniteVerbReading(string form)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Verb && reading.Person is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the first reading of a surface form that has the given part of speech, or null.</summary>
    public LexicalFeatures? FirstReading(string form, string pos)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == pos)
            {
                return reading;
            }
        }

        return null;
    }

    private string[] FormsOf(string pos)
    {
        return Lexicon.Entries.Where(e => e.Pos == pos).Select(static e => e.Form).Distinct(StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<NounLexeme> BuildNouns()
    {
        var result = new List<NounLexeme>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Lexicon.Entries)
        {
            if (entry.Pos != LexiconSchema.Noun
                || entry.Features.Number != LexiconSchema.Singular
                || entry.Features.Gender is null
                || entry.Form.Contains(' ', StringComparison.Ordinal)
                || !seen.Add(entry.Lemma))
            {
                continue;
            }

            var plural = Morphology.InflectNoun(entry.Lemma, LexiconSchema.Plural);
            var singular = Morphology.InflectNoun(entry.Lemma, LexiconSchema.Singular);
            if (string.Equals(plural, singular, StringComparison.Ordinal)
                || !HasNounReading(singular, LexiconSchema.Singular, entry.Features.Gender)
                || !HasNounReading(plural, LexiconSchema.Plural, entry.Features.Gender))
            {
                continue;
            }

            result.Add(new NounLexeme(entry.Lemma, entry.Features.Gender, singular, plural, HasFiniteVerbReading(singular) || HasFiniteVerbReading(plural)));
        }

        return result;
    }

    // A noun with a reading of the other gender (el estudiante, la estudiante) agrees with both articles, so it cannot be used to build a gender mistake.
    private bool HasNounReading(string form, string number, string gender)
    {
        var found = false;
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos != LexiconSchema.Noun)
            {
                continue;
            }

            if (reading.Gender != gender)
            {
                return false;
            }

            found |= reading.Number == number;
        }

        return found;
    }

    private IReadOnlyList<VerbLexeme> BuildVerbs()
    {
        var result = new List<VerbLexeme>();
        foreach (var lemma in Lexicon.Lemmas)
        {
            var hasInfinitive = Lexicon.EntriesForLemma(lemma).Any(static e => e.Pos == LexiconSchema.Verb && e.Features.Person is null && e.Features.Tense is null && e.Features.Mood is null);
            if (!hasInfinitive || !HasParadigm(lemma, LexiconSchema.Present))
            {
                continue;
            }

            result.Add(new VerbLexeme(lemma, HasParadigm(lemma, LexiconSchema.Preterite)));
        }

        return result;
    }

    private bool HasParadigm(string lemma, string tense)
    {
        foreach (var number in Numbers)
        {
            foreach (var person in new[] { "1", "2", "3" })
            {
                var form = Morphology.Conjugate(lemma, person, number, tense);
                if (form is null || !HasVerbReading(form, lemma, person, number, tense))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasVerbReading(string form, string lemma, string person, string number, string tense)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Verb && reading.Lemma == lemma && reading.Person == person && reading.Number == number && reading.Tense == tense)
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<string> BuildAdjectives()
    {
        var result = new List<string>();
        foreach (var lemma in Lexicon.Lemmas)
        {
            var isAdjective = Lexicon.EntriesForLemma(lemma).Any(static e => e.Pos == LexiconSchema.Adjective);
            var isNounOrVerb = Lexicon.EntriesForLemma(lemma).Any(static e => e.Pos is LexiconSchema.Noun or LexiconSchema.Verb);
            if (!isAdjective || isNounOrVerb || DeterminerAdjectiveLemmas.Contains(lemma) || lemma is "pasado" or "cuánto")
            {
                continue;
            }

            var complete = Genders.All(g => Numbers.All(n => AdjectiveForm(lemma, g, n) is not null));
            if (complete)
            {
                result.Add(lemma);
            }
        }

        return result;
    }
}
