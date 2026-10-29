namespace Vocaterra.Game.Core.Language.Book;

/// <summary>One visible column of a conjugation table: a tense.</summary>
/// <param name="Tense">The tense code: <c>pres</c>, <c>pret</c> or <c>ir-a</c> (the near future, <c>ir</c> + <c>a</c> + the infinitive).</param>
/// <param name="TitleId">The id of its heading in the support language (<c>ui.&lt;id&gt;</c>: present, past or future (ir a); C04).</param>
public sealed record ConjugationColumn(string Tense, string TitleId);
