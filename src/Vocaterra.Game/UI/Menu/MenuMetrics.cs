using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.UI.Menu;

/// <summary>
/// What the menu's state machine asks of the drawn page (<see cref="IMenuMetrics"/>), answered from <see cref="MenuLayout"/> and the controls page's wrapped lines on the canvas
/// as it is now, so a click lands on what is drawn at any text size.
/// </summary>
public sealed class MenuMetrics : IMenuMetrics
{
    private readonly Func<(int Width, int Height)> _canvas;
    private readonly Func<IReadOnlyList<MenuRow>> _rows;
    private readonly Func<MenuLayout, IReadOnlyList<ControlsPageLine>> _lines;

    /// <summary>Creates the metrics.</summary>
    /// <param name="canvas">The canvas's size now.</param>
    /// <param name="rows">The settings page's rows.</param>
    /// <param name="lines">The controls page's lines for a layout.</param>
    public MenuMetrics(Func<(int Width, int Height)> canvas, Func<IReadOnlyList<MenuRow>> rows, Func<MenuLayout, IReadOnlyList<ControlsPageLine>> lines)
    {
        _canvas = canvas;
        _rows = rows;
        _lines = lines;
    }

    /// <inheritdoc />
    public int SettingsCapacity => LayoutOf(MenuPage.Settings).Capacity;

    /// <inheritdoc />
    public int ControlsMaxScroll
    {
        get
        {
            var layout = LayoutOf(MenuPage.Controls);
            return Math.Max(0, _lines(layout).Count - layout.LineCapacity);
        }
    }

    /// <inheritdoc />
    public MenuHit? HitAt(MenuPage page, int settingsScroll, int x, int y)
    {
        var layout = LayoutOf(page);
        if (page != MenuPage.Main && layout.Back.Contains(x, y))
        {
            return MenuHit.Back;
        }

        if (page == MenuPage.Main)
        {
            for (var i = 0; i < Enum.GetValues<MenuItem>().Length; i++)
            {
                if (layout.Button(i).Contains(x, y))
                {
                    return new MenuHit(i);
                }
            }

            return null;
        }

        if (page == MenuPage.Controls)
        {
            return null;
        }

        var rows = _rows();
        for (var shown = 0; shown < layout.Capacity && settingsScroll + shown < rows.Count; shown++)
        {
            var row = layout.Row(shown);
            var index = settingsScroll + shown;
            if (!row.Contains(x, y) || !rows[index].Selectable)
            {
                continue;
            }

            var track = MenuLayout.Track(row);
            return new MenuHit(index, rows[index].Kind == MenuRowKind.Slider && x >= track.X - 6 && x <= track.Right + 6);
        }

        return null;
    }

    /// <inheritdoc />
    public double TrackFraction(int settingsScroll, int row, int x)
    {
        var layout = LayoutOf(MenuPage.Settings);
        var track = MenuLayout.Track(layout.Row(Math.Clamp(row - settingsScroll, 0, layout.Capacity - 1)));
        return Math.Clamp((x - track.X) / (double)Math.Max(1, track.Width), 0, 1);
    }

    private MenuLayout LayoutOf(MenuPage page)
    {
        var (width, height) = _canvas();
        return new MenuLayout(width, height, page);
    }
}
