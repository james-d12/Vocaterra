using Vocaterra.Game;

namespace Vocaterra.MonoGame.UI.Book;

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
    public IReadOnlyList<BookLine> Lines => BookPages.ListFor(_session, _book.Page, _recordings);

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

    /// <summary>The scripted run's step (<c>--book-verb</c>, S28): selects a verb of the verbs page, so the right page shows its table. Returns whether the verb is in the book.</summary>
    /// <param name="lemma">The verb's infinitive.</param>
    public bool SelectVerb(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        _book.Show(BookPage.Verbs, animate: false);
        var rows = BookSpeakers.Selectable(Lines);
        var lines = Lines;
        for (var i = 0; i < rows.Count; i++)
        {
            if (string.Equals(lines[rows[i]].Verb, lemma, StringComparison.Ordinal))
            {
                _book.SelectRow(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A click on a verb's row of the verbs page (S28) selects it, so the right page shows its table. Returns whether the click chose a verb.
    /// </summary>
    /// <param name="geometry">The book's geometry at the window's size.</param>
    /// <param name="x">The x of the click.</param>
    /// <param name="y">The y of the click.</param>
    public bool ClickVerb(BookGeometry geometry, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (_book.Page != BookPage.Verbs || x < geometry.ContentLeft || x >= geometry.ContentRight || y < geometry.ContentTop)
        {
            return false;
        }

        var lines = Lines;
        var offset = BookScroll.Clamp(_book.Offset, lines.Count, geometry.LinesVisible);
        var line = offset + ((y - geometry.ContentTop) / BookGeometry.LineHeight);
        if (line >= lines.Count || line >= offset + geometry.LinesVisible)
        {
            return false;
        }

        var rows = BookSpeakers.Selectable(lines);
        var index = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i] == line)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            return false;
        }

        _book.SelectRow(index);
        return true;
    }

    /// <summary>
    /// A click at a point on a lesson's "Practise again" button (S26). Returns the lesson that was asked to be practised again, or <see langword="null"/> when the point
    /// is on no such button (or the lesson refused: it was never completed).
    /// </summary>
    /// <param name="geometry">The book's geometry at the window's size.</param>
    /// <param name="x">The x of the click.</param>
    /// <param name="y">The y of the click.</param>
    public string? ClickPractise(BookGeometry geometry, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var lines = Lines;
        var offset = BookScroll.Clamp(_book.Offset, lines.Count, geometry.LinesVisible);
        return BookPractise.At(geometry, lines, offset, x, y) is { } lesson ? Practise(lesson) : null;
    }

    /// <summary>The P key (S26): "Practise again" on the first lesson with a button on screen. Returns the lesson asked for, or <see langword="null"/>.</summary>
    /// <param name="geometry">The book's geometry.</param>
    public string? PractiseFirst(BookGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var lines = Lines;
        var offset = BookScroll.Clamp(_book.Offset, lines.Count, geometry.LinesVisible);
        return BookPractise.FirstOnScreen(geometry, lines, offset) is { } lesson ? Practise(lesson) : null;
    }

    /// <summary>
    /// Asks to practise a lesson again (S26): it starts when the learner talks to its NPC. Returns the lesson id when the lesson accepted, or <see langword="null"/> when
    /// it was never completed, is unknown or another lesson is under way.
    /// </summary>
    /// <param name="lessonId">The lesson's id.</param>
    public string? Practise(string lessonId)
    {
        ArgumentNullException.ThrowIfNull(lessonId);
        return _session.Lessons.RequestReplay(lessonId) ? lessonId : null;
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
        var rows = BookSpeakers.Selectable(Lines);
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
