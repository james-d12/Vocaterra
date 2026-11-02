using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Es;
using Vocaterra.Nlp.Es.Analysis;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using NlpLexicon = Vocaterra.Nlp.Language.Lexicon.Lexicon;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>A noun with both numbers in the lexicon, as the sentence generator needs it.</summary>
/// <param name="Lemma">The lemma (singular form).</param>
/// <param name="Gender">The lexicon gender, <c>m</c> or <c>f</c>.</param>
/// <param name="Singular">The singular surface form.</param>
/// <param name="Plural">The plural surface form (differs from the singular).</param>
/// <param name="AlsoVerb">True when a form of this noun also reads as a verb, finite (<c>lanza</c>) or the infinitive (<c>proceder</c>, <c>deber</c>).</param>
/// <param name="AlsoOtherGender">True when a form of this noun also reads as a noun of the other gender (<c>el</c> and <c>la estudiante</c>), so either article agrees with it and no gender mistake can be built on it.</param>
public sealed record NounLexeme(string Lemma, string Gender, string Singular, string Plural, bool AlsoVerb, bool AlsoOtherGender);

/// <summary>A verb whose six present cells (and optionally the six preterite cells) are all in the lexicon.</summary>
/// <param name="Lemma">The infinitive, which is also the lemma.</param>
/// <param name="HasPreterite">True when all six preterite cells are in the lexicon.</param>
public sealed record VerbLexeme(string Lemma, bool HasPreterite);

/// <summary>
/// The lexicon seen through the generators' eyes: forms by part of speech, nouns with both numbers, verbs with full paradigms and the
/// small closed classes the analyzer special-cases. The closed classes in this file mirror <c>WordClasses</c> and <c>AnalysisTables</c> of
/// <c>Vocaterra.Nlp.Language</c>, which are internal; if the analyzer's tables change, the sentence and mutation oracles fail and say which list drifted.
/// </summary>
public sealed class LexiconCatalog
{
    /// <summary>Adjectives that behave as determiners (mirror of <c>WordClasses.DeterminerAdjectives</c>, minus the adverb-like <c>cuánto</c>).</summary>
    public static readonly IReadOnlyList<string> DeterminerAdjectiveLemmas =
        ["mucho", "poco", "otro", "varios", "todo", "mi", "tu", "su", "nuestro", "vuestro", "este", "ese", "aquel"];

    /// <summary>Adjectives that normally precede the noun (mirror of <c>AnalysisTables.PrenominalAdjectives</c>; <c>cada</c> since M66).</summary>
    public static readonly IReadOnlyList<string> PrenominalLemmas = ["grande", "bueno", "malo", "cada"];

    // M62: the Spanish analysis rules, read through the public interface rather than mirrored: which adjectives go after their noun (the colours).
    private static readonly IAnalysisRules SpanishRules = AnalysisRules.For(SpanishLanguagePack.Instance);

    /// <summary>Adjectives that take <c>estar</c> (mirror of <c>AnalysisTables.EstarAdjectives</c>).</summary>
    public static readonly IReadOnlyList<string> EstarAdjectives =
        ["cansado", "enfermo", "contento", "triste", "ocupado", "preocupado", "enojado", "listo", "lleno", "vacío", "cerrado", "abierto"];

    /// <summary>Adjectives that take <c>ser</c> (mirror of <c>AnalysisTables.SerAdjectives</c>).</summary>
    public static readonly IReadOnlyList<string> SerAdjectives =
        ["alto", "bajo", "inteligente", "simpático", "amable", "guapo", "feo", "importante", "fácil", "difícil", "trabajador"];

    /// <summary>Adverbs that may stand directly before a noun (mirror of <c>AnalysisTables.NounModifyingAdverbs</c>).</summary>
    public static readonly IReadOnlyList<string> NounModifyingAdverbs =
        ["solo", "sólo", "también", "tampoco", "incluso", "casi", "apenas", "aun", "aún", "más", "menos", "hasta", "ni"];

    // The time markers are not mirrored: since M57 they come from the pack's public table SpanishTimeMarkers, which the checker reads too, and the realiser finds
    // their phrases with TimeMarkerModel.

    /// <summary>Adverbs that put a sentence in the past (<c>SpanishTimeMarkers.PastAdverbs</c>, the checker's own list).</summary>
    public static readonly IReadOnlyList<string> PastTimeAdverbs = SpanishTimeMarkers.PastAdverbs;

