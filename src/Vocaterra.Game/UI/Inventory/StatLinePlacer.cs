using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>Where the stats line's chips go (I04): left to right in the room under the doll, wrapping to a new row when one does not fit, dropping any that would not fit in the rows the room has. Plain numbers, so a test can check it without a font.</summary>
public static class StatLinePlacer
{
    /// <summary>The boxes of the chips that fit, in order (a chip that fits no row is left out, and so is every one after it).</summary>
    /// <param name="widths">Each chip's width in pixels.</param>
    /// <param name="area">The room.</param>
    /// <param name="rowHeight">The height of a chip and of a row.</param>
    /// <param name="gap">The gap between chips.</param>
    public static IReadOnlyList<PixelBox> Place(IReadOnlyList<int> widths, PixelBox area, int rowHeight, int gap)
    {
        ArgumentNullException.ThrowIfNull(widths);
        var boxes = new List<PixelBox>();
        var x = 0;
        var row = 0;
        foreach (var requested in widths)
        {
            var width = Math.Min(requested, area.Width);
            if (x > 0 && x + width > area.Width)
            {
                row++;
                x = 0;
            }

            if ((row + 1) * rowHeight > area.Height || width <= 0)
            {
                break;
            }

            boxes.Add(new PixelBox(area.X + x, area.Y + (row * rowHeight), width, rowHeight));
            x += width + gap;
        }

        return boxes;
    }
}
