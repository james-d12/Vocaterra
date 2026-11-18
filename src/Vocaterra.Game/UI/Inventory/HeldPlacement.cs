using Vocaterra.Game.Core.Items.Containers;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Inventory;

/// <summary>
/// Where the held stack's icon is drawn (I04b): at the pointer when there is one; with no pointer (a scripted run, a pad, a pointer outside the window) beside the cell the stack was picked up from, shifted
/// so the empty cell shows, and kept inside the canvas. Plain numbers, so a test places it with no window.
/// </summary>
public static class HeldPlacement
{
    /// <summary>The icon's box.</summary>
    /// <param name="pointer">The pointer, or <see langword="null"/> when there is none.</param>
    /// <param name="origin">The box of the cell the stack came from, or <see langword="null"/> when it has none on screen.</param>
    /// <param name="icon">The icon's side.</param>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    public static PixelBox Icon(Microsoft.Xna.Framework.Point? pointer, PixelBox? origin, int icon, int canvasWidth, int canvasHeight)
    {
        int x;
        int y;
        if (pointer is { } at)
        {
            x = at.X - (icon / 2);
            y = at.Y - (icon / 2);
        }
        else if (origin is { } cell)
        {
            x = cell.Right - (icon / 3); // its left third over the cell's right edge: the cell's own icon place stays empty
            y = cell.Y + ((cell.Height - icon) / 2) + (icon / 4);
        }
        else
        {
            x = (canvasWidth - icon) / 2;
            y = (canvasHeight - icon) / 2;
        }

        return new PixelBox(Math.Clamp(x, 0, Math.Max(0, canvasWidth - icon)), Math.Clamp(y, 0, Math.Max(0, canvasHeight - icon)), icon, icon);
    }

    /// <summary>The box of the cell a stack was picked up from, on this layout.</summary>
    /// <param name="layout">The panel's layout.</param>
    /// <param name="container">The origin container (<see cref="ContainerIds"/>).</param>
    /// <param name="slot">The origin slot in it.</param>
    /// <param name="bagSlots">How many bag slots are active.</param>
    public static PixelBox? OriginCell(InventoryLayout layout, int container, int slot, int bagSlots)
    {
        if (container == ContainerIds.Bag && (uint)slot < (uint)bagSlots)
        {
            return layout.Cell(slot);
        }

        if (container == ContainerIds.Equipment)
        {
            foreach (var cell in layout.Shape.Cells)
            {
                if (cell.Flat == slot)
                {
                    return layout.EquipmentCellBox(cell);
                }
            }
        }

        return container == ContainerIds.Trash ? layout.TrashCell : null;
    }
}
