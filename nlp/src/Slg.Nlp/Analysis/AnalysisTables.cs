using System.Collections.Frozen;

namespace Slg.Nlp.Analysis;

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

    private static readonly FrozenSet<string> PrenominalAdjectives = new[] { "grande", "bueno", "malo" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> PastTimeAdverbs = new[] { "ayer", "anoche", "anteayer" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TimeNouns = new[]
    {
        "semana", "mes", "año", "día", "noche", "vez", "hora", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo",
    }.ToFrozenSet(StringComparer.Ordinal);

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

    /// <summary>True for nouns that combine with <c>pasado</c> to mark past time (la semana pasada).</summary>
    public static bool IsTimeNoun(string lemma)
    {
        return TimeNouns.Contains(lemma);
    }
}
