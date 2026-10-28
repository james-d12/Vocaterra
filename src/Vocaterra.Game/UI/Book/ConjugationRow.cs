namespace Vocaterra.Game.UI.Book;

/// <summary>One row of a conjugation table: a person (yo, tú, él/ella/usted, nosotros, ustedes/ellos).</summary>
/// <param name="Label">The Spanish pronoun.</param>
/// <param name="Person">The grammatical person, <c>1</c> to <c>3</c>.</param>
/// <param name="Number"><c>sg</c> or <c>pl</c>.</param>
/// <param name="Cells">One cell per visible column, in column order.</param>
public sealed record ConjugationRow(string Label, string Person, string Number, IReadOnlyList<ConjugationCell> Cells);
