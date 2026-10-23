using Vocaterra.Game;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The book's speaker controls (S20b): a click on a row's button, Enter and Shift+Enter on the selected row, Up and Down among the rows that have a
/// button, and the scripted run's <c>--book-play</c>. The game forwards its input here, so the tests drive the same code. Engine free.
/// </summary>
public sealed class BookSpeechControls
{
    private readonly BookModel _book;
    private readonly BookSpeaker _speaker;
    private readonly GuideSession _session;
    private readonly BookRecordings _recordings;

    /// <summary>Creates the controls over the book, the speaker, the session and the recordings.</summary>
    /// <param name="book">The book.</param>
    /// <param name="speaker">The speaker (the voice and the sound log).</param>
    /// <param name="session">The session the pages are built from.</param>
    /// <param name="recordings">Which recording each row plays.</param>
    public BookSpeechControls(BookModel book, BookSpeaker speaker, GuideSession session, BookRecordings recordings)
    {
        _book = book ?? throw new ArgumentNullException(nameof(book));
        _speaker = speaker ?? throw new ArgumentNullException(nameof(speaker));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _recordings = recordings ?? throw new ArgumentNullException(nameof(recordings));
    }

    /// <summary>The lines of the page on screen.</summary>
    public IReadOnlyList<BookLine> Lines => BookPages.ForPage(_session, _book.Page, _recordings);

    /// <summary>
    /// A click at a point. A click on a row's speaker button selects that row and plays it (Shift: the slow recording). Returns whether the click was on a
    /// button (played or not); a click elsewhere returns false and changes nothing here.
    /// </summary>
    /// <param name="geometry">The book's geometry at the window's size.</param>
    /// <param name="x">The x of the click.</param>
    /// <param name="y">The y of the click.</param>
    /// <param name="slow">Whether Shift is held.</param>
    public bool Click(BookGeometry geometry, int x, int y, bool slow)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var lines = Lines;
        var offset = BookScroll.Clamp(_book.Offset, lines.Count, geometry.LinesVisible);
        if (BookSpeakers.At(geometry, lines, offset, x, y) is not { } index)
        {
            return false;
        }

        _book.SelectRow(index);
        _ = _speaker.Speak(lines, index, slow);
        return true;
    }

    /// <summary>Enter (Shift+Enter: the slow recording) plays the selected row; the first row when none has been chosen. Returns whether a clip was accepted.</summary>
    /// <param name="slow">Whether Shift is held.</param>
    public bool Enter(bool slow)
    {
        var lines = Lines;
        var index = _book.SelectedIndex(BookSpeakers.Speakable(lines).Count);
        return index >= 0 && _speaker.Speak(lines, index, slow);
    }

    /// <summary>
    /// Up (<paramref name="delta"/> -1) and Down (+1): moves the selection among the rows with a button and keeps the selected row on screen. A page with
    /// no such row scrolls a line instead.
    /// </summary>
    /// <param name="delta">The direction: -1 or 1.</param>
    /// <param name="visible">The lines on screen.</param>
    public void Move(int delta, int visible)
    {
        var rows = BookSpeakers.Speakable(Lines);
        if (rows.Count == 0)
        {
            _book.Scroll(delta);
            return;
        }

        _book.MoveSelection(delta, rows.Count);
        _book.Reveal(rows[_book.SelectedIndex(rows.Count)], visible);
    }

    /// <summary>
    /// The scripted run's step (<c>--book-play</c>, S20b): shows the page of the target (a noun, or <c>phrase:&lt;id&gt;</c>), selects its row and plays it,
    /// as Enter does. Returns the line the console prints.
    /// </summary>
    /// <param name="target">The noun's lemma or the phrase's id with its prefix.</param>
    /// <param name="slow">Whether to play the slow recording.</param>
    public string PlayTarget(string target, bool slow)
    {
        ArgumentNullException.ThrowIfNull(target);
        var recording = _recordings.ForTarget(target);
        if (recording is null)
        {
            return $"book: no recording for '{target}'";
        }

        _book.Show(target.StartsWith(BookRecordings.PhraseTargetPrefix, StringComparison.Ordinal) ? BookPage.Phrases : BookPage.Nouns);
        var lines = Lines;
        var rows = BookSpeakers.Speakable(lines);
        var index = -1;
        for (var i = 0; i < rows.Count && index < 0; i++)
        {
            if (lines[rows[i]].Speak == recording)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            return $"book: '{target}' has no button on its page (not met yet)";
        }

        _book.SelectRow(index);
        return _speaker.Speak(lines, index, slow)
            ? $"book: play {recording}{(slow ? " (slow)" : string.Empty)}"
            : $"book: {recording} is not played (muted, or no voice)";
    }
}
