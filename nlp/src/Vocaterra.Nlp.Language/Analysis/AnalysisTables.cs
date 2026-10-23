using System.Collections.Frozen;

namespace Vocaterra.Nlp.Language.Analysis;

/// <summary>Small hand-written word tables used by the linguistic analyzer.</summary>
internal static class AnalysisTables
{
    private static readonly FrozenSet<string> EstarAdjectives = new[]
    {
        "cansado", "enfermo", "contento", "triste", "ocupado", "preocupado", "enojado", "listo", "lleno", "vacío", "cerrado", "abierto",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> SerAdjectives = new[]
    {
        "alto", "bajo", "inteligente", "simpático", "amable", "guapo", "feo", "importante", "fácil", "difícil", "trabajador",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PrenominalAdjectives = new[] { "grande", "bueno", "malo", "próximo" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> NounModifyingAdverbs = new[]
    {
        "solo", "sólo", "también", "tampoco", "incluso", "casi", "apenas", "aun", "aún", "más", "menos", "hasta", "ni",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PastTimeAdverbs = new[] { "ayer", "anoche", "anteayer" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TimeNouns = new[]
    {
        "semana", "mes", "año", "día", "noche", "vez", "hora", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Fixed expressions that are plural as a whole (buenos días, buenas tardes, buenas noches, muchas gracias): modifier lemma, then the noun lemmas it forms one with.
    // "mucha gracia" (singular) is ordinary Spanish, so the table applies only once a plural shows up on either side (see AgreementChecker, rule M29c).
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
        return PrenominalAdjectives.Contains(adjectiveLemma) || Lexicon.WordClasses.IsDeterminerAdjective(adjectiveLemma);
    }

    /// <summary>True for adverbs that put the sentence in the past (ayer, anoche, anteayer).</summary>
    public static bool IsPastTimeAdverb(string lemma)
    {
        return PastTimeAdverbs.Contains(lemma);
    }

    /// <summary>True for a word that is reported as an interjection unless an article or a numeral precedes it (<c>gracias</c>, but <c>las gracias</c>); see <see cref="ReportedReadings"/>.</summary>
    public static bool IsInterjectionFirst(string word)
    {
        return InterjectionFirstWords.Contains(word);
    }

    /// <summary>True when the adjective lemma and the noun lemma form a fixed expression that is plural as a whole (<c>bueno</c> + <c>día</c>, <c>mucho</c> + <c>gracia</c>).</summary>
    public static bool IsFixedPluralExpression(string adjectiveLemma, string nounLemma)
    {
        return FixedPluralExpressions.TryGetValue(adjectiveLemma, out var nouns) && nouns.Contains(nounLemma);
    }

    /// <summary>True for nouns that combine with <c>pasado</c> to mark past time (la semana pasada).</summary>
    public static bool IsTimeNoun(string lemma)
    {
        return TimeNouns.Contains(lemma);
    }
}
