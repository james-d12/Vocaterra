namespace Vocaterra.Game.Core.Language.Book;

/// <summary>How a cell of a conjugation table is shown (S28, D-game-19).</summary>
public enum CellState
{
    /// <summary>The learner has used this form: highlighted, with its count.</summary>
    Used,

    /// <summary>A lesson that is completed or under way has taught this form and the learner has not used it yet: shown plainly.</summary>
    Taught,

    /// <summary>Neither taught nor used: the cell is empty (a dash), so nothing overwhelms.</summary>
    Locked,
}
