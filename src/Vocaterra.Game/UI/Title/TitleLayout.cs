using Vocaterra.Game.Core.Title;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Title;

/// <summary>
/// The title screen's pages on a canvas, in plain numbers (Q01; <see cref="TitleLayer"/> draws them, <see cref="TitleMetrics"/> answers the pointer from them). Every page is a panel in the Esc menu's look, centred;
/// the main page has the game's name above it. The New game page's rows run from the name to the Create button, with a place reserved under the world type for the terrain options (<see cref="TerrainTop"/>,
/// <see cref="TerrainHeight"/>, 0 until Q03 adds them: the rows below it move down by its height).
/// </summary>
public readonly record struct TitleLayout
{
    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The height of a button of the main page.</summary>
    public const int ButtonHeight = 44;

    /// <summary>The space between the main page's buttons.</summary>
    public const int ButtonGap = 12;

    /// <summary>The space above a page's rows (its heading).</summary>
    public const int HeaderHeight = 44;

    /// <summary>The height of a text field and of a button inside a page.</summary>
    public const int FieldHeight = 30;

    /// <summary>The height of a world's row on the Load page: its name, and under it its seed, type and time.</summary>
    public const int WorldRowHeight = 48;

    /// <summary>The height of the game's name above the main page (a 64 pixel font).</summary>
    public const int NameHeight = 64;

    /// <summary>The height the terrain options take on the New page: none yet (Q03 gives it a value and the rows under it move down).</summary>
    public const int TerrainHeight = 0;

    private const int ActionHeight = 36;
    private const int NoticeHeight = 22;
    private const int HintHeight = 34;

    // What the Load page has besides its rows: the heading, the notice line, the buttons and the hint, with their gaps.
    private const int ListOverhead = HeaderHeight + NoticeHeight + 4 + ActionHeight + 8 + HintHeight;

    /// <summary>Computes the layout of a page on a canvas.</summary>
    /// <param name="viewportWidth">The canvas's width.</param>
    /// <param name="viewportHeight">The canvas's height.</param>
    /// <param name="page">The page.</param>
    public TitleLayout(int viewportWidth, int viewportHeight, TitlePage page)
    {
        Page = page;
        var limit = viewportHeight - (2 * Margin);
        var width = page switch
        {
            TitlePage.Main => Math.Min(440, viewportWidth - 32),
            TitlePage.New => Math.Min(560, viewportWidth - 32),
            TitlePage.Load => Math.Min(640, viewportWidth - 32),
            TitlePage.ConfirmDelete => Math.Min(520, viewportWidth - 32),
            _ => Math.Min(680, viewportWidth - 32),
        };
        var newBottom = HeaderHeight + 176 + TerrainHeight + 4 + NoticeHeight + 8 + ActionHeight + 8 + HintHeight;
        var wanted = page switch
        {
            TitlePage.Main => 16 + (TitleSlot.MainCount * ButtonHeight) + ((TitleSlot.MainCount - 1) * ButtonGap) + 8 + NoticeHeight + HintHeight,
            TitlePage.New => newBottom,
            TitlePage.Load => ListOverhead + (5 * WorldRowHeight),
            TitlePage.ConfirmDelete => HeaderHeight + 96 + ActionHeight + 8 + HintHeight,
            _ => 640,
        };
        var height = Math.Min(wanted, limit);
        var block = page == TitlePage.Main ? NameHeight + 24 + height : height;
        var top = Math.Max(Margin, (viewportHeight - block) / 2);
        NameY = top;
        var panelY = page == TitlePage.Main ? top + NameHeight + 24 : top;
        if (page == TitlePage.Main && panelY + height > viewportHeight - Margin)
        {
            panelY = Math.Max(Margin, viewportHeight - Margin - height);
            NameY = Math.Max(0, panelY - 24 - NameHeight);
        }

        Panel = new PixelBox((viewportWidth - width) / 2, panelY, width, height);
        var left = Panel.X + 16;
        var inner = Panel.Width - 32;
        ListTop = Panel.Y + (page == TitlePage.Main ? 16 : HeaderHeight);
        var hintY = Panel.Bottom - 28;
        HintY = hintY;
        var buttonsY = Panel.Bottom - HintHeight - 8 - ActionHeight;
        Notice = page == TitlePage.Main
            ? new PixelBox(left, Panel.Bottom - HintHeight - NoticeHeight - 2, inner, NoticeHeight)
            : new PixelBox(left, buttonsY - 4 - NoticeHeight, inner, NoticeHeight);
        Capacity = Math.Max(1, (height - ListOverhead) / WorldRowHeight);
        NameField = new PixelBox(left, ListTop + 20, inner, FieldHeight);
        SeedField = new PixelBox(left, ListTop + 80, inner - 8 - 110, FieldHeight);
        RandomButton = new PixelBox(left + inner - 110, ListTop + 80, 110, FieldHeight);
        TypeRow = new PixelBox(left, ListTop + 140, inner, FieldHeight);
        TerrainTop = ListTop + 176;
        var half = (inner - 8) / 2;
        Create = new PixelBox(left, buttonsY, half, ActionHeight);
        NewBack = new PixelBox(left + half + 8, buttonsY, half, ActionHeight);
        LoadButton = new PixelBox(left, buttonsY, 120, ActionHeight);
        DeleteButton = new PixelBox(left + 128, buttonsY, 120, ActionHeight);
        LoadBack = new PixelBox(left + inner - 120, buttonsY, 120, ActionHeight);
        Message = new PixelBox(left, ListTop + 8, inner, 80);
        var confirmY = Panel.Bottom - HintHeight - 8 - ActionHeight;
        ConfirmDelete = new PixelBox(Panel.X + (Panel.Width / 2) - 156, confirmY, 140, ActionHeight);
        ConfirmKeep = new PixelBox(Panel.X + (Panel.Width / 2) + 16, confirmY, 140, ActionHeight);
    }

    /// <summary>The page.</summary>
    public TitlePage Page { get; }

    /// <summary>The panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The top of the game's name above the main page.</summary>
    public int NameY { get; }

    /// <summary>The top of what is under the heading.</summary>
    public int ListTop { get; }

    /// <summary>The top of the hint line at the panel's foot.</summary>
    public int HintY { get; }

    /// <summary>The line above the buttons where a refusal or a failure is said.</summary>
    public PixelBox Notice { get; }

    /// <summary>How many worlds the Load page shows at once.</summary>
    public int Capacity { get; }

    /// <summary>The New page's name field.</summary>
    public PixelBox NameField { get; }

    /// <summary>The New page's seed field.</summary>
    public PixelBox SeedField { get; }

    /// <summary>The New page's Random button.</summary>
    public PixelBox RandomButton { get; }

    /// <summary>The New page's world type row.</summary>
    public PixelBox TypeRow { get; }

    /// <summary>Where the terrain options' rows start on the New page (Q03; <see cref="TerrainHeight"/> tall: the panel is that much taller and the rows under it, the notice and the buttons, keep their places at its foot).</summary>
    public int TerrainTop { get; }

    /// <summary>The New page's Create button.</summary>
    public PixelBox Create { get; }

    /// <summary>The New page's Back button.</summary>
    public PixelBox NewBack { get; }

    /// <summary>The Load page's Load button.</summary>
    public PixelBox LoadButton { get; }

    /// <summary>The Load page's Delete button.</summary>
    public PixelBox DeleteButton { get; }

    /// <summary>The Load page's Back button.</summary>
    public PixelBox LoadBack { get; }

    /// <summary>The confirmation's message.</summary>
    public PixelBox Message { get; }

    /// <summary>The confirmation's Delete button.</summary>
    public PixelBox ConfirmDelete { get; }

    /// <summary>The confirmation's Keep button.</summary>
    public PixelBox ConfirmKeep { get; }

    /// <summary>A button of the main page.</summary>
    /// <param name="index">The button's place from 0.</param>
    public PixelBox Button(int index) => new(Panel.X + 32, ListTop + (index * (ButtonHeight + ButtonGap)), Panel.Width - 64, ButtonHeight);

    /// <summary>The box of the world shown <paramref name="shown"/> rows from the top of the Load page's list.</summary>
    /// <param name="shown">How far down the list, from 0.</param>
    public PixelBox WorldRow(int shown) => new(Panel.X + 12, ListTop + (shown * WorldRowHeight), Panel.Width - 24, WorldRowHeight);
}
