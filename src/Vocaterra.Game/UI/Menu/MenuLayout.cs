using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Menu;

/// <summary>
/// The menu's panel on a canvas, in plain numbers (the engine draws them, <see cref="MenuMetrics"/> answers the pointer from them). The main page is a short panel with five
/// buttons; the settings and controls pages are as tall as the canvas allows and scroll: the settings by rows of <see cref="RowHeight"/>, the controls by lines of
/// <see cref="LineHeight"/>. The Back button sits at the foot of both.
/// </summary>
public readonly record struct MenuLayout
{
    /// <summary>The height of a settings row (a heading and a note take one row as well).</summary>
    public const int RowHeight = 30;

    /// <summary>The height of a line of text on the controls page.</summary>
    public const int LineHeight = 20;

    /// <summary>The space above the list (the title).</summary>
    public const int HeaderHeight = 44;

    /// <summary>The space under the list (the hint and the Back button).</summary>
    public const int FooterHeight = 40;

    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The height of a main-page button.</summary>
    public const int ButtonHeight = 44;

    /// <summary>The space between main-page buttons.</summary>
    public const int ButtonGap = 12;

    /// <summary>How many buttons the main page has (<see cref="MenuItem"/>: Resume, Settings, Controls, Main menu and Exit).</summary>
    public static readonly int ButtonCount = Enum.GetValues<MenuItem>().Length;

    /// <summary>The widest a settings row's label column is.</summary>
    public const int MaxLabelWidth = 250;

    /// <summary>The width reserved for a slider's number.</summary>
    public const int NumberWidth = 64;

    /// <summary>Computes the layout of a page on a canvas.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="page">The page shown.</param>
    public MenuLayout(int viewportWidth, int viewportHeight, MenuPage page)
    {
        Page = page;
        var limit = viewportHeight - (2 * Margin);
        var width = page switch
        {
            MenuPage.Main => Math.Min(440, viewportWidth - 32),
            MenuPage.Settings => Math.Min(680, viewportWidth - 32),
            _ => Math.Min(780, viewportWidth - 32),
        };
        var wanted = page == MenuPage.Main
            ? HeaderHeight + (ButtonCount * ButtonHeight) + ((ButtonCount - 1) * ButtonGap) + 28 + FooterHeight
            : 640;
        var height = Math.Min(wanted, limit);
        Panel = new PixelBox((viewportWidth - width) / 2, Math.Max(Margin, (viewportHeight - height) / 2), width, height);
        var room = Math.Max(RowHeight, height - HeaderHeight - FooterHeight);
        ListTop = Panel.Y + HeaderHeight;
        ListHeight = room;
        Capacity = Math.Max(1, room / RowHeight);
        LineCapacity = Math.Max(1, room / LineHeight);
        Back = new PixelBox(Panel.Right - 16 - 100, Panel.Bottom - FooterHeight + 4, 100, 28);
    }

    /// <summary>The page.</summary>
    public MenuPage Page { get; }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The top of the list under the title.</summary>
    public int ListTop { get; }

    /// <summary>The height of the list.</summary>
    public int ListHeight { get; }

    /// <summary>How many settings rows are shown at once.</summary>
    public int Capacity { get; }

    /// <summary>How many lines of the controls page are shown at once.</summary>
    public int LineCapacity { get; }

    /// <summary>The Back button.</summary>
    public PixelBox Back { get; }

    /// <summary>A button of the main page.</summary>
    /// <param name="index">The button's place from 0.</param>
    public PixelBox Button(int index)
    {
        var width = Panel.Width - 64;
        return new PixelBox(Panel.X + 32, ListTop + 8 + (index * (ButtonHeight + ButtonGap)), width, ButtonHeight);
    }

    /// <summary>The box of the settings row shown <paramref name="shown"/> rows from the top of the list.</summary>
    /// <param name="shown">How far down the list, from 0.</param>
    public PixelBox Row(int shown) => new(Panel.X + 12, ListTop + (shown * RowHeight), Panel.Width - 24, RowHeight);

    /// <summary>The slider track of a settings row.</summary>
    /// <param name="row">The row's box.</param>
    public static PixelBox Track(PixelBox row)
    {
        var x = ControlX(row);
        return new PixelBox(x, row.Y + 11, Math.Max(40, row.Right - 8 - x - NumberWidth), 8);
    }

    /// <summary>Where the control of a settings row starts (after the label column).</summary>
    /// <param name="row">The row's box.</param>
    public static int ControlX(PixelBox row) => row.X + 8 + LabelWidth(row) + 8;

    /// <summary>The width of a settings row's label column: <see cref="MaxLabelWidth"/>, or two fifths of the row on a narrow canvas.</summary>
    /// <param name="row">The row's box.</param>
    public static int LabelWidth(PixelBox row) => Math.Min(MaxLabelWidth, row.Width * 2 / 5);

    /// <summary>The width left to a settings row's control.</summary>
    /// <param name="row">The row's box.</param>
    public static int ControlWidth(PixelBox row) => row.Right - 8 - ControlX(row);

    /// <summary>The width of the keys column on the controls page.</summary>
    public int KeysWidth => Math.Min(260, Panel.Width / 3);

    /// <summary>The width the controls page leaves to a description.</summary>
    public int TextWidth => Panel.Width - 32 - KeysWidth - 8;
}
