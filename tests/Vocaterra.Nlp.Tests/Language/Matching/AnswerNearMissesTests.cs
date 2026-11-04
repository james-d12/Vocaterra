using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;

namespace Vocaterra.Nlp.Language.Tests.Matching;

/// <summary>
/// M75 (the owner, 2026-11-02, D-game-22: "a near miss must name its word"): <see cref="AnswerNearMisses"/> finds the one word by which a line misses the answer
/// phrase nearest it, when the two have the same number of words, exactly one differs, and the two words are a slip pair: close in spelling within the answer
/// word's own fuzzy budget, or both question words, articles, prepositions or pronouns. On the game bundle's lexicon and Spanish rules.
/// </summary>
public class AnswerNearMissesTests
{
    private static readonly NormalizationOptions Options = new(ConvertIndefiniteArticles: false);

    private static ContentBundle Bundle => GameBundleLocator.Bundle;

    [Theory]
    [InlineData("cuál es esto", "cuál")]
    [InlineData("cual es esto", "cual")] // typed without its accent: still a question word
    [InlineData("quién es esto", "quién")]
    [InlineData("cuánto es esto", "cuánto")]
    public void AQuestionWordForTheAnswersQuestionWordNamesIt(string line, string actual)
    {
        // lesson-what asks ¿Qué es esto?; M67's case. The nearest phrase is the canonical qué es esto.
        var nearMiss = Find(line, Bundle.AnswerSets["lesson-what"]);

        Assert.Equal(new AnswerNearMiss("what", "qué es esto", new TextSpan(0, actual.Length), actual, "qué", SlipPair.QuestionWord), nearMiss);
    }

    [Theory]
    [InlineData("smith-open", "quiero hacer un hacha")] // an added word: no answer phrase has four words one slip away
    [InlineData("lesson-will-go", "pasado mañana iré al río con Mar")] // pasado added to the answer's mañana iré al río con Mar
    [InlineData("lesson-had", "rápido por favor")] // one word from the alias ayuda por favor, but rápido and ayuda are content words far apart
    [InlineData("baker-open", "buenas tardes luz")] // a greeting and the listener's name: a word added to buenas tardes
    [InlineData("lesson-ate", "no quise")] // one word from no sé, but quise and sé are verbs far apart
    public void ACorrectLineThatIsNotOneSlipFromTheNearestAnswerIsNoNearMiss(string prompt, string line)
    {
        // Each was tagged vocab.wrong by M67's rule (within the fuzzy budget for the line's own length), and scored 0.4 while the agent praised it.
        Assert.Null(Find(line, Bundle.AnswerSets[Bundle.Prompts[prompt].AnswerSetId]));
    }

    [Fact]
    public void ASetWithNoAnswersHasNoNearMiss()
    {
        // The PR #28 review's item 8: the matcher reports distance 0 when it has no phrase to measure, which M67's rule read as a near miss of every line.
        var empty = new AnswerSet("empty", []);

        Assert.Equal(0, AnswerMatcher.Match(Norm("cuál es esto"), empty, Difficulty.Beginner, Bundle.Language, Options).Distance);
        Assert.Null(Find("cuál es esto", empty));
        Assert.Null(Find("qué es esto", empty));
    }

    [Fact]
    public void AnEmptyLineHasNoNearMiss()
    {
        Assert.Null(Find(string.Empty, Bundle.AnswerSets["lesson-what"]));
        Assert.Null(Find("¿?", Bundle.AnswerSets["lesson-what"]));
    }

    [Theory]
    [InlineData("quiero una espadas", "quiero una espada", 11, "espadas", "espada")] // one edit, within the budget of espada (6 letters: 1)
    [InlineData("la baca es grande", "la vaca es grande", 3, "baca", "vaca")] // the same sound: phonetic distance 0
    public void AWordCloseInSpellingIsASlip(string line, string canonical, int start, string actual, string expected)
    {
        var nearMiss = Find(line, Set(("a", canonical)));

        Assert.Equal(new AnswerNearMiss("a", canonical, new TextSpan(start, actual.Length), actual, expected, SlipPair.Spelling), nearMiss);
    }

    [Fact]
    public void AWordBeyondTheAnswerWordsOwnBudgetIsNoSpellingSlip()
    {
        // panes and peces: two edits, against a budget of 1 for the five letters of peces; both nouns, so no closed class either.
        Assert.Null(Find("quiero dos panes", Set(("a", "quiero dos peces"))));
    }

