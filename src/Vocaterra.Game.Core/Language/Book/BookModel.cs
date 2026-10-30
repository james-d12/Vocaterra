using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Language.Book;

/// <summary>
/// The book (J): which page is on screen, whether it is open, and the scroll. It takes the keyboard while it is open (the player does not walk).
/// It is opened by J and by using the book item (both call <see cref="Open"/>), so the two open the same book. Plain values; no clock.
/// </summary>
public sealed class BookModel
{
    private WordProgress? _words;
    private bool _read;
    private int _selected;
    private (BookPage Page, int Line)? _detailKey;

    /// <summary>Whether the book is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The page on screen (the nouns page opens first).</summary>
    public BookPage Page { get; private set; } = BookPage.Nouns;

    /// <summary>The first line on screen of the page (lines, not pixels).</summary>
    public int Offset { get; private set; }

    /// <summary>How far the right page is scrolled, in pixels (X03). It moves in whole lines and resets when the selection or the page changes.</summary>
    public int DetailOffset { get; private set; }

    /// <summary>How long a page turn lasts, in seconds (S28).</summary>
    public const double TurnSeconds = 0.3;

    /// <summary>Whether a page turn is under way (S28): the leaf is drawn while it is.</summary>
    public bool IsTurning { get; private set; }

    /// <summary>How far the page turn has gone, 0 to 1 (0 when none is under way).</summary>
    public double TurnProgress { get; private set; }

    /// <summary>Whether the turn under way goes to a later page (the leaf lifts from the right).</summary>
    public bool TurnForward { get; private set; }

    /// <summary>Moves the page turn on by some seconds of the frame clock (no wall clock here); it ends at the full duration.</summary>
    /// <param name="seconds">The seconds since the last frame.</param>
    public void Advance(double seconds)
    {
        if (!IsTurning)
        {
            return;
        }

        TurnProgress += Math.Max(0, seconds) / TurnSeconds;
        if (TurnProgress >= 1)
        {
            FinishTurn();
        }
    }

    /// <summary>Ends a page turn at once (a scripted screenshot run does not wait for the leaf).</summary>
    public void FinishTurn()
    {
        IsTurning = false;
        TurnProgress = 0;
    }

    /// <summary>
    /// Opens the book on its nouns page. Each seen noun's label counts as read once per opening, when the nouns page is shown (the research's "shown
    /// for a second" rule, with the opening as the unit of time, so no clock is needed).
    /// </summary>
    /// <param name="words">The learner's word progress.</param>
    public void Open(WordProgress words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        FinishTurn();
        _words = words;
        _read = false;
        Page = BookPage.Nouns;
        Offset = 0;
        _selected = 0;
        DetailOffset = 0;
        _detailKey = null;
        MarkRead();
    }

    /// <summary>Shows a page. The nouns page counts the seen labels as read, once per opening; the scroll goes back to the top.</summary>
    /// <param name="page">The page.</param>
    /// <param name="animate">Whether a changed page starts the page turn (the opening page of a scripted run does not).</param>
    public void Show(BookPage page, bool animate = true)
    {
        if (!IsOpen)
        {
            return;
        }

        if (page != Page)
        {
            Offset = 0;
            _selected = 0;
            DetailOffset = 0;
            if (animate)
            {
                IsTurning = true;
                TurnProgress = 0;
                TurnForward = (int)page > (int)Page;
            }
        }

        Page = page;
        MarkRead();
    }

    /// <summary>Turns to the next page (Tab, Right) or the previous one (Left, Shift+Tab).</summary>
    /// <param name="forward">Whether to go forward.</param>
    public void Turn(bool forward)
    {
        Show(BookTabs.Turn(Page, forward));
        if (IsTurning)
        {
            TurnForward = forward; // Tab on the last page wraps to the first: the leaf still lifts the way the key says
        }
    }

    /// <summary>Moves the scroll by some lines (the wheel up is negative in the layer's terms: pass the lines to go down).</summary>
    /// <param name="lines">The lines to go down (negative: up).</param>
    public void Scroll(int lines)
    {
        if (IsOpen)
        {
            Offset = Math.Max(0, Offset + lines);
        }
    }

