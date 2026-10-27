namespace Vocaterra.Nlp.Language.Analysis;

using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Lexicon;

/// <summary>The coarse word classes the reading resolver chooses between. A token that has readings in more than one class is a part-of-speech homograph.</summary>
public enum WordClass
{
    /// <summary>A noun.</summary>
    Noun,

    /// <summary>A verb form that has a person (indicative, subjunctive, imperative).</summary>
    FiniteVerb,

    /// <summary>An infinitive, a participle or a gerund: a verb reading without a person.</summary>
    NonFiniteVerb,

    /// <summary>An adjective, including the determiner adjectives (possessives, demonstratives, quantifiers).</summary>
    Adjective,

    /// <summary>An article or a numeral; they are one class because the checks treat them alike (<c>un</c> is both).</summary>
    Determiner,

    /// <summary>A pronoun.</summary>
    Pronoun,

    /// <summary>An adverb.</summary>
    Adverb,

    /// <summary>A preposition.</summary>
    Preposition,

    /// <summary>A conjunction.</summary>
    Conjunction,

    /// <summary>An interjection.</summary>
    Interjection,
}

/// <summary>Predicates over single readings that the resolver and the agreement checks share, so both always mean the same thing by "finite verb" and "skippable".</summary>
public static class ReadingClasses
{
    /// <summary>How many adverbs, interjections or clitics may sit between a subject (or a copula) and the word it governs.</summary>
    public const int MaxSkippedBetween = 2;

    /// <summary>The word class of a reading.</summary>
    public static WordClass ClassOf(LexicalFeatures reading)
    {
        return reading.Pos switch
        {
            LexiconSchema.Noun => WordClass.Noun,
            LexiconSchema.Verb => reading.Person is null ? WordClass.NonFiniteVerb : WordClass.FiniteVerb,
            LexiconSchema.Adjective => WordClass.Adjective,
            LexiconSchema.Article or LexiconSchema.Numeral => WordClass.Determiner,
            LexiconSchema.Pronoun => WordClass.Pronoun,
            LexiconSchema.Adverb => WordClass.Adverb,
            LexiconSchema.Preposition => WordClass.Preposition,
            LexiconSchema.Conjunction => WordClass.Conjunction,
            _ => WordClass.Interjection,
        };
    }

    /// <summary>True for a verb form with a person; the imperative counts.</summary>
    public static bool IsFiniteVerb(LexicalFeatures reading)
    {
        return reading.Pos == LexiconSchema.Verb && reading.Person is not null;
    }

    /// <summary>True for an article, a numeral or a determiner adjective (possessive, demonstrative, quantifier).</summary>
    public static bool IsDeterminerReading(LexicalFeatures reading, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return reading.Pos is LexiconSchema.Article or LexiconSchema.Numeral
            || (reading.Pos == LexiconSchema.Adjective && rules.IsDeterminerAdjective(reading.Lemma));
    }

    /// <summary>
    /// True for a reading of a closed class: an article, numeral, determiner adjective, pronoun, preposition, conjunction, adverb or interjection. A word
    /// whose readings are all closed cannot be a <c>[WORD]</c> name (see <see cref="SlotGrammar.TypedMatching.CanBeName"/>).
    /// </summary>
    public static bool IsClosedClassReading(LexicalFeatures reading, IAnalysisRules rules)
    {
        return IsDeterminerReading(reading, rules)
            || reading.Pos is LexiconSchema.Pronoun or LexiconSchema.Preposition or LexiconSchema.Conjunction or LexiconSchema.Adverb or LexiconSchema.Interjection;
    }

    /// <summary>True when a word with these readings can sit between a subject and its verb without being checked: every reading is an adverb or interjection, or every reading is a clitic.</summary>
    public static bool IsSkippable(IReadOnlyList<LexicalFeatures> readings, IAnalysisRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (readings.Count == 0)
        {
            return false;
        }

        return readings.All(static r => r.Pos is LexiconSchema.Adverb or LexiconSchema.Interjection)
            || readings.All(r => r.Pos == LexiconSchema.Pronoun && rules.IsClitic(r.Lemma));
    }
}
