using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Hud;

/// <summary>Where the needs' HUD stands on a canvas (N05): the block of drive icons and bars, and the stage line above the hotbar.</summary>
/// <param name="Block">The icons and bars together.</param>
/// <param name="Health">The health bar (always there).</param>
/// <param name="Stamina">The stamina bar, or <see langword="null"/> when stamina is at its maximum.</param>
/// <param name="Icons">The drive icons' cells, left to right, in the order given.</param>
/// <param name="Line">The stage line's box (the Spanish with the English beneath), or <see langword="null"/> when none is on show.</param>
/// <param name="Beside">Whether the block stands beside the hotbar (otherwise it stands just above its left end, when the canvas has no room at the side).</param>
public sealed record NeedsHudBoxes(PixelBox Block, PixelBox Health, PixelBox? Stamina, IReadOnlyList<PixelBox> Icons, PixelBox? Line, bool Beside);

/// <summary>
/// The layout of the needs HUD (N05, design 9.9): plain numbers, so a test places it with no window at every canvas and scale. Health and stamina are two thin bars beside
/// the hotbar's left end, the drive icons a row over them; where the canvas has no room beside the hotbar (the small ones) the block stands just above the hotbar's left end
/// instead. The stage line is centred over the hotbar (and over the block, when that is above it). Nothing here overlaps the hotbar, the objective tracker (top right) or the
/// tip box (top left) at the canvases the interface scale makes; the layout test says so.
/// </summary>
public static class NeedsHudLayout
{
    /// <summary>The width of a bar and of the block.</summary>
    public const int BarWidth = 110;

    /// <summary>The height of a bar.</summary>
    public const int BarHeight = 8;

    /// <summary>The space between two parts of the block.</summary>
    public const int Gap = 4;

    /// <summary>The width of an icon's cell.</summary>
    public const int IconWidth = 22;

    /// <summary>The height of an icon's cell: the picture and, under it, the notches.</summary>
    public const int IconHeight = 26;

    /// <summary>The space between two icons.</summary>
    public const int IconGap = 4;

    /// <summary>The space between the block and the hotbar, and between the line and what is under it.</summary>
    public const int Spacing = 8;

    /// <summary>The padding either side of the stage line's text.</summary>
    public const int LinePadding = 10;

    /// <summary>The height of the stage line's box: two rows of text.</summary>
    public const int LineHeight = 8 + (2 * HudLayout.RowHeight);

    /// <summary>The most icons there are: the three drives' and the cold's (I06).</summary>
    public const int MaxIcons = 4;

    /// <summary>The block's height for what it holds.</summary>
    /// <param name="icons">How many drive icons.</param>
    /// <param name="stamina">Whether the stamina bar is shown.</param>
    public static int BlockHeight(int icons, bool stamina) =>
        (icons > 0 ? IconHeight + Gap : 0) + BarHeight + (stamina ? Gap + BarHeight : 0);

    /// <summary>The hotbar's box on a canvas (the strip of ten slots at the bottom).</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    public static PixelBox HotbarBox(int canvasWidth, int canvasHeight) => HotbarLayer.BoxFor(canvasWidth, canvasHeight);

    /// <summary>Places the HUD.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    /// <param name="icons">How many icons are shown, the drives' and the cold's (0 to 4).</param>
    /// <param name="stamina">Whether the stamina bar is shown.</param>
    /// <param name="spanish">The stage line's Spanish, or <see langword="null"/> for no line.</param>
    /// <param name="english">The stage line's English.</param>
    /// <param name="measure">The width of a text in the small font.</param>
    /// <param name="offHand">Whether the off-hand cell stands left of the hotbar (I04b): the block beside the hotbar then stands clear of it, or above the hotbar's left end where there is no room.</param>
    public static NeedsHudBoxes Place(int canvasWidth, int canvasHeight, int icons, bool stamina, string? spanish, string? english, Func<string, float> measure, bool offHand = false)
    {
        ArgumentNullException.ThrowIfNull(measure);
        icons = Math.Clamp(icons, 0, MaxIcons);
        var hotbar = HotbarBox(canvasWidth, canvasHeight);
        var height = BlockHeight(icons, stamina);
        var reserve = offHand ? HotbarLayer.OffHandReserve : 0;
        var beside = hotbar.X - reserve - Spacing - BarWidth >= HudLayout.Margin;
        var x = beside ? hotbar.X - reserve - Spacing - BarWidth : hotbar.X;
        var bottom = beside ? hotbar.Bottom : hotbar.Y - Spacing - (offHand && HotbarLayer.OffHandAbove(canvasWidth, canvasHeight) ? HotbarLayer.OffHandReserve : 0);
        var block = new PixelBox(x, bottom - height, BarWidth, height);

        var y = block.Y;
        var iconBoxes = new List<PixelBox>();
        if (icons > 0)
        {
            for (var i = 0; i < icons; i++)
            {
                iconBoxes.Add(new PixelBox(block.X + (i * (IconWidth + IconGap)), y, IconWidth, IconHeight));
            }

            y += IconHeight + Gap;
        }

        var healthBar = new PixelBox(block.X, y, BarWidth, BarHeight);
        PixelBox? staminaBar = stamina ? new PixelBox(block.X, y + BarHeight + Gap, BarWidth, BarHeight) : null;

        PixelBox? line = null;
        if (spanish is not null)
        {
            var widest = (int)Math.Ceiling(Math.Max(measure(spanish), measure(english ?? string.Empty)));
            var width = Math.Min(widest + (2 * LinePadding), canvasWidth - (2 * HudLayout.Margin));
            var lineBottom = beside ? hotbar.Y - Spacing : block.Y - Spacing;
            line = new PixelBox((canvasWidth - width) / 2, lineBottom - LineHeight, width, LineHeight);
        }

        return new NeedsHudBoxes(block, healthBar, staminaBar, iconBoxes, line, beside);
    }
}
