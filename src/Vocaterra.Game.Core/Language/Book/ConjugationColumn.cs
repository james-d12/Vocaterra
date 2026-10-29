namespace Vocaterra.Game.Core.Language.Book;

/// <summary>One visible column of a conjugation table: a tense.</summary>
/// <param name="Tense">The tense code: <c>pres</c>, <c>pret</c> or <c>ir-a</c> (the near future, <c>ir</c> + <c>a</c> + the infinitive).</param>
/// <param name="Title">The English heading: present, past or future (ir a).</param>
public sealed record ConjugationColumn(string Tense, string Title);
