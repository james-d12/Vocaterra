namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// One line of a book page (S20, U07). A page is a list of these. An <see cref="BookLineKind.Entry"/> is a row of the left list that can be selected; the
/// <see cref="BookLineKind.Detail"/> lines after it are its block, which the right page shows. Sections, gaps, notes and rows of tiles shape the list. Plain values.
/// </summary>
/// <param name="Tone">The colour family.</param>
/// <param name="Text">The text, in English or Spanish as the learner reads it.</param>
/// <param name="Aside">Text after <paramref name="Text"/> in the meta colour: a phrase's English, a lesson's state.</param>
/// <param name="Right">Text at the right edge (a section's count, a verb's use, where a line was said).</param>
/// <param name="Lemma">The noun whose icon the line shows at its left, or <see langword="null"/>.</param>
/// <param name="Pips">The three progress pips at the right (catalogue nouns), or <see langword="null"/>.</param>
/// <param name="Mark">A mark at the left (a lesson's state, how a line the learner said was taken), or <see langword="null"/> for none.</param>
/// <param name="Lamps">Small boxes at the right, lit or not (the card and the audio of a phrase's help), or <see langword="null"/>.</param>
/// <param name="Speak">The line id of the recording the row's speaker button plays (S20b), or <see langword="null"/> for no button.</param>
/// <param name="Practise">The id of the completed lesson the row's "Practise again" button starts (S26), or <see langword="null"/> for no button.</param>
/// <param name="Verb">The infinitive of the verb the row lists on the verbs page (S28): choosing the row shows its conjugation table on the right page, or <see langword="null"/>.</param>
/// <param name="Next">Whether the line is the lesson the objective sends the learner to (the lessons page's <c>next:</c> lesson, V21b): the page opens scrolled to it.</param>
/// <param name="Ask">Whether the row's button asks for a class (L01: "Ask for a class") rather than to practise a finished lesson again.</param>
/// <param name="Kind">What the line is.</param>
/// <param name="Role">For a <see cref="BookLineKind.Detail"/> line, what it is on the right page.</param>
/// <param name="Stats">The numbers of a <see cref="BookDetailRole.Stats"/> line.</param>
/// <param name="Lemmas">The nouns of a <see cref="BookLineKind.Tiles"/> row, whose icons it shows in grey.</param>
/// <param name="Marks">The marks of a <see cref="BookDetailRole.Units"/> line, one per unit.</param>
public sealed record BookLine(
    BookTone Tone,
    string Text,
    string? Aside = null,
    string? Right = null,
    string? Lemma = null,
    int? Pips = null,
    BookMark? Mark = null,
    IReadOnlyList<bool>? Lamps = null,
    string? Speak = null,
    string? Practise = null,
    string? Verb = null,
    bool Next = false,
    bool Ask = false,
    BookLineKind Kind = BookLineKind.Entry,
    BookDetailRole Role = BookDetailRole.Text,
    IReadOnlyList<BookStat>? Stats = null,
    IReadOnlyList<string>? Lemmas = null,
    IReadOnlyList<BookMark>? Marks = null)
{
    /// <summary>How many icons one row of tiles holds.</summary>
    public const int TilesPerRow = 12;

    /// <summary>The empty line between two sections.</summary>
    public static BookLine Gap { get; } = new(BookTone.Hint, string.Empty, Kind: BookLineKind.Gap);

    /// <summary>Whether the line belongs to the left list (everything but a <see cref="BookLineKind.Detail"/>).</summary>
    public bool InList => Kind != BookLineKind.Detail;
}
