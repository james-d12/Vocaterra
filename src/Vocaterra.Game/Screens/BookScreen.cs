using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Input;
using Vocaterra.Game.UI;
using Vocaterra.Game.UI.Book;

namespace Vocaterra.Game.Screens;

/// <summary>
/// The book (S20, S20b, S26): J or the book item opens it, Esc or J closes it; its pages turn with Tab and the arrows, its lists scroll with the
/// keys and the wheel, a click on a tab turns to that page, the rows with a speaker button play their recordings, and "Practise again" starts a
/// lesson over. The book's state is <see cref="BookModel"/>; the book takes the keyboard while it is open.
/// </summary>
public sealed class BookScreen : IScreen
{
    private readonly BookModel _book;
    private readonly BookSpeechControls _controls;
    private readonly InputFocus _focus;
    private readonly VoxelSession _session;
    private readonly BookPage? _startPage;
    private readonly string? _startVerb;
    private readonly int _detailLines;
    private readonly bool _scripted;
    private readonly ToastQueue _toasts;
    private readonly SoundFrontend _sound;
    private readonly UiCanvas _canvas;
    private readonly Action _closeDialogue;
    private readonly Action<string> _console;

    /// <summary>Creates the book's behaviour.</summary>
    /// <param name="book">The book's state.</param>
    /// <param name="controls">The rows' speaker buttons, the verb tables and the practise buttons.</param>
    /// <param name="focus">Where the keyboard goes (the book takes it while open).</param>
    /// <param name="session">The session whose words and lessons the book shows.</param>
    /// <param name="startPage">The page a scripted run opens on (<c>--book-page</c>), or <see langword="null"/>.</param>
    /// <param name="startVerb">The verb whose table a scripted run opens (<c>--book-verb</c>), or <see langword="null"/>.</param>
    /// <param name="detailLines">The lines a scripted run scrolls the right page down (<c>--book-detail</c>).</param>
    /// <param name="scripted">Whether a script drives the run (it asks for the journal itself).</param>
    /// <param name="toasts">Where "Practise again" says where to go.</param>
    /// <param name="sound">Plays the clicks.</param>
    /// <param name="canvas">The interface canvas the book is laid out on.</param>
    /// <param name="closeDialogue">Closes the dialogue panel when it is open.</param>
    /// <param name="console">Where the book's lines go.</param>
    public BookScreen(
        BookModel book,
        BookSpeechControls controls,
        InputFocus focus,
        VoxelSession session,
        BookPage? startPage,
        string? startVerb,
        int detailLines,
        bool scripted,
        ToastQueue toasts,
        SoundFrontend sound,
        UiCanvas canvas,
        Action closeDialogue,
        Action<string> console)
    {
        _book = book;
        _controls = controls;
        _focus = focus;
        _session = session;
        _startPage = startPage;
        _startVerb = startVerb;
        _detailLines = detailLines;
        _scripted = scripted;
        _toasts = toasts;
        _sound = sound;
        _canvas = canvas;
        _closeDialogue = closeDialogue;
        _console = console;
    }

    /// <inheritdoc />
    public string Name => "book";

    /// <inheritdoc />
    public bool IsOpen => _book.IsOpen;

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>Opens the book (J, or the book item); a scripted run's <c>--book-page</c>, <c>--book-verb</c> and <c>--book-detail</c> apply.</summary>
    public void Open()
    {
        _book.Open(_session.Words);
        if (_startPage is { } start)
        {
            _book.Show(start, animate: false);
        }

        if (_startVerb is { } verb)
        {
            _console(_controls.SelectVerb(verb) ? $"book: the table of {verb}" : $"book: no verb '{verb}' in the book");
        }

        if (_detailLines > 0)
        {
            _book.ScrollDetail(_detailLines * BookGeometry.LineHeight); // a scripted run's right page, scrolled down (X03)
        }

        _focus.BookOpen = true;
    }

    /// <summary>Closes the book.</summary>
    public void Close()
    {
        _book.Close();
        _focus.BookOpen = false;
    }

    /// <summary>
    /// The end of the lesson's recap (S17): the panel closes and the journal opens on its phrases page, where each phrase the lesson taught is listed
    /// with the help it has now. A scripted run goes on with its steps and asks for the journal itself (an overlay would cover its screenshots).
    /// </summary>
    public void ShowPhrases()
    {
        if (_scripted)
        {
            return;
        }

        _closeDialogue();
        _book.Open(_session.Words);
        _book.Show(BookPage.Phrases);
        _focus.BookOpen = true;
    }

