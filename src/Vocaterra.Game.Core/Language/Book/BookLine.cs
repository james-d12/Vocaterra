namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// One line of a book page (S20). A page is a list of these, drawn from the top with the scroll applied; a heading line may carry a noun's icon, its
/// pips, lamps (the phrases' help) or a tick box (the lessons' units). Plain values.
/// </summary>
/// <param name="Tone">The colour family.</param>
/// <param name="Text">The text, in English or Spanish as the learner reads it.</param>
/// <param name="Aside">Text after <paramref name="Text"/> in the dim colour: a phrase's English, a lesson's state.</param>
/// <param name="Right">Text at the right edge.</param>
/// <param name="Lemma">The noun whose icon the line shows at its left, or <see langword="null"/>.</param>
/// <param name="Pips">The three progress pips at the right (catalogue nouns), or <see langword="null"/>.</param>
/// <param name="Tick">A tick box at the left, ticked or not, or <see langword="null"/> for none.</param>
/// <param name="Lamps">Two small boxes at the right, lit or not (the card and the audio of a phrase's help), or <see langword="null"/>.</param>
/// <param name="Indent">Whether the line sits under its heading.</param>
/// <param name="Speak">The line id of the recording the row's speaker button plays (S20b), or <see langword="null"/> for no button.</param>
/// <param name="Practise">The id of the completed lesson the row's "Practise again" button starts (S26), or <see langword="null"/> for no button.</param>
/// <param name="Verb">The infinitive of the verb the row lists on the verbs page (S28): choosing the row shows its conjugation table on the right page, or <see langword="null"/>.</param>
/// <param name="Next">Whether the line is the title of the lesson the objective sends the learner to (the lessons page's <c>next:</c> lesson, V21b): the page opens scrolled to it.</param>
public sealed record BookLine(
    BookTone Tone,
    string Text,
    string? Aside = null,
    string? Right = null,
    string? Lemma = null,
    int? Pips = null,
    bool? Tick = null,
    IReadOnlyList<bool>? Lamps = null,
    bool Indent = false,
    string? Speak = null,
    string? Practise = null,
    string? Verb = null,
    bool Next = false)
{
    /// <summary>The empty line between two entries.</summary>
    public static BookLine Gap { get; } = new(BookTone.Hint, string.Empty);
}