    /// <summary>
    /// Scrolls the right page by some pixels (X03): the wheel over that page, and Shift with Up, Down, Page Up or Page Down. Down is positive. It stays at zero or
    /// more here; the layer keeps it inside the detail's height (<see cref="ClampDetail"/>).
    /// </summary>
    /// <param name="pixels">The pixels to go down (negative: up).</param>
    public void ScrollDetail(int pixels)
    {
        if (IsOpen)
        {
            DetailOffset = Math.Max(0, DetailOffset + pixels);
        }
    }

    /// <summary>Keeps the right page's scroll inside its detail, once the layer knows how tall the detail is and how much of the page it shows.</summary>
    /// <param name="content">The detail's height, in pixels.</param>
    /// <param name="view">The height of the page's writing, in pixels.</param>
    public void ClampDetail(int content, int view) => DetailOffset = DetailScroll.Clamp(DetailOffset, content, view);

    /// <summary>
    /// Tells the right page which line it details this frame (the selected row, or the first line on screen when none is selected). A change of that line or of the
    /// page resets the scroll, so a new item always starts at its top. The first call after opening keeps the scroll the open set.
    /// </summary>
    /// <param name="line">The line the detail is of.</param>
    public void KeepDetail(int line)
    {
        var key = (Page, line);
        if (_detailKey is { } previous && previous != key)
        {
            DetailOffset = 0;
        }

        _detailKey = key;
    }

    /// <summary>Keeps the scroll inside a page's lines, once the layer knows how many there are and how many fit.</summary>
    /// <param name="total">The page's lines.</param>
    /// <param name="visible">The lines on screen.</param>
    public void ClampTo(int total, int visible) => Offset = BookScroll.Clamp(Offset, total, visible);

    /// <summary>
    /// The selected row among the page's speakable rows (S20b): the first one until the learner moves the selection; <c>-1</c> when the page has none.
    /// The index is kept inside <c>[0, count - 1]</c>.
    /// </summary>
    /// <param name="count">How many speakable rows the page has.</param>
    public int SelectedIndex(int count) => count == 0 ? -1 : Math.Clamp(_selected, 0, count - 1);

    /// <summary>Selects a speakable row by its index (a scripted run's book step, S20b).</summary>
    /// <param name="index">The row's index among the speakable rows.</param>
    public void SelectRow(int index)
    {
        if (IsOpen)
        {
            var chosen = Math.Max(0, index);
            if (chosen != _selected)
            {
                DetailOffset = 0;
            }

            _selected = chosen;
        }
    }

    /// <summary>Moves the selection among the speakable rows (Up is negative, Down positive), stopping at the first and the last.</summary>
    /// <param name="delta">The steps.</param>
    /// <param name="count">How many speakable rows the page has.</param>
    public void MoveSelection(int delta, int count)
    {
        if (!IsOpen || count == 0)
        {
            return;
        }

        var moved = Math.Clamp(SelectedIndex(count) + delta, 0, count - 1);
        if (moved != _selected)
        {
            DetailOffset = 0;
        }

        _selected = moved;
    }

    /// <summary>Scrolls, if needed, so that a line is on screen (the selected row stays visible when the learner moves it).</summary>
    /// <param name="line">The line's index.</param>
    /// <param name="visible">The lines on screen.</param>
    public void Reveal(int line, int visible)
    {
        if (!IsOpen || visible <= 0)
        {
            return;
        }

        if (line < Offset)
        {
            Offset = line;
        }
        else if (line >= Offset + visible)
        {
            Offset = line - visible + 1;
        }
    }

    /// <summary>Closes the book.</summary>
    public void Close()
    {
        IsOpen = false;
        FinishTurn();
    }

    private void MarkRead()
    {
        if (Page != BookPage.Nouns || _read || _words is null)
        {
            return;
        }

        _read = true;
        foreach (var row in JournalRows.Build(_words, unseen: string.Empty)) // only which nouns were seen is read here, not their labels
        {
            if (row.Seen)
            {
                _words.MarkRead(row.Lemma);
            }
        }
    }
}
