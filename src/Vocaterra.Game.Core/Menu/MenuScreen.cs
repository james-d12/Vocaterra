using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Screens;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The menu (the menu issue): Esc in the world opens it over the game, which it pauses the way the other panels do. Its main page has Resume, Settings, Controls and Exit; the
/// settings page lists the <see cref="MenuRow"/>s (Left and Right change a row, Enter or Space flips or runs it); the controls page scrolls the control table. Esc goes back a page, and
/// closes the menu from the main page (or from a page that <c>--controls</c> or <c>--settings</c> opened directly). Up and Down choose, the mouse clicks, drags a slider and scrolls with the wheel. Exit
/// asks the front-end to save and quit. All state is here; the front-end only draws it.
/// </summary>
public sealed class MenuScreen : IScreen
{
    private readonly IReadOnlyList<MenuRow> _rows;
    private readonly IMenuMetrics _metrics;
    private readonly Action _exit;
    private bool _direct;
    private int? _dragging;
    private (int X, int Y)? _lastPointer;

    /// <summary>Creates the menu.</summary>
    /// <param name="settingsRows">The rows of the settings page.</param>
    /// <param name="metrics">How the page is drawn: what the pointer is on and how much fits.</param>
    /// <param name="exit">Saves and closes the game (the Exit button).</param>
    public MenuScreen(IReadOnlyList<MenuRow> settingsRows, IMenuMetrics metrics, Action exit)
    {
        ArgumentNullException.ThrowIfNull(settingsRows);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(exit);
        _rows = settingsRows;
        _metrics = metrics;
        _exit = exit;
        SettingsSelected = FirstSelectable();
    }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public string Name => "menu (Esc)";

    /// <inheritdoc />
    public bool FreesMouse => true;

    /// <summary>The page shown.</summary>
    public MenuPage Page { get; private set; }

    /// <summary>The chosen button of the main page.</summary>
    public MenuItem MainSelected { get; private set; }

    /// <summary>The chosen row of the settings page (an index into <see cref="Rows"/>).</summary>
    public int SettingsSelected { get; private set; }

    /// <summary>The first settings row shown.</summary>
    public int SettingsScroll { get; private set; }

    /// <summary>The first line of the controls page shown.</summary>
    public int ControlsScroll { get; private set; }

    /// <summary>The settings page's rows.</summary>
    public IReadOnlyList<MenuRow> Rows => _rows;

    /// <summary>The row under the pointer, or <see langword="null"/> (the pointer is drawn as a highlight).</summary>
    public int? Hover { get; private set; }

    /// <summary>Opens the menu on its main page, on Resume.</summary>
    public void Open() => Show(MenuPage.Main, direct: false);

    /// <summary>Opens the menu on the controls page (<c>--controls</c>); Esc then closes it.</summary>
    public void OpenControls() => Show(MenuPage.Controls, direct: true);

    /// <summary>Opens the menu on the settings page (<c>--settings</c>); Esc then closes it.</summary>
    public void OpenSettings() => Show(MenuPage.Settings, direct: true);

    /// <summary>Scrolls the page shown down by <paramref name="rows"/> rows, within what it allows (a scripted run's way to show the lower rows).</summary>
    /// <param name="rows">The rows.</param>
    public void ScrollTo(int rows)
    {
        if (Page == MenuPage.Settings)
        {
            SettingsScroll = Math.Clamp(rows, 0, MaxSettingsScroll());
        }
        else if (Page == MenuPage.Controls)
        {
            ControlsScroll = Math.Clamp(rows, 0, _metrics.ControlsMaxScroll);
        }
    }

    /// <summary>Closes the menu.</summary>
    public void Close()
    {
        IsOpen = false;
        _dragging = null;
        Hover = null;
    }

    /// <inheritdoc />
    public void HandleInput(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Pressed(GameKey.Escape))
        {
            GoBack();
            return;
        }

        HandleMouse(input);
        if (!IsOpen)
        {
            return;
        }

