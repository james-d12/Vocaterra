using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Core.Stages;

/// <summary>Why the two words of an <see cref="AnswerNearMiss"/> are a slip pair (M75), in the order the pair is tested.</summary>
public enum SlipPair
{
    /// <summary>Close in spelling: the two words are within the fuzzy budget of the answer's word, measured as the answer matcher measures a phrase.</summary>
    Spelling,

    /// <summary>Both are question words (Spanish: <c>cuál</c> for <c>qué</c>).</summary>
    QuestionWord,

    /// <summary>Both are articles (Spanish: <c>el</c> for <c>un</c>).</summary>
    Article,

    /// <summary>Both are prepositions (Spanish: <c>en</c> for <c>a</c>).</summary>
    Preposition,

    /// <summary>Both are pronouns (Spanish: <c>me</c> for <c>te</c>).</summary>
    Pronoun,
}

/// <summary>
/// A line that matched no answer of a constrained prompt's answer set but misses the answer phrase nearest it by one word, a slip the learner could make (M75; the
/// owner, 2026-11-02, D-game-22: "a near miss must name its word"). The verdict's derived <c>vocab.wrong</c> names that word (<c>cuál es esto</c> for <c>qué es
/// esto</c>: <c>cuál</c>, expected <c>qué</c>).
/// </summary>
/// <param name="AnswerId">The answer whose phrase the line misses by one word.</param>
/// <param name="Phrase">That phrase, its canonical phrase or an alias, normalized as the line is.</param>
/// <param name="Span">The line's word that differs, in the normalized text.</param>
/// <param name="Actual">The line's word, as normalized.</param>
/// <param name="Expected">The answer's word at its place, in its spelling with accents and a number in words (<c>qué</c>, <c>tres</c>).</param>
/// <param name="Pair">Why the two words are a slip pair.</param>
public sealed record AnswerNearMiss(string AnswerId, string Phrase, TextSpan Span, string Actual, string Expected, SlipPair Pair);