    /// <summary>The adverb that puts a sentence in the future (<c>SpanishTimeMarkers.FutureAdverb</c>); the generated adverbs leave it out, as they leave out the past ones.</summary>
    public static readonly IReadOnlyList<string> FutureTimeAdverbs = [SpanishTimeMarkers.FutureAdverb];

    /// <summary>Nouns that combine with <c>pasado</c> to mark past time and with <c>que viene</c> to mark the future (<c>SpanishTimeMarkers.TimeNouns</c>).</summary>
    public static readonly IReadOnlyList<string> TimeNouns = SpanishTimeMarkers.TimeNouns;

    /// <summary>Nouns after which <c>próximo/a</c> may come first (<c>SpanishTimeMarkers.UpcomingTimeNouns</c>, M49): "los próximos domingos" is correct Spanish, so it is no word-order mistake.</summary>
    public static readonly IReadOnlyList<string> UpcomingTimeNouns = SpanishTimeMarkers.UpcomingTimeNouns;

    /// <summary>Feminine nouns that take <c>el</c> and <c>un</c> (mirror of <c>WordClasses.FeminineNounsTakingEl</c>).</summary>
    public static readonly IReadOnlyList<string> FeminineNounsTakingEl =
        ["agua", "arma", "hacha", "águila", "alma", "aula", "hada", "hambre", "área", "ala"];

    /// <summary>Pronouns that can be the explicit subject of a verb (mirror of <c>WordClasses.SubjectPronouns</c>).</summary>
    public static readonly IReadOnlyList<string> SubjectPronounLemmas =
        ["yo", "tú", "él", "ella", "usted", "nosotros", "nosotras", "vosotros", "vosotras", "ellos", "ellas", "ustedes"];

    /// <summary>Object pronouns that may sit between a subject pronoun and its verb (mirror of <c>WordClasses.Clitics</c>).</summary>
    public static readonly IReadOnlyList<string> CliticLemmas = ["me", "te", "lo", "le", "nos", "les"];

    /// <summary>Verbs whose subject follows them after a dative pronoun (mirror of <c>SpanishAnalysisTables.ExperiencerVerbs</c>, M56: me gustan las manzanas).</summary>
    public static readonly IReadOnlyList<string> ExperiencerVerbs =
        ["gustar", "encantar", "interesar", "importar", "doler", "faltar", "quedar", "sobrar", "molestar", "apetecer", "fascinar", "agradar", "disgustar", "preocupar"];

    /// <summary>
    /// The verbs whose noun phrase after them the analyzer may check as their subject (M56): estar, the experiencer verbs and the verbs of price (mirror of
    /// <c>SpanishAgreementChecker.TakesPostverbalSubject</c>, which also asks for a dative pronoun, dónde or cuánto before some of them; the realiser agrees them always).
    /// </summary>
    public static readonly IReadOnlyList<string> PostverbalSubjectVerbs = ["estar", "costar", "valer", .. ExperiencerVerbs];

    /// <summary>The pronouns that stand before a verb, which the analyzer skips between a noun subject and its verb (mirror of <c>SpanishAnalysisTables.PreverbalPronouns</c>, M59).</summary>
    public static readonly IReadOnlyList<string> PreverbalPronounLemmas = ["me", "te", "se", "lo", "la", "los", "las", "le", "les", "nos", "os"];

    /// <summary>The verbs whose clitic after <c>cómo</c> is reflexive and agrees with them (mirror of <c>SpanishAnalysisTables.ReflexiveQuestionVerbs</c>, M59: ¿cómo te llamas?).</summary>
    public static readonly IReadOnlyList<string> ReflexiveQuestionVerbs = ["llamar", "sentir"];

    /// <summary>
    /// The experiencer verbs whose subject is the thing liked and never the one who likes (mirror of <c>SpanishAnalysisTables.LikingVerbs</c>, M70): behind a dative
    /// pronoun a form of the first or second person with a phrase after it is the learner's slip (me gusto el pan, gusta).
    /// </summary>
    public static readonly IReadOnlyList<string> LikingVerbs = ["gustar", "encantar"];

    /// <summary>The dative pronouns before an experiencer verb (mirror of <c>SpanishAnalysisTables.DativePronouns</c>, M56).</summary>
    public static readonly IReadOnlyList<string> DativePronouns = ["me", "te", "le", "nos", "os", "les"];