        switch (Page)
        {
            case MenuPage.Main:
                KeysOnMain(input);
                break;
            case MenuPage.Settings:
                KeysOnSettings(input);
                break;
            default:
                KeysOnControls(input);
                break;
        }
    }

    private static int Move(IInputFrame input) => (input.Pressed(GameKey.Down) ? 1 : 0) - (input.Pressed(GameKey.Up) ? 1 : 0);

    private void Show(MenuPage page, bool direct)
    {
        IsOpen = true;
        Page = page;
        _direct = direct;
        _dragging = null;
        Hover = null;
        _lastPointer = null;
        MainSelected = page switch { MenuPage.Settings => MenuItem.Settings, MenuPage.Controls => MenuItem.Controls, _ => MenuItem.Resume };
        SettingsSelected = FirstSelectable();
        SettingsScroll = 0;
        ControlsScroll = 0;
    }

    private void GoBack()
    {
        if (Page == MenuPage.Main || _direct)
        {
            Close();
            return;
        }

        Page = MenuPage.Main;
        _dragging = null;
    }

    private void KeysOnMain(IInputFrame input)
    {
        var count = Enum.GetValues<MenuItem>().Length;
        var step = Move(input);
        if (step != 0)
        {
            MainSelected = (MenuItem)(((int)MainSelected + step + count) % count);
        }

        if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
        {
            Choose(MainSelected);
        }
    }

    private void Choose(MenuItem item)
    {
        MainSelected = item;
        switch (item)
        {
            case MenuItem.Resume:
                Close();
                break;
            case MenuItem.Settings:
                Page = MenuPage.Settings;
                SettingsSelected = FirstSelectable();
                SettingsScroll = 0;
                break;
            case MenuItem.Controls:
                Page = MenuPage.Controls;
                ControlsScroll = 0;
                break;
            default:
                _exit();
                break;
        }
    }

    private void KeysOnSettings(IInputFrame input)
    {
        var step = Move(input);
        if (step != 0)
        {
            SelectNext(step);
        }

        if (input.Pressed(GameKey.PageDown))
        {
            SelectBy(Math.Max(1, _metrics.SettingsCapacity - 1));
        }
        else if (input.Pressed(GameKey.PageUp))
        {
            SelectBy(-Math.Max(1, _metrics.SettingsCapacity - 1));
        }

        var row = _rows[SettingsSelected];
        var change = (input.Pressed(GameKey.Right) || input.Pressed(GameKey.OemPlus) ? 1 : 0) - (input.Pressed(GameKey.Left) || input.Pressed(GameKey.OemMinus) ? 1 : 0);
        if (change != 0)
        {
            row.Change(change);
        }
        else if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
        {
            row.Activate();
        }
    }

    private void KeysOnControls(IInputFrame input)
    {
        var step = Move(input) + (input.Pressed(GameKey.PageDown) ? 5 : 0) - (input.Pressed(GameKey.PageUp) ? 5 : 0);
        ControlsScroll = Math.Clamp(ControlsScroll + step, 0, _metrics.ControlsMaxScroll); // the wheel is the mouse handler's
    }

    private void HandleMouse(IInputFrame input)
    {
        var pointer = (input.PointerX, input.PointerY);
        var moved = _lastPointer != pointer;
        _lastPointer = pointer;
        if (_dragging is { } dragged)
        {
            if (!input.LeftDown)
            {
                _dragging = null;
            }
            else
            {
                _rows[dragged].SetFraction(_metrics.TrackFraction(SettingsScroll, dragged, input.PointerX));
            }

            return;
        }

        if (Page == MenuPage.Settings && input.Wheel != 0)
        {
            SettingsScroll = Math.Clamp(SettingsScroll - ((int)Math.Round(input.Wheel / 120.0) * 2), 0, MaxSettingsScroll());
        }
        else if (Page == MenuPage.Controls && input.Wheel != 0)
        {
            ControlsScroll = Math.Clamp(ControlsScroll - ((int)Math.Round(input.Wheel / 120.0) * 3), 0, _metrics.ControlsMaxScroll);
        }

        var hit = _metrics.HitAt(Page, SettingsScroll, input.PointerX, input.PointerY);
        Hover = hit is { IsBack: false } over ? over.Index : null;
        if (hit is { IsBack: false } hovered && moved)
        {
            Select(hovered.Index);
        }

        if (!input.LeftClicked || hit is not { } clicked)
        {
            return;
        }

        if (clicked.IsBack)
        {
            GoBack();
            return;
        }

        if (Page == MenuPage.Main)
        {
            Choose((MenuItem)clicked.Index);
            return;
        }

        if (Page != MenuPage.Settings)
        {
            return;
        }

        SettingsSelected = clicked.Index;
        var row = _rows[clicked.Index];
        if (row.Kind == MenuRowKind.Slider)
        {
            if (clicked.OnTrack)
            {
                row.SetFraction(_metrics.TrackFraction(SettingsScroll, clicked.Index, input.PointerX));
                _dragging = clicked.Index;
            }
        }
        else
        {
            row.Activate();
        }
    }

    private void Select(int index)
    {
        if (Page == MenuPage.Main)
        {
            MainSelected = (MenuItem)index;
        }
        else if (Page == MenuPage.Settings && _rows[index].Selectable)
        {
            SettingsSelected = index;
        }
    }

    private int FirstSelectable() => Math.Max(0, _rows.ToList().FindIndex(static row => row.Selectable));

    private int MaxSettingsScroll() => Math.Max(0, _rows.Count - Math.Max(1, _metrics.SettingsCapacity));

    private void SelectNext(int direction)
    {
        var index = SettingsSelected;
        for (var i = 0; i < _rows.Count; i++)
        {
            index = (index + direction + _rows.Count) % _rows.Count;
            if (_rows[index].Selectable)
            {
                break;
            }
        }

        SettingsSelected = index;
        KeepVisible();
    }

    private void SelectBy(int rows)
    {
        var target = Math.Clamp(SettingsSelected + rows, 0, _rows.Count - 1);
        var direction = rows >= 0 ? -1 : 1;
        while (target >= 0 && target < _rows.Count && !_rows[target].Selectable)
        {
            target += direction;
        }

        SettingsSelected = Math.Clamp(target, 0, _rows.Count - 1);
        KeepVisible();
    }

    // The chosen row stays on the page, with the heading above it when it is the first of its section.
    private void KeepVisible()
    {
        var capacity = Math.Max(1, _metrics.SettingsCapacity);
        var top = SettingsSelected > 0 && _rows[SettingsSelected - 1].Kind is MenuRowKind.Heading or MenuRowKind.Subheading ? SettingsSelected - 1 : SettingsSelected;
        if (top < SettingsScroll)
        {
            SettingsScroll = top;
        }
        else if (SettingsSelected >= SettingsScroll + capacity)
        {
            SettingsScroll = SettingsSelected - capacity + 1;
        }

        SettingsScroll = Math.Clamp(SettingsScroll, 0, MaxSettingsScroll());
    }
}
