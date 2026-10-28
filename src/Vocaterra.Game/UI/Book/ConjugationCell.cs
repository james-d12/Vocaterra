namespace Vocaterra.Game.UI.Book;

/// <summary>One cell of a conjugation table.</summary>
/// <param name="Text">The form, from the core's morphology (<c>quiero</c>, <c>voy a comer</c>), or <see langword="null"/> for a locked cell.</param>
/// <param name="State">How the cell is shown.</param>
/// <param name="Uses">How many times the learner has used the form (0 unless <see cref="CellState.Used"/>).</param>
public sealed record ConjugationCell(string? Text, CellState State, int Uses);