    /// <summary>
    /// The nouns whose phrase after a verb the analyzer may not take for its subject (M56): the nouns of time and duration (mirror of
    /// <c>SpanishAnalysisTables.IsDurationNoun</c>) and of part or quantity (mirror of <c>SpanishAnalysisTables.QuantityNouns</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> PostverbalNonSubjectNouns =
    [
        .. UpcomingTimeNouns, "tarde", "mañana", "rato", "momento", "mediodía", "medianoche", "minuto", "segundo", "tiempo", "temporada", "época",
        "mayoría", "minoría", "mitad", "resto", "parte", "grupo", "cantidad", "montón", "número", "totalidad", "serie", "conjunto", "docena", "par", "tercio",
    ];

    /// <summary>
    /// The words after which a phrase opens its clause (mirror of <c>SpanishAnalysisTables.SubjectClauseOpeners</c>, M59), less those the analyzer may read past as an
    /// adverb: the realiser models a coordinated subject (M64) only where its first member surely opens its clause.
    /// </summary>
    public static readonly IReadOnlyList<string> SubjectClauseOpeners = ["y", "e", "pero", "porque", "si", "cuando", "aunque"];

    /// <summary>
    /// The question words, as written, that may stand between a subject pronoun and its verb (mirror of <c>SpanishAnalysisTables.QuestionWordsAfterSubject</c>,
    /// M66): the pronoun before one is the subject of the verb after it (<c>ella qué quiere</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> QuestionWordsAfterSubject = ["qué", "dónde", "adónde", "cuándo", "cómo"];

    /// <summary>
    /// The question words and relatives, as written with and without the accent, that open a clause whose subject pronoun may follow its verb (mirror of
    /// <c>SpanishAnalysisTables.ClauseOpeners</c> less <c>porque</c> and <c>si</c>, M69): the analyzer agrees the verb with a subject pronoun right after it
    /// (<c>¿dónde estás tú?</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> InvertingClauseOpeners =
    [
        "que", "qué", "dónde", "donde", "adónde", "adonde", "cuándo", "cuando", "cómo", "como", "cuánto", "cuanto", "cuánta", "cuanta", "cuántos", "cuantos", "cuántas",
        "cuantas", "quién", "quien", "quiénes", "quienes", "cuál", "cual", "cuáles", "cuales",
    ];

    /// <summary>
    /// The words that say how much a sum is, as written (mirror of <c>SpanishAnalysisTables.AmountPredicates</c>, M64): after an amount and a singular <c>ser</c>
    /// (<c>dos panes es suficiente</c>) the analyzer reads the amount as one sum.
    /// </summary>
    public static readonly IReadOnlyList<string> AmountPredicates = ["mucho", "poco", "bastante", "demasiado", "suficiente", "caro", "barato", "nada", "algo"];

    /// <summary>
    /// Nouns of events, whose place takes <c>ser</c> (mirror of <c>SpanishAnalysisTables.EventNouns</c>, M56 and M69): the <c>ser_estar</c> mutation of M69 makes no
    /// <c>es en</c> after a subject of one (<c>la fiesta es en la plaza</c> is correct).
    /// </summary>
    public static readonly IReadOnlyList<string> EventNouns =
    [
        "fiesta", "reunión", "boda", "concierto", "clase", "lección", "curso", "examen", "partido", "partida", "juego", "carrera", "competición", "concurso",
        "torneo", "cena", "comida", "almuerzo", "desayuno", "merienda", "banquete", "cumpleaños", "festival", "feria", "mercadillo", "misa", "funeral",
        "entierro", "ceremonia", "celebración", "espectáculo", "función", "obra", "película", "exposición", "conferencia", "congreso", "cita", "encuentro",
        "evento", "acto", "entrevista", "baile", "desfile", "procesión", "carnaval", "salida", "llegada", "entrega", "visita", "excursión", "viaje", "sesión",
        "votación", "inauguración", "graduación", "verbena", "romería", "ensayo", "actuación", "taller", "charla", "prueba", "subasta", "sorteo", "batalla", "pelea",
        "lucha", "accidente",
    ];

