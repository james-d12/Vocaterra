namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The small mark at the left of a row or a sub line (U07): a lesson's state, a unit's, or how a line the learner said was taken. The layer draws each in 11 px.</summary>
public enum BookMark
{
    /// <summary>A ticked box: a lesson or a unit done.</summary>
    Done,

    /// <summary>A box half filled: a lesson or a unit under way.</summary>
    Half,

    /// <summary>An arrow in the page's accent: the lesson the objective sends the learner to.</summary>
    Next,

    /// <summary>A dashed box: a lesson that waits.</summary>
    Wait,

    /// <summary>An empty box: a unit not begun.</summary>
    Empty,

    /// <summary>A tick in green: a line the core understood.</summary>
    Understood,

    /// <summary>An exclamation mark in amber: understood with a small mistake.</summary>
    Slip,

    /// <summary>A cross in red: not understood.</summary>
    Missed,

    /// <summary>A dot: a line that was held back or asked for help, and so says nothing about the learner's word.</summary>
    Dot,
}
