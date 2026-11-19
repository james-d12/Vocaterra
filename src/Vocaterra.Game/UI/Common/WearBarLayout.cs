using Vocaterra.Game.Core.Items.Wearing;

namespace Vocaterra.Game.UI.Common;

/// <summary>
/// Where an item's wear bar sits in a slot (I07): a thin track along the bottom of the cell, inset a little, and the filled part of it (the share of the item that is left) with the bar's colour. Plain pixels and numbers, so a test checks it
/// without a window; <see cref="Core.Items.Wearing.WearBar"/> says how much is left and in what colour, and nothing is drawn for an item that has no wear.
/// </summary>
/// <param name="Track">The whole bar, the dark strip behind the fill.</param>
/// <param name="Fill">The coloured part: the share left, at least one pixel wide while anything is left and none when the item is worn out.</param>
/// <param name="Red">The fill's red.</param>
/// <param name="Green">The fill's green.</param>
/// <param name="Blue">The fill's blue.</param>
public readonly record struct WearBarLayout(PixelBox Track, PixelBox Fill, int Red, int Green, int Blue)
{
    /// <summary>The bar's height in pixels.</summary>
    public const int BarHeight = 3;

    /// <summary>The space left at each side and under the bar, in pixels.</summary>
    public const int Inset = 3;

    /// <summary>The bar of an item in <paramref name="cell"/>, or <see langword="null"/> when the cell is too small for one or the item shows none.</summary>
    /// <param name="cell">The slot's cell.</param>
    /// <param name="bar">The wear bar of the stack (<see cref="WearBar.Of"/>), or <see langword="null"/> for none.</param>
    public static WearBarLayout? Of(PixelBox cell, WearBar? bar)
    {
        if (bar is not { } wear || cell.Width < (2 * Inset) + 4 || cell.Height < BarHeight + Inset + 2)
        {
            return null;
        }

        var track = new PixelBox(cell.X + Inset, cell.Bottom - Inset - BarHeight, cell.Width - (2 * Inset), BarHeight);
        var width = wear.Remaining <= 0 ? 0 : Math.Clamp((int)Math.Round(track.Width * wear.Remaining), 1, track.Width);
        return new WearBarLayout(track, new PixelBox(track.X, track.Y, width, BarHeight), wear.Red, wear.Green, wear.Blue);
    }
}