    /// <summary>
    /// Nouns that are no place after <c>en</c> (M69, mirror of <c>SpanishAnalysisTables.IsDurationNoun</c> and <c>AdverbialPhraseNouns</c>): <c>ser</c> before
    /// <c>en</c> and one of them is not checked (<c>la clase es en la tarde</c>, <c>es en el fondo bueno</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> NonPlaceNouns =
    [
        .. UpcomingTimeNouns, "tarde", "mañana", "rato", "momento", "mediodía", "medianoche", "minuto", "segundo", "tiempo", "temporada", "época",
        "fondo", "práctica", "actualidad", "medida", "sentido", "caso", "realidad", "principio", "teoría", "verdad", "general", "parte", "total", "cambio", "forma",
        "manera", "modo", "palabra", "seguida", "absoluto", "punto",
    ];

    /// <summary>Adverbs of place that take <c>estar</c>, so <c>ser</c> before one is a mistake (mirror of <c>SpanishAnalysisTables.LocationAdverbs</c>, M56).</summary>
    public static readonly IReadOnlyList<string> LocationAdverbs = ["lejos", "cerca"];

    /// <summary>The question words after which the analyzer reads a word that can be a finite verb as the verb (mirror of <c>SpanishAnalysisTables.VerbSeekingQuestionWords</c>, M56).</summary>
    public static readonly IReadOnlyList<string> VerbSeekingQuestionWords = ["dónde", "donde", "cómo"];

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
            .Where(f => !PastTimeAdverbs.Contains(f) && !FutureTimeAdverbs.Contains(f) && !f.Contains(' ', StringComparison.Ordinal) && Stable(f) && OnlyReadings(f, LexiconSchema.Adverb, LexiconSchema.Interjection))
            .ToArray();
        Prepositions = FormsOf(LexiconSchema.Preposition).Where(f => Stable(f) && OnlyReadings(f, LexiconSchema.Preposition)).ToArray();
        Conjunctions = FormsOf(LexiconSchema.Conjunction).Where(f => Stable(f) && OnlyReadings(f, LexiconSchema.Conjunction)).ToArray();
        Interjections = FormsOf(LexiconSchema.Interjection)
            .Where(f => !f.Contains(' ', StringComparison.Ordinal) && Stable(f) && OnlyReadings(f, LexiconSchema.Adverb, LexiconSchema.Interjection))
            .ToArray();
        SubjectPronouns = SubjectPronounLemmas.Where(p => lexicon.Lookup(p).Count > 0).ToArray();
        Clitics = CliticLemmas.Where(p => lexicon.Lookup(p).Count > 0 && OnlyReadings(p, LexiconSchema.Pronoun)).ToArray();

        // Indexes for the generators' lookups; with thousands of lemmas a linear scan per sentence is what the suite would spend its time on.
        DescriptiveAdjectiveSet = new HashSet<string>(DescriptiveAdjectives, StringComparer.Ordinal);
        PlainNouns = Nouns.Where(static n => !n.AlsoVerb).ToArray();
        _nounByLemma = Nouns.ToDictionary(static n => n.Lemma, StringComparer.Ordinal);
        _verbByLemma = Verbs.ToDictionary(static v => v.Lemma, StringComparer.Ordinal);
        _verbByFoldedLemma = new Dictionary<string, VerbLexeme>(StringComparer.Ordinal);
        foreach (var verb in Verbs)
        {
            _verbByFoldedLemma.TryAdd(Accents.Strip(verb.Lemma), verb);
        }

        SerClassAdjectives = SerAdjectives.Where(DescriptiveAdjectiveSet.Contains).ToArray();
        EstarClassAdjectives = EstarAdjectives.Where(DescriptiveAdjectiveSet.Contains).ToArray();
        NeutralAdjectives = DescriptiveAdjectives.Where(l => !SerClassAdjectives.Contains(l) && !EstarClassAdjectives.Contains(l)).ToArray();
        _strippedByLength = forms.Select(Accents.Strip).Distinct(StringComparer.Ordinal).GroupBy(static f => f.Length)
            .ToDictionary(static g => g.Key, static g => g.Select(static f => (f, Histogram(f))).ToArray());

