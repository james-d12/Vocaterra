namespace Vocaterra.Game.Core.Language.Words;

/// <summary>
/// The word card a first pickup shows: the noun with its article and its written form (<c>la madera</c>). The audio belongs to the card's
/// slot and comes with S9; until then the card shows an empty audio slot. A card is shown once per noun and counts as heard and read
/// (<see cref="WordProgress"/>), never as a quiz.
/// </summary>
/// <param name="Lemma">The catalogue noun (<c>madera</c>).</param>
/// <param name="Phrase">The noun with its definite article (<c>la madera</c>).</param>
public sealed record WordCard(string Lemma, string Phrase);