    [Fact]
    public void TheSpellingBudgetFollowsTheDifficulty()
    {
        // hacho for hacha: one edit. The budget of the five letters of hacha is 1 at Beginner (5 / 4) and at Intermediate (5 / 5), and 0 at Advanced (5 / 6).
        var set = Set(("a", "quiero un hacha"));

        Assert.Equal(SlipPair.Spelling, Find("quiero un hacho", set, Difficulty.Beginner)?.Pair);
        Assert.Equal(SlipPair.Spelling, Find("quiero un hacho", set, Difficulty.Intermediate)?.Pair);
        Assert.Null(Find("quiero un hacho", set, Difficulty.Advanced));
    }

    [Theory]
    [InlineData("es un pan", "es el pan", 3, "un", "el", SlipPair.Article)]
    [InlineData("voy en casa", "voy a casa", 4, "en", "a", SlipPair.Preposition)]
    [InlineData("te llamo luz", "me llamo luz", 0, "te", "me", SlipPair.Pronoun)]
    [InlineData("cuándo está el río", "dónde está el río", 0, "cuándo", "dónde", SlipPair.QuestionWord)]
    public void TwoWordsOfOneClosedClassAreASlip(string line, string canonical, int start, string actual, string expected, SlipPair pair)
    {
        var nearMiss = Find(line, Set(("a", canonical)));

        Assert.Equal(new AnswerNearMiss("a", canonical, new TextSpan(start, actual.Length), actual, expected, pair), nearMiss);
    }

    [Fact]
    public void AnAccentAloneIsNoDifference()
    {
        // esta for está differs only in its accent, which the matcher ignores, so the one word that differs is cuándo.
        var nearMiss = Find("cuándo esta el río", Set(("a", "dónde está el río")));

        Assert.Equal(("cuándo", "dónde", SlipPair.QuestionWord), (nearMiss?.Actual, nearMiss?.Expected, nearMiss?.Pair));
    }

    [Theory]
    [InlineData("quiero tres manzanas")] // two words differ
    [InlineData("yo quiero dos panes")] // a word added
    [InlineData("quiero panes")] // a word left out
    [InlineData("quiero dos panes")] // no word differs (an exact match is not a near miss)
    public void ALineThatDoesNotDifferInExactlyOneWordIsNoNearMiss(string line)
    {
        Assert.Null(Find(line, Set(("a", "quiero dos panes"))));
    }

    [Fact]
    public void OnlyTheNearestPhrasesCount()
    {
        // dónde está la casa is two edits from dónde está la mesa (nouns, no slip) and farther from cuándo está la casa, which is one slip away (two question
        // words) but not the nearest: no near miss.
        var set = Set(("a", "dónde está la mesa"), ("b", "cuándo está la casa"));

        Assert.Null(Find("dónde está la casa", set));
    }

    [Fact]
    public void AmongTheNearestPhrasesTheFirstOneSlipAwayInTheMatchersOrderNamesTheWord()
    {
        // qué es la casa is two edits from both qué es la mesa (answer a, nouns: no slip) and qué es una casa (answer b, two articles): b names una.
        var set = Set(("a", "qué es la mesa"), ("b", "qué es una casa"));

        Assert.Equal(new AnswerNearMiss("b", "qué es una casa", new TextSpan(7, 2), "la", "una", SlipPair.Article), Find("qué es la casa", set));
    }

    [Fact]
    public void AnAliasIsAPhraseOfItsAnswer()
    {
        // cuál es eso is one slip from the alias qué es eso, nearer than the canonical qué es esto.
        var nearMiss = Find("cuál es eso", Bundle.AnswerSets["lesson-what"]);

        Assert.Equal(("what", "qué es eso", "qué"), (nearMiss?.AnswerId, nearMiss?.Phrase, nearMiss?.Expected));
    }

    [Fact]
    public void TheSameInputGivesTheSameNearMiss()
    {
        var set = Bundle.AnswerSets["lesson-what"];

        Assert.Equal(Find("cuál es esto", set), Find("cuál es esto", set));
    }

    private static AnswerNearMiss? Find(string line, AnswerSet set, Difficulty difficulty = Difficulty.Beginner)
    {
        return AnswerNearMisses.Find(Norm(line), set, difficulty, Bundle.Language, Bundle.Lexicon, Bundle.AnalysisRules, Options);
    }

    private static NormalizedText Norm(string text)
    {
        return Bundle.Language.Normalizer.Normalize(text, Options);
    }

    private static AnswerSet Set(params (string Id, string Canonical)[] answers)
    {
        return new AnswerSet("fx", [.. answers.Select(static a => new Answer(a.Id, a.Canonical, [], []))]);
    }
}