    /// <summary>
    /// The book's keys: Escape or J closes it; Tab (Shift and Tab turns back), Left and Right turn the page; Page Up and Page Down scroll. Up and Down
    /// move the selection among the rows with a speaker button (the selection is kept on screen); a page with none scrolls a line. Enter plays the
    /// selected row, Shift+Enter its slow recording; P practises the first lesson on offer.
    /// </summary>
    public void HandleInput(InputSnapshot input)
    {
        if (input.Pressed(Keys.Escape) || input.Pressed(Keys.J))
        {
            Close();
            return;
        }

        if (input.Pressed(Keys.Tab))
        {
            _book.Turn(forward: !input.Shift);
        }
        else if (input.Pressed(Keys.Left))
        {
            _book.Turn(forward: false);
        }
        else if (input.Pressed(Keys.Right))
        {
            _book.Turn(forward: true);
        }

        var geometry = new BookGeometry(_canvas.Width, _canvas.Height);
        if (input.Shift && (input.Pressed(Keys.Down) || input.Pressed(Keys.Up)))
        {
            // Shift with Up or Down scrolls the right page a line (X03); Shift with Page Up or Page Down, a page of it.
            _book.ScrollDetail(input.Pressed(Keys.Down) ? BookGeometry.LineHeight : -BookGeometry.LineHeight);
        }
        else if (input.Shift && (input.Pressed(Keys.PageDown) || input.Pressed(Keys.PageUp)))
        {
            _book.ScrollDetail(input.Pressed(Keys.PageDown) ? geometry.DetailHeight : -geometry.DetailHeight);
        }
        else if (input.Pressed(Keys.PageDown))
        {
            _book.Scroll(geometry.LinesVisible);
        }
        else if (input.Pressed(Keys.PageUp))
        {
            _book.Scroll(-geometry.LinesVisible);
        }
        else if (input.Pressed(Keys.Down) || input.Pressed(Keys.Up))
        {
            _controls.Move(input.Pressed(Keys.Down) ? 1 : -1, geometry.LinesVisible);
        }
        else if (input.Pressed(Keys.Enter))
        {
            _ = _controls.Enter(input.Shift);
        }
        else if (input.Pressed(Keys.P) && _controls.PractiseFirst(geometry) is { } practise)
        {
            PractiseAgain(practise);
        }
    }

    /// <summary>
    /// The book's mouse: the wheel scrolls the right page when the pointer is over it (X03) and the list otherwise (three lines a notch, up for a
    /// turn up); a click on a tab turns to that page, and a click on a row's speaker, a verb or a practise button presses it.
    /// </summary>
    public void HandleMouse(InputSnapshot input)
    {
        var geometry = new BookGeometry(_canvas.Width, _canvas.Height);
        var (x, y) = (input.MouseState.X, input.MouseState.Y);
        if (input.Wheel != 0)
        {
            var notchLines = -(int)Math.Round(input.Wheel / 40.0);
            if (geometry.RightPage.Contains(x, y))
            {
                _book.ScrollDetail(notchLines * BookGeometry.LineHeight);
            }
            else
            {
                _book.Scroll(notchLines);
            }
        }

        if (!input.LeftClicked)
        {
            return;
        }

        if (geometry.TabAt(x, y) is { } tab)
        {
            _book.Show(tab);
            _sound.QueueClick(SoundCue.UiTab);
        }
        else if (_controls.Click(geometry, x, y, input.Shift) || _controls.ClickVerb(geometry, x, y))
        {
            _sound.QueueClick(SoundCue.UiClick);
        }
        else if (_controls.ClickPractise(geometry, x, y) is { } practise)
        {
            _sound.QueueClick(SoundCue.UiClick);
            PractiseAgain(practise);
        }
    }

    /// <summary>The scripted run's book step (S26, <c>--book-practise</c>): opens the book on its Lessons page, then presses the lesson's button.</summary>
    public void PractiseFromScript(string lessonId)
    {
        if (!_book.IsOpen)
        {
            Open();
        }

        _book.Show(BookPage.Lessons);
        if (_controls.Practise(lessonId) is { } asked)
        {
            PractiseAgain(asked);
        }
        else
        {
            _console($"book: '{lessonId}' cannot be practised again (it is not a completed lesson, or another lesson is under way)");
            Close();
        }
    }

    /// <summary>The scripted run's book step (S20b, <c>--book-play</c> and <c>--book-play-slow</c>): opens the book, then plays the target's row as Enter does.</summary>
    public void PlayTarget(string target, bool slow)
    {
        if (!_book.IsOpen)
        {
            Open();
        }

        _console(_controls.PlayTarget(target, slow));
    }

    // "Practise again" (S26): the lesson starts when the learner talks to its NPC. The book closes, the objective area says where to go and the beacon points there.
    private void PractiseAgain(string lessonId)
    {
        Close();
        var line = _session.Lessons.ReplayHud ?? lessonId;
        _toasts.Show(line);
        _console($"book: practise again {lessonId}; {line}");
    }
}
