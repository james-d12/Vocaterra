using Vocaterra.Game.Core.Content;

namespace Vocaterra.Game.Core.Language.Book;

/// <summary>The labels of the book's pages, in tab order, in the support language (D-game-13; <c>ui.bookTab*</c>, C04).</summary>
public static class BookTabs
{
    /// <summary>The ids of the tab labels (<c>ui.&lt;id&gt;</c>), indexed by <see cref="BookPage"/>.</summary>
    public static IReadOnlyList<string> LabelIds { get; } = ["bookTabNouns", "bookTabVerbs", "bookTabPhrases", "bookTabLessons", "bookTabSaid"];

    /// <summary>The tab labels, indexed by <see cref="BookPage"/>.</summary>
    /// <param name="ui">The support language.</param>
    public static IReadOnlyList<string> Labels(LocaleText ui) => LabelIds.Select(id => Ui(ui).Ui(id)).ToList();

    /// <summary>The title over the pages.</summary>
    /// <param name="ui">The support language.</param>
    public static string Title(LocaleText ui) => Ui(ui).Ui("bookTitle");

    /// <summary>The key hints under the pages.</summary>
    /// <param name="ui">The support language.</param>
    public static string KeyHints(LocaleText ui) => Ui(ui).Ui("bookHints");

    /// <summary>The key hints when the full line does not fit the cover's width (X08): the keys that matter, shorter.</summary>
    /// <param name="ui">The support language.</param>
    public static string KeyHintsShort(LocaleText ui) => Ui(ui).Ui("bookHintsShort");

    /// <summary>The label of a page.</summary>
    /// <param name="ui">The support language.</param>
    /// <param name="page">The page.</param>
    public static string LabelOf(LocaleText ui, BookPage page) => Ui(ui).Ui(LabelIds[(int)page]);

    /// <summary>The page after <paramref name="page"/> (Tab, Right), or before it (Left, Shift+Tab); the pages wrap round.</summary>
    /// <param name="page">The page.</param>
    /// <param name="forward">Whether to go forward.</param>
    public static BookPage Turn(BookPage page, bool forward)
    {
        var count = LabelIds.Count;
        var next = ((int)page + (forward ? 1 : count - 1)) % count;
        return (BookPage)next;
    }

    private static LocaleText Ui(LocaleText ui) => ui ?? throw new ArgumentNullException(nameof(ui));
}
