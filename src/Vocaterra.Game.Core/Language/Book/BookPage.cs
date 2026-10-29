namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The book's five pages (S20, D-game-16), in tab order.</summary>
public enum BookPage
{
    /// <summary>The catalogue nouns (seen or <c>¿?</c>, icon, pips) and the other nouns the learner used, with their forms and last sentence.</summary>
    Nouns,

    /// <summary>The verbs the learner used: the infinitive with its English, each form with its label, counts and last sentence.</summary>
    Verbs,

    /// <summary>The phrases of the phrase sheet with their help and counts (S8).</summary>
    Phrases,

    /// <summary>Every lesson in the bundle with its state, units, phrases, help and (once done) its English summary.</summary>
    Lessons,

    /// <summary>The newest lines the learner gave, newest first, with the core's feedback in English.</summary>
    Said,
}
