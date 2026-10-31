using System.Collections.Frozen;

using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.Es.Analysis;

/// <summary>Small hand-written Spanish word tables used by the Spanish analysis rules (M34: moved into the Spanish pack; the shared analysis reaches them through <see cref="IAnalysisRules"/>).</summary>
internal static class SpanishAnalysisTables
{
    private static readonly FrozenSet<string> EstarAdjectives = new[]
    {
        "cansado", "enfermo", "contento", "triste", "ocupado", "preocupado", "enojado", "listo", "lleno", "vacío", "cerrado", "abierto",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SerAdjectives = new[]
    {
        "alto", "bajo", "inteligente", "simpático", "amable", "guapo", "feo", "importante", "fácil", "difícil", "trabajador",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PrenominalAdjectives = new[] { "grande", "bueno", "malo" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NounModifyingAdverbs = new[]
    {
        "solo", "sólo", "también", "tampoco", "incluso", "casi", "apenas", "aun", "aún", "más", "menos", "hasta", "ni",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PastTimeAdverbs = new[] { "ayer", "anoche", "anteayer" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UpcomingExtraTimeNouns = new[] { "siglo", "verano", "invierno", "otoño", "primavera", "fin", "finde", "sábado" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TimeNouns = new[]
    {
        "semana", "mes", "año", "día", "noche", "vez", "hora", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Fixed expressions that are plural as a whole (buenos días, buenas tardes, buenas noches, muchas gracias): modifier lemma, then the noun lemmas it forms one with.
    // "mucha gracia" (singular) is ordinary Spanish, so the table applies only once a plural shows up on either side (see SpanishAgreementChecker, rule M29c).
    private static readonly FrozenDictionary<string, FrozenSet<string>> FixedPluralExpressions = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
    {
        ["bueno"] = new[] { "día", "tarde", "noche" }.ToFrozenSet(StringComparer.Ordinal),
        ["mucho"] = new[] { "gracia" }.ToFrozenSet(StringComparer.Ordinal),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Words whose use as an interjection is the first reading to report although the lexicon also lists them as a plural noun (gracias, the plural of gracia).
    private static readonly FrozenSet<string> InterjectionFirstWords = new[] { "gracias" }.ToFrozenSet(StringComparer.Ordinal);

    // The question words that take a verb right after them (dónde está, cómo está), spelled with and without the accent, folded to lower case.
    private static readonly FrozenSet<string> VerbSeekingQuestionWords = new[] { "dónde", "donde", "cómo" }.ToFrozenSet(StringComparer.Ordinal);

    // Pronouns that stand before a verb or before another such pronoun (te lo doy); a word spelled like one that has no verb after it is not one of them.
    private static readonly FrozenSet<string> PreverbalPronouns = new[]
    {
        "me", "te", "se", "lo", "la", "los", "las", "le", "les", "nos", "os",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Adverbs of place that say where something is, which takes estar (está lejos, está cerca); ser with one is a mistake for a place (es lejos). aquí, allí and
    // ahí are left out on purpose: es aquí identifies a place or an event (¿es aquí la fiesta?, es aquí donde vivo) and is correct.
    private static readonly FrozenSet<string> LocationAdverbs = new[] { "lejos", "cerca" }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Nouns of events, whose place takes ser (la fiesta es en la plaza, ¿dónde es la fiesta?: where it takes place), unlike things and places, which take estar.
    // A noun that names an event and a thing (comida, the food and the meal; salida, the exit and the departure) is listed, so that it is read the way that is correct.
    private static readonly FrozenSet<string> EventNouns = new[]
    {
        "fiesta", "reunión", "boda", "concierto", "clase", "lección", "curso", "examen", "partido", "partida", "juego", "carrera", "competición", "concurso",
        "torneo", "cena", "comida", "almuerzo", "desayuno", "merienda", "banquete", "cumpleaños", "festival", "feria", "mercadillo", "misa", "funeral",
        "entierro", "ceremonia", "celebración", "espectáculo", "función", "obra", "película", "exposición", "conferencia", "congreso", "cita", "encuentro",
        "evento", "acto", "entrevista", "baile", "desfile", "procesión", "carnaval", "salida", "llegada",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Verbs whose subject follows them after a dative pronoun, and which take no direct object there: me gusta el pan, me gustan las manzanas, me duele la cabeza,
    // me faltan dos monedas. parecer (me parece una buena idea: a predicate) and verbs that also take an object (me dan el pan) are not among them.
    private static readonly FrozenSet<string> ExperiencerVerbs = new[]
    {
        "gustar", "encantar", "interesar", "importar", "doler", "faltar", "quedar", "sobrar", "molestar", "apetecer", "fascinar", "agradar", "disgustar", "preocupar",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. The dative pronouns before an experiencer verb (me gusta, les gustan); lo, la, los and las are accusative and se is left out.
    private static readonly FrozenSet<string> DativePronouns = new[] { "me", "te", "le", "nos", "os", "les" }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Verbs of price, whose subject follows them in the price question (¿cuánto cuesta el pan?, ¿cuánto valen los panes?); elsewhere the noun phrase after them is
    // the price (cuestan dos monedas).
    private static readonly FrozenSet<string> PriceVerbs = new[] { "costar", "valer" }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Nouns that measure a stretch of time, beside the time nouns: a noun phrase of one of them after a verb is how long or when (están la tarde en casa), not its subject.
    private static readonly FrozenSet<string> DurationNouns = new[]
    {
        "tarde", "mañana", "rato", "momento", "mediodía", "medianoche", "minuto", "segundo", "tiempo", "temporada", "época",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Words that open a clause of their own (the question words and relatives, que, si, porque), spelled with and without the accent: a word before one is not in
    // the clause of the verb after it.
    private static readonly FrozenSet<string> ClauseOpeners = new[]
    {
        "que", "qué", "dónde", "donde", "adónde", "adonde", "cuándo", "cuando", "cómo", "cuánto", "cuanto", "cuánta", "cuanta", "cuántos", "cuantos", "cuántas",
        "cuantas", "quién", "quien", "quiénes", "quienes", "cuál", "cual", "cuáles", "cuales", "porque", "si",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Nouns of a part or a quantity, which agree with the noun of their de phrase as well as with themselves (la mayoría de los panes cuestan, or cuesta).
    private static readonly FrozenSet<string> QuantityNouns = new[]
    {
        "mayoría", "minoría", "mitad", "resto", "parte", "grupo", "cantidad", "montón", "número", "totalidad", "serie", "conjunto", "docena", "par", "tercio",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M59. The words that may open the clause of a subject before its verb, besides the start of the utterance: the conjunctions and the question words that do not
    // stand for a noun phrase themselves (cuando Domingo trabaja, pero Luz hizo pan, ¿dónde Domingo trabaja?). qué, cuánto, cuál and quién are left out: a noun phrase
    // after one is the phrase asked about (¿qué fruta come Luz?, ¿cuántas manzanas quieres?), and so is que, which a learner types for qué.
    private static readonly FrozenSet<string> SubjectClauseOpeners = new[]
    {
        "y", "e", "pero", "porque", "pues", "si", "cuando", "cuándo", "aunque", "mientras", "como", "cómo", "donde", "dónde",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M59. The answers that may stand before a subject (sí, Domingo trabaja en la herrería); any other interjection before a noun makes it the person spoken to (hola Domingo).
    private static readonly FrozenSet<string> AnswerWords = new[] { "sí", "no" }.ToFrozenSet(StringComparer.Ordinal);

    // M59. Nouns that are said as interjections (gracias, perdón, cuidado): a bare one before a verb is not its subject (gracias, quiero pan).
    private static readonly FrozenSet<string> InterjectionNouns = new[]
    {
        "gracias", "perdón", "adiós", "cuidado", "vale", "claro", "hola", "salud", "socorro", "enhorabuena", "felicidades", "ojo", "venga", "bienvenido", "bienvenida",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M59. Nouns a speaker calls people by: a bare one before a verb of the first or second person is the person spoken to, not the subject (señor, quiero pan). The
    // normalized text keeps no comma, so a name said that way (Domingo, quiero un hacha) is read as the subject; see API.md.
    private static readonly FrozenSet<string> VocativeNouns = new[]
    {
        "señor", "señora", "señorita", "amigo", "amiga", "chico", "chica", "niño", "niña", "mamá", "papá", "hijo", "hija", "hermano", "hermana", "abuelo", "abuela",
        "tío", "tía", "doctor", "doctora", "profesor", "profesora", "hombre", "mujer", "jefe", "jefa", "compañero", "compañera", "vecino", "vecina", "maestro", "maestra",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M59. The verbs whose clitic after cómo is reflexive and so agrees with the verb: llamarse, the question of a name (¿cómo te llamas?, ¿cómo se llama?), and sentirse
    // (¿cómo te sientes?). ¿cómo te va? (ir, with a dative) is not one of them.
    private static readonly FrozenSet<string> ReflexiveQuestionVerbs = new[] { "llamar", "sentir" }.ToFrozenSet(StringComparer.Ordinal);

    // M59. The preposition of elapsed time, which is also a form of hacer (hace dos días), and the nouns of a stretch of time it takes. The days of the week are left out
    // (¿qué hace Domingo?), and so are the weather nouns (hace frío, hace buen tiempo: the verb).
    private static readonly FrozenSet<string> ElapsedTimeNouns = new[]
    {
        "segundo", "minuto", "hora", "día", "noche", "semana", "mes", "año", "década", "siglo", "rato", "momento", "tiempo",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>True for a word that may open the clause of a subject before its verb (<c>y</c>, <c>pero</c>, <c>cuando</c>, <c>dónde</c>...; not <c>qué</c> or <c>que</c>; M59).</summary>
    public static bool IsSubjectClauseOpener(string word)
    {
        return SubjectClauseOpeners.Contains(word);
    }

    /// <summary>True for <c>sí</c> and <c>no</c>, the answers that may stand before a subject (M59).</summary>
    public static bool IsAnswerWord(string word)
    {
        return AnswerWords.Contains(word);
    }

    /// <summary>True for a noun that is said as an interjection (<c>gracias</c>, <c>perdón</c>), which is no subject when it stands alone before a verb (M59).</summary>
    public static bool IsInterjectionNoun(string word)
    {
        return InterjectionNouns.Contains(word);
    }

    /// <summary>True for a noun a speaker calls people by (<c>señor</c>, <c>amigo</c>), which alone before a verb of the first or second person is the person spoken to (M59).</summary>
    public static bool IsVocativeNoun(string word)
    {
        return VocativeNouns.Contains(word);
    }

    /// <summary>True for the verbs whose clitic after <c>cómo</c> is reflexive (<c>llamar</c>, <c>sentir</c>: <c>¿cómo te llamas?</c>; M59).</summary>
    public static bool IsReflexiveQuestionVerb(string lemma)
    {
        return ReflexiveQuestionVerbs.Contains(lemma);
    }

    /// <summary>The person and number a reflexive pronoun stands for (<c>me</c> 1 sg, <c>te</c> 2 sg, <c>nos</c> 1 pl, <c>os</c> 2 pl, <c>se</c> 3 of either number), or null (M59).</summary>
    public static (string Person, string? Number)? ReflexivePerson(string word)
    {
        return word switch
        {
            "me" => ("1", LexiconSchema.Singular),
            "te" => ("2", LexiconSchema.Singular),
            "nos" => ("1", LexiconSchema.Plural),
            "os" => ("2", LexiconSchema.Plural),
            "se" => ("3", null),
            _ => null,
        };
    }

    /// <summary>True for the preposition of elapsed time, <c>hace</c> (<c>hace dos días</c>), which is also a form of <c>hacer</c> (M59).</summary>
    public static bool IsElapsedTimePreposition(string lemma)
    {
        return string.Equals(lemma, "hace", StringComparison.Ordinal);
    }

    /// <summary>True for the nouns of a stretch of time that <c>hace</c> takes as a preposition (<c>día</c>, <c>semana</c>, <c>año</c>, <c>rato</c>, <c>tiempo</c>...; M59).</summary>
    public static bool IsElapsedTimeNoun(string lemma)
    {
        return ElapsedTimeNouns.Contains(lemma);
    }

    /// <summary>True for the question words that are followed by a verb (<c>dónde</c>, <c>donde</c>, <c>cómo</c>), so a word that is a determiner or a verb form after one is the verb.</summary>
    public static bool IsVerbSeekingQuestionWord(string word)
    {
        return VerbSeekingQuestionWords.Contains(word);
    }

    /// <summary>True for the pronouns that stand before a verb (<c>me</c>, <c>te</c>, <c>se</c>, <c>lo</c>, <c>la</c>...), which may be followed by another one.</summary>
    public static bool IsPreverbalPronoun(string lemma)
    {
        return PreverbalPronouns.Contains(lemma);
    }

    /// <summary>True for adverbs that may stand directly before a noun (solo agua, también espadas, más agua), unlike manner and time adverbs (temprano, bien, ahora).</summary>
    public static bool IsNounModifyingAdverb(string lemma)
    {
        return NounModifyingAdverbs.Contains(lemma);
    }

    /// <summary>True for the interrogative quantifier <c>cuánto</c>, which is an adverb before a verb (cuánto cuesta) and a determiner before a noun (cuántas espadas).</summary>
    public static bool IsInterrogativeQuantifier(string lemma)
    {
        return string.Equals(lemma, "cuánto", StringComparison.Ordinal);
    }

    /// <summary>True for adjectives that describe a temporary state and take estar.</summary>
    public static bool TakesEstar(string adjectiveLemma)
    {
        return EstarAdjectives.Contains(adjectiveLemma);
    }

    /// <summary>True for adjectives that describe an inherent quality and take ser.</summary>
    public static bool TakesSer(string adjectiveLemma)
    {
        return SerAdjectives.Contains(adjectiveLemma);
    }

    /// <summary>True for adjectives that normally precede their noun (grande, bueno, malo; determiner adjectives are handled elsewhere).</summary>
    public static bool IsPrenominal(string adjectiveLemma)
    {
        return PrenominalAdjectives.Contains(adjectiveLemma) || WordClasses.IsDeterminerAdjective(adjectiveLemma);
    }

    /// <summary>True for adverbs that put the sentence in the past (ayer, anoche, anteayer).</summary>
    public static bool IsPastTimeAdverb(string lemma)
    {
        return PastTimeAdverbs.Contains(lemma);
    }

    /// <summary>True for a word that is reported as an interjection unless an article or a numeral precedes it (<c>gracias</c>, but <c>las gracias</c>); see <c>ReportedReadings</c>.</summary>
    public static bool IsInterjectionFirst(string word)
    {
        return InterjectionFirstWords.Contains(word);
    }

    /// <summary>True when the adjective lemma and the noun lemma form a fixed expression that is plural as a whole (<c>bueno</c> + <c>día</c>, <c>mucho</c> + <c>gracia</c>).</summary>
    public static bool IsFixedPluralExpression(string adjectiveLemma, string nounLemma)
    {
        return FixedPluralExpressions.TryGetValue(adjectiveLemma, out var nouns) && nouns.Contains(nounLemma);
    }

    /// <summary>True for a noun that <c>próximo</c> precedes as a time word (la próxima semana, el próximo verano): the time nouns and the seasons, centuries and weekend.</summary>
    public static bool IsUpcomingTimeNoun(string lemma)
    {
        return TimeNouns.Contains(lemma) || UpcomingExtraTimeNouns.Contains(lemma);
    }

    /// <summary>True for nouns that combine with <c>pasado</c> to mark past time (la semana pasada).</summary>
    public static bool IsTimeNoun(string lemma)
    {
        return TimeNouns.Contains(lemma);
    }

    /// <summary>True for the adverbs of place that take <c>estar</c> (<c>lejos</c>, <c>cerca</c>): <c>es lejos</c> is a mistake for <c>está lejos</c> (M56).</summary>
    public static bool IsLocationAdverb(string lemma)
    {
        return LocationAdverbs.Contains(lemma);
    }

    /// <summary>True for the question word of place (<c>dónde</c>, and <c>donde</c> typed without its accent).</summary>
    public static bool IsLocationQuestionWord(string word)
    {
        return word is "dónde" or "donde";
    }

    /// <summary>True for nouns of events (<c>fiesta</c>, <c>reunión</c>, <c>clase</c>...), whose place takes <c>ser</c>: <c>¿dónde es la fiesta?</c> is correct (M56).</summary>
    public static bool IsEventNoun(string lemma)
    {
        return EventNouns.Contains(lemma);
    }

    /// <summary>True for verbs whose subject follows them after a dative pronoun (<c>me gusta el pan</c>, <c>me duelen los pies</c>; M56).</summary>
    public static bool IsExperiencerVerb(string lemma)
    {
        return ExperiencerVerbs.Contains(lemma);
    }

    /// <summary>True for the dative pronouns that stand before an experiencer verb (<c>me</c>, <c>te</c>, <c>le</c>, <c>nos</c>, <c>os</c>, <c>les</c>).</summary>
    public static bool IsDativePronoun(string word)
    {
        return DativePronouns.Contains(word);
    }

    /// <summary>True for the verbs of price (<c>costar</c>, <c>valer</c>), whose subject follows them after <c>cuánto</c> (M56).</summary>
    public static bool IsPriceVerb(string lemma)
    {
        return PriceVerbs.Contains(lemma);
    }

    /// <summary>True for a noun that says when or for how long (the time nouns, the seasons, <c>tarde</c>, <c>rato</c>...): a noun phrase of one after a verb is no subject.</summary>
    public static bool IsDurationNoun(string lemma)
    {
        return TimeNouns.Contains(lemma) || UpcomingExtraTimeNouns.Contains(lemma) || DurationNouns.Contains(lemma);
    }

    /// <summary>True for a word that opens a clause of its own: a question word or relative (<c>dónde</c>, <c>cuánto</c>, <c>que</c>...), <c>si</c> or <c>porque</c>.</summary>
    public static bool IsClauseOpener(string word)
    {
        return ClauseOpeners.Contains(word);
    }

    /// <summary>True for nouns of a part or a quantity (<c>mayoría</c>, <c>mitad</c>, <c>resto</c>...), which may agree with the noun of the <c>de</c> phrase after them.</summary>
    public static bool IsQuantityNoun(string lemma)
    {
        return QuantityNouns.Contains(lemma);
    }
}
