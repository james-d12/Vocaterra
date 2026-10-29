namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The labels of the book's pages, in tab order. English only (D-game-13).</summary>
public static class BookTabs
{
    /// <summary>The tab labels, indexed by <see cref="BookPage"/>.</summary>
    public static IReadOnlyList<string> Labels { get; } = ["Nouns", "Verbs", "Phrases", "Lessons", "What I said"];

    /// <summary>The title over the pages.</summary>
    public const string Title = "Your book";

    /// <summary>The key hints under the pages.</summary>
    public const string KeyHints = "J or Esc: close the book   Tab, Left, Right or click: turn the page   wheel: scroll   Shift+Up/Down: right page";

    /// <summary>The key hints when the full line does not fit the cover's width (X08): the keys that matter, shorter.</summary>
    public const string KeyHintsShort = "J or Esc: close   Tab or click: turn page   wheel: scroll";

    /// <summary>The label of a page.</summary>
    /// <param name="page">The page.</param>
    public static string LabelOf(BookPage page) => Labels[(int)page];

    /// <summary>The page after <paramref name="page"/> (Tab, Right), or before it (Left, Shift+Tab); the pages wrap round.</summary>
    /// <param name="page">The page.</param>
    /// <param name="forward">Whether to go forward.</param>
    public static BookPage Turn(BookPage page, bool forward)
    {
        var count = Labels.Count;
        var next = ((int)page + (forward ? 1 : count - 1)) % count;
        return (BookPage)next;
    }
}