        _diagnostics["catalog:lexicon-entries"] = lexicon.Entries.Count;
        _diagnostics["catalog:lexicon-lemmas"] = lexicon.Lemmas.Count;
        _diagnostics["catalog:forms"] = AllForms.Count;
        _diagnostics["verbs:usable-with-a-full-present-paradigm"] = Verbs.Count;
        _diagnostics["verbs:usable-with-a-full-preterite-paradigm"] = Verbs.Count(static v => v.HasPreterite);
        _diagnostics["adjectives:usable-descriptive"] = DescriptiveAdjectives.Count;
        _diagnostics["adverbs:usable"] = Adverbs.Count;
        _diagnostics["prepositions:usable"] = Prepositions.Count;
        _diagnostics["conjunctions:usable"] = Conjunctions.Count;
        _diagnostics["interjections:usable"] = Interjections.Count;
    }

    /// <summary>
    /// What the generators can use of the lexicon, and what they leave out because of homographs: counts by reason, in ordinal key order. The words that do
    /// not make it into the closed lists above are exactly the ones the analyzer would misread (nouns that are also articles, verbs, other nouns).
    /// </summary>
    public IReadOnlyDictionary<string, int> Diagnostics => _diagnostics;

    private readonly SortedDictionary<string, int> _diagnostics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NounLexeme> _nounByLemma;
    private readonly Dictionary<string, VerbLexeme> _verbByLemma;
    private readonly Dictionary<string, VerbLexeme> _verbByFoldedLemma;
    private readonly Dictionary<int, (string Form, byte[] Histogram)[]> _strippedByLength;

    /// <summary>The descriptive adjective lemmas as a set.</summary>
    public IReadOnlySet<string> DescriptiveAdjectiveSet { get; }

    /// <summary>The nouns that are not also finite verb forms.</summary>
    public IReadOnlyList<NounLexeme> PlainNouns { get; }

    /// <summary>The descriptive adjectives that take <c>ser</c>.</summary>
    public IReadOnlyList<string> SerClassAdjectives { get; }

    /// <summary>The descriptive adjectives that take <c>estar</c>.</summary>
    public IReadOnlyList<string> EstarClassAdjectives { get; }

    /// <summary>The descriptive adjectives that are in neither class.</summary>
    public IReadOnlyList<string> NeutralAdjectives { get; }

    /// <summary>Returns the noun with this lemma, or null.</summary>
    public NounLexeme? NounOf(string? lemma)
    {
        return lemma is null ? null : _nounByLemma.GetValueOrDefault(lemma);
    }

    /// <summary>Returns the verb with this lemma, or null.</summary>
    public VerbLexeme? VerbOf(string lemma)
    {
        return _verbByLemma.GetValueOrDefault(lemma);
    }

    /// <summary>Returns the verb whose lemma equals the given one once accents are ignored, or null.</summary>
    public VerbLexeme? VerbOfIgnoringAccents(string lemma)
    {
        return _verbByFoldedLemma.GetValueOrDefault(Accents.Strip(lemma));
    }

    /// <summary>True when every lexicon form, accents ignored, is at least <paramref name="minDistance"/> Damerau-Levenshtein edits from the word.</summary>
    public bool IsFarFromEveryForm(string word, int minDistance)
    {
        var wordHistogram = Histogram(word);
        // The distance is at least the difference in length, so only forms of a similar length need to be compared.
        for (var length = word.Length - (minDistance - 1); length <= word.Length + (minDistance - 1); length++)
        {
            if (!_strippedByLength.TryGetValue(length, out var candidates))
            {
                continue;
            }

            foreach (var (form, histogram) in candidates)
            {
                // One edit changes the letter counts by at most two in total (a transposition by none), so words whose counts differ by
                // 2 * minDistance or more cannot be closer than minDistance: the full comparison is only needed for the few similar words.
                if (HistogramDistance(histogram, wordHistogram) < 2 * minDistance && DamerauLevenshtein.Distance(form, word) < minDistance)
                {
                    return false;
                }
            }
        }

        return true;
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

    /// <summary>Adverbs without the past-time and future-time markers and without spaces, whose every reading is an adverb or interjection (the analyzer skips them between a subject and its verb).</summary>
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

        return compatible && Stable(form) ? form : null;
    }

    /// <summary>True when the form has at least one reading and every reading has one of the given parts of speech.</summary>
    public bool OnlyReadings(string form, params string[] partsOfSpeech)
    {
        var readings = Lexicon.Lookup(form);
        return readings.Count > 0 && readings.All(r => partsOfSpeech.Contains(r.Pos));
    }

    /// <summary>
    /// True when the normalizer leaves the word as it is. The normalizer turns number words into digits (<c>cero</c>, <c>mil</c>, <c>diez</c>, <c>doce</c> are
    /// nouns in the imported lexicon too), so a sentence built from such a word would not normalize back to the words that were realised; the generators
    /// leave those words to the numeral slots.
    /// </summary>
    public static bool Stable(string form)
    {
        var tokens = PipelineHarness.Normalize(form).Tokens;
        return tokens.Count == 1 && string.Equals(tokens[0].Text, form, StringComparison.Ordinal);
    }

    private static byte[] Histogram(string word)
    {
        var counts = new byte[28];
        foreach (var c in word)
        {
            var index = c is >= 'a' and <= 'z' ? c - 'a' : c == 'ñ' ? 26 : 27;
            if (counts[index] < byte.MaxValue)
            {
                counts[index]++;
            }
        }

        return counts;
    }

    private static int HistogramDistance(byte[] left, byte[] right)
    {
        var total = 0;
        for (var i = 0; i < left.Length; i++)
        {
            total += Math.Abs(left[i] - right[i]);
        }

        return total;
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

    /// <summary>True when the surface form has at least one verb reading, finite or not (the infinitive of <c>proceder</c> is also a noun).</summary>
    public bool HasVerbReading(string form)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Verb)
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
        // The singular entries of each noun lemma, in the order the lemmas first appear. A lemma can have an entry per gender (abogada, abogado), so the gender of
        // the lexeme is the one of the entry whose form is the singular the generator will write; only when no entry has that form does the first entry decide.
        var order = new List<string>();
        var entriesByLemma = new Dictionary<string, List<LexiconEntry>>(StringComparer.Ordinal);
        foreach (var entry in Lexicon.Entries)
        {
            if (entry.Pos != LexiconSchema.Noun
                || entry.Features.Number != LexiconSchema.Singular
                || entry.Features.Gender is null
                || entry.Form.Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            if (!entriesByLemma.TryGetValue(entry.Lemma, out var forLemma))
            {
                forLemma = [];
                entriesByLemma.Add(entry.Lemma, forLemma);
                order.Add(entry.Lemma);
            }

            forLemma.Add(entry);
        }

        var result = new List<NounLexeme>();
        foreach (var lemma in order)
        {
            var plural = Morphology.InflectNoun(lemma, LexiconSchema.Plural);
            var singular = Morphology.InflectNoun(lemma, LexiconSchema.Singular);
            var candidates = entriesByLemma[lemma];
            var entry = candidates.Find(e => string.Equals(e.Form, singular, StringComparison.Ordinal)) ?? candidates[0];
            var excluded = NounExclusion(entry, singular, plural);
            if (excluded is not null)
            {
                Count(excluded);
                continue;
            }

            var alsoVerb = HasVerbReading(singular) || HasVerbReading(plural);
            var alsoOtherGender = HasNounOfOtherGender(singular, entry.Features.Gender!) || HasNounOfOtherGender(plural, entry.Features.Gender!);
            Count(alsoVerb ? "nouns:usable-homograph-with-a-verb" : "nouns:usable-plain");
            if (alsoOtherGender)
            {
                Count("nouns:usable-homograph-with-the-other-gender");
            }

            result.Add(new NounLexeme(lemma, entry.Features.Gender!, singular, plural, alsoVerb, alsoOtherGender));
        }

        return result;
    }

    private void Count(string key)
    {
        _diagnostics[key] = _diagnostics.GetValueOrDefault(key) + 1;
    }

    // The first reason a noun lemma cannot be used to build a sentence, or null. A noun that is also a verb form, or also a noun of the other gender, is not among
    // them: the analyzer resolves the part of speech of each word (M18), so those nouns are used, and the two flags of NounLexeme tell the mutation generator which
    // mistakes it cannot build on them. What remains are words the sentence generator cannot spell correctly: no distinct plural, a form the normalizer rewrites,
    // a spelling that two noun lemmas share, and one that reads as an article, numeral or determiner. Each reason is counted in the diagnostics.
    private string? NounExclusion(LexiconEntry entry, string singular, string plural)
    {
        if (string.Equals(plural, singular, StringComparison.Ordinal))
        {
            return "nouns:excluded-no-distinct-plural";
        }

        if (ReadsAsDeterminer(singular) || ReadsAsDeterminer(plural))
        {
            return "nouns:excluded-reads-as-article-numeral-or-determiner";
        }

        if (!Stable(singular) || !Stable(plural))
        {
            return "nouns:excluded-rewritten-by-the-normalizer";
        }

        if (!HasNounReading(singular, LexiconSchema.Singular, entry.Features.Gender!) || !HasNounReading(plural, LexiconSchema.Plural, entry.Features.Gender!))
        {
            return "nouns:excluded-no-reading-for-the-form";
        }

        if (!OneNounLemma(singular, entry.Lemma) || !OneNounLemma(plural, entry.Lemma))
        {
            return "nouns:excluded-form-shared-with-another-noun-lemma";
        }

        // M58: a form that is also the regular cell of an irregular verb (costa for cuesta, the stem change missed; cabo, jugo, mente) is read by the analyzer as
        // that verb, with conj.irregular, wherever a verb is expected (after a subject pronoun or a clitic, after cuánto, after the noun of a phrase that opens the
        // sentence), and as the noun elsewhere. The generators place nouns by the pattern, not by those rules, so such a noun is not drawn (four lemmas in the
        // bundle's lexicon).
        if (Morphology.TryGetRegularizationOfWord(singular, out _) || Morphology.TryGetRegularizationOfWord(plural, out _))
        {
            return "nouns:excluded-also-a-learners-slip-of-a-verb";
        }

        return null;
    }

    // A noun spelled like an article, a numeral or a determiner adjective (poca, "a bit", next to poco) is read as the determiner by the analyzer, which is a
    // property of the lexicon (the word is a homograph), so the generators never build a noun phrase around it.
    private bool ReadsAsDeterminer(string form)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos is LexiconSchema.Article or LexiconSchema.Numeral
                || (reading.Pos == LexiconSchema.Adjective && DeterminerAdjectiveLemmas.Contains(reading.Lemma)))
            {
                return true;
            }
        }

        return false;
    }

    // A form that two noun lemmas share (ratos: rato, and rata in its masculine sense) has no single correction, so the analyzer's expected form depends on which
    // reading it takes first; the generators leave those nouns to the homograph report.
    private bool OneNounLemma(string form, string lemma)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Noun && !string.Equals(reading.Lemma, lemma, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    // True when the form has a noun reading of this number and gender. A noun that also has readings of the other gender (el estudiante, la estudiante) still counts:
    // the generator spells the form for the gender of its lexeme and the analyzer finds the article compatible either way.
    private bool HasNounReading(string form, string number, string gender)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Noun && reading.Number == number && reading.Gender == gender)
            {
                return true;
            }
        }

        return false;
    }

    // A noun with a reading of the other gender (el estudiante, la estudiante) agrees with both articles, so it cannot be used to build a gender mistake.
    private bool HasNounOfOtherGender(string form, string gender)
    {
        foreach (var reading in Lexicon.Lookup(form))
        {
            if (reading.Pos == LexiconSchema.Noun && reading.Gender is not null && reading.Gender != gender)
            {
                return true;
            }
        }

        return false;
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
                if (form is null || !Stable(form) || !HasVerbReading(form, lemma, person, number, tense))
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

    /// <summary>
    /// True for an adjective that goes after its noun (M62: the colours, <see cref="IAnalysisRules.IsPostnominalAdjective"/> of the Spanish pack): the reading resolver
    /// reads a word that is such an adjective and a noun as the adjective right before a word that can only be a noun.
    /// </summary>
    public static bool IsPostnominalAdjective(string lemma)
    {
        return SpanishRules.IsPostnominalAdjective(lemma);
    }

    /// <summary>
    /// True for an adverb of degree (M62: <c>muy</c>, <c>tan</c>; <see cref="IAnalysisRules.IsDegreeAdverb"/> of the Spanish pack): the reading resolver reads the word after it
    /// as no noun.
    /// </summary>
    public static bool IsDegreeAdverb(string lemma)
    {
        return SpanishRules.IsDegreeAdverb(lemma);
    }

    /// <summary>
    /// True for a verb that takes an infinitive right after it, or after <paramref name="linker"/> (M66: <c>poder</c>, <c>querer</c>, <c>deber</c>, <c>necesitar</c>;
    /// <c>tener que</c>; <see cref="IAnalysisRules.IsModalVerb"/> of the Spanish pack): a finite verb there is the learner's slip for the infinitive.
    /// </summary>
    public static bool IsModalVerb(string lemma, string? linker)
    {
        return SpanishRules.IsModalVerb(lemma, linker);
    }

    /// <summary>True for a modal that may also take a noun as its object (M66: <c>querer</c>, <c>deber</c>, <c>necesitar</c>; <see cref="IAnalysisRules.ModalTakesNounObject"/>).</summary>
    public static bool ModalTakesNounObject(string lemma)
    {
        return SpanishRules.ModalTakesNounObject(lemma);
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
