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

    // M69. Common descriptive adjectives, several of whose forms are also nouns that are no predicate of a person or a thing without a determiner (alta: the discharge;
    // cara: the face; bonito: the tuna; lista: the list; nueva: the news), so that after a copula such a form is the adjective where its noun would not agree with the
    // subject and the copula (él es alta, la casa es bonito; IAnalysisRules.IsPredicateAdjective). An adjective whose noun may be a predicate noun of a person (médico,
    // pareja) or of a thing (comida, the food, of comido) is left out on purpose, and so are the colours that are nouns of their own (rosa, naranja).
    private static readonly FrozenSet<string> PredicateAdjectives = new[]
    {
        "alto", "bajo", "bonito", "lindo", "guapo", "feo", "caro", "barato", "nuevo", "viejo", "rico", "pobre", "bueno", "malo", "pequeño", "grande", "largo",
        "corto", "gordo", "delgado", "limpio", "sucio", "claro", "oscuro", "rojo", "blanco", "negro", "amarillo", "frío", "fresco", "duro", "blando", "rápido",
        "lento", "raro", "cómodo", "lleno", "vacío", "listo", "tranquilo", "simpático", "antipático", "sano", "enfermo", "precioso", "sabroso", "dulce",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The adjectives that go before their noun, so that order is no slip. M66: cada (cada manzana cuesta una moneda gave word_order "manzana cada"); it is no
    // determiner adjective here, because cada dos días puts it before a plural.
    private static readonly FrozenSet<string> PrenominalAdjectives = new[] { "grande", "bueno", "malo", "cada" }.ToFrozenSet(StringComparer.Ordinal);

    // M62: the colours go after their noun in neutral Spanish (manzanas rojas; tres rojas manzanas is a slip of order). Several are also nouns (roja, blanca, rosa,
    // gris), so the reading resolver reads such a word as the adjective right before a word that can only be a noun (IAnalysisRules.IsPostnominalAdjective).
    private static readonly FrozenSet<string> ColourAdjectives = new[]
    {
        "rojo", "azul", "verde", "amarillo", "blanco", "negro", "gris", "rosa", "naranja", "marrón", "morado", "violeta", "celeste", "dorado", "plateado",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M62: the adverbs of degree that modify an adjective or an adverb and never a noun (muy caro, tan cara; never muy pan).
    private static readonly FrozenSet<string> DegreeAdverbs = new[] { "muy", "tan" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NounModifyingAdverbs = new[]
    {
        "solo", "sólo", "también", "tampoco", "incluso", "casi", "apenas", "aun", "aún", "más", "menos", "hasta", "ni",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The time markers live in SpanishTimeMarkers (M57), which the fuzz suite's sentence generator reads too.
    private static readonly FrozenSet<string> PastTimeAdverbs = SpanishTimeMarkers.PastAdverbs.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PastTimeAdjectiveForms = SpanishTimeMarkers.PastAdjectiveForms.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> WordsBeforeTheNounMañana = SpanishTimeMarkers.WordsBeforeTheNounMañana.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UpcomingTimeNouns = SpanishTimeMarkers.UpcomingTimeNouns.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TimeNouns = SpanishTimeMarkers.TimeNouns.ToFrozenSet(StringComparer.Ordinal);

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

        // M69: more events, since ser with en and a place is checked too (la entrega es en la plaza, la visita es en el museo).
        "entrega", "visita", "excursión", "viaje", "sesión", "votación", "inauguración", "graduación", "verbena", "romería", "ensayo", "actuación", "taller",
        "charla", "prueba", "subasta", "sorteo", "batalla", "pelea", "lucha", "accidente",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M69. Nouns of the adverbial phrases that en makes with a determiner (en el fondo, en la práctica, en la actualidad, en el caso de), which say how or when and not
    // where: ser before one is no place (es, en el fondo, bueno). Bare phrases (en serio, en general) are never a place for the rule.
    private static readonly FrozenSet<string> AdverbialPhraseNouns = new[]
    {
        "fondo", "práctica", "actualidad", "medida", "sentido", "caso", "realidad", "principio", "teoría", "verdad", "general", "parte", "total", "cambio", "forma",
        "manera", "modo", "palabra", "seguida", "absoluto", "punto",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M69. The ordinals and words of sequence, as written, which may stand for a noun left unsaid (la tercera: the third time, ¿dónde estuviste la tercera?).
    private static readonly FrozenSet<string> Ordinals = new[]
    {
        "primero", "primera", "segundo", "segunda", "tercero", "tercera", "cuarto", "cuarta", "quinto", "quinta", "sexto", "sexta", "séptimo", "séptima", "octavo",
        "octava", "noveno", "novena", "décimo", "décima", "último", "última", "próximo", "próxima", "siguiente", "anterior",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M69. The nouns that make a place with en without a determiner (en casa: at home); any other bare noun after en is a time or a manner (en marzo, en monedas, en tren).
    private static readonly FrozenSet<string> BarePlaceNouns = new[] { "casa" }.ToFrozenSet(StringComparer.Ordinal);

    // M56. Verbs whose subject follows them after a dative pronoun, and which take no direct object there: me gusta el pan, me gustan las manzanas, me duele la cabeza,
    // me faltan dos monedas. parecer (me parece una buena idea: a predicate) and verbs that also take an object (me dan el pan) are not among them.
    private static readonly FrozenSet<string> ExperiencerVerbs = new[]
    {
        "gustar", "encantar", "interesar", "importar", "doler", "faltar", "quedar", "sobrar", "molestar", "apetecer", "fascinar", "agradar", "disgustar", "preocupar",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M56. The dative pronouns before an experiencer verb (me gusta, les gustan); lo, la, los and las are accusative and se is left out.
    private static readonly FrozenSet<string> DativePronouns = new[] { "me", "te", "le", "nos", "os", "les" }.ToFrozenSet(StringComparer.Ordinal);

    // M70. The experiencer verbs whose subject is the thing liked and never the one who likes, so that a form of the first or second person with a phrase after it, or a
    // subject pronoun before it with no dative, is the learner's English order (me gusto el pan, yo gusto el pan); the others also take a subject of the first or second
    // person, or an object, in other senses (me quedo el libro, me preocupo, te molesto).
    private static readonly FrozenSet<string> LikingVerbs = new[] { "gustar", "encantar" }.ToFrozenSet(StringComparer.Ordinal);

    // M70. The dative pronoun of each subject pronoun (yo: me, usted: le, ustedes: les).
    private static readonly FrozenDictionary<string, string> DativeBySubject = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["yo"] = "me",
        ["tú"] = "te",
        ["él"] = "le",
        ["ella"] = "le",
        ["usted"] = "le",
        ["nosotros"] = "nos",
        ["nosotras"] = "nos",
        ["vosotros"] = "os",
        ["vosotras"] = "os",
        ["ellos"] = "les",
        ["ellas"] = "les",
        ["ustedes"] = "les",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // M56. Verbs of price, whose subject follows them in the price question (¿cuánto cuesta el pan?, ¿cuánto valen los panes?); elsewhere the noun phrase after them is
    // the price (cuestan dos monedas).
    private static readonly FrozenSet<string> PriceVerbs = new[] { "costar", "valer" }.ToFrozenSet(StringComparer.Ordinal);

    // M66. The verbs that take an infinitive right after them (puedo pagar, quiero vender, debo comer, necesito comprar); tener takes it after que (tengo que ir). A finite
    // form of a verb in that place is the learner's slip for the infinitive (puedo pago -> pagar), as after ir a.
    private static readonly FrozenSet<string> ModalVerbs = new[] { "poder", "querer", "deber", "necesitar" }.ToFrozenSet(StringComparer.Ordinal);

    // M66. The modals of ModalVerbs that also take a noun as their object (quiero vino, necesito pago, debo dinero); poder and tener que take none.
    private static readonly FrozenSet<string> NounObjectModals = new[] { "querer", "deber", "necesitar" }.ToFrozenSet(StringComparer.Ordinal);

    // M66. The neuter pronouns, which are singular: the subject of a verb after it in the price question (¿cuánto cuesta todo?, ¿cuánto es eso?). todo is an adjective in
    // the lexicon; alone after the verb it is the pronoun.
    private static readonly FrozenSet<string> NeuterPronouns = new[] { "todo", "eso", "esto", "aquello" }.ToFrozenSet(StringComparer.Ordinal);

    // M66 (M63). The question words that may stand between a subject pronoun and its verb (ellas dónde están, tú qué quieres), written with their accent: without it
    // (que, donde, como) they may open a relative clause (ella que quiere pan) or be another word.
    private static readonly FrozenSet<string> QuestionWordsAfterSubject = new[] { "qué", "dónde", "adónde", "cuándo", "cómo" }.ToFrozenSet(StringComparer.Ordinal);

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

    // M59. The verbs whose clitic after cómo is reflexive and so agrees with the verb: llamarse, the question of a name (¿cómo te llamas?, ¿cómo se llama?), and sentirse
    // (¿cómo te sientes?). ¿cómo te va? (ir, with a dative) is not one of them.
    private static readonly FrozenSet<string> ReflexiveQuestionVerbs = new[] { "llamar", "sentir" }.ToFrozenSet(StringComparer.Ordinal);

    // M59. The preposition of elapsed time, which is also a form of hacer (hace dos días), and the nouns of a stretch of time it takes. The days of the week are left out
    // (¿qué hace Domingo?), and so are the weather nouns (hace frío, hace buen tiempo: the verb).
    private static readonly FrozenSet<string> ElapsedTimeNouns = new[]
    {
        "segundo", "minuto", "hora", "día", "noche", "semana", "mes", "año", "década", "siglo", "rato", "momento", "tiempo",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M64 (the review of feature/village, R5). The words that say how much a sum is, as written in the singular masculine, the form a predicate takes when the amount
    // before ser is read as one sum (dos panes es suficiente, diez monedas es caro, quince monedas es mucho, dos panes no es nada).
    private static readonly FrozenSet<string> AmountPredicates = new[]
    {
        "mucho", "poco", "bastante", "demasiado", "suficiente", "caro", "barato", "nada", "algo",
    }.ToFrozenSet(StringComparer.Ordinal);

    // M73 (M72's finding). The words of AmountPredicates that say a price, which a sum of money has (diez monedas es caro) and goods have themselves (dos panes son
    // caros), and the nouns of money units, the amounts that are such a sum.
    private static readonly FrozenSet<string> PricePredicates = new[] { "caro", "barato" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MoneyNouns = new[]
    {
        "moneda", "euro", "céntimo", "centavo", "dólar", "peso", "libra", "real",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>True for a word, as written, that says how much a sum is (<c>mucho</c>, <c>poco</c>, <c>suficiente</c>, <c>caro</c>...; M64).</summary>
    public static bool IsAmountPredicate(string word)
    {
        return AmountPredicates.Contains(word);
    }

    /// <summary>True for a word of <see cref="IsAmountPredicate"/> that says a price (<c>caro</c>, <c>barato</c>), which only a sum of money has as one sum (M73).</summary>
    public static bool IsPricePredicate(string word)
    {
        return PricePredicates.Contains(word);
    }

    /// <summary>True for the lemma of a noun of a money unit (<c>moneda</c>, <c>euro</c>, <c>peso</c>...), whose amount is a sum of money (M73).</summary>
    public static bool IsMoneyNoun(string lemma)
    {
        return MoneyNouns.Contains(lemma);
    }

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

    /// <summary>True for the colour adjectives, which go after their noun in neutral Spanish (M62: <c>manzanas rojas</c>, not <c>rojas manzanas</c>).</summary>
    public static bool IsColourAdjective(string adjectiveLemma)
    {
        return ColourAdjectives.Contains(adjectiveLemma);
    }

    /// <summary>True for the adverbs of degree that modify an adjective or an adverb and never a noun (M62: <c>muy</c>, <c>tan</c>).</summary>
    public static bool IsDegreeAdverb(string lemma)
    {
        return DegreeAdverbs.Contains(lemma);
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

    /// <summary>True for a written form of <c>pasado</c>, which after a time noun puts it in the past (la semana pasada; M57: matched as written).</summary>
    public static bool IsPastTimeAdjectiveForm(string word)
    {
        return PastTimeAdjectiveForms.Contains(word);
    }

    /// <summary>True for a word after which <c>mañana</c> is the noun, the morning, and no future marker (esta mañana, por la mañana).</summary>
    public static bool IsWordBeforeTheNounMañana(string word)
    {
        return WordsBeforeTheNounMañana.Contains(word);
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
        return UpcomingTimeNouns.Contains(lemma);
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

    /// <summary>True for a common descriptive adjective whose noun homographs are no bare predicate (<c>alto</c>: <c>alta</c>, the discharge; M69).</summary>
    public static bool IsPredicateAdjective(string lemma)
    {
        return PredicateAdjectives.Contains(lemma);
    }

    /// <summary>True for the noun of an adverbial phrase with <c>en</c> and a determiner (<c>en el fondo</c>, <c>en la práctica</c>), which is no place (M69).</summary>
    public static bool IsAdverbialPhraseNoun(string lemma)
    {
        return AdverbialPhraseNouns.Contains(lemma);
    }

    /// <summary>True for an ordinal or a word of sequence as written (<c>tercera</c>, <c>última</c>), which may stand for a noun left unsaid (M69).</summary>
    public static bool IsOrdinal(string word)
    {
        return Ordinals.Contains(word);
    }

    /// <summary>True for a noun that is a place after <c>en</c> without a determiner (<c>en casa</c>; M69).</summary>
    public static bool IsBarePlaceNoun(string lemma)
    {
        return BarePlaceNouns.Contains(lemma);
    }

    /// <summary>True for verbs whose subject follows them after a dative pronoun (<c>me gusta el pan</c>, <c>me duelen los pies</c>; M56).</summary>
    public static bool IsExperiencerVerb(string lemma)
    {
        return ExperiencerVerbs.Contains(lemma);
    }

    /// <summary>True for <c>gustar</c> and <c>encantar</c>, whose subject is the thing liked and never the one who likes (M70).</summary>
    public static bool IsLikingVerb(string lemma)
    {
        return LikingVerbs.Contains(lemma);
    }

    /// <summary>The dative pronoun of a subject pronoun (<c>yo</c>: <c>me</c>, <c>ellas</c>: <c>les</c>), or null for another word (M70).</summary>
    public static string? DativeOf(string subjectPronoun)
    {
        return DativeBySubject.TryGetValue(subjectPronoun, out var dative) ? dative : null;
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

    /// <summary>True for a verb that takes an infinitive right after it (<c>poder</c>, <c>querer</c>, <c>deber</c>, <c>necesitar</c>; M66).</summary>
    public static bool IsModalVerb(string lemma)
    {
        return ModalVerbs.Contains(lemma);
    }

    /// <summary>True for the modal that takes an infinitive after a linking word: <c>tener</c> with <c>que</c> (M66).</summary>
    public static bool IsModalWithLinker(string lemma, string word)
    {
        return lemma == "tener" && word == "que";
    }

    /// <summary>True for a modal that may also take a noun as its object (<c>querer</c>, <c>deber</c>, <c>necesitar</c>: <c>quiero vino</c>; M66).</summary>
    public static bool ModalTakesNounObject(string lemma)
    {
        return NounObjectModals.Contains(lemma);
    }

    /// <summary>True for a neuter pronoun as written (<c>todo</c>, <c>eso</c>, <c>esto</c>, <c>aquello</c>), which is singular (M66).</summary>
    public static bool IsNeuterPronoun(string word)
    {
        return NeuterPronouns.Contains(word);
    }

    /// <summary>True for a question word, as written with its accent, that may stand between a subject pronoun and its verb (<c>tú qué quieres</c>; M66).</summary>
    public static bool IsQuestionWordAfterSubject(string word)
    {
        return QuestionWordsAfterSubject.Contains(word);
    }

    /// <summary>True for a noun that says when or for how long (the time nouns, the seasons, <c>tarde</c>, <c>rato</c>...): a noun phrase of one after a verb is no subject.</summary>
    public static bool IsDurationNoun(string lemma)
    {
        return UpcomingTimeNouns.Contains(lemma) || DurationNouns.Contains(lemma);
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
