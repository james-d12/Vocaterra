namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The colour family of a book line: the layer maps it to a colour.</summary>
public enum BookTone
{
    /// <summary>A bright line: a known word, a completed lesson, an understood line.</summary>
    Known,

    /// <summary>A line in progress: a word with some credit, a lesson under way.</summary>
    Learning,

    /// <summary>A line the learner has met.</summary>
    Seen,

    /// <summary>A line the learner has not met yet.</summary>
    Unseen,

    /// <summary>A dim line: counts, help, feedback.</summary>
    Hint,

    /// <summary>A gold line: a summary, a due word.</summary>
    Gold,
}
